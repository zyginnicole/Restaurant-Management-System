using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Customer orders: track the signed-in customer's open orders.
    // Static UI for now – backend notes:
    //  - OnGet: load the customer's orders (custID from Session) whose orderStatus is
    //    "Order placed", "Preparing" or "Served", newest first, with OrderItems (+ Menu) and Payment.
    //    Completed and Cancelled orders are shown on the History page, not here.
    //  - Tracker steps: Order placed → Preparing → Served → Completed. Mark finished steps "done"
    //    and the current one "current"; show the time each step was reached if you store it.
    //  - OnPostCancel (field: orderId): only the customer's own order, and only while
    //    orderStatus == "Order placed" → orderStatus = "Cancelled" (refund a paid takeaway).
    public class OrdersModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only – backend: set orderStatus = "Cancelled" (and refund) first.
        public IActionResult OnPostCancel()
        {
            return RedirectToPage();
        }
    }
}
