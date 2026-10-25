using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Feedback: ratings and comments from completed dining experiences (read-only).
    // Static UI for now – backend notes:
    //  - OnGet(string? period): Feedback (+ Customer, Order → Reservation) for ?period=week · month (default) · all,
    //    newest first (feedbackDate).
    //    Stat cards: average rating · number of reviews · low ratings (rating <= 2).
    //    Rating distribution: share of 5 / 4 / 3 / 2 / 1-star feedback in the period.
    //  - Visit = the Order the feedback is for (Feedback.orderID) and its Reservation, if dine-in.
    //  - Clicking a row shows the full comment and the customer's phone / email, so the admin can contact them.
    //  - Only existing model fields are used. "Low ratings" is filtered on screen; Export CSV is done in the browser.
    public class FeedbackModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
