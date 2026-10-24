using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Customer
{
    // History: the signed-in customer's past reservations, orders and payments, and rating a visit.
    //  - Reservations tab (connected): finished bookings – Completed, Cancelled, No-show, Declined – newest first,
    //    with the visit's order + payment and the Feedback rating if there is one (ReservationService.GetHistoryForCustomerAsync).
    //  - Orders and Payments tabs: still example data (orders / payments feature).
    //  - "Rate this visit" shows only for a Completed visit whose order has no Feedback row yet
    //    (one rating per visit); a rated visit shows its stars instead.
    //  - OnPostRate (fields: orderId, rating 1–5, comment): not saved yet (feedback feature) –
    //    backend: save a Feedback row (custID from Session, feedbackDate = now). The "What stood out?" chips
    //    are added to the comment text, because Feedback has no separate field for them.
    public class HistoryModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ReservationService _reservations;
        private readonly SettingsService _settings;

        public HistoryModel(ApplicationDbContext db, ReservationService reservations, SettingsService settings)
        {
            _db = db;
            _reservations = reservations;
            _settings = settings;
        }

        public List<Reservation> PastReservations { get; set; } = new();
        public Dictionary<int, Feedback> FeedbackByOrder { get; set; } = new();   // orderID → the customer's rating
        public int DiningMinutes { get; set; } = 120;

        public async Task OnGetAsync()
        {
            var custId = HttpContext.Session.GetUserId();
            if (custId == null) return;

            DiningMinutes = (await _settings.GetAsync()).diningMinutes;
            PastReservations = await _reservations.GetHistoryForCustomerAsync(custId.Value);

            // Ratings for the orders of these visits (one Feedback per order)
            var orderIds = PastReservations.SelectMany(r => r.Orders).Select(o => o.orderID).ToList();
            var feedback = await _db.Feedbacks.AsNoTracking()
                .Where(f => f.custID == custId.Value && orderIds.Contains(f.orderID))
                .ToListAsync();
            FeedbackByOrder = feedback.GroupBy(f => f.orderID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.feedbackDate).First());
        }

        // UI flow only (no saving yet) – feedback feature: save the Feedback first, then redirect.
        public IActionResult OnPostRate()
        {
            return RedirectToPage();
        }
    }
}
