using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin;

// PageModel powering the Admin Reports dashboard and export functionality.
public class ReportsModel : PageModel
{
    private readonly ReportService _reports;
    private readonly ExportService _exports;

    // The currently active date range filter (defaults to current month).
    public DateRange SelectedRange { get; private set; } =
        ReportService.GetRange("month", null, null);

    // The active granularity grouping ("daily" or "monthly").
    public string SelectedPeriod { get; private set; } = "daily";

    // Holds the compiled sales report data and summary totals to display in the UI.
    public SalesReport Sales { get; private set; } = new();
    public KpiSummary StatisticKpis { get; private set; } = new();
    public List<NameValue> StatisticRevenue { get; private set; } = new();
    public List<NameValue> StatisticNewCustomers { get; private set; } = new();
    public List<NameValue> StatisticPeakHours { get; private set; } = new();
    public List<TopItem> StatisticTopItems { get; private set; } = new();
    public List<NameValue> StatisticUtilization { get; private set; } = new();
    public List<NameValue> StatisticStatusBreakdown { get; private set; } = new();

    // Constructor initializing injected reporting and export services.
    public ReportsModel(
        ReportService reports,
        ExportService exports)
    {
        _reports = reports;
        _exports = exports;
    }

    // Handles standard page load, parsing date range and period parameters to fetch sales data.
    public async Task OnGetAsync(
        string? range = "month",
        DateTime? from = null,
        DateTime? to = null,
        string? period = "daily",
        string? tab = "statistic")
    {
        if (tab?.Equals("statistic", StringComparison.OrdinalIgnoreCase) == true)
            SelectedRange = StatisticRange(period, from, to);
        else
            SelectedRange = ReportService.GetRange(range, from, to);

        // Validates and sets period grouping to either "monthly" or "daily"
        SelectedPeriod = period?.Equals(
            "monthly",
            StringComparison.OrdinalIgnoreCase) == true
                ? "monthly"
                : "daily";

        // Fetches aggregated sales report data from the database
        Sales = await _reports.GetSalesReportAsync(
            SelectedRange,
            SelectedPeriod == "monthly");

        var previous = ReportService.PreviousRange(SelectedRange);
        StatisticKpis = await _reports.GetKpisAsync(SelectedRange, previous);
        var granularity = period?.Equals("yearly", StringComparison.OrdinalIgnoreCase) == true
            ? Granularity.Year
            : Granularity.Month;
        StatisticRevenue = await _reports.GetRevenueSeriesAsync(SelectedRange, granularity);
        StatisticNewCustomers = await _reports.GetNewCustomerSeriesAsync(SelectedRange, granularity);
        StatisticPeakHours = await _reports.GetPeakHoursAsync(SelectedRange);
        StatisticTopItems = await _reports.GetTopItemsAsync(SelectedRange, 5);
        StatisticUtilization = await _reports.GetTableUtilizationAsync(SelectedRange);
        StatisticStatusBreakdown = await _reports.GetStatusBreakdownAsync(SelectedRange);
    }

    private static DateRange StatisticRange(string? period, DateTime? from, DateTime? to)
    {
        if (period?.Equals("yearly", StringComparison.OrdinalIgnoreCase) == true)
        {
            int startYear = from?.Year ?? DateTime.Today.Year;
            int endYear = to?.Year ?? startYear;
            if (endYear < startYear) (startYear, endYear) = (endYear, startYear);
            return new DateRange(new DateTime(startYear, 1, 1), new DateTime(endYear + 1, 1, 1), "Yearly");
        }

        var start = new DateTime(from?.Year ?? DateTime.Today.Year, from?.Month ?? 1, 1);
        var endMonth = new DateTime(to?.Year ?? start.Year, to?.Month ?? start.Month, 1);
        if (endMonth < start) (start, endMonth) = (endMonth, start);
        return new DateRange(start, endMonth.AddMonths(1), "Monthly");
    }

    // Handles file download requests, exporting the sales report as CSV, Excel, or PDF.
    public async Task<IActionResult> OnGetExportAsync(
        string format = "csv",
        string? range = "month",
        DateTime? from = null,
        DateTime? to = null,
        string? period = "daily")
    {
        var selectedRange = ReportService.GetRange(
            range,
            from,
            to);

        var monthly = period?.Equals(
            "monthly",
            StringComparison.OrdinalIgnoreCase) == true;

        // Retrieves sales report for the specified export range and period
        var report = await _reports.GetSalesReportAsync(
            selectedRange,
            monthly);

        // Generates structured filename (e.g., Lumiere-report-20261001-20261031)
        var fileName =
            $"Lumiere-report-{selectedRange.Start:yyyyMMdd}-{selectedRange.End.AddDays(-1):yyyyMMdd}";

        // Matches export format and returns the generated file stream with correct MIME type
        return format.Trim().ToLowerInvariant() switch
        {
            "csv" => File(
                _exports.ToCsv(report),
                "text/csv",
                fileName + ".csv"),

            "xlsx" or "excel" => File(
                _exports.ToExcel(report),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName + ".xlsx"),

            "pdf" => File(
                _exports.ToPdf(
                    report,
                    $"Lumiere report · {selectedRange.Label}"),
                "application/pdf",
                fileName + ".pdf"),

            _ => BadRequest(new
            {
                error = "format must be csv, xlsx, excel or pdf"
            })
        };
    }
}
