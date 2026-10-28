using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;

namespace RestaurantManagementSystem.Services;

// Service responsible for exporting SalesReport data into CSV, Excel, and PDF formats.
public class ExportService
{
    private static readonly string[] Headers =
    {
        "Period",
        "Orders",
        "Covers",
        "Net sales",
        "Service charge",
        "SST",
        "Total"
    };

    // Generates a CSV file byte array with UTF-8 BOM encoding for proper Excel display.
    public byte[] ToCsv(SalesReport report)
    {
        var builder = new StringBuilder();

        //Write header row
        builder.AppendLine(string.Join(",", Headers));

        //Write daily/period data rows
        foreach (var row in report.Rows)
        {
            builder.AppendLine(string.Join(",",
                Quote(row.Label),
                row.Orders,
                row.Covers,
                Money(row.NetSales),
                Money(row.ServiceCharge),
                Money(row.Sst),
                Money(row.Total)));
        }

        //Write summary totals row
        builder.AppendLine(string.Join(",",
            Quote("Total"),
            report.TotalOrders,
            report.TotalCovers,
            Money(report.TotalNetSales),
            Money(report.TotalServiceCharge),
            Money(report.TotalSst),
            Money(report.GrandTotal)));

        var body = Encoding.UTF8.GetBytes(builder.ToString());

        // Prepend UTF-8 Byte Order Mark (BOM) so software like Excel reads special characters correctly
        return new UTF8Encoding(true)
            .GetPreamble()
            .Concat(body)
            .ToArray();
    }

    // Generates an Excel spreadsheet (.xlsx) using ClosedXML with bold headers and currency formatting.
    public byte[] ToExcel(SalesReport report)
    {
        using var workbook = new XLWorkbook();

        var sheet = workbook.AddWorksheet("Sales Report");

        sheet.Cell(1, 1).Value = "Period";
        sheet.Cell(1, 2).Value = "Orders";
        sheet.Cell(1, 3).Value = "Covers";
        sheet.Cell(1, 4).Value = "Net sales";
        sheet.Cell(1, 5).Value = "Service charge";
        sheet.Cell(1, 6).Value = "SST";
        sheet.Cell(1, 7).Value = "Total";

        sheet.Row(1).Style.Font.Bold = true;

        var rowNumber = 2;

        foreach (var row in report.Rows)
        {
            sheet.Cell(rowNumber, 1).Value = row.Label;
            sheet.Cell(rowNumber, 2).Value = row.Orders;
            sheet.Cell(rowNumber, 3).Value = row.Covers;
            sheet.Cell(rowNumber, 4).Value = row.NetSales;
            sheet.Cell(rowNumber, 5).Value = row.ServiceCharge;
            sheet.Cell(rowNumber, 6).Value = row.Sst;
            sheet.Cell(rowNumber, 7).Value = row.Total;

            rowNumber++;
        }

        // Write bold totals row at the bottom
        sheet.Cell(rowNumber, 1).Value = "Total";
        sheet.Cell(rowNumber, 2).Value = report.TotalOrders;
        sheet.Cell(rowNumber, 3).Value = report.TotalCovers;
        sheet.Cell(rowNumber, 4).Value = report.TotalNetSales;
        sheet.Cell(rowNumber, 5).Value = report.TotalServiceCharge;
        sheet.Cell(rowNumber, 6).Value = report.TotalSst;
        sheet.Cell(rowNumber, 7).Value = report.GrandTotal;

        sheet.Row(rowNumber).Style.Font.Bold = true;

        // Apply two-decimal place number formatting to monetary columns
        sheet.Column(4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(5).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(7).Style.NumberFormat.Format = "#,##0.00";

        // Auto-fit column widths to fit cell contents
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    // Generates a formatted PDF document using QuestPDF Fluent API.
    public byte[] ToPdf(SalesReport report, string title)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Margin(30);

                // Render document title and generation timestamp
                page.Header()
                    .Column(column =>
                    {
                        column.Item()
                            .Text(title)
                            .FontSize(18)
                            .Bold();

                        column.Item()
                            .Text($"Generated {DateTimeOffset.Now:dd MMM yyyy HH:mm}")
                            .FontSize(9);
                    });

                // Build main sales data table
                page.Content()
                    .PaddingVertical(15)
                    .Table(table =>
                    {
                        // Define 7 relative column layout proportions
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        //Table header row
                        table.Header(header =>
                        {
                            header.Cell().Text("Period").Bold();
                            header.Cell().Text("Orders").Bold();
                            header.Cell().Text("Covers").Bold();
                            header.Cell().Text("Net sales").Bold();
                            header.Cell().Text("Service").Bold();
                            header.Cell().Text("SST").Bold();
                            header.Cell().Text("Total").Bold();
                        });

                        //Populate data rows with sales report information
                        foreach (var row in report.Rows)
                        {
                            table.Cell().Text(row.Label);
                            table.Cell().Text(row.Orders.ToString());
                            table.Cell().Text(row.Covers.ToString());
                            table.Cell().Text(row.NetSales.ToString("N2"));
                            table.Cell().Text(row.ServiceCharge.ToString("N2"));
                            table.Cell().Text(row.Sst.ToString("N2"));
                            table.Cell().Text(row.Total.ToString("N2"));
                        }

                        //Summary row
                        table.Cell().Text("Total").Bold();
                        table.Cell().Text(report.TotalOrders.ToString()).Bold();
                        table.Cell().Text(report.TotalCovers.ToString()).Bold();
                        table.Cell().Text(report.TotalNetSales.ToString("N2")).Bold();
                        table.Cell().Text(report.TotalServiceCharge.ToString("N2")).Bold();
                        table.Cell().Text(report.TotalSst.ToString("N2")).Bold();
                        table.Cell().Text(report.GrandTotal.ToString("N2")).Bold();
                    });

                //render page numbering in the footer
                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span("Page ");
                        text.CurrentPageNumber();
                    });
            });
        }).GeneratePdf();
    }

    // Helper: Formats decimal numbers into fixed 2-decimal invariant strings (e.g. 12.34).
    private static string Money(decimal value)
    {
        return value.ToString("F2", CultureInfo.InvariantCulture);
    }

    // Helper: Escapes string values for CSV compliance by wrapping in quotes and escaping internal double quotes.
    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}