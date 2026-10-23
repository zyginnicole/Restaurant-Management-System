using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    //All table-reservation rules in one place: availability now, booking/cancelling next.
    public class ReservationService
    {
        //Staff can still able to book a walk-in "now" a few minutes after the time they picked
        private const int StaffGraceMinutes = 15; //15 minutes
        private readonly ApplicationDbContext _db;
        private readonly SettingsService _settings;

        public ReservationService(ApplicationDbContext db, SettingsService settings)
        {
            _db = db;
            _settings = settings;
        }

        //Shared simple rules (no database needed).
        //Returns an error message, or null when the request passes.
        private static string? CheckBasicRules(SystemSetting settings, DateTime start, int partySize, bool byStaff)
        {
            //Party size within Admin > Settings limits
            if (partySize < settings.minPartySize || partySize > settings.maxPartySize)
                return $"Party size must be between {settings.minPartySize} and {settings.maxPartySize}.";

            //Not in the past (staff get a short grace period for walk-ins booked "now")
            var earliest = byStaff ? DateTime.Now.AddMinutes(-StaffGraceMinutes) : DateTime.Now;
            if (start < earliest)
                return "That time has already passed.";

            //Not further ahead than the booking window
            if (start.Date > DateTime.Today.AddDays(settings.bookingWindowDays))
                return $"Bookings can be made up to {settings.bookingWindowDays} days ahead.";
            //Customers must book at least minNoticeHours ahead (staff can book closer)
            if (!byStaff && start < DateTime.Now.AddHours(settings.minNoticeHours))
                return $"Please book at least {settings.minNoticeHours} hours ahead.";

            return null;
        }

        //1. Is this request allowed at all? Returns an error message, or null when it is OK.
        // byStaff = true skips the customer-only rules (minimum notice, admin-blocked slots).
        public async Task<string?> ValidateRequestAsync(DateTime start, int partySize, bool byStaff = false)
        {
            var settings = await _settings.GetAsync();

            //Party size, past, booking window,minimum notice
            var error = CheckBasicRules(settings, start, partySize, byStaff);
                
            if (error != null) 
                return error;

            //Customers must pick a real, unblocked slot of that day.
            //SlotHelper covers opening hours, closed days, slot interval, last seating, past times and admin-blocked slots.
            if (!byStaff)
            {
                var slots = await SlotHelper.GetBookableSlotsAsync(_db, start.Date);
                if (!slots.Contains(start))
                    return "That time is not available for booking. Please choose another time.";
            }

            return null;
        }

        //2. Free tables for this time and party, best fit first (fewest spare seats).
        //ignoreReservationId: when EDITING a booking, don't count that booking against itself.
        public async Task<List<DiningTable>> GetFreeTablesAsync(DateTime start, int partySize, int? areaId = null, int? ignoreReservationId = null)
        {
            var settings = await _settings.GetAsync();
            var hold = TimeSpan.FromMinutes(settings.diningMinutes);

            //A booking holds its table from its start time for diningMinutes. 
            //Two bookings clash when their start times are less than diningMinutes apart.
            var clashFrom = start - hold;
            var clashTo = start + hold;

            //Tables already held by an active booking (Pending, Confirmed, Seated) at that time
            var busyTableIds = await _db.Reservations.AsNoTracking()
                .Where(r => ReservationStatus.Active.Contains(r.reservationStatus)
                        && r.reservationDateTime > clashFrom
                        && r.reservationDateTime < clashTo
                        && (ignoreReservationId == null || r.reservationID != ignoreReservationId))
                .Select(r => r.tableID)
                .ToListAsync();

            //Tables that can take the party: active area, not under maintenance, enough seats, not busy.
            //tableStatus (Available / Occupied / Reserved) only describes RIGHT NOW, so it is not used
            //for future times - only "Maintenance" is excluded.
            return await _db.DiningTables.AsNoTracking()
                .Include(t => t.DiningArea)
                .Where(t => t.DiningArea!.areaActive
                    && t.tableStatus != "Maintenance"
                    && t.tableCapacity >= partySize
                    && (areaId == null || t.areaID == areaId)
                    && !busyTableIds.Contains(t.tableID))
                .OrderBy(t => t.tableCapacity) //best fit: a party of 2 gets a 2-seater first
                .ThenBy(t => t.tableNumber)
                .ToListAsync();
        }

        //3. Times a customer can pick on this date for this party
        //(bookable slot AND at least one free table)
        public async Task<List<DateTime>> GetAvailableSlotsAsync(DateTime date, int partySize, int? areaId = null)
        {
            var settings = await _settings.GetAsync();
            var hold = TimeSpan.FromMinutes(settings.diningMinutes);
            var day = date.Date;

            // Open, future, not blocked by the admin (SlotHelper)
            var slots = await SlotHelper.GetBookableSlotsAsync(_db, day);
            if (slots.Count == 0) return slots;

            // Tables that could ever take this party (same filters as GetFreeTablesAsync, minus "busy")
            var tableIds = await _db.DiningTables.AsNoTracking()
                .Where(t => t.DiningArea!.areaActive
                         && t.tableStatus != "Maintenance"
                         && t.tableCapacity >= partySize
                         && (areaId == null || t.areaID == areaId))
                .Select(t => t.tableID)
                .ToListAsync();

            if (tableIds.Count == 0) 
                return new List<DateTime>();

            // Active bookings that could clash with any slot of this day
            // (a booking late the evening before can still hold a table at opening time)
            var from = day - hold;
            var to = day.AddDays(1) + hold;
            var bookings = await _db.Reservations.AsNoTracking()
                .Where(r => ReservationStatus.Active.Contains(r.reservationStatus)
                         && r.reservationDateTime > from
                         && r.reservationDateTime < to
                         && tableIds.Contains(r.tableID))
                .Select(r => new { r.tableID, r.reservationDateTime })
                .ToListAsync();

            var available = new List<DateTime>();
            foreach (var slot in slots)
            {
                // Customer rules (party size, notice, window). Slot/blocked/past are already handled by SlotHelper.
                if (CheckBasicRules(settings, slot, partySize, byStaff: false) != null) continue;

                // Tables busy at this slot = bookings starting less than diningMinutes before or after it
                var busy = bookings
                    .Where(b => b.reservationDateTime > slot - hold && b.reservationDateTime < slot + hold)
                    .Select(b => b.tableID)
                    .ToHashSet();

                // At least one suitable table is free → the customer can pick this time
                if (tableIds.Any(id => !busy.Contains(id)))
                    available.Add(slot);
            }
            return available;
        }

        //4. Create a booking. Returns (new booking, null) when saved, or (null, error message).
        //The free table is checked again right before saving, in case someone else just took it.
        public async Task<(Reservation? booking, string? error)> CreateAsync(int custId, DateTime start, int partySize,
            int? areaId, string? specialRequest, bool byStaff = false, int? staffId = null)
        {
            var error = await ValidateRequestAsync(start, partySize, byStaff);
            if (error != null)
                return (null, error);

            //Best-fit free table (GetFreeTablesAsync already sorts smallest first)
            var table = (await GetFreeTablesAsync(start, partySize, areaId)).FirstOrDefault();
            if (table == null)
                return (null, "Sorry, no table is free at that time. Please choose another time.");

            var settings = await _settings.GetAsync();
            var booking = new Reservation
            {
                custID = custId,
                tableID = table.tableID,
                staffID = staffId,
                reservationDateTime = start,
                partySize = partySize,
                specialRequest = string.IsNullOrWhiteSpace(specialRequest) ? null : specialRequest.Trim(),
                //Admin > Settings: auto-confirm on = Confirmed straight away, off = staff must approve
                reservationStatus = settings.autoConfirm ? ReservationStatus.Confirmed : ReservationStatus.Pending
            };

            _db.Reservations.Add(booking);
            await _db.SaveChangesAsync();
            return (booking, null);
        }

        //5. Customer cancels their own booking. Returns an error message, or null when cancelled.
        public async Task<string?> CancelByCustomerAsync(int reservationId, int custId)
        {
            //custId check: a customer can never cancel someone else's booking
            var booking = await _db.Reservations
                .FirstOrDefaultAsync(r => r.reservationID == reservationId && r.custID == custId);
            if (booking == null)
                return "Reservation not found.";

            //Seated / Completed / Cancelled / No-show / Declined can't be cancelled
            if (booking.reservationStatus != ReservationStatus.Pending && booking.reservationStatus != ReservationStatus.Confirmed)
                return "This reservation can no longer be cancelled.";

            var settings = await _settings.GetAsync();
            if (DateTime.Now > CancelDeadline(booking, settings))
                return $"Reservations can only be cancelled online up to {settings.cancelWindowHours} hours before. Please call us.";

            booking.reservationStatus = ReservationStatus.Cancelled;
            await _db.SaveChangesAsync();
            return null;
        }

        //Last moment a customer can still cancel online (also shown on the page)
        public static DateTime CancelDeadline(Reservation booking, SystemSetting settings)
            => booking.reservationDateTime.AddHours(-settings.cancelWindowHours);

        //6. The customer's active bookings from today on, soonest first (with table + area for display)
        public async Task<List<Reservation>> GetUpcomingForCustomerAsync(int custId)
        {
            return await _db.Reservations.AsNoTracking()
                .Include(r => r.DiningTable!).ThenInclude(t => t.DiningArea)
                .Where(r => r.custID == custId
                         && ReservationStatus.Active.Contains(r.reservationStatus)
                         && r.reservationDateTime >= DateTime.Today)
                .OrderBy(r => r.reservationDateTime)
                .ToListAsync();
        }
    }
}
