using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff – Book reservations: book a table for a guest (walk-in or phone) on any date and time.
    //   /Staff/WalkIn?table=T-05   → that table is chosen (Reservations floor → Book Table)
    //   /Staff/WalkIn?guest=12     → that customer is filled in
    //   /Staff/WalkIn?date=2026-10-25 → the table list shows that day's bookings (changing the date reloads)
    // Staff enter the guest's name, email and phone. The email links the booking to the guest's account
    // (or a guest profile the guest activates by registering) – see ReservationService.CreateWalkInAsync.
    public class WalkInModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ReservationService _reservations;
        private readonly SettingsService _settings;

        public WalkInModel(ApplicationDbContext db, ReservationService reservations, SettingsService settings)
        {
            _db = db;
            _reservations = reservations;
            _settings = settings;
        }

        public SystemSetting Settings { get; set; } = new();
        public List<TableOption> Tables { get; set; } = new();
        public List<Model.Customer> Guests { get; set; } = new();     // "Find existing guest"

        // One table in the dropdown + that day's active bookings on it as "19:00-21:00" windows
        public record TableOption(DiningTable Table, string Busy);

        // ---- The form (kept when booking fails) ----
        public DateTime Date { get; set; } = DateTime.Today;
        public string Time { get; set; } = "";
        public int? TableId { get; set; }
        public int PartySize { get; set; } = 2;
        public string? GuestName { get; set; }
        public string? GuestEmail { get; set; }
        public string? GuestPhone { get; set; }
        public string? SpecialRequest { get; set; }
        public bool SeatNow { get; set; } = true;
        public int? PresetGuestId { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync(DateTime? date, string? table, int? guest)
        {
            Date = date?.Date ?? DateTime.Today;
            SeatNow = Date == DateTime.Today;
            PresetGuestId = guest;
            await LoadAsync();

            // ?table=T-05 (floor tile) → that table
            TableId = Tables.FirstOrDefault(t => t.Table.tableNumber == table)?.Table.tableID;
            // Today: the next quarter hour (used when Seat now is unticked); other days: 19:00
            Time = Date == DateTime.Today ? NextQuarter(DateTime.Now).ToString("HH:mm") : "19:00";
        }

        public async Task<IActionResult> OnPostAsync(DateTime reservationDate, string? reservationTime, int tableId, int partySize,
            string? guestName, string? guestEmail, string? guestPhone, string? specialRequest, bool seatNow)
        {
            // Seat now: the guests are here, so the booking starts now (the time box is switched off on screen)
            var now = DateTime.Now;
            if (seatNow)
            {
                reservationDate = now.Date;
                reservationTime = now.ToString("HH:mm");
            }

            if (TimeSpan.TryParse(reservationTime, out var time))
            {
                var staffId = HttpContext.Session.IsSignedInAs(UserRoles.Staff) ? HttpContext.Session.GetUserId() : null;
                var (booking, error) = await _reservations.CreateWalkInAsync(guestName, guestEmail, guestPhone,
                    reservationDate.Date + time, partySize, tableId, specialRequest, staffId, seatNow);
                if (booking != null)
                {
                    var table = await _db.DiningTables.AsNoTracking().FirstAsync(t => t.tableID == booking.tableID);
                    TempData["StaffMessage"] = $"{ReservationService.Code(booking)} {(seatNow ? "seated" : "booked")} for {guestName!.Trim()} " +
                        $"at {table.tableNumber} on {booking.reservationDateTime:d MMM}, {booking.reservationDateTime:HH:mm}.";
                    return RedirectToPage("/Staff/Reservations", new { date = booking.reservationDateTime.ToString("yyyy-MM-dd") });
                }
                ErrorMessage = error;
            }
            else
            {
                ErrorMessage = "Choose a time.";
            }

            // Failed: show the form again with what staff typed
            Date = reservationDate.Date;
            Time = reservationTime ?? "";
            TableId = tableId;
            PartySize = partySize;
            GuestName = guestName;
            GuestEmail = guestEmail;
            GuestPhone = guestPhone;
            SpecialRequest = specialRequest;
            SeatNow = seatNow;
            await LoadAsync();
            return Page();
        }

        private async Task LoadAsync()
        {
            Settings = await _settings.GetAsync();
            PartySize = Math.Clamp(PartySize, Settings.minPartySize, Settings.maxPartySize);

            var tables = await _db.DiningTables.AsNoTracking()
                .Include(t => t.DiningArea)
                .Where(t => t.tableStatus != "Maintenance" && t.DiningArea!.areaActive)
                .OrderBy(t => t.tableNumber)
                .ToListAsync();

            // Active bookings of the chosen day → busy windows per table (shown on screen; the server checks again on Book)
            var day = (await _reservations.GetDayAsync(Date))
                .Where(r => ReservationStatus.Active.Contains(r.reservationStatus))
                .OrderBy(r => r.reservationDateTime)
                .ToList();
            Tables = tables.Select(t => new TableOption(t, string.Join(" ", day
                .Where(r => r.tableID == t.tableID)
                .Select(r => r.reservationDateTime.ToString("HH:mm") + "-" + r.reservationDateTime.AddMinutes(Settings.diningMinutes).ToString("HH:mm")))))
                .ToList();

            Guests = await _db.Customers.AsNoTracking().OrderBy(c => c.custName).ToListAsync();
        }

        private static DateTime NextQuarter(DateTime t)
        {
            var minutes = (t.Minute / 15 + 1) * 15;
            return t.Date.AddHours(t.Hour).AddMinutes(minutes);
        }
    }
}
