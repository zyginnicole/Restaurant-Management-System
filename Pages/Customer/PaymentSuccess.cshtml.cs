using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Payment successful: receipt + live order tracker.
    // Static UI for now – backend notes:
    //  - OnGet(int orderId): load the customer's own Order (+ OrderItems, Menu, Payment); NotFound() otherwise.
    //  - Receipt: fill a ReceiptView (Pages/Shared/Partials/ReceiptView.cs) from the Order + Payment and pass it to
    //    the shared _Receipt partial. It works out the service charge (dine-in only), SST and total itself.
    //    Receipt no. = "RCP-" + paymentID.ToString("0000").
    //  - Tracker uses the same 4 steps as the Orders page: Order placed → Preparing → Served → Completed.
    public class PaymentSuccessModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
