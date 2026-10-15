using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Payment: pay a takeaway order online.
    // Static UI for now – backend notes:
    //  - OnGet(int orderId): load the customer's own Order (+ OrderItems, Menu); NotFound() otherwise.
    //  - OnPost (fields: orderId, paymentMethod = "Card" / "Online Banking" / "E-Wallet"):
    //      create Payment (paymentAmount = Order.totalAmount, paymentStatus "Paid" when the (simulated)
    //      payment succeeds, "Failed" otherwise), then RedirectToPage("/Customer/PaymentSuccess").
    //  - Card details: for this project the payment is simulated. Do NOT save the card number or CVC in
    //    the database – at most keep the card type and last 4 digits for the receipt.
    public class PaymentModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: create the Payment first, then redirect.
        public IActionResult OnPost()
        {
            return RedirectToPage("/Customer/PaymentSuccess");
        }
    }
}
