using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System.Globalization;
using System.Text;

namespace RestaurantManagementSystem.Services;

// ExportService turns the daily report rows into a downloadable file.
// Each method returns the file as a byte[] and the page sends it to the browser.
public class ExportService
{
    private static readonly string[] Headers =
        { "Period", "Orders", "Covers", "Net sales", "Service charge", "SST", "Total" };

    // ---------- CSV: plain text, one line per row ----------
    public byte[] ToCsv(SalesReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Headers));
        foreach (var r in report.Rows)
            sb.AppendLine(string.Join(",", Quote(r.Label), r.Orders, r.Covers,
                Money(r.NetSales), Money(r.ServiceCharge), Money(r.Sst), Money(r.Total)));
        sb.AppendLine(string.Join(",", "Total", report.TotalOrders, report.TotalCovers,
            Money(report.TotalNetSales), Money(report.TotalServiceCharge), Money(report.TotalSst), Money(report.GrandTotal)));

        // The BOM at the start makes Excel open the file as UTF-8
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Money(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);
    private static string Quote(string text) => "\"" + text.Replace("\"", "\"\"") + "\"";

    // ---------- Excel (.xlsx) using the ClosedXML library ----------
    public byte[] ToExcel(SalesReport report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sales Report");

        // Header row
        sheet.Cell(1, 1).Value = "Date";
        sheet.Cell(1, 2).Value = "Reservations";
        sheet.Cell(1, 3).Value = "Orders";
        sheet.Cell(1, 4).Value = "Revenue";
        sheet.Row(1).Style.Font.Bold = true;

        // Data rows start at row 2
        int line = 2;
        foreach (var r in rows)
        {
            sheet.Cell(line, 1).Value = r.Date;
            sheet.Cell(line, 2).Value = r.Reservations;
            sheet.Cell(line, 3).Value = r.Orders;
            sheet.Cell(line, 4).Value = r.Revenue;
            line++;
        }

        // Total row using a real Excel formula
        sheet.Cell(line, 1).Value = "Total";
        sheet.Cell(line, 2).FormulaA1 = $"SUM(B2:B{line - 1})";
        sheet.Cell(line, 3).FormulaA1 = $"SUM(C2:C{line - 1})";
        sheet.Cell(line, 4).FormulaA1 = $"SUM(D2:D{line - 1})";
        sheet.Row(line).Style.Font.Bold = true;

        sheet.Column(1).Style.DateFormat.Format = "yyyy-mm-dd";
        sheet.Columns().AdjustToContents();   // make columns wide enough for the text

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // ---------- PDF using the QuestPDF library ----------
    public byte[] ToPdf(SalesReport report, string title)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Margin(30);

                // Top of the page: title and generation time
                page.Header().Column(col =>
                {
                    col.Item().Text(title).FontSize(18).Bold();
                    col.Item().Text($"Generated {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(9);
                });

                // Middle: the table of daily rows
                page.Content().PaddingVertical(15).Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2);   // Date column is twice as wide
                        c.RelativeColumn();
                        c.RelativeColumn();
                        c.RelativeColumn();
                    });

                    table.Header(h =>
                    {
                        h.Cell().Text("Date").Bold();
                        h.Cell().Text("Reservations").Bold();
                        h.Cell().Text("Orders").Bold();
                        h.Cell().Text("Revenue").Bold();
                    });

                    foreach (var r in rows)
                    {
                        table.Cell().Text($"{r.Date:dd MMM yyyy}");
                        table.Cell().Text(r.Reservations.ToString());
                        table.Cell().Text(r.Orders.ToString());
                        table.Cell().Text($"{r.Revenue:N2}");
                    }

                    // Total row
                    table.Cell().Text("Total").Bold();
                    table.Cell().Text(rows.Sum(r => r.Reservations).ToString()).Bold();
                    table.Cell().Text(rows.Sum(r => r.Orders).ToString()).Bold();
                    table.Cell().Text($"{rows.Sum(r => r.Revenue):N2}").Bold();
                });

                // Bottom: page numbers
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Page ");
                    t.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }
}
