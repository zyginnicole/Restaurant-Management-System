using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin dashboard: live operations (today) and sales & trends (for the chosen date range).
    // Access is limited to the Admin role by the folder rule in Program.cs.
    //  - Date buttons: ?range=today | week | month (default) | custom&from=2026-09-01&to=2026-09-15
    //  - ?handler=Live returns today's live figures as JSON, so the page can refresh them without reloading.
    //  - ?handler=Export&format=csv|excel|pdf&range=... downloads the daily sales report for the range.
    public class DashboardModel : PageModel
    {
        private readonly ReportService _reports;
        private readonly ExportService _export;

        public DashboardModel(ReportService reports, ExportService export)
        {
            _reports = reports;
            _export = export;
        }

        // ---- What the page shows (read these through "Model" in the .cshtml) ----
        public string Range { get; set; } = "month";           // which date button is active
        public DateTime? From { get; set; }                     // custom range inputs
        public DateTime? To { get; set; }
        public string RangeLabel { get; set; } = "";            // "This Month", "Today", ...

        public KpiSummary Kpis { get; set; } = new();           // the four cards at the top
        public LiveSnapshot Live { get; set; } = new();         // Live operations tab
        public List<NameValue> Utilization { get; set; } = new();   // table utilization by area (percent)
        public List<NameValue> DailySales { get; set; } = new();    // revenue per day (Sales & trends chart)
        public List<NameValue> PeakHours { get; set; } = new();     // reservations per hour
        public List<TopItem> TopItems { get; set; } = new();        // top 5 menu items

        // The two charts as ready-made JSON for the page script, e.g.  const sales = @Html.Raw(Model.SalesChartJson);
        public string SalesChartJson => ChartJson(DailySales);
        public string PeakChartJson => ChartJson(PeakHours);

        public async Task OnGetAsync(string? range, DateTime? from, DateTime? to)
        {
            var r = ReportService.GetRange(range, from, to);

            Range = (range ?? "month").ToLower();
            if (Range != "today" && Range != "week" && Range != "custom") Range = "month";
            From = from;
            To = to;
            RangeLabel = r.Label;

            Kpis = await _reports.GetKpisAsync(r, ReportService.PreviousRange(r));
            Live = await _reports.GetLiveAsync();
            Utilization = await _reports.GetUtilizationAsync(r);
            DailySales = await _reports.GetRevenueSeriesAsync(r, Granularity.Day);
            PeakHours = await _reports.GetPeakHoursAsync(r);
            TopItems = await _reports.GetTopItemsAsync(r);
        }

        // Live operations only (cheap), for auto-refresh
        public async Task<IActionResult> OnGetLiveAsync() => new JsonResult(await _reports.GetLiveAsync());

        // Download the daily sales report for the selected range
        public async Task<IActionResult> OnGetExportAsync(string format, string? range, DateTime? from, DateTime? to)
        {
            var r = ReportService.GetRange(range, from, to);
            var report = await _reports.GetSalesReportAsync(r, monthly: false);
            var name = $"sales-report-{r.Start:yyyyMMdd}-{r.End.AddDays(-1):yyyyMMdd}";

            return format.ToLower() switch
            {
                // + ".CSV" forces the browser to download a .csv file.
                "csv" => File(_export.ToCsv(report), "text/csv", name + ".csv"),
                "excel" => File(_export.ToExcel(report),
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx"),
                "pdf" => File(_export.ToPdf(report, "Sales Report"), "application/pdf", name + ".pdf"),
                _ => BadRequest("Unknown format. Use csv, excel or pdf.")
            };
        }

        private static string ChartJson(List<NameValue> series) =>
            JsonSerializer.Serialize(new
            {
                labels = series.Select(s => s.Name),
                values = series.Select(s => s.Value)
            });
    }
}
