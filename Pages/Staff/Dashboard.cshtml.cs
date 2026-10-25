using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff dashboard: today's reservations (connected) and active orders (still example data – orders feature).
    // Clicking a reservation opens the shared details pop-up; its actions post to /Staff/Reservations and come back here.
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ReservationService _reservations;
        private readonly SettingsService _settings;

        public DashboardModel(ApplicationDbContext db, ReservationService reservations, SettingsService settings)
        {
            _db = db;
            _reservations = reservations;
            _settings = settings;
        }

        // Statuses shown on the dashboard (Cancelled, Declined and No-show are only on the Reservations page)
        public static readonly string[] Shown =
            { ReservationStatus.Pending, ReservationStatus.Confirmed, ReservationStatus.Seated, ReservationStatus.Completed };

        public SystemSetting Settings { get; set; } = new();
        public List<Reservation> Today { get; set; } = new();     // every booking today (for the "next on this table" text)
        public List<Reservation> Guests { get; set; } = new();    // the ones shown, earliest first
        public string StaffName { get; set; } = "";
        public int TablesOccupied { get; set; }
        public int TablesTotal { get; set; }

        public async Task OnGetAsync()
        {
            Settings = await _settings.GetAsync();
            StaffName = HttpContext.Session.GetUserName() ?? "";

            Today = await _reservations.GetDayAsync(DateTime.Today);
            Guests = Today.Where(r => Shown.Contains(r.reservationStatus))
                          .OrderBy(r => r.reservationDateTime)
                          .ToList();

            // Tables in use right now: a Seated booking, or tableStatus Occupied (e.g. a walk-in order). Out-of-service tables don't count.
            var tables = await _db.DiningTables.AsNoTracking().Where(t => t.tableStatus != "Maintenance").ToListAsync();
            var seatedTableIds = Today.Where(r => r.reservationStatus == ReservationStatus.Seated).Select(r => r.tableID).ToHashSet();
            TablesTotal = tables.Count;
            TablesOccupied = tables.Count(t => t.tableStatus == "Occupied" || seatedTableIds.Contains(t.tableID));
        }
    }
}
