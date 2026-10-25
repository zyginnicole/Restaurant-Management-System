using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // History: the signed-in customer's past reservations, orders and payments, and rating a visit.
    // Static UI for now – backend notes:
    //  - OnGet: load for custID (from Session), newest first:
    //      Reservations with status "Completed" or "Cancelled" (+ their Orders and Payment),
    //      Orders with status "Completed" or "Cancelled",
    //      Payments of those orders (Paid, Refunded, Failed).
    //  - "Rate this visit" shows only for a Completed visit whose order has no Feedback row yet
    //    (one rating per visit); a rated visit shows its stars instead.
    //  - OnPostRate (fields: orderId, rating 1–5, comment): save a Feedback row
    //    (custID from Session, feedbackDate = now). The "What stood out?" chips are added to the comment
    //    text, because Feedback has no separate field for them.
    public class HistoryModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: save the Feedback first, then redirect.
        public IActionResult OnPostRate()
        {
            return RedirectToPage();
        }
    }
}
