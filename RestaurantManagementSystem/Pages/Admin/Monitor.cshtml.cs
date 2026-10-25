using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Monitor: read-only overview of reservations, orders and payments across the restaurant.
    // Staff do the actions (confirm, seat, cancel, take payment) on the Staff pages.
    // Static UI for now – backend notes (existing model fields only):
    //  - Tabs: ?tab=reservations (default) · orders · payments.
    //  - Reservations: OnGet(DateTime? from, DateTime? to) – Reservations between the two dates (default today → +4 days)
    //    with Customer (name, phone) + DiningTable (tableNumber, diningArea). Status and search are filtered on screen.
    //    Clicking a row opens the details pop-up (?view=R-0041 opens it straight away).
    //  - Orders: today's Orders with Customer, Reservation (+ DiningTable), OrderItems count, totalAmount.
    //    Type = Dine-in when reservationID != null, else Takeaway. Statuses as agreed for the Staff pages:
    //    Order placed → Preparing → Served → Completed, or Cancelled. Filter: All · Active · Completed · Cancelled.
    //    Clicking an order opens the Order details pop-up: its OrderItems (quantity, Menu.menuName, unitPrice,
    //    itemRemark), subtotal, service charge 10% (dine-in only), SST 6% and total.
    //  - Payments: Payments with Order + Customer, newest first. paymentStatus Pending / Paid / Failed / Refunded.
    //    "Receipt" fills the shared _Receipt partial (ReceiptView) for Paid / Refunded payments.
    //  - Export CSV is done in the browser from the rows shown on the open tab (no backend needed).
    public class MonitorModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
