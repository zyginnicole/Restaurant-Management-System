using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin;

// Loads the figures and collections used by the existing admin dashboard page.
public class DashboardModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ReservationService _reservations;
    private readonly ReportService _reports;

    public DashboardModel(ApplicationDbContext db, ReservationService reservations, ReportService reports)
    {
        _db = db;
        _reservations = reservations;
        _reports = reports;
    }

    public KpiSummary Analytics { get; private set; } = new();
    public List<NameValue> DailySales { get; private set; } = new();
    public List<TopItem> TopItems { get; private set; } = new();
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public string PeriodLabel { get; set; } = "";
    public string PreviousLabel { get; set; } = "";
    public List<int> Years { get; set; } = new();
    public int ResTotal { get; set; }
    public int ResCompleted { get; set; }
    public int ResEnded { get; set; }
    public int ResUpcoming { get; set; }
    public int? ResDeltaPercent { get; set; }
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public List<string> PeakLabels { get; set; } = new();
    public List<int> PeakValues { get; set; } = new();
    public record AreaCount(string Area, int Count, int Total);
    public int TablesInService { get; set; }
    public int TablesUsed { get; set; }
    public List<AreaCount> Utilization { get; set; } = new();
    public int OccupiedNow { get; set; }
    public int GuestsNow { get; set; }
    public List<AreaCount> OccupiedByArea { get; set; } = new();
    public List<Reservation> Arrivals { get; set; } = new();
    public TimeSpan WindowStart { get; set; }
    public TimeSpan WindowEnd { get; set; }
    // Recent activity: the latest events from the times the database keeps —
    // new booking (Reservation.reservationCreateAt) · order placed (Order.orderDateTime) · payment (Payment.paymentDate).
    // Css = edge colour (md-list-row is-…), Link = where the code opens on Admin > Monitor.
    public record ActivityItem(DateTimeOffset When, string Title, string Code, string Link, string Meta, string Css);
    public List<ActivityItem> RecentActivity { get; set; } = new();
    private const int RecentActivityCount = 4;

    // Loads the selected period and today's live reservation information.
    public async Task OnGetAsync(int? year, int? month, int? day)
    {
        var today = DateTime.Today;
        Year = year is >= 2000 and <= 2100 ? year.Value : today.Year;
        Month = month is >= 0 and <= 12 ? month.Value : (year == null ? today.Month : 0);
        Day = Month > 0 && day is >= 1 && day <= DateTime.DaysInMonth(Year, Month) ? day.Value : 0;

        var start = Day > 0 ? new DateTime(Year, Month, Day) :
            Month > 0 ? new DateTime(Year, Month, 1) : new DateTime(Year, 1, 1);
        var end = Day > 0 ? start.AddDays(1) : Month > 0 ? start.AddMonths(1) : start.AddYears(1);
        var previousStart = Day > 0 ? start.AddDays(-1) : Month > 0 ? start.AddMonths(-1) : start.AddYears(-1);
        PeriodLabel = Day > 0 ? start.ToString("d MMMM yyyy") : Month > 0 ? start.ToString("MMMM") : Year.ToString();
        PreviousLabel = Day > 0 ? "the day before" : previousStart.ToString(Month > 0 ? "MMMM" : "yyyy");

        var range = new DateRange(start, end, PeriodLabel);
        Analytics = await _reports.GetKpisAsync(range, new DateRange(previousStart, start, PreviousLabel));
        DailySales = await _reports.GetRevenueSeriesAsync(range, Granularity.Day);
        TopItems = await _reports.GetTopItemsAsync(range, 5);

        Years = await _db.Reservations.AsNoTracking().Select(r => r.reservationDateTime.Year).Distinct().ToListAsync();
        if (!Years.Contains(today.Year)) Years.Add(today.Year);
        if (!Years.Contains(Year)) Years.Add(Year);
        Years = Years.OrderBy(y => y).ToList();

        var reservations = await _reservations.GetBetweenAsync(start, end.AddDays(-1));
        var previousCount = await _db.Reservations.CountAsync(r => r.reservationDateTime >= previousStart && r.reservationDateTime < start);
        ResTotal = reservations.Count;
        ResCompleted = reservations.Count(r => r.reservationStatus == ReservationStatus.Completed);
        ResUpcoming = reservations.Count(r => ReservationStatus.Active.Contains(r.reservationStatus));
        ResEnded = ResTotal - ResCompleted - ResUpcoming;
        ResDeltaPercent = previousCount == 0 ? null : (int)Math.Round((ResTotal - previousCount) * 100.0 / previousCount);

        var statuses = new[] { ReservationStatus.Completed, ReservationStatus.Seated, ReservationStatus.Confirmed,
            ReservationStatus.Pending, ReservationStatus.Cancelled, ReservationStatus.NoShow, ReservationStatus.Declined };
        StatusCounts = statuses.ToDictionary(s => s, s => reservations.Count(r => r.reservationStatus == s));

        var peak = reservations.Where(r => r.reservationStatus != ReservationStatus.Cancelled && r.reservationStatus != ReservationStatus.Declined)
            .GroupBy(r => r.reservationDateTime.Hour).OrderBy(g => g.Key).ToList();
        PeakLabels = peak.Select(g => $"{g.Key:00}:00").ToList();
        PeakValues = peak.Select(g => g.Count()).ToList();

        var tables = await _db.DiningTables.AsNoTracking().Include(t => t.DiningArea)
            .Where(t => t.tableStatus != "Maintenance" && t.DiningArea!.areaActive).ToListAsync();
        var areas = tables.GroupBy(t => t.DiningArea!.areaName).OrderBy(g => g.Min(t => t.areaID)).ToList();
        var usedIds = reservations.Where(r => r.reservationStatus is ReservationStatus.Completed or ReservationStatus.Seated)
            .Select(r => r.tableID).ToHashSet();
        TablesInService = tables.Count;
        TablesUsed = tables.Count(t => usedIds.Contains(t.tableID));
        Utilization = areas.Select(g => new AreaCount(g.Key, g.Count(t => usedIds.Contains(t.tableID)), g.Count())).ToList();

        var todayReservations = await _reservations.GetDayAsync(today);
        var seated = todayReservations.Where(r => r.reservationStatus == ReservationStatus.Seated).ToList();
        var occupiedIds = seated.Select(r => r.tableID).Concat(tables.Where(t => t.tableStatus == "Occupied").Select(t => t.tableID)).ToHashSet();
        OccupiedNow = tables.Count(t => occupiedIds.Contains(t.tableID));
        GuestsNow = seated.Sum(r => r.partySize);
        OccupiedByArea = areas.Select(g => new AreaCount(g.Key, g.Count(t => occupiedIds.Contains(t.tableID)), g.Count())).ToList();
        Arrivals = todayReservations.Where(r => ReservationStatus.Active.Contains(r.reservationStatus) || r.reservationStatus == ReservationStatus.Completed)
            .OrderBy(r => r.reservationDateTime).ToList();
        WindowStart = TimeSpan.FromHours(18);
        WindowEnd = TimeSpan.FromHours(22);
        if (Arrivals.Count > 0)
        {
            WindowStart = TimeSpan.FromHours(Math.Min(18, Arrivals.First().reservationDateTime.Hour));
            WindowEnd = TimeSpan.FromHours(Math.Max(22, Arrivals.Last().reservationDateTime.Hour + 1));
        }

        RecentActivity = await LoadRecentActivityAsync();
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

    // Formats activity timestamps used by the dashboard view.
    public static string Ago(DateTimeOffset when)
    {
        var span = DateTimeOffset.Now - when;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} minute{(span.TotalMinutes < 2 ? "" : "s")} ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hour{(span.TotalHours < 2 ? "" : "s")} ago";
        return when.ToString("d MMM, HH:mm");
    }
}
