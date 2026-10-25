using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff dashboard. Only the "Kitchen & service" panel (Active orders) reads real data so far;
    // the stat cards and the Upcoming guests panel are still the static design.
    public class DashboardModel : PageModel
    {
        // Open orders only, same statuses as the Active tab on the Orders page.
        // The array order is also the list order: what needs staff first.
        private static readonly string[] OpenStatuses = { "Order placed", "Preparing", "Served" };

        private readonly ApplicationDbContext _context;

        public DashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // Kitchen & service: Order placed → Preparing → Served, newest first within each status
        public List<ActiveOrderRow> ActiveOrders { get; set; } = new();

        public class ActiveOrderRow
        {
            public string Code { get; set; } = string.Empty;        // "ORD-0085" (link: /Staff/Orders?view=ORD-0085)
            public string? Table { get; set; }                      // "T-09"; null for takeaway
            public string Name { get; set; } = string.Empty;        // customer / guest name
            public string Status { get; set; } = string.Empty;      // Order placed / Preparing / Served
            public int ItemCount { get; set; }                      // total quantity of dishes
            public decimal Total { get; set; }                      // amount paid, or due (same figure as the Orders page)
        }

        public async Task OnGetAsync()
        {
            var openStatuses = OpenStatuses;

            var orders = await _context.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.DiningTable)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                .Where(o => openStatuses.Contains(o.orderStatus))
                .OrderByDescending(o => o.orderDateTime)
                .ToListAsync();

            // OrderBy is stable, so newest stays first inside each status
            ActiveOrders = orders
                .OrderBy(o => Array.IndexOf(OpenStatuses, o.orderStatus))
                .Select(o => new ActiveOrderRow
                {
                    Code = "ORD-" + o.orderID.ToString("0000"),
                    Table = o.DiningTable?.tableNumber,
                    Name = o.Customer?.custName ?? "Guest",
                    Status = o.orderStatus,
                    ItemCount = o.OrderItems.Sum(oi => oi.quantity),
                    Total = o.Payment != null && o.Payment.paymentStatus == "Paid" ? o.Payment.paymentAmount : o.totalAmount
                })
                .ToList();
        }
    }
}