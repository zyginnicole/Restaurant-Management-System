using System.Globalization;
using System.Security;
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

    // Generates a statistic report PDF with the same summaries and chart data shown on the admin page.
    public byte[] ToStatisticPdf(StatisticReport report, string title)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Margin(28);
                page.Header().Column(column =>
                {
                    column.Item().Text(title).FontSize(18).Bold();
                    column.Item().Text($"Generated {DateTimeOffset.Now:dd MMM yyyy HH:mm}").FontSize(9);
                });

                page.Content().PaddingVertical(12).Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text("Key figures").FontSize(13).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Cell().Text($"Revenue\nRM {report.Kpis.Revenue:N0}").Bold();
                        table.Cell().Text($"Reservations\n{report.Kpis.Reservations:N0}").Bold();
                        table.Cell().Text($"Customers\n{report.Kpis.Customers:N0}").Bold();
                        table.Cell().Text($"Average rating\n{report.Kpis.AverageRating:0.0} / 5").Bold();
                    });

                    column.Item().Text("Revenue trend").FontSize(13).Bold();
                    column.Item().Height(155).Svg(BuildLineChart(report.RevenueSeries, "Revenue (RM)"));

                    column.Item().Text("New customers").FontSize(13).Bold();
                    column.Item().Height(135).Svg(BuildLineChart(report.NewCustomerSeries, "New customers"));

                    column.Item().Text("Peak dining hours").FontSize(13).Bold();
                    column.Item().Height(145).Svg(BuildBarChart(report.PeakHours, "Reservations"));

                    column.Item().Text("Reservations by status").FontSize(13).Bold();
                    column.Item().Height(55).Svg(BuildStackedBar(report.StatusBreakdown));

                    column.Item().Text("Top menu items").FontSize(13).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Text("Dish").Bold();
                            header.Cell().Text("Sold").Bold();
                            header.Cell().Text("Revenue").Bold();
                        });
                        foreach (var item in report.TopItems)
                        {
                            table.Cell().Text(item.Name);
                            table.Cell().Text(item.Quantity.ToString("N0"));
                            table.Cell().Text($"RM {item.Revenue:N2}");
                        }
                    });

                    column.Item().Text("Utilization and rating distribution").FontSize(13).Bold();
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        int maxRows = Math.Max(report.Utilization.Count, 5);
                        for (int i = 0; i < maxRows; i++)
                        {
                            if (i < report.Utilization.Count)
                            {
                                table.Cell().Text(report.Utilization[i].Name);
                                table.Cell().Text($"{report.Utilization[i].Value:0}%");
                            }
                            else
                            {
                                table.Cell().Text("");
                                table.Cell().Text("");
                            }

                            int stars = 5 - i;
                            table.Cell().Text($"{stars} stars");
                            table.Cell().Text($"{report.Kpis.StarSharePercent[stars]}%");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Page ");
                    text.CurrentPageNumber();
                });
            });
        }).GeneratePdf();
    }

    private static string BuildLineChart(IReadOnlyList<NameValue> series, string axisLabel)
    {
        const double width = 720;
        const double height = 220;
        const double left = 48;
        const double right = 12;
        const double top = 16;
        const double bottom = 42;
        var values = series.Select(x => x.Value).ToList();
        var max = values.Count == 0 ? 1 : Math.Max(1, values.Max());
        var step = values.Count <= 1 ? 0 : (width - left - right) / (values.Count - 1);
        var points = new StringBuilder();

        for (int i = 0; i < values.Count; i++)
        {
            var x = left + i * step;
            var y = height - bottom - values[i] / max * (height - top - bottom);
            if (i > 0) points.Append(' ');
            points.Append(x.ToString("0.##", CultureInfo.InvariantCulture));
            points.Append(',');
            points.Append(y.ToString("0.##", CultureInfo.InvariantCulture));
        }

        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}'>");
        svg.Append("<rect width='100%' height='100%' fill='white'/>");
        svg.Append($"<line x1='{left}' y1='{height - bottom}' x2='{width - right}' y2='{height - bottom}' stroke='#d8d3c8'/>");
        svg.Append($"<line x1='{left}' y1='{top}' x2='{left}' y2='{height - bottom}' stroke='#d8d3c8'/>");
        if (values.Count > 0)
            svg.Append($"<polyline points='{points}' fill='none' stroke='#5165ad' stroke-width='3'/>");
        svg.Append($"<text x='{left}' y='{height - 10}' font-size='12' fill='#6f6a61'>{Xml(axisLabel)}</text>");
        for (int i = 0; i < series.Count; i++)
        {
            if (series.Count > 8 && i % Math.Max(1, series.Count / 8) != 0) continue;
            var x = left + i * step;
            svg.Append($"<text x='{x:0.##}' y='{height - 23}' text-anchor='middle' font-size='10' fill='#6f6a61'>{Xml(series[i].Name)}</text>");
        }
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string BuildBarChart(IReadOnlyList<NameValue> series, string axisLabel)
    {
        const double width = 720;
        const double height = 210;
        const double left = 42;
        const double bottom = 42;
        var max = series.Count == 0 ? 1 : Math.Max(1, series.Max(x => x.Value));
        var slot = series.Count == 0 ? width - left : (width - left - 12) / series.Count;
        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}'>");
        svg.Append("<rect width='100%' height='100%' fill='white'/>");
        svg.Append($"<line x1='{left}' y1='{height - bottom}' x2='{width - 12}' y2='{height - bottom}' stroke='#d8d3c8'/>");
        for (int i = 0; i < series.Count; i++)
        {
            var barHeight = series[i].Value / max * (height - bottom - 20);
            var x = left + i * slot + slot * .15;
            var y = height - bottom - barHeight;
            svg.Append($"<rect x='{x:0.##}' y='{y:0.##}' width='{slot * .7:0.##}' height='{barHeight:0.##}' fill='#5165ad'/>");
            svg.Append($"<text x='{x + slot * .35:0.##}' y='{height - 24}' text-anchor='middle' font-size='10' fill='#6f6a61'>{Xml(series[i].Name)}</text>");
        }
        svg.Append($"<text x='{left}' y='{height - 7}' font-size='12' fill='#6f6a61'>{Xml(axisLabel)}</text></svg>");
        return svg.ToString();
    }

    private static string BuildStackedBar(IReadOnlyList<NameValue> statuses)
    {
        const double width = 720;
        const double height = 80;
        var total = Math.Max(1, statuses.Sum(x => x.Value));
        var colors = new[] { "#4b9a8e", "#b33b46", "#c8c4ba", "#5165ad", "#8f7a5b" };
        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 {width} {height}'>");
        svg.Append("<rect width='100%' height='100%' fill='white'/>");
        double x = 0;
        for (int i = 0; i < statuses.Count; i++)
        {
            var w = statuses[i].Value / total * width;
            svg.Append($"<rect x='{x:0.##}' y='10' width='{w:0.##}' height='24' fill='{colors[i % colors.Length]}'/>");
            svg.Append($"<text x='{x + w / 2:0.##}' y='58' text-anchor='middle' font-size='10' fill='#333'>{Xml(statuses[i].Name)} {statuses[i].Value:0}</text>");
            x += w;
        }
        svg.Append("</svg>");
        return svg.ToString();
    }

    private static string Xml(string value)
    {
        return SecurityElement.Escape(value) ?? string.Empty;
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
