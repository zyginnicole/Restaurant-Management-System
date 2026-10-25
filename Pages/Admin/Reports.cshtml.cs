using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Reports: two tabs, each with its own GET form (choices stay in the address bar).
    // Static UI for now – backend notes (existing model fields only):
    //
    //  Statistic report – ?tab=statistic&period=monthly&from=2026-01&to=2026-09   (or period=yearly&from=2024&to=2026)
    //    period: monthly (From/To = months, default) · yearly (From/To = years)
    //    KPI cards for the range, each compared with the previous range of the same length:
    //      Revenue          sum of Paid Payment.paymentAmount (mini bars = revenue per month / year)
    //      Reservations     count of Reservations; bar = Completed / Cancelled / other
    //      Registered       all Customers; "new in range" = custCreateAt inside the range (mini bars per month / year)
    //      Average rating   Feedback.rating average + share of 5 / 4 / 3 / 2 / 1 stars
    //    Then the same sections as the Dashboard's Sales & trends tab, for the range:
    //      revenue by month / year (chart) · peak dining hours (Reservations by start hour)
    //      · top 5 menu items (OrderItem quantity and quantity × unitPrice) · table utilization by area
    //      · reservations by status
    //    "Download PDF" saves the whole report in the browser (pdf-download.js).
    //
    //  Sales report – ?tab=sales&period=daily&from=2026-09-01&to=2026-09-15
    //    period: daily (one row per day, default) · monthly (one row per month)
    //    Orders (not Cancelled) with a Paid Payment, grouped by orderDateTime: orders, covers (Reservation.partySize;
    //    takeaway = 0), net sales = subtotal, service charge 10% (dine-in), SST 6%, total. CSV / PDF in the browser.
    public class ReportsModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
