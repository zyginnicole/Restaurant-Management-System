namespace RestaurantManagementSystem.Services;

// DTOs carry report and chatbot results from services to page models.
public enum Granularity { Day, Month, Year }

// End is exclusive: a range for one day ends at the next day's midnight.
public record DateRange(DateTime Start, DateTime End, string Label);

public record NameValue(string Name, double Value);

public record TopItem(string Name, int Quantity, decimal Revenue)
{
    public string Category { get; init; } = "Menu";
}

public record ChatRequest(string Message);

// KPI values shared by the admin dashboard and statistic report.
public class KpiSummary
{
    public decimal Revenue { get; set; }
    public decimal PreviousRevenue { get; set; }
    public double RevenueChangePercent { get; set; }
    public int Reservations { get; set; }
    public int PreviousReservations { get; set; }
    public double ReservationsChangePercent { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int OtherStatuses { get; set; }
    public int Customers { get; set; }
    public int NewCustomers { get; set; }
    public int PreviousNewCustomers { get; set; }
    public double NewCustomersChangePercent { get; set; }
    public int PromoOptInCustomers { get; set; }
    public double AverageRating { get; set; }
    public double PreviousAverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int[] StarCounts { get; set; } = new int[6];
    public int[] StarSharePercent { get; set; } = new int[6];
}

// Data used by the statistic report and its exports.
public class StatisticReport
{
    public string Period { get; set; } = "monthly";
    public DateRange Range { get; set; } = new(DateTime.Today, DateTime.Today.AddDays(1), "");
    public KpiSummary Kpis { get; set; } = new();
    public List<NameValue> RevenueSeries { get; set; } = new();
    public List<NameValue> NewCustomerSeries { get; set; } = new();
    public List<NameValue> PeakHours { get; set; } = new();
    public List<TopItem> TopItems { get; set; } = new();
    public List<NameValue> Utilization { get; set; } = new();
    public List<NameValue> StatusBreakdown { get; set; } = new();
}

public record SalesRow(DateTime Period, string Label, int Orders, int Covers,
                       decimal NetSales, decimal ServiceCharge, decimal Sst, decimal Total);

// Data used by the sales report and its exports.
public class SalesReport
{
    public string Period { get; set; } = "daily";
    public string RangeLabel { get; set; } = "";
    public List<SalesRow> Rows { get; set; } = new();
    public int TotalOrders => Rows.Sum(r => r.Orders);
    public int TotalCovers => Rows.Sum(r => r.Covers);
    public decimal TotalNetSales => Rows.Sum(r => r.NetSales);
    public decimal TotalServiceCharge => Rows.Sum(r => r.ServiceCharge);
    public decimal TotalSst => Rows.Sum(r => r.Sst);
    public decimal GrandTotal => Rows.Sum(r => r.Total);
}
