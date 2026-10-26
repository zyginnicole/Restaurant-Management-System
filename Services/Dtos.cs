namespace RestaurantManagementSystem.Services;

// DTOs ("data transfer objects") are small classes that carry results from the service to the page.
// Records are used because they are short and read-only.

//How a series is bucketed, one point per day, per month or per year 
public enum Granularity { Day, Month, Year }

// A reporting period. End is EXCLUSIVE, so "today" is Start = today 00:00, End = tomorrow 00:00.
public record DateRange(DateTime Start, DateTime End, string Label);

// One day in the sales report (used by the chart AND by the CSV / Excel / PDF export).
public record DailyRow(DateTime Date, int Reservations, int Orders, decimal Revenue);

// A simple label + number pair used for bar charts and progress bars.
public record NameValue(string Name, double Value);

//One row of the "Top Menu Items" quantity sold and quantity x unit price
public record TopItem(string Name, int Quantity, decimal Revenue);

// One line in the "Live Reservation Feed".
public record FeedItem(DateTime Time, string Customer, int Guests, string Table, string Status);

public record ChatRequest(string Message);

// The four KPI cards (used by the Admin dashboard and the Statistic report).
// Every figure comes with the same figure for the previous period of the same length.
public class KpiSummary
{
    // Revenue = sum of Paid payments
    public decimal Revenue { get; set; }
    public decimal PreviousRevenue { get; set; }
    public double RevenueChangePercent { get; set; }

    // Reservations in the period, split by status
    public int Reservations { get; set; }
    public int PreviousReservations { get; set; }
    public double ReservationsChangePercent { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int OtherStatuses { get; set; }

    // Registered customers: all customers, and how many joined in the period
    public int Customers { get; set; }
    public int NewCustomers { get; set; }
    public int PreviousNewCustomers { get; set; }
    public double NewCustomersChangePercent { get; set; }

    // Ratings in the period. StarCounts / StarSharePercent use the star number as the index (1 to 5; index 0 unused).
    public double AverageRating { get; set; }
    public double PreviousAverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int[] StarCounts { get; set; } = new int[6];
    public int[] StarSharePercent { get; set; } = new int[6];
}

// The "Live operations" tab (always about TODAY, whatever date range is selected)
public class LiveSnapshot
{
    public int TablesSeated { get; set; }
    public int TotalTables { get; set; }
    public int GuestsSeated { get; set; }
    public int CoversBooked { get; set; }
    public int PendingApprovals { get; set; }
    public DateTime? NextArrival { get; set; }
    public List<FeedItem> Feed { get; set; } = new();
}

// The Statistic report on the Reports page
public class StatisticReport
{
    public string Period { get; set; } = "monthly";        // monthly or yearly
    public DateRange Range { get; set; } = new(DateTime.Today, DateTime.Today.AddDays(1), "");
    public DateRange PreviousRange { get; set; } = new(DateTime.Today, DateTime.Today.AddDays(1), "");
    public KpiSummary Kpis { get; set; } = new();
    public List<NameValue> RevenueSeries { get; set; } = new();       // revenue per month / year
    public List<NameValue> NewCustomerSeries { get; set; } = new();   // new customers per month / year
    public List<NameValue> PeakHours { get; set; } = new();
    public List<TopItem> TopItems { get; set; } = new();
    public List<NameValue> Utilization { get; set; } = new();
    public List<NameValue> StatusBreakdown { get; set; } = new();     // reservations by status
    public string? Notice { get; set; }                               // e.g. "Range shortened to 36 months"
}

// One row of the Sales report (one day or one month)
public record SalesRow(DateTime Period, string Label, int Orders, int Covers,
                       decimal NetSales, decimal ServiceCharge, decimal Sst, decimal Total);

// The Sales report on the Reports page
public class SalesReport
{
    public string Period { get; set; } = "daily";          // daily or monthly
    public string RangeLabel { get; set; } = "";
    public List<SalesRow> Rows { get; set; } = new();
    public int TotalOrders => Rows.Sum(r => r.Orders);
    public int TotalCovers => Rows.Sum(r => r.Covers);
    public decimal TotalNetSales => Rows.Sum(r => r.NetSales);
    public decimal TotalServiceCharge => Rows.Sum(r => r.ServiceCharge);
    public decimal TotalSst => Rows.Sum(r => r.Sst);
    public decimal GrandTotal => Rows.Sum(r => r.Total);
    public string? Notice { get; set; }
}

public class FeedbackDto
{
    public int Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Customer { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public int? ReservationId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
}

// The four KPI cards at the top of the dashboard.
public class SummaryDto
{
    public decimal Revenue { get; set; }
    public double RevenueChangePercent { get; set; }
    public int Reservations { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int Customers { get; set; }
    public int NewCustomersThisWeek { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
}

// The "Live Operations" tab (always about TODAY, whatever date range is selected).
public class LiveDto
{
    public int TablesSeated { get; set; }
    public int TotalTables { get; set; }
    public int GuestsSeated { get; set; }
    public int CoversBooked { get; set; }
    public int PendingApprovals { get; set; }
    public DateTime? NextArrival { get; set; }
    public List<FeedItem> Feed { get; set; } = new();
}
