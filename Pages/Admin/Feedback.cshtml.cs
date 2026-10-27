using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Feedback: ratings and comments from completed dining experiences (read-only).
    //  - OnGet(string? period): Feedback (+ Customer, Order) for ?period=week (since Monday) · month (default) · all,
    //    newest first (feedbackDate). Customers save them on History → "Rate this visit".
    //    Stat cards: average rating · number of reviews · low ratings (rating <= 2).
    //    Rating distribution: share of 5 / 4 / 3 / 2 / 1-star feedback in the period.
    //  - Visit = the Order the feedback is for (Feedback.orderID) and its Reservation, if dine-in.
    //  - Clicking a row shows the full comment and the customer's phone / email, so the admin can contact them.
    //  - "Low ratings" is filtered on screen; Export CSV is done in the browser.
    public class FeedbackModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public FeedbackModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public string Period { get; set; } = "month";
        public List<FeedbackRow> Items { get; set; } = new();
        public double? Average { get; set; }                      // null = no reviews in the period
        public List<(int Stars, int Percent)> Distribution { get; set; } = new();

        public class FeedbackRow
        {
            public DateTimeOffset Date { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Order { get; set; } = string.Empty;     // "ORD-0080"
            public string? Res { get; set; }                      // "R-0036" (dine-in) or null (takeaway)
            public int Rating { get; set; }
            public string? Comment { get; set; }
        }

        public async Task OnGetAsync(string? period)
        {
            Period = period is "week" or "all" ? period : "month";

            var today = DateTime.Today;
            DateTime? since = Period switch
            {
                "week" => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),   // Monday of this week
                "month" => new DateTime(today.Year, today.Month, 1),
                _ => null
            };

            var query = _db.Feedbacks.AsNoTracking()
                .Include(f => f.Customer)
                .Include(f => f.Order)
                .AsQueryable();
            if (since != null)
            {
                var from = new DateTimeOffset(since.Value);
                query = query.Where(f => f.feedbackDate >= from);
            }

            var feedback = await query.OrderByDescending(f => f.feedbackDate).ToListAsync();

            Items = feedback.Select(f => new FeedbackRow
            {
                Date = f.feedbackDate,
                Name = f.Customer?.custName ?? "Customer",
                Phone = f.Customer?.custPhone ?? "",
                Email = f.Customer?.custEmail ?? "",
                Order = "ORD-" + f.orderID.ToString("0000"),
                Res = f.Order?.reservationID is int r ? "R-" + r.ToString("0000") : null,
                Rating = f.rating,
                Comment = f.comment
            }).ToList();

            Average = Items.Count > 0 ? Math.Round(Items.Average(i => i.Rating), 1) : null;
            Distribution = Enumerable.Range(1, 5).Reverse()
                .Select(s => (s, Items.Count == 0 ? 0 : (int)Math.Round(100.0 * Items.Count(i => i.Rating == s) / Items.Count)))
                .ToList();
        }
    }
}
