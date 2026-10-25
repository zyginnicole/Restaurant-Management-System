using RestaurantManagementSystem.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Customer overview (dashboard). The recommendation hero and the "Order in progress" card read real data;
    // the other cards are still the static design.
    public class DashboardModel : PageModel
    {
        // Same open statuses as the Orders page (Completed and Cancelled orders are history)
        private static readonly string[] OpenStatuses = { "Order placed", "Preparing", "Served" };

        private readonly ApplicationDbContext _context;
        private readonly RecommendationService _recommendations;

        public DashboardModel(ApplicationDbContext context, RecommendationService recommendations)
        {
            _context = context;
            _recommendations = recommendations;
        }

        // Slides of the black recommendation section at the top (empty = the section is hidden)
        public List<HeroSlide> HeroSlides { get; set; } = new();

        // The customer's most recent open order, or null (the card then shows an empty state)
        public OrderInProgressView? OrderInProgress { get; set; }

        public class OrderInProgressView
        {
            public string OrderCode { get; set; } = string.Empty;        // "ORD-0088"
            public string Status { get; set; } = string.Empty;           // Order placed / Preparing / Served
            public decimal TotalAmount { get; set; }
            public List<OrderLineView> Items { get; set; } = new();
        }

        public class OrderLineView
        {
            public string Name { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal LineTotal { get; set; }
        }

        public async Task OnGetAsync()
        {
            int? custId = HttpContext.Session.IsSignedInAs(UserRoles.Customer)
                ? HttpContext.Session.GetUserId()
                : null;
            if (custId == null) return;

            // Same recommendations as the Menu page (allergies already filtered out)
            var recs = await _recommendations.GetForCustomerAsync(custId.Value);
            HeroSlides = recs.ToHeroSlides();

            var openStatuses = OpenStatuses;

            // Latest open order. A takeaway order whose payment has not been completed is not being
            // prepared yet, so it is skipped (dine-in orders are paid at the end, so they always count).
            var order = await _context.Orders.AsNoTracking()
                .Include(o => o.Payment)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu)
                .Where(o => o.custID == custId.Value
                         && openStatuses.Contains(o.orderStatus)
                         && (o.tableID != null || (o.Payment != null && o.Payment.paymentStatus == "Paid")))
                .OrderByDescending(o => o.orderDateTime)
                .FirstOrDefaultAsync();

            if (order == null) return;

            OrderInProgress = new OrderInProgressView
            {
                OrderCode = "ORD-" + order.orderID.ToString("0000"),
                Status = order.orderStatus,
                TotalAmount = order.totalAmount,
                Items = order.OrderItems.Select(oi => new OrderLineView
                {
                    Name = oi.Menu?.menuName ?? "Dish",
                    Quantity = oi.quantity,
                    LineTotal = oi.quantity * oi.unitPrice
                }).ToList()
            };
        }
    }
}