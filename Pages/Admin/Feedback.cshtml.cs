using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Feedback: ratings and comments from completed dining experiences (read-only).
    // Static UI for now – backend notes:
    //  - OnGet filters feedback by quick period (?period=week · month · all) or
    //    by a custom date range (?from=yyyy-MM-dd&to=yyyy-MM-dd),
    //    newest first (feedbackDate).
    //    Stat cards: average rating · number of reviews · low ratings (rating <= 2).
    //    Rating distribution: share of 5 / 4 / 3 / 2 / 1-star feedback in the period.
    //  - Visit = the Order the feedback is for (Feedback.orderID) and its Reservation, if dine-in.
    //  - Clicking a row shows the full comment and the customer's phone / email, so the admin can contact them.
    //  - Only existing model fields are used. "Low ratings" is filtered on screen; Export CSV is done in the browser.
    public class FeedbackModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public string Period { get; private set; } = "month";
        public DateTime? SelectedFrom { get; private set; }
        public DateTime? SelectedTo { get; private set; }
        public string PeriodText => Period switch
        {
            "week" => "this week",
            "all" => "all time",
            "custom" => SelectedFrom.HasValue && SelectedTo.HasValue
                ? $"from {SelectedFrom.Value:dd MMM yyyy} to {SelectedTo.Value:dd MMM yyyy}"
                : "in the selected period",
            _ => "this month"
        };

        public double AverageRating { get; private set; }
        public int ReviewCount { get; private set; }
        public int LowRatingCount { get; private set; }
        public List<DistributionItem> Distribution { get; private set; } = new();
        public List<FeedbackItem> Items { get; private set; } = new();

        public FeedbackModel(ApplicationDbContext context) => _context = context;

        public async Task OnGetAsync(string? period, DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue)
            {
                // Keep the custom date selectors and the query range consistent
                // when an administrator enters the dates in reverse order.
                var start = from.Value.Date;
                var end = to.Value.Date;
                if (start > end) (start, end) = (end, start);

                SelectedFrom = start;
                SelectedTo = end;
                Period = "custom";
            }
            else
            {
                SelectedFrom = from?.Date;
                SelectedTo = to?.Date;
                Period = period is "week" or "month" or "all" ? period : "month";
            }

            var query = _context.Feedbacks
                .AsNoTracking()
                .Include(f => f.Customer)
                .Include(f => f.Order)
                    .ThenInclude(o => o!.Reservation)
                .AsQueryable();

            if (Period == "custom")
            {
                var start = new DateTimeOffset(SelectedFrom!.Value, DateTimeOffset.Now.Offset);
                var end = new DateTimeOffset(SelectedTo!.Value.AddDays(1), DateTimeOffset.Now.Offset);
                query = query.Where(f => f.feedbackDate >= start && f.feedbackDate < end);
            }
            else if (Period == "week")
                query = query.Where(f => f.feedbackDate >= DateTimeOffset.Now.AddDays(-7));
            else if (Period == "month")
            {
                var now = DateTimeOffset.Now;
                var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
                query = query.Where(f => f.feedbackDate >= monthStart);
            }

            var feedback = await query.OrderByDescending(f => f.feedbackDate).ToListAsync();

            ReviewCount = feedback.Count;
            LowRatingCount = feedback.Count(f => f.rating <= 2);
            AverageRating = ReviewCount == 0 ? 0 : Math.Round(feedback.Average(f => f.rating), 1);

            Distribution = Enumerable.Range(1, 5)
                .OrderByDescending(x => x)
                .Select(stars =>
                {
                    var count = feedback.Count(f => f.rating == stars);
                    return new DistributionItem
                    {
                        Stars = stars,
                        Count = count,
                        Percent = ReviewCount == 0 ? 0 : (int)Math.Round(count * 100.0 / ReviewCount)
                    };
                })
                .ToList();

            Items = feedback.Select(f => new FeedbackItem
            {
                Date = f.feedbackDate.ToLocalTime().ToString("dd MMM yyyy"),
                Customer = f.Customer?.custName ?? "Unknown customer",
                Phone = f.Customer?.custPhone ?? "Not provided",
                Email = f.Customer?.custEmail ?? "Not provided",
                Order = $"ORD-{f.orderID:0000}",
                Reservation = f.Order?.Reservation == null ? null : $"R-{f.Order.Reservation.reservationID:0000}",
                Rating = f.rating,
                Comment = f.comment ?? string.Empty
            }).ToList();
        }

        public class DistributionItem
        {
            public int Stars { get; set; }
            public int Count { get; set; }
            public int Percent { get; set; }
        }

        public class FeedbackItem
        {
            public string Date { get; set; } = string.Empty;
            public string Customer { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Order { get; set; } = string.Empty;
            public string? Reservation { get; set; }
            public int Rating { get; set; }
            public string Comment { get; set; } = string.Empty;
        }
    }
}
