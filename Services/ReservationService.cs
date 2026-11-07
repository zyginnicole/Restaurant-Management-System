using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    //All table-reservation rules in one place: availability, booking, editing, cancelling and the staff actions.
    //Times use the server's clock (DateTime.Now), so the server must run in the restaurant's time zone (GMT+8).
    //The Admin "Time zone" setting is not used yet (known limitation).
    public class ReservationService
    {
        //Staff can still able to book a walk-in "now" a few minutes after the time they picked
        private const int StaffGraceMinutes = 15; //15 minutes

        //Even with auto-confirm on, parties this big (or bookings with a special request) wait for staff approval
        public const int LargePartySize = 9;

        //A table with guests still at it (Seated past their dining time, or tableStatus Occupied) is treated as busy
        //until this many minutes from now, so it isn't given to a new booking while they are still there.
        public const int SeatedReleaseMinutes = 15;

        //Staff must answer a Pending request at least this many minutes before the booking time;
        //after that it can't be confirmed and is declined automatically (CloseOverdueAsync).
        public const int PendingDeadlineMinutes = 15;

        //Last moment staff can confirm a Pending request
        public static DateTime PendingDeadline(Reservation booking) => booking.reservationDateTime.AddMinutes(-PendingDeadlineMinutes);

        //Guests who arrive early can be seated this many minutes before the booking time (not earlier)
        public const int EarlySeatMinutes = 15;

        //First moment staff can seat a Confirmed booking (15 minutes before the booking time)
        public static DateTime SeatFrom(Reservation booking) => booking.reservationDateTime.AddMinutes(-EarlySeatMinutes);

        //First moment staff can mark a Confirmed booking as no-show (booking time + Admin > Settings lateHoldMinutes)
        public static DateTime NoShowFrom(Reservation booking, int lateHoldMinutes) => booking.reservationDateTime.AddMinutes(lateHoldMinutes);

        //Used by the staff pages to show the Seat / No-show buttons only when the server will accept them
        public static bool CanSeatNow(Reservation booking) =>
            booking.reservationStatus == ReservationStatus.Confirmed
            && booking.reservationDateTime.Date == DateTime.Today
            && DateTime.Now >= SeatFrom(booking);

        public static bool CanMarkNoShowNow(Reservation booking, int lateHoldMinutes) =>
            booking.reservationStatus == ReservationStatus.Confirmed
            && booking.reservationDateTime.Date == DateTime.Today
            && DateTime.Now >= NoShowFrom(booking, lateHoldMinutes);
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

        //Status for a new or changed booking:
        //auto-confirm off → Pending; on → Confirmed, unless staff should check it first
        //(a special request, or a large party).
        public static bool NeedsStaffReview(int partySize, string? specialRequest)
            => partySize >= LargePartySize || !string.IsNullOrWhiteSpace(specialRequest);

        private static string StatusFor(SystemSetting settings, int partySize, string? specialRequest)
            => settings.autoConfirm && !NeedsStaffReview(partySize, specialRequest)
                ? ReservationStatus.Confirmed
                : ReservationStatus.Pending;

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

            //Guests still at a table (even past their dining time) keep it until shortly after now
            var releaseAt = DateTime.Now.AddMinutes(SeatedReleaseMinutes);
            if (start < releaseAt)
            {
                busyTableIds.AddRange(await _db.Reservations.AsNoTracking()
                    .Where(r => r.reservationStatus == ReservationStatus.Seated && r.reservationDateTime <= start
                             && r.reservationDateTime >= DateTime.Today   // a Seated booking from an earlier day isn't at the table any more
                             && (ignoreReservationId == null || r.reservationID != ignoreReservationId))
                    .Select(r => r.tableID)
                    .ToListAsync());
                busyTableIds.AddRange(await _db.DiningTables.AsNoTracking()
                    .Where(t => t.tableStatus == "Occupied")
                    .Select(t => t.tableID)
                    .ToListAsync());
            }

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

        //Times shown to a customer on this date (before checking tables): open, not blocked by the admin,
        //and at least minNoticeHours ahead. Too-soon times are left out, so "Full" on the page only means no free table.
        public async Task<List<DateTime>> GetCustomerSlotsAsync(DateTime date)
        {
            var settings = await _settings.GetAsync();
            var earliest = DateTime.Now.AddHours(settings.minNoticeHours);
            var slots = await SlotHelper.GetBookableSlotsAsync(_db, date.Date);
            return slots.Where(s => s >= earliest).ToList();
        }

        //3. Times a customer can pick on this date for this party
        //(bookable slot AND at least one free table)
        //ignoreReservationId: when EDITING a booking, its own table/time still counts as free.
        public async Task<List<DateTime>> GetAvailableSlotsAsync(DateTime date, int partySize, int? areaId = null, int? ignoreReservationId = null)
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
                         && tableIds.Contains(r.tableID)
                         && (ignoreReservationId == null || r.reservationID != ignoreReservationId))
                .Select(r => new { r.tableID, r.reservationDateTime, r.reservationStatus })
                .ToListAsync();
            var occupied = await OccupiedNowAsync(ignoreReservationId);
            var releaseAt = DateTime.Now.AddMinutes(SeatedReleaseMinutes);

            var available = new List<DateTime>();
            foreach (var slot in slots)
            {
                // Customer rules (party size, notice, window). Slot/blocked/past are already handled by SlotHelper.
                if (CheckBasicRules(settings, slot, partySize, byStaff: false) != null) continue;

                // Tables busy at this slot = bookings starting less than diningMinutes before or after it,
                // plus tables with guests still at them (until shortly after now)
                var busy = bookings
                    .Where(b => b.reservationDateTime > slot - hold && b.reservationDateTime < slot + hold)
                    .Select(b => b.tableID)
                    .ToHashSet();
                if (slot < releaseAt) busy.UnionWith(occupied);

                // At least one suitable table is free → the customer can pick this time
                if (tableIds.Any(id => !busy.Contains(id)))
                    available.Add(slot);
            }
            return available;
        }

        //Tables with guests at them right now: today's Seated booking, or tableStatus Occupied (e.g. set by an order)
        private async Task<HashSet<int>> OccupiedNowAsync(int? ignoreReservationId = null)
        {
            var seated = await _db.Reservations.AsNoTracking()
                .Where(r => r.reservationStatus == ReservationStatus.Seated && r.reservationDateTime >= DateTime.Today
                         && (ignoreReservationId == null || r.reservationID != ignoreReservationId))
                .Select(r => r.tableID)
                .ToListAsync();
            var occupied = await _db.DiningTables.AsNoTracking()
                .Where(t => t.tableStatus == "Occupied")
                .Select(t => t.tableID)
                .ToListAsync();
            return seated.Concat(occupied).ToHashSet();
        }

        public async Task<bool> IsOccupiedNowAsync(int tableId) => (await OccupiedNowAsync()).Contains(tableId);

        //3b. Times staff can offer on a date: the day's opening-hours slots (admin-blocked slots included –
        //staff may override them), not already passed.
        public async Task<List<DateTime>> GetStaffSlotsAsync(DateTime date)
        {
            var restaurant = await _db.Restaurants.AsNoTracking().OrderBy(r => r.restaurantID).FirstOrDefaultAsync();
            if (restaurant == null) return new List<DateTime>();

            var day = date.Date;
            var hour = await _db.OperatingHours.AsNoTracking()
                .FirstOrDefaultAsync(h => h.restaurantID == restaurant.restaurantID && h.dayOfWeek == day.DayOfWeek);
            return SlotHelper.Generate(hour, restaurant.slotIntervalMinutes, restaurant.lastSeatingMinutes)
                .Select(t => day + t)
                .Where(s => s > DateTime.Now)
                .ToList();
        }

        //3c. Staff time choices for ONE table: the staff slots where that table is free for this party
        //(same busy rules as GetFreeTablesAsync). ignoreReservationId: the booking being edited.
        public async Task<List<DateTime>> GetFreeTimesForTableAsync(DateTime date, int partySize, int tableId, int? ignoreReservationId = null)
        {
            var table = await _db.DiningTables.AsNoTracking().Include(t => t.DiningArea).FirstOrDefaultAsync(t => t.tableID == tableId);
            if (table == null || table.tableStatus == "Maintenance" || table.DiningArea?.areaActive != true || table.tableCapacity < partySize)
                return new List<DateTime>();

            var settings = await _settings.GetAsync();
            var hold = TimeSpan.FromMinutes(settings.diningMinutes);
            var day = date.Date;
            var from = day - hold;
            var to = day.AddDays(1) + hold;
            var starts = await _db.Reservations.AsNoTracking()
                .Where(r => r.tableID == tableId
                         && ReservationStatus.Active.Contains(r.reservationStatus)
                         && r.reservationDateTime > from && r.reservationDateTime < to
                         && (ignoreReservationId == null || r.reservationID != ignoreReservationId))
                .Select(r => r.reservationDateTime)
                .ToListAsync();
            var occupiedNow = (await OccupiedNowAsync(ignoreReservationId)).Contains(tableId);
            var releaseAt = DateTime.Now.AddMinutes(SeatedReleaseMinutes);

            return (await GetStaffSlotsAsync(day))
                .Where(slot => CheckBasicRules(settings, slot, partySize, byStaff: true) == null
                            && !starts.Any(s => s > slot - hold && s < slot + hold)
                            && !(occupiedNow && slot < releaseAt))
                .ToList();
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
                //Admin > Settings auto-confirm, except special requests / large parties (see StatusFor)
                reservationStatus = StatusFor(settings, partySize, specialRequest)
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

        //6b. The customer's finished bookings (Completed, Cancelled, No-show, Declined), newest first,
        //with table + area, staff (who cancelled/declined) and the visit's orders + payment (History page)
        public async Task<List<Reservation>> GetHistoryForCustomerAsync(int custId)
        {
            string[] ended = { ReservationStatus.Completed, ReservationStatus.Cancelled, ReservationStatus.NoShow, ReservationStatus.Declined };
            return await _db.Reservations.AsNoTracking()
                .Include(r => r.DiningTable!).ThenInclude(t => t.DiningArea)
                .Include(r => r.Staff)
                .Include(r => r.Orders).ThenInclude(o => o.Payment)
                .Where(r => r.custID == custId && ended.Contains(r.reservationStatus))
                .OrderByDescending(r => r.reservationDateTime)
                .ToListAsync();
        }

        //7. One of the customer's own bookings (with table, area and customer), or null if it isn't theirs.
        //Used by the Confirmed and Edit pages.
        public async Task<Reservation?> GetCustomerReservationAsync(int reservationId, int custId)
        {
            return await _db.Reservations.AsNoTracking()
                .Include(r => r.DiningTable!).ThenInclude(t => t.DiningArea)
                .Include(r => r.Customer)
                .FirstOrDefaultAsync(r => r.reservationID == reservationId && r.custID == custId);
        }

        //8. Customer changes their own booking. Returns an error message, or null when saved.
        //Same deadline as cancelling. The area stays the same; the table is kept when it is still free.
        public async Task<string?> UpdateByCustomerAsync(int reservationId, int custId, DateTime start, int partySize, string? specialRequest)
        {
            var booking = await _db.Reservations
                .Include(r => r.DiningTable)
                .FirstOrDefaultAsync(r => r.reservationID == reservationId && r.custID == custId);
            if (booking == null)
                return "Reservation not found.";

            if (booking.reservationStatus != ReservationStatus.Pending && booking.reservationStatus != ReservationStatus.Confirmed)
                return "This reservation can no longer be changed.";

            var settings = await _settings.GetAsync();
            if (DateTime.Now > CancelDeadline(booking, settings))
                return $"Reservations can only be changed online up to {settings.cancelWindowHours} hours before. Please call us.";

            var newRequest = string.IsNullOrWhiteSpace(specialRequest) ? null : specialRequest.Trim();
            var timeOrSizeChanged = start != booking.reservationDateTime || partySize != booking.partySize;

            //Only a new time or party size needs a table check (a new special request alone doesn't)
            if (timeOrSizeChanged)
            {
                var error = await ValidateRequestAsync(start, partySize);
                if (error != null)
                    return error;

                //Free tables in the same area, not counting this booking itself
                var free = await GetFreeTablesAsync(start, partySize, booking.DiningTable?.areaID, ignoreReservationId: booking.reservationID);
                var table = free.FirstOrDefault(t => t.tableID == booking.tableID) ?? free.FirstOrDefault();
                if (table == null)
                    return "Sorry, no table is free at that time. Please choose another time.";

                booking.tableID = table.tableID;
                booking.reservationDateTime = start;
                booking.partySize = partySize;
            }

            //A new time/size, or a new/changed special request, gets its status decided again like a new booking
            //(removing a request alone keeps the current status)
            if (timeOrSizeChanged || (newRequest != null && newRequest != booking.specialRequest))
                booking.reservationStatus = StatusFor(settings, partySize, newRequest);

            booking.specialRequest = newRequest;
            await _db.SaveChangesAsync();
            return null;
        }

        // ======================= Overdue bookings =======================

        //Bookings that can't still be waiting are closed automatically
        //(ReservationCleanupService runs this when the app starts and every minute):
        //  Pending   → Declined   when nobody confirmed it by 15 minutes before the booking time (any day)
        //  Confirmed → No-show    from an earlier day (the guests were never seated)
        //  Seated    → Completed  from an earlier day (the visit happened; the table is freed if no one else is seated there today)
        //Returns how many bookings were closed.
        public async Task<int> CloseOverdueAsync()
        {
            var today = DateTime.Today;
            var pendingCutoff = DateTime.Now.AddMinutes(PendingDeadlineMinutes);   // bookings starting before this are past the deadline
            var overdue = await _db.Reservations
                .Include(r => r.DiningTable)
                .Where(r => (r.reservationStatus == ReservationStatus.Pending && r.reservationDateTime <= pendingCutoff)
                         || (r.reservationDateTime < today && ReservationStatus.Active.Contains(r.reservationStatus)))
                .ToListAsync();
            if (overdue.Count == 0) return 0;

            var seatedTodayTableIds = await _db.Reservations.AsNoTracking()
                .Where(r => r.reservationStatus == ReservationStatus.Seated && r.reservationDateTime >= today)
                .Select(r => r.tableID)
                .ToListAsync();

            foreach (var r in overdue)
            {
                if (r.reservationStatus == ReservationStatus.Pending)
                    r.reservationStatus = ReservationStatus.Declined;
                else if (r.reservationStatus == ReservationStatus.Confirmed)
                    r.reservationStatus = ReservationStatus.NoShow;
                else
                {
                    r.reservationStatus = ReservationStatus.Completed;
                    if (r.DiningTable is { tableStatus: "Occupied" } table && !seatedTodayTableIds.Contains(table.tableID))
                        table.tableStatus = "Available";
                }
            }
            await _db.SaveChangesAsync();
            return overdue.Count;
        }

        // ======================= Staff =======================

        //"R-0041"
        public static string Code(Reservation r) => "R-" + r.reservationID.ToString("D4");

        //9. Every booking of one day (all statuses) with customer, table + area and staff, latest first
        public async Task<List<Reservation>> GetDayAsync(DateTime date)
        {
            var from = date.Date;
            var to = from.AddDays(1);
            return await _db.Reservations.AsNoTracking()
                .Include(r => r.DiningTable!).ThenInclude(t => t.DiningArea)
                .Include(r => r.Customer)
                .Include(r => r.Staff)
                .Where(r => r.reservationDateTime >= from && r.reservationDateTime < to)
                .OrderByDescending(r => r.reservationDateTime)
                .ToListAsync();
        }

        //9a. Pending requests from today on, soonest first – "waiting for approval" on the Staff pages
        //(requests past their 15-minute deadline are already declined by CloseOverdueAsync)
        public async Task<List<Reservation>> GetPendingUpcomingAsync()
        {
            return await _db.Reservations.AsNoTracking()
                .Where(r => r.reservationStatus == ReservationStatus.Pending && r.reservationDateTime >= DateTime.Today)
                .OrderBy(r => r.reservationDateTime)
                .ToListAsync();
        }

        //9b. Every booking between two dates (both included, all statuses) with customer, table + area and staff,
        //in date/time order (Admin › Monitor)
        public async Task<List<Reservation>> GetBetweenAsync(DateTime from, DateTime to)
        {
            var start = from.Date;
            var end = to.Date.AddDays(1);
            return await _db.Reservations.AsNoTracking()
                .Include(r => r.DiningTable!).ThenInclude(t => t.DiningArea)
                .Include(r => r.Customer)
                .Include(r => r.Staff)
                .Where(r => r.reservationDateTime >= start && r.reservationDateTime < end)
                .OrderBy(r => r.reservationDateTime)
                .ToListAsync();
        }

        //Loads a booking for a staff action and checks its current status.
        private async Task<(Reservation? booking, string? error)> FindForStaffAsync(int reservationId, params string[] allowed)
        {
            var booking = await _db.Reservations
                .Include(r => r.DiningTable)
                .FirstOrDefaultAsync(r => r.reservationID == reservationId);
            if (booking == null)
                return (null, "Reservation not found.");
            if (!allowed.Contains(booking.reservationStatus))
                return (null, $"{Code(booking)} is {booking.reservationStatus}, so this can't be done any more.");
            return (booking, null);
        }

        //10. Pending → Confirmed (staffID = who approved it). Only until 15 minutes before the booking time.
        public async Task<string?> ConfirmAsync(int reservationId, int? staffId)
        {
            var (booking, error) = await FindForStaffAsync(reservationId, ReservationStatus.Pending);
            if (booking == null) return error;

            if (DateTime.Now > PendingDeadline(booking))
                return $"{Code(booking)} starts in less than {PendingDeadlineMinutes} minutes, so it can't be confirmed any more. " +
                       "Decline it, or book the guest again on Book reservations.";

            booking.reservationStatus = ReservationStatus.Confirmed;
            booking.staffID = staffId ?? booking.staffID;
            await _db.SaveChangesAsync();
            return null;
        }

        //11. Pending → Declined (the table is free again)
        public async Task<string?> DeclineAsync(int reservationId, int? staffId)
        {
            var (booking, error) = await FindForStaffAsync(reservationId, ReservationStatus.Pending);
            if (booking == null) return error;

            booking.reservationStatus = ReservationStatus.Declined;
            booking.staffID = staffId ?? booking.staffID;
            await _db.SaveChangesAsync();
            return null;
        }

        //12. Confirmed → Seated, table Occupied. Only on the booking's day, from 15 minutes before the booking time
        //(EarlySeatMinutes), and only when the table is empty.
        //(Seated → Completed happens in Billing when the order is paid; the table goes back to Available there.)
        public async Task<string?> SeatAsync(int reservationId, int? staffId)
        {
            var (booking, error) = await FindForStaffAsync(reservationId, ReservationStatus.Confirmed);
            if (booking == null) return error;

            if (booking.reservationDateTime.Date != DateTime.Today)
                return $"{Code(booking)} is for {booking.reservationDateTime:d MMM}. Guests can only be seated on the day.";

            if (DateTime.Now < SeatFrom(booking))
                return $"{Code(booking)} can be seated from {SeatFrom(booking):HH:mm} ({EarlySeatMinutes} minutes before the booking time).";

            var table = booking.DiningTable!;
            if (await IsTableOccupiedAsync(table, booking.reservationID))
                return $"{table.tableNumber} is still occupied. Move {Code(booking)} to a free table first.";

            booking.reservationStatus = ReservationStatus.Seated;
            booking.staffID = staffId ?? booking.staffID;
            table.tableStatus = "Occupied";
            await _db.SaveChangesAsync();
            return null;
        }

        //Someone is at this table right now: another of today's Seated bookings, or tableStatus Occupied
        private async Task<bool> IsTableOccupiedAsync(DiningTable table, int? ignoreReservationId = null)
        {
            if (table.tableStatus == "Occupied") return true;
            return await _db.Reservations.AnyAsync(r => r.tableID == table.tableID
                && r.reservationStatus == ReservationStatus.Seated && r.reservationDateTime >= DateTime.Today
                && (ignoreReservationId == null || r.reservationID != ignoreReservationId));
        }

        //13. Confirmed → No-show. Only after the late-arrival hold (Admin > Settings lateHoldMinutes) has passed.
        public async Task<string?> NoShowAsync(int reservationId, int? staffId)
        {
            var (booking, error) = await FindForStaffAsync(reservationId, ReservationStatus.Confirmed);
            if (booking == null) return error;

            var settings = await _settings.GetAsync();
            var from = NoShowFrom(booking, settings.lateHoldMinutes);
            if (DateTime.Now < from)
                return $"{Code(booking)} can be marked as no-show from {from:HH:mm} ({settings.lateHoldMinutes} minutes after the booking time).";

            booking.reservationStatus = ReservationStatus.NoShow;
            booking.staffID = staffId ?? booking.staffID;
            await _db.SaveChangesAsync();
            return null;
        }

        //14. Staff cancel a Pending / Confirmed booking (no deadline for staff)
        public async Task<string?> CancelByStaffAsync(int reservationId, int? staffId)
        {
            var (booking, error) = await FindForStaffAsync(reservationId, ReservationStatus.Pending, ReservationStatus.Confirmed);
            if (booking == null) return error;

            booking.reservationStatus = ReservationStatus.Cancelled;
            booking.staffID = staffId ?? booking.staffID;
            await _db.SaveChangesAsync();
            return null;
        }

        //15. Staff change a Pending / Confirmed booking: date, time, guests, table.
        //The special request is the guest's own words, so staff can't change it.
        //Staff rules (no notice / slot check), and the chosen table must be free and big enough. Status stays the same.
        public async Task<string?> UpdateByStaffAsync(int reservationId, DateTime start, int partySize, int tableId)
        {
            var (booking, error) = await FindForStaffAsync(reservationId, ReservationStatus.Pending, ReservationStatus.Confirmed);
            if (booking == null) return error;

            //Nothing changed: nothing to check or save
            if (start == booking.reservationDateTime && partySize == booking.partySize && tableId == booking.tableID)
                return null;

            error = await ValidateRequestAsync(start, partySize, byStaff: true);
            if (error != null) return error;

            var free = await GetFreeTablesAsync(start, partySize, ignoreReservationId: booking.reservationID);
            if (!free.Any(t => t.tableID == tableId))
            {
                var name = (await _db.DiningTables.FindAsync(tableId))?.tableNumber ?? "That table";
                return $"{name} is not free at {start:HH:mm} or is too small for {partySize} guests.";
            }

            booking.reservationDateTime = start;
            booking.partySize = partySize;
            booking.tableID = tableId;
            await _db.SaveChangesAsync();
            return null;
        }

        //16. Staff book a table for a guest (walk-in or phone booking): name, email, phone, table and time.
        //The email links the booking to an account:
        //  - a customer account with this email → the booking goes to that account (details unchanged);
        //  - no account yet → a "guest profile" is created (name, email, phone, NO password). When the guest later
        //    registers with this email, Register activates that same profile (AuthService), so the booking is already there.
        //Staff rules (no notice / slot check); the chosen table must be free and big enough. Saved as Confirmed,
        //or Seated straight away when seatNow (guests at the door: today only, table must be empty → Occupied).
        public async Task<(Reservation? booking, string? error)> CreateWalkInAsync(string? name, string? email, string? phone,
            DateTime start, int partySize, int tableId, string? specialRequest, int? staffId, bool seatNow)
        {
            // Guest details
            name = name?.Trim();
            var normalisedEmail = AuthService.NormaliseEmail(email);
            var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
            if (string.IsNullOrEmpty(name))
                return (null, "Enter the guest's name.");
            if (!new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(normalisedEmail) || normalisedEmail.Length == 0)
                return (null, "Enter a valid email address for the guest.");
            if (digits.Length < 10 || digits.Length > 11)
                return (null, "Phone number must be 10 to 11 digits.");
            if (await _db.StaffMembers.AnyAsync(s => s.staffEmail == normalisedEmail) || await _db.Admins.AnyAsync(a => a.adminEmail == normalisedEmail))
                return (null, "That email belongs to a staff account. Use the guest's own email.");

            // Booking rules (staff) + the chosen table
            var error = await ValidateRequestAsync(start, partySize, byStaff: true);
            if (error != null) return (null, error);

            var table = await _db.DiningTables.FirstOrDefaultAsync(t => t.tableID == tableId);
            if (table == null) return (null, "Choose a table.");
            var free = await GetFreeTablesAsync(start, partySize);
            if (!free.Any(t => t.tableID == tableId))
                return (null, $"{table.tableNumber} is not free at {start:HH:mm} or is too small for {partySize} guests.");

            if (seatNow)
            {
                if (start.Date != DateTime.Today)
                    return (null, "Seat now is only for guests arriving today.");
                if (await IsTableOccupiedAsync(table))
                    return (null, $"{table.tableNumber} is occupied right now. Choose another table.");
            }

            // Account for the email: existing customer, or a new guest profile (saved together with the booking)
            var customer = await _db.Customers.FirstOrDefaultAsync(c => c.custEmail == normalisedEmail);
            if (customer == null)
            {
                customer = new Customer
                {
                    custName = name,
                    custEmail = normalisedEmail,
                    custPhone = digits,
                    custPassword = ""   // guest profile: no password until the guest registers with this email
                };
                _db.Customers.Add(customer);
            }

            var booking = new Reservation
            {
                Customer = customer,
                tableID = table.tableID,
                staffID = staffId,
                reservationDateTime = start,
                partySize = partySize,
                specialRequest = string.IsNullOrWhiteSpace(specialRequest) ? null : specialRequest.Trim(),
                reservationStatus = seatNow ? ReservationStatus.Seated : ReservationStatus.Confirmed   // staff made it
            };
            if (seatNow) table.tableStatus = "Occupied";

            _db.Reservations.Add(booking);
            await _db.SaveChangesAsync();
            return (booking, null);
        }
    }
}
