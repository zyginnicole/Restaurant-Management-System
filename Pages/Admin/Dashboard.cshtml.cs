using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin dashboard: live operations and sales & trends.
    // Connected (reservations): Reservations KPI, Tables occupied, Arrivals today, Peak dining hours,
    // Table utilization by area, Reservations by status.
    // Still example data (other features): Revenue, Registered customers, Average rating, Recent activity,
    // Collected tonight, Daily sales, Top menu items.
    //  - Period: ?year=2026&month=10&day=25 (one day) · ?year=2026&month=10 (whole month, default this month)
    //    · ?year=2026 (whole year). Each figure is compared with the previous day / month / year.
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ReservationService _reservations;

        public DashboardModel(ApplicationDbContext db, ReservationService reservations)
        {
            _db = db;
            _reservations = reservations;
        }

        // ---- Period ----
        public int Year { get; set; }
        public int Month { get; set; }            // 0 = whole year
        public int Day { get; set; }              // 0 = whole month (or year)
        public string PeriodLabel { get; set; } = "";
        public string PreviousLabel { get; set; } = "";
        public List<int> Years { get; set; } = new();

        // ---- Reservations KPI (period) ----
        public int ResTotal { get; set; }
        public int ResCompleted { get; set; }
        public int ResEnded { get; set; }          // Cancelled + No-show + Declined
        public int ResUpcoming { get; set; }       // Pending + Confirmed + Seated
        public int? ResDeltaPercent { get; set; }  // vs the previous period (null when it had none)

        // ---- Reservations by status (period) ----
        public Dictionary<string, int> StatusCounts { get; set; } = new();

        // ---- Peak dining hours (period): bookings by start hour, Cancelled / Declined left out ----
        public List<string> PeakLabels { get; set; } = new();
        public List<int> PeakValues { get; set; } = new();

        // ---- Table utilization by area (period): tables that had a Seated / Completed booking ----
        public record AreaCount(string Area, int Count, int Total);
        public int TablesInService { get; set; }
        public int TablesUsed { get; set; }
        public List<AreaCount> Utilization { get; set; } = new();

        // ---- Live, today ----
        public int OccupiedNow { get; set; }
        public int GuestsNow { get; set; }
        public List<AreaCount> OccupiedByArea { get; set; } = new();
        public List<Reservation> Arrivals { get; set; } = new();   // today's Pending / Confirmed / Seated / Completed
        public TimeSpan WindowStart { get; set; }
        public TimeSpan WindowEnd { get; set; }

        // ---- Recent activity: the latest events from the times the database keeps ----
        //  new booking (Reservation.reservationCreateAt) · order placed (Order.orderDateTime) · payment (Payment.paymentDate).
        //  Status changes (seated, cancelled, preparing …) have no saved time, so they can't be listed.
        //  Css = edge colour (md-list-row is-…), Link = where the code opens.
        public record ActivityItem(DateTimeOffset When, string Title, string Code, string Link, string Meta, string Css);
        public List<ActivityItem> RecentActivity { get; set; } = new();
        private const int RecentActivityCount = 4;

        public async Task OnGetAsync(int? year, int? month, int? day)
        {
            // Period from the address (default: this month)
            var today = DateTime.Today;
            Year = year is >= 2000 and <= 2100 ? year.Value : today.Year;
            Month = month is >= 0 and <= 12 ? month.Value : (year == null ? today.Month : 0);
            Day = Month > 0 && day is >= 1 && day <= DateTime.DaysInMonth(Year, Month) ? day.Value : 0;

            DateTime start, end, prevStart;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (Day > 0)
            {
                start = new DateTime(Year, Month, Day); end = start.AddDays(1); prevStart = start.AddDays(-1);
                PeriodLabel = start.ToString("d MMMM yyyy", inv); PreviousLabel = "the day before";
            }
            else if (Month > 0)
            {
                start = new DateTime(Year, Month, 1); end = start.AddMonths(1); prevStart = start.AddMonths(-1);
                PeriodLabel = start.ToString("MMMM", inv); PreviousLabel = prevStart.ToString("MMMM", inv);
            }
            else
            {
                start = new DateTime(Year, 1, 1); end = start.AddYears(1); prevStart = start.AddYears(-1);
                PeriodLabel = Year.ToString(); PreviousLabel = (Year - 1).ToString();
            }

            // Years for the selector: years with bookings + this year
            Years = await _db.Reservations.AsNoTracking().Select(r => r.reservationDateTime.Year).Distinct().ToListAsync();
            if (!Years.Contains(today.Year)) Years.Add(today.Year);
            if (!Years.Contains(Year)) Years.Add(Year);
            Years = Years.OrderBy(y => y).ToList();

            // Bookings in the period and the previous one
            var period = await _reservations.GetBetweenAsync(start, end.AddDays(-1));
            var previousCount = await _db.Reservations.CountAsync(r => r.reservationDateTime >= prevStart && r.reservationDateTime < start);

            ResTotal = period.Count;
            ResCompleted = period.Count(r => r.reservationStatus == ReservationStatus.Completed);
            ResUpcoming = period.Count(r => ReservationStatus.Active.Contains(r.reservationStatus));
            ResEnded = ResTotal - ResCompleted - ResUpcoming;
            ResDeltaPercent = previousCount > 0 ? (int)Math.Round((ResTotal - previousCount) * 100.0 / previousCount) : null;

            string[] order = { ReservationStatus.Completed, ReservationStatus.Seated, ReservationStatus.Confirmed, ReservationStatus.Pending,
                               ReservationStatus.Cancelled, ReservationStatus.NoShow, ReservationStatus.Declined };
            StatusCounts = order.ToDictionary(s => s, s => period.Count(r => r.reservationStatus == s));

            var peak = period
                .Where(r => r.reservationStatus != ReservationStatus.Cancelled && r.reservationStatus != ReservationStatus.Declined)
                .GroupBy(r => r.reservationDateTime.Hour)
                .OrderBy(g => g.Key)
                .ToList();
            PeakLabels = peak.Select(g => g.Key.ToString("00") + ":00").ToList();
            PeakValues = peak.Select(g => g.Count()).ToList();

            // Tables in service (not under maintenance, area active)
            var tables = await _db.DiningTables.AsNoTracking()
                .Include(t => t.DiningArea)
                .Where(t => t.tableStatus != "Maintenance" && t.DiningArea!.areaActive)
                .ToListAsync();
            var areas = tables.GroupBy(t => t.DiningArea!.areaName).OrderBy(g => g.Min(t => t.areaID)).ToList();
            TablesInService = tables.Count;

            var usedIds = period
                .Where(r => r.reservationStatus == ReservationStatus.Completed || r.reservationStatus == ReservationStatus.Seated)
                .Select(r => r.tableID).ToHashSet();
            TablesUsed = tables.Count(t => usedIds.Contains(t.tableID));
            Utilization = areas.Select(g => new AreaCount(g.Key, g.Count(t => usedIds.Contains(t.tableID)), g.Count())).ToList();

            // Live: today's Seated bookings (or tableStatus Occupied) = tables in use right now
            var todayBookings = await _reservations.GetDayAsync(today);
            var seatedNow = todayBookings.Where(r => r.reservationStatus == ReservationStatus.Seated).ToList();
            var occupiedIds = seatedNow.Select(r => r.tableID).Concat(tables.Where(t => t.tableStatus == "Occupied").Select(t => t.tableID)).ToHashSet();
            OccupiedNow = tables.Count(t => occupiedIds.Contains(t.tableID));
            GuestsNow = seatedNow.Sum(r => r.partySize);
            OccupiedByArea = areas.Select(g => new AreaCount(g.Key, g.Count(t => occupiedIds.Contains(t.tableID)), g.Count())).ToList();

            RecentActivity = await LoadRecentActivityAsync();

            // Arrivals today on a timeline: from 18:00 (or the first booking's hour) to 22:00 (or one hour after the last)
            string[] shown = { ReservationStatus.Pending, ReservationStatus.Confirmed, ReservationStatus.Seated, ReservationStatus.Completed };
            Arrivals = todayBookings.Where(r => shown.Contains(r.reservationStatus)).OrderBy(r => r.reservationDateTime).ToList();
            WindowStart = TimeSpan.FromHours(18);
            WindowEnd = TimeSpan.FromHours(22);
            if (Arrivals.Count > 0)
            {
                var first = TimeSpan.FromHours(Arrivals.First().reservationDateTime.Hour);
                var last = TimeSpan.FromHours(Arrivals.Last().reservationDateTime.Hour + 1);
                if (first < WindowStart) WindowStart = first;
                if (last > WindowEnd) WindowEnd = last;
            }
        }

        // Latest bookings, orders and payments merged, newest first (orders and payments are only read here)
        private async Task<List<ActivityItem>> LoadRecentActivityAsync()
        {
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var items = new List<ActivityItem>();

            var bookings = await _db.Reservations.AsNoTracking()
                .Include(r => r.Customer)
                .OrderByDescending(r => r.reservationCreateAt)
                .Take(RecentActivityCount)
                .ToListAsync();
            foreach (var r in bookings)
            {
                var code = ReservationService.Code(r);
                var day = r.reservationDateTime.Date == DateTime.Today ? "today" : r.reservationDateTime.ToString("d MMM", inv);
                var title = (r.reservationStatus == ReservationStatus.Pending ? "Booking request: " : "New booking: ")
                            + $"{r.Customer?.custName ?? "Guest"}, {r.partySize} {(r.partySize == 1 ? "guest" : "guests")}, {day} {r.reservationDateTime:HH:mm}";
                var date = r.reservationDateTime.ToString("yyyy-MM-dd", inv);
                items.Add(new ActivityItem(r.reservationCreateAt, title, code,
                    $"/Admin/Monitor?tab=reservations&from={date}&to={date}&q={code}&view={code}",
                    r.reservationStatus, Pages.Staff.StaffReservationText.StatusClass(r.reservationStatus)));
            }

            var orders = await _db.Orders.AsNoTracking()
                .Include(o => o.DiningTable)
                .Include(o => o.OrderItems)
                .OrderByDescending(o => o.orderDateTime)
                .Take(RecentActivityCount)
                .ToListAsync();
            foreach (var o in orders)
            {
                var count = o.OrderItems.Sum(i => i.quantity);
                var where = o.DiningTable != null ? o.DiningTable.tableNumber : "Takeaway";
                items.Add(new ActivityItem(o.orderDateTime, $"Order placed: {where}, {count} {(count == 1 ? "item" : "items")}, RM {o.totalAmount.ToString("N2", inv)}",
                    "ORD-" + o.orderID.ToString("D4"), "/Admin/Monitor?tab=orders",
                    o.tableID != null ? "Dine-in" : "Takeaway", "is-placed"));
            }

            var payments = await _db.Payments.AsNoTracking()
                .OrderByDescending(p => p.paymentDate)
                .Take(RecentActivityCount)
                .ToListAsync();
            foreach (var p in payments)
            {
                var (verb, css) = p.paymentStatus switch
                {
                    "Paid" => ("Payment received", "is-completed"),
                    "Refunded" => ("Payment refunded", "is-cancelled"),
                    "Failed" => ("Payment failed", "is-pending"),
                    _ => ("Payment started", "is-placed")
                };
                items.Add(new ActivityItem(p.paymentDate, $"{verb}: RM {p.paymentAmount.ToString("N2", inv)}, {p.paymentMethod}",
                    "ORD-" + p.orderID.ToString("D4"), "/Admin/Monitor?tab=payments", p.paymentStatus, css));
            }

            return items.OrderByDescending(i => i.When).Take(RecentActivityCount).ToList();
        }

        // "2 minutes ago" / "3 hours ago" / "24 Oct, 18:40"
        public static string Ago(DateTimeOffset when)
        {
            var span = DateTimeOffset.Now - when;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minute{((int)span.TotalMinutes == 1 ? "" : "s")} ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours} hour{((int)span.TotalHours == 1 ? "" : "s")} ago";
            return when.ToString("d MMM, HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
