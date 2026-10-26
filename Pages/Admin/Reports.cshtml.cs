using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin
{

    public class ReportsModel : PageModel
    {

        private readonly ReportService _reports;
        private readonly ExportService _exports;

        //holds the date range filter chosen by the user
        public DateRange SelectedRange { get; private set; } = ReportService.GetRange("month", null, null);

        public SalesReport Sales { get; private set; } = new();

        public ReportsModel(ReportService reports, ExportService exports) { _reports = reports; _exports = exports; }


        public async Task OnGetAsync(
            string? range = "month",
            DateTime? from = null,
            DateTime? to = null,
            string? period = "daily")
        {
            SelectedRange = ReportService.GetRange(range, from, to);

            var monthly = period?.Equals(
                "monthly",
                StringComparison.OrdinalIgnoreCase) == true;

            Sales = await _reports.GetSalesReportAsync(
                SelectedRange,
                monthly);
        }

        //runs automatically whenever a user opens or navigates to the reports page
        public async Task<IActionResult> OnGetExportAsync(string format = "csv", string? range = "month", DateTime? from = null, DateTime? to = null)
        {
            //converts input parameters into a standardized date range object
            var selected = ReportService.GetRange(range, from, to);
            //fetches overall KPI summary numbers from the database
            var rows = await _reports.GetDailyRowsAsync(selected);
            //fetches daily breakdown records from the database for the data
            var name = $"MaisonDoree-report-{selected.Start:yyyyMMdd}-{selected.End.AddDays(-1):yyyyMMdd}";
            
            
            return format.Trim().ToLowerInvariant() switch
            {
                //returns CSV byte data with text/csv header
                "csv" => File(_exports.ToCsv(rows), "text/csv", name + ".csv"),
                //returns excel spreadsheet file
                "xlsx" or "excel" => File(_exports.ToExcel(rows), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name + ".xlsx"),
                "pdf" => File(_exports.ToPdf(rows, $"Maison Dorée report · {selected.Label}"), "application/pdf", name + ".pdf"),

                //if user enters an invalid format, return an error message
                _ => BadRequest(new { error = "format must be csv, xlsx or pdf" })
            };
        }
    }
}
