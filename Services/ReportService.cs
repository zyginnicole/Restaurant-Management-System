using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;
using System.Globalization; //helps to deal with regional standards (e.g. diff date formats)

namespace RestaurantManagementSystem.Services;

// ReportService holds ALL the analytics queries. The dashboard, the exports and the chatbot
// all call this one class, so each calculation is written in exactly one place.


public class ReportService
{
    private readonly ApplicationDbContext _db;
    private readonly SettingsService _settings;
    public ReportService(ApplicationDbContext db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    // Assumptions used by the table-utilisation formula (see GetTableUtilizationAsync).
    private const double HoursPerReservation = 2;   // average time a party occupies a table
    private const double OpenHoursPerDay = 10;      // opening hours per day 


    // 1. DATE RANGE:
    // Dashboard buttons: turns "today" / "week" / "month" / "custom" into Start and End dates
    public static DateRange GetRange(string? range, DateTime? from, DateTime? to)
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        switch ((range ?? "month").ToLower())
        {
            case "today":
                return new DateRange(today, tomorrow, "Today");

            case "week":
                int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;   // Monday = 0 ... Sunday = 6
                var monday = today.AddDays(-daysSinceMonday);
                return new DateRange(monday, tomorrow, "This Week");    // stop at today, no future days

            case "custom" when from.HasValue && to.HasValue:
                var start = from.Value.Date;
                var end = to.Value.Date.AddDays(1);                     // +1 so the "to" day is included
                if (end <= start) (start, end) = (end.AddDays(-1), start.AddDays(1)); // swap if dates were reversed
                return new DateRange(start, end, "Custom Range");

            default:   // "month" (also the fallback)
                var firstOfMonth = new DateTime(today.Year, today.Month, 1);
                return new DateRange(firstOfMonth, tomorrow, "This Month");
        }
    }

    //The same number of days directly before a range (used by the dashboard's "vs previous period" comparisons)
    public static DateRange PreviousRange(DateRange r)
    {
        var length = r.End - r.Start;
        return new DateRange(r.Start - length, r.Start, "Previous Period");
    }


    // 2. KPI CARDS
    public async Task<KpiSummary> GetKpisAsync(DateRange r, DateRange previous)
    {
        var k = new KpiSummary();

        // Revenue
        k.Revenue = await RevenueAsync(r);
        k.PreviousRevenue = await RevenueAsync(previous);
        k.RevenueChangePercent = Change(k.Revenue, k.PreviousRevenue);

        // Reservations
        var reservations = ReservationsIn(r);
        k.Reservations = await reservations.CountAsync();
        k.Completed = await reservations.CountAsync(x => x.reservationStatus == "Completed");
        k.Cancelled = await reservations.CountAsync(x => x.reservationStatus == "Cancelled");
        k.OtherStatuses = k.Reservations - k.Completed - k.Cancelled;
        k.PreviousReservations = await ReservationsIn(previous).CountAsync();
        k.ReservationsChangePercent = Change(k.Reservations, k.PreviousReservations);

        // Registered customers
        DateTimeOffset fromAt = Off(r.Start), toAt = Off(r.End);
        DateTimeOffset prevFrom = Off(previous.Start), prevTo = Off(previous.End);
        k.Customers = await _db.Customers.CountAsync();
        k.NewCustomers = await _db.Customers.CountAsync(c => c.custCreateAt >= fromAt && c.custCreateAt < toAt);
        k.PreviousNewCustomers = await _db.Customers.CountAsync(c => c.custCreateAt >= prevFrom && c.custCreateAt < prevTo);
        k.NewCustomersChangePercent = Change(k.NewCustomers, k.PreviousNewCustomers);

        // Ratings. (double?) lets Average return null when there are no ratings instead of crashing.
        var ratings = _db.Feedbacks.Where(f => f.feedbackDate >= fromAt && f.feedbackDate < toAt);
        k.ReviewCount = await ratings.CountAsync();
        k.AverageRating = Math.Round(await ratings.AverageAsync(f => (double?)f.rating) ?? 0, 1);
        k.PreviousAverageRating = Math.Round(await _db.Feedbacks
            .Where(f => f.feedbackDate >= prevFrom && f.feedbackDate < prevTo)
            .AverageAsync(f => (double?)f.rating) ?? 0, 1);

        // How many 5, 4, 3, 2 and 1 star ratings, and their share in percent
        var counts = await ratings.GroupBy(f => f.rating)
            .Select(g => new { Stars = g.Key, Count = g.Count() }).ToListAsync();
        foreach (var c in counts.Where(c => c.Stars >= 1 && c.Stars <= 5))
        {
            k.StarCounts[c.Stars] = c.Count;
            k.StarSharePercent[c.Stars] = k.ReviewCount == 0
                ? 0
                : (int)Math.Round(c.Count * 100.0 / k.ReviewCount);
        }
        return k;
    }

    //Percent change versus the previous. 0 when there is nothing to compare with. 
    private static double Change(decimal current, decimal previous) =>
        previous == 0 ? 0 : Math.Round((double)((current - previous) / previous * 100), 1);

    private static double Change(int current, int previous) =>
        previous == 0 ? 0 : Math.Round((double)((current - previous) / (double)previous * 100), 1);

    //normalizes incoming date inputs to prevent timezone issues
    private static DateTimeOffset Off(DateTime value)
    {
        return new DateTimeOffset(value);
    }

    //filters and returns a queryable list of reservations that fall wthin the specified date range
    private IQueryable<Reservation> ReservationsIn(DateRange range)
    {
        return _db.Reservations.Where(r =>
            r.reservationDateTime >= range.Start &&
            r.reservationDateTime < range.End);
    }

    // Calculates and returns the total revenue asynchronously for the given date range.
    private Task<decimal> RevenueAsync(DateRange range)
    {
        return RevenueBetweenAsync(range.Start, range.End);
    }

    public async Task<SummaryDto> GetSummaryAsync(DateRange r)
    {
        // The "previous period" is the same number of days directly before the selected period.
        var previousStart = r.Start - (r.End - r.Start);
        decimal revenue = await RevenueBetweenAsync(r.Start, r.End);
        decimal previousRevenue = await RevenueBetweenAsync(previousStart, r.Start);
        double change = previousRevenue == 0 ? 0 : (double)((revenue - previousRevenue) / previousRevenue * 100);

        DateTimeOffset fromAt = Off(r.Start);
        DateTimeOffset toAt = Off(r.End);
        DateTimeOffset weekAgo = new(DateTime.Today.AddDays(-7));

        var reservations = _db.Reservations
            .Where(x => x.reservationDateTime >= r.Start && x.reservationDateTime < r.End);
        var ratings = _db.Feedbacks.Where(f => f.feedbackDate >= fromAt && f.feedbackDate < toAt);

        return new SummaryDto
        {
            Revenue = revenue,
            RevenueChangePercent = Math.Round(change, 1),
            Reservations = await reservations.CountAsync(),
            Completed = await reservations.CountAsync(x => x.reservationStatus == "Completed"),
            Cancelled = await reservations.CountAsync(x => x.reservationStatus == "Cancelled"),
            Customers = await _db.Customers.CountAsync(),
            NewCustomersThisWeek = await _db.Customers.CountAsync(c => c.custCreateAt >= weekAgo),
            // (double?) lets Average return null when there are no ratings, instead of crashing
            AverageRating = Math.Round(await ratings.AverageAsync(f => (double?)f.rating) ?? 0, 1),
            ReviewCount = await ratings.CountAsync()
        };
    }

    // Total of all PAID payments in a period. Only paid money counts as revenue.
    private async Task<decimal> RevenueBetweenAsync(DateTime start, DateTime end)
    {
        DateTimeOffset fromDate = new(start);
        DateTimeOffset toDate = new(end);

        return await _db.Payments
            .Where(p => p.paymentStatus == "Paid" && p.paymentDate >= fromDate && p.paymentDate < toDate)
            .SumAsync(p => (decimal?)p.paymentAmount) ?? 0;   // (decimal?) so an empty table gives 0, not an error
    }

    // ------------------------------------------------------------------
    // 3. LIVE OPERATIONS (today only)
    //    The team's models have no "Seated" status, so "seated" uses DiningTable.tableStatus == "Occupied".
    // ------------------------------------------------------------------
    public async Task<LiveSnapshot> GetLiveAsync()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var now = DateTime.Now;

        // Today's reservations except cancelled ones (a restaurant has a few dozen at most, so loading them is fine)
        var todays = await _db.Reservations.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.DiningTable)
            .Where(r => r.reservationDateTime >= today && r.reservationDateTime < tomorrow
                        && r.reservationStatus != "Cancelled")
            .OrderBy(r => r.reservationDateTime)
            .ToListAsync();

        // "Seated" = staff marked the reservation Seated, or it is confirmed and its table is marked Occupied
        var seated = todays.Where(r => r.reservationStatus == "Seated"
                                    || (r.reservationStatus == "Confirmed" && r.DiningTable?.tableStatus == "Occupied")).ToList();

        var occupiedTableIds = await _db.DiningTables
            .Where(t => t.tableStatus == "Occupied").Select(t => t.tableID).ToListAsync();

        return new LiveSnapshot
        {
            TotalTables = await _db.DiningTables.CountAsync(),
            TablesSeated = seated.Select(r => r.tableID).Union(occupiedTableIds).Distinct().Count(),
            GuestsSeated = seated.Sum(r => r.partySize),
            CoversBooked = todays.Sum(r => r.partySize),
            PendingApprovals = todays.Count(r => r.reservationStatus == "Pending"),
            NextArrival = todays
                .Where(r => (r.reservationStatus == "Pending" || r.reservationStatus == "Confirmed") && r.reservationDateTime > now)
                .Select(r => (DateTime?)r.reservationDateTime).FirstOrDefault(),
            Feed = todays.Take(8).Select(r => new FeedItem(
                 r.reservationDateTime,
                    r.Customer?.custName ?? "Guest",
                    r.partySize,
                    r.DiningTable?.tableNumber ?? "-",
                    r.reservationStatus)).ToList()
            };
    }

    // 4. TABLE UTILISATION per dining area (Indoor / Outdoor / VIP)
    //    utilisation % = (reservations x 2 hours) / (tables x days x 10 open hours) x 100
    public async Task<List<NameValue>> GetTableUtilizationAsync(DateRange r)
    {
        int days = Math.Max(1, (int)(r.End - r.Start).TotalDays);

        // How many tables each dining area has
        var tablesPerArea = await _db.DiningTables
            .GroupBy(t => t.DiningArea!.areaName)
            .Select(g => new { Area = g.Key, Count = g.Count() })
            .ToListAsync();

        // How many reservations each dining area received (cancelled ones do not use the table)
        var bookedPerArea = await _db.Reservations
            .Where(x => x.reservationDateTime >= r.Start && x.reservationDateTime < r.End
                        && x.reservationStatus != "Cancelled")
            .GroupBy(x => x.DiningTable!.DiningArea!.areaName)
            .Select(g => new { Area = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Area, x => x.Count);

        return tablesPerArea.Select(a =>
        {
            double hoursAvailable = a.Count * days * OpenHoursPerDay;
            double hoursUsed = bookedPerArea.GetValueOrDefault(a.Area) * HoursPerReservation;
            double percent = Math.Min(100, hoursUsed / hoursAvailable * 100);   // never above 100%
            return new NameValue(a.Area, Math.Round(percent));
        }).ToList();
    }

    // ------------------------------------------------------------------
    // 5. PEAK DINING HOURS: number of reservations per hour (12:00 to 22:00)
    // ------------------------------------------------------------------
    public async Task<List<NameValue>> GetPeakHoursAsync(DateRange r)
    {
        var countsByHour = await _db.Reservations
            .Where(x => x.reservationDateTime >= r.Start && x.reservationDateTime < r.End
                        && x.reservationStatus != "Cancelled")
            .GroupBy(x => x.reservationDateTime.Hour)
            .Select(g => new { Hour = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Hour, x => x.Count);

        // Always return every hour so hours with no bookings still appear as 0 on the chart
        return Enumerable.Range(12, 11)
            .Select(h => new NameValue($"{h}:00", countsByHour.GetValueOrDefault(h)))
            .ToList();
    }

    // ------------------------------------------------------------------
    // 6. POPULAR MENU ITEMS: total quantity sold per item (also used by the chatbot)
    // ------------------------------------------------------------------
    public async Task<List<TopItem>> GetTopItemsAsync(DateRange r, int take = 5)
    {
        DateTimeOffset fromDate = new(r.Start);
        DateTimeOffset toDate = new(r.End);

        var rows = await _db.OrderItems
            .Where(i => i.Order!.orderDateTime >= fromDate && i.Order.orderDateTime < toDate
                        && i.Order.orderStatus != "Cancelled")
            .GroupBy(i => i.Menu!.menuName)
            .Select(g => new { Name = g.Key, Quantity = g.Sum(i => i.quantity), Revenue = g.Sum(i => i.quantity * i.unitPrice) })
            .OrderByDescending(x => x.Quantity).ThenBy(x => x.Name)
            .Take(take)
            .ToListAsync();

        return rows.Select(x => new TopItem(x.Name, x.Quantity, x.Revenue)).ToList();
    }

    // ------------------------------------------------------------------
    // 7. DAILY REPORT: one row per day. Feeds the sales chart AND the CSV / Excel / PDF export.
    // ------------------------------------------------------------------
    // Revenue per day / month / year
    public async Task<List<NameValue>> GetRevenueSeriesAsync(DateRange r, Granularity g)
    {
        DateTimeOffset fromAt = Off(r.Start), toAt = Off(r.End);

        var perDay = await _db.Payments
            .Where(p => p.paymentStatus == "Paid" && p.paymentDate >= fromAt && p.paymentDate < toAt)
            .GroupBy(p => p.paymentDate.Date)
            .Select(x => new { Day = x.Key, Total = x.Sum(p => p.paymentAmount) })
            .ToListAsync();

        return Bucket(r, g, perDay.ToDictionary(x => x.Day, x => (double)x.Total));
    }

    // New customers per day / month / year
    public async Task<List<NameValue>> GetNewCustomerSeriesAsync(DateRange r, Granularity g)
    {
        DateTimeOffset fromAt = Off(r.Start), toAt = Off(r.End);

        var perDay = await _db.Customers
            .Where(c => c.custCreateAt >= fromAt && c.custCreateAt < toAt)
            .GroupBy(c => c.custCreateAt.Date)
            .Select(x => new { Day = x.Key, Count = x.Count() })
            .ToListAsync();

        return Bucket(r, g, perDay.ToDictionary(x => x.Day, x => (double)x.Count));
    }

    // Reservations by status (Completed, Cancelled, Pending ...)
    public async Task<List<NameValue>> GetStatusBreakdownAsync(DateRange r)
    {
        var rows = await ReservationsIn(r)
            .GroupBy(x => x.reservationStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync();

        return rows.Select(x => new NameValue(x.Status, x.Count)).ToList();
    }

    // Turns "value per day" into one point per day, month or year, including periods with no data (0)
    private static List<NameValue> Bucket(DateRange r, Granularity g, Dictionary<DateTime, double> perDay)
    {
        var result = new List<NameValue>();
        var lastDay = r.End.AddDays(-1);

        switch (g)
        {
            case Granularity.Day:
                bool sameMonth = r.Start.Year == lastDay.Year && r.Start.Month == lastDay.Month;
                for (var d = r.Start.Date; d < r.End; d = d.AddDays(1))
                    result.Add(new NameValue(sameMonth ? d.Day.ToString() : d.ToString("d MMM", CultureInfo.InvariantCulture),
                                             perDay.GetValueOrDefault(d)));
                break;

            case Granularity.Month:
                for (var m = new DateTime(r.Start.Year, r.Start.Month, 1); m < r.End; m = m.AddMonths(1))
                {
                    var next = m.AddMonths(1);
                    double total = perDay.Where(kv => kv.Key >= m && kv.Key < next).Sum(kv => kv.Value);
                    result.Add(new NameValue(m.ToString("MMM yyyy", CultureInfo.InvariantCulture), total));
                }
                break;

            default: // Year
                for (var y = new DateTime(r.Start.Year, 1, 1); y < r.End; y = y.AddYears(1))
                {
                    var next = y.AddYears(1);
                    double total = perDay.Where(kv => kv.Key >= y && kv.Key < next).Sum(kv => kv.Value);
                    result.Add(new NameValue(y.Year.ToString(), total));
                }
                break;
        }
        return result;
    }


  // =====================================================================
    // 8. SALES REPORT: orders that are not Cancelled and have a Paid payment, grouped by day or month
    //    net sales = items subtotal; service charge = dine-in only (same rule as Checkout);
    //    SST = what is left of the order total, so the columns always add up (same rule as the Payment page).
    // =====================================================================
    public async Task<SalesReport> GetSalesReportAsync(DateRange r, bool monthly)
    {
        var settings = await _settings.GetAsync();
        decimal servicePct = (decimal?)settings.serviceChargePct ?? 10m;
        DateTimeOffset fromAt = Off(r.Start), toAt = Off(r.End);

        var orders = await _db.Orders.AsNoTracking()
            .Include(o => o.OrderItems)
            .Include(o => o.Reservation)
            .Where(o => o.orderDateTime >= fromAt && o.orderDateTime < toAt
                        && o.orderStatus != "Cancelled"
                        && o.Payment != null && o.Payment.paymentStatus == "Paid")
            .ToListAsync();

        var rows = orders
            .GroupBy(o => monthly ? new DateTime(o.orderDateTime.Year, o.orderDateTime.Month, 1) : o.orderDateTime.Date)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                decimal net = 0, service = 0, sst = 0, total = 0;
                foreach (var o in g)
                {
                    // Net sales are after line discounts; service charge is calculated on that discounted subtotal.
                    decimal subtotal = o.OrderItems.Sum(i => i.quantity * i.unitPrice - i.discountAmount);
                    // Dine-in = the order has a table (the rule used by the Orders and Payment pages)
                    decimal serviceCharge = o.tableID != null
                        ? Math.Round(subtotal * servicePct / 100m, 2, MidpointRounding.AwayFromZero)
                        : 0m;
                    net += subtotal;
                    service += serviceCharge;
                    sst += o.totalAmount - subtotal - serviceCharge;
                    total += o.totalAmount;
                }

                // Covers = guests of the reservations behind these orders (each reservation counted once; takeaway = 0)
                int covers = g.Where(o => o.Reservation != null)
                              .Select(o => o.Reservation!)
                              .DistinctBy(x => x.reservationID)
                              .Sum(x => x.partySize);

                string label = monthly
                    ? g.Key.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
                    : g.Key.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

                return new SalesRow(g.Key, label, g.Count(), covers, net, service, sst, total);
            })
            .ToList();

        return new SalesReport
        {
            Period = monthly ? "monthly" : "daily",
            RangeLabel = $"{r.Start:dd MMM yyyy} to {r.End.AddDays(-1):dd MMM yyyy}",
            Rows = rows
        };
    }
}
