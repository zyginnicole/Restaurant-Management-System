using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Reservations: floor status (right now) + the bookings of one day, with the actions for each status.
    // The rules live in ReservationService (Confirm / Decline / Seat / NoShow / CancelByStaff / UpdateByStaff).
    //  - ?date=2026-10-25 picks the day (default today). ?view=R-0043 opens that booking's pop-up;
    //    ?status=Pending picks Pending in the status filter (Dashboard "need approval").
    //  - The details pop-up (Pages/Staff/_ReservationDetailPopup) is also on the Staff Dashboard; its forms post here
    //    with returnUrl, so the staff member goes back to the page they were on.
    //  - Every action redirects back with TempData["StaffMessage"] (green box, red when StaffMessageIsError).
    //  - Seated → Completed has no button: Billing sets it when the order is paid (and the table back to Available).
    public class ReservationsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ReservationService _reservations;
        private readonly SettingsService _settings;

        public ReservationsModel(ApplicationDbContext db, ReservationService reservations, SettingsService settings)
        {
            _db = db;
            _reservations = reservations;
            _settings = settings;
        }

        // Page to go back to after an action ("/Staff/Dashboard" when the pop-up was used there). Local URLs only.
        [BindProperty]
        public string? ReturnUrl { get; set; }

        public DateTime Date { get; set; } = DateTime.Today;
        public SystemSetting Settings { get; set; } = new();
        public List<Reservation> Bookings { get; set; } = new();     // the chosen day, latest first
        public List<string> AreaNames { get; set; } = new();
        public List<FloorTable> Floor { get; set; } = new();          // every table, state right now
        public List<Reservation> PendingUpcoming { get; set; } = new(); // all Pending requests from today on (approval notice)

        // One floor tile. State: free · seated · reserved (Info = time of the next booking today) · unavailable
        // Bookings = today's Pending / Confirmed / Seated bookings on the table (the list in the table pop-up)
        public record FloorTable(DiningTable Table, string State, string Info, Reservation? Booking)
        {
            public List<Reservation> Bookings { get; init; } = new();
        }

        public async Task OnGetAsync(DateTime? date)
        {
            Date = date?.Date ?? DateTime.Today;
            Settings = await _settings.GetAsync();
            Bookings = await _reservations.GetDayAsync(Date);
            AreaNames = await _db.DiningAreas.AsNoTracking().OrderBy(a => a.areaID).Select(a => a.areaName).ToListAsync();
            PendingUpcoming = await _reservations.GetPendingUpcomingAsync();

            // Floor = today, whatever day is picked below
            var today = Date == DateTime.Today ? Bookings : await _reservations.GetDayAsync(DateTime.Today);
            var tables = await _db.DiningTables.AsNoTracking()
                .Include(t => t.DiningArea)
                .OrderBy(t => t.tableNumber)
                .ToListAsync();
            Floor = tables.Select(t => FloorState(t, today) with
            {
                Bookings = today.Where(r => r.tableID == t.tableID && ReservationStatus.Active.Contains(r.reservationStatus))
                                .OrderBy(r => r.reservationDateTime)
                                .ToList()
            }).ToList();
        }

        private FloorTable FloorState(DiningTable t, List<Reservation> today)
        {
            if (t.tableStatus == "Maintenance")
                return new FloorTable(t, "unavailable", "Out of service", null);

            var seated = today.FirstOrDefault(r => r.tableID == t.tableID && r.reservationStatus == ReservationStatus.Seated);
            if (seated != null || t.tableStatus == "Occupied")
                return new FloorTable(t, "seated", "Seated", seated);

            // Next Pending / Confirmed booking on this table today (late guests still count until the hold has passed)
            var next = today
                .Where(r => r.tableID == t.tableID
                         && (r.reservationStatus == ReservationStatus.Pending || r.reservationStatus == ReservationStatus.Confirmed)
                         && r.reservationDateTime.AddMinutes(Settings.lateHoldMinutes) >= DateTime.Now)
                .OrderBy(r => r.reservationDateTime)
                .FirstOrDefault();
            return next != null
                ? new FloorTable(t, "reserved", next.reservationDateTime.ToString("HH:mm"), next)
                : new FloorTable(t, "free", "Free", null);
        }

        // ---- Availability (JSON) for the details pop-up and Book reservations ----
        // /Staff/Reservations?handler=Availability&date=2026-10-25&time=19:00&partySize=4&tableId=3&ignoreId=12
        //   freeTables = tables free at that date + time for the party (ignoring the booking being edited)
        //   times      = free times for tableId on that date (only these are offered in the time list)
        //   seatNow    = tableId can take the party right now (today only); seatNowReason explains why not
        public async Task<IActionResult> OnGetAvailabilityAsync(DateTime date, string? time, int partySize, int? tableId, int? ignoreId)
        {
            partySize = Math.Max(partySize, 1);
            var freeTables = new List<int>();
            if (TimeSpan.TryParse(time, out var t))
                freeTables = (await _reservations.GetFreeTablesAsync(date.Date + t, partySize, ignoreReservationId: ignoreId))
                    .Select(x => x.tableID).ToList();

            var times = new List<string>();
            bool seatNow = false;
            string? seatNowReason = null;
            if (tableId != null)
            {
                times = (await _reservations.GetFreeTimesForTableAsync(date, partySize, tableId.Value, ignoreId))
                    .Select(s => s.ToString("HH:mm")).ToList();

                if (date.Date != DateTime.Today)
                    seatNowReason = "Seat now is only for today.";
                else if (await _reservations.IsOccupiedNowAsync(tableId.Value))
                    seatNowReason = "This table is occupied right now.";
                else if (!(await _reservations.GetFreeTablesAsync(DateTime.Now, partySize, ignoreReservationId: ignoreId)).Any(x => x.tableID == tableId))
                    seatNowReason = "This table is too small or has a booking soon, so it can't be used now.";
                else
                    seatNow = true;
            }
            return new JsonResult(new { freeTables, times, seatNow, seatNowReason });
        }

        // ---- Actions (reservationId = Reservation.reservationID) ----

        // Details pop-up "Save changes" (fields: reservationDate, reservationTime, partySize, tableId).
        // The special request is read-only for staff. Opens the same pop-up again after saving.
        public async Task<IActionResult> OnPostSaveAsync(int reservationId, DateTime reservationDate, TimeSpan reservationTime,
            int partySize, int tableId)
        {
            var error = await _reservations.UpdateByStaffAsync(reservationId, reservationDate.Date + reservationTime, partySize, tableId);
            SetMessage(error, $"{Code(reservationId)} saved.");
            // Back to the booking's (new) day with its pop-up open
            var view = Code(reservationId);
            return Url.IsLocalUrl(ReturnUrl) && !ReturnUrl!.StartsWith("/Staff/Reservations")
                ? LocalRedirect($"{ReturnUrl}{(ReturnUrl.Contains('?') ? "&" : "?")}view={view}")
                : RedirectToPage(new { date = reservationDate.ToString("yyyy-MM-dd"), view });
        }

        // Pending → Confirmed. From the pop-up the form fields come too: they are saved first.
        public async Task<IActionResult> OnPostConfirmAsync(int reservationId, DateTime? reservationDate, TimeSpan? reservationTime,
            int? partySize, int? tableId)
        {
            var error = await SaveFormFirstAsync(reservationId, reservationDate, reservationTime, partySize, tableId)
                        ?? await _reservations.ConfirmAsync(reservationId, StaffId());
            return Done(error, $"{Code(reservationId)} confirmed.");
        }

        // Pending → Declined (cancelReason is not saved yet – no column on Reservation)
        public async Task<IActionResult> OnPostDeclineAsync(int reservationId, string? cancelReason)
            => Done(await _reservations.DeclineAsync(reservationId, StaffId()), $"{Code(reservationId)} declined.");

        // Confirmed → Seated; table Occupied. From the pop-up the form fields are saved first.
        public async Task<IActionResult> OnPostSeatAsync(int reservationId, DateTime? reservationDate, TimeSpan? reservationTime,
            int? partySize, int? tableId)
        {
            var error = await SaveFormFirstAsync(reservationId, reservationDate, reservationTime, partySize, tableId)
                        ?? await _reservations.SeatAsync(reservationId, StaffId());
            return Done(error, $"{Code(reservationId)} seated.");
        }

        // Confirmed → No-show (only after the late-arrival hold)
        public async Task<IActionResult> OnPostNoShowAsync(int reservationId)
            => Done(await _reservations.NoShowAsync(reservationId, StaffId()), $"{Code(reservationId)} marked as no-show.");

        // Pending / Confirmed → Cancelled (cancelReason is not saved yet – no column on Reservation)
        public async Task<IActionResult> OnPostCancelAsync(int reservationId, string? cancelReason)
            => Done(await _reservations.CancelByStaffAsync(reservationId, StaffId()), $"{Code(reservationId)} cancelled.");

        // Confirm / Seat buttons in the pop-up submit the whole form: save the edits before changing the status
        private async Task<string?> SaveFormFirstAsync(int reservationId, DateTime? date, TimeSpan? time, int? partySize, int? tableId)
        {
            if (date == null || time == null || partySize == null || tableId == null) return null;   // posted from a list row
            return await _reservations.UpdateByStaffAsync(reservationId, date.Value.Date + time.Value, partySize.Value, tableId.Value);
        }

        // Signed-in staff member (Reservation.staffID); null for anyone else
        private int? StaffId() => HttpContext.Session.IsSignedInAs(UserRoles.Staff) ? HttpContext.Session.GetUserId() : null;

        private static string Code(int id) => "R-" + id.ToString("D4");

        private void SetMessage(string? error, string success)
        {
            TempData["StaffMessage"] = error ?? success;
            TempData["StaffMessageIsError"] = error != null;
        }

        private IActionResult Done(string? error, string success)
        {
            SetMessage(error, success);
            return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl!) : RedirectToPage();
        }
    }
}
