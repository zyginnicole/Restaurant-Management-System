using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin;

// PageModel powering the Admin Reports dashboard and export functionality.
public class ReportsModel : PageModel
{
    private readonly ReportService _reports;
    private readonly ExportService _exports;
    private readonly SettingsService _settings;

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
        ExportService exports,
        SettingsService settings)
    {
        _reports = reports;
        _exports = exports;
        _settings = settings;
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
        var today = DateTime.Today;

        if (period?.Equals("yearly", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Keep the first server load consistent with the year selectors shown by the page.
            int startYear = from?.Year ?? today.Year - 2;
            int endYear = to?.Year ?? today.Year;
            if (endYear < startYear) (startYear, endYear) = (endYear, startYear);
            string label = startYear == endYear
                ? startYear.ToString()
                : $"{startYear} - {endYear}";
            return new DateRange(new DateTime(startYear, 1, 1), new DateTime(endYear + 1, 1, 1), label);
        }

        // Keep the first server load consistent with the month selectors shown by the page.
        var defaultStart = new DateTime(today.Year - 2, 1, 1);
        var defaultEnd = new DateTime(today.Year, today.Month, 1);
        var start = new DateTime(from?.Year ?? defaultStart.Year, from?.Month ?? defaultStart.Month, 1);
        var endMonth = new DateTime(to?.Year ?? defaultEnd.Year, to?.Month ?? defaultEnd.Month, 1);
        if (endMonth < start) (start, endMonth) = (endMonth, start);
        string monthlyLabel = start == endMonth
            ? start.ToString("MMMM yyyy")
            : $"{start:MMMM yyyy} - {endMonth:MMMM yyyy}";
        return new DateRange(start, endMonth.AddMonths(1), monthlyLabel);
    }

    // Handles file download requests, exporting the sales report as CSV, Excel, or PDF.
    public async Task<IActionResult> OnGetExportAsync(
        string format = "csv",
        string? range = "month",
        DateTime? from = null,
        DateTime? to = null,
        string? period = "daily",
        string? tab = "sales")
    {
        if (format.Trim().Equals("pdf", StringComparison.OrdinalIgnoreCase) &&
            tab?.Equals("statistic", StringComparison.OrdinalIgnoreCase) == true)
        {
            var statisticRange = StatisticRange(period, from, to);
            var statistic = await GetStatisticReportAsync(statisticRange, period);
            var statisticSystemName = FileNamePart((await _settings.GetAsync()).restaurantName);
            var statisticFileName = $"{statisticSystemName}-statistics-{statisticRange.Start:yyyyMMdd}-to-{statisticRange.End.AddDays(-1):yyyyMMdd}.pdf";

            return File(
                _exports.ToStatisticPdf(statistic, $"{statisticSystemName} · Statistic report · {statisticRange.Label}"),
                "application/pdf",
                statisticFileName);
        }

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

        var systemName = FileNamePart((await _settings.GetAsync()).restaurantName);

        // Generates a filename using the current Admin > Settings restaurant name.
        var fileName =
            $"{systemName}-report-{selectedRange.Start:yyyyMMdd}-{selectedRange.End.AddDays(-1):yyyyMMdd}";

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
                    $"{systemName} report · {selectedRange.Label}"),
                "application/pdf",
                fileName + ".pdf"),

            _ => BadRequest(new
            {
                error = "format must be csv, xlsx, excel or pdf"
            })
        };
    }

    private async Task<StatisticReport> GetStatisticReportAsync(DateRange range, string? period)
    {
        var previous = ReportService.PreviousRange(range);
        var granularity = period?.Equals("yearly", StringComparison.OrdinalIgnoreCase) == true
            ? Granularity.Year
            : Granularity.Month;

        return new StatisticReport
        {
            Period = period?.Equals("yearly", StringComparison.OrdinalIgnoreCase) == true ? "yearly" : "monthly",
            Range = range,
            Kpis = await _reports.GetKpisAsync(range, previous),
            RevenueSeries = await _reports.GetRevenueSeriesAsync(range, granularity),
            NewCustomerSeries = await _reports.GetNewCustomerSeriesAsync(range, granularity),
            PeakHours = await _reports.GetPeakHoursAsync(range),
            TopItems = await _reports.GetTopItemsAsync(range, 5),
            Utilization = await _reports.GetTableUtilizationAsync(range),
            StatusBreakdown = await _reports.GetStatusBreakdownAsync(range)
        };
    }

    private static string FileNamePart(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Trim().Select(c => invalid.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "Restaurant" : safe.Replace(' ', '-');
    }
}
