using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Customer orders: track open orders, pay a dine-in table bill, cancel before the kitchen starts.
    //  - Every order keeps its OWN row (a table can have several orders, each at a different stage).
    //  - Dine-in: all unpaid orders of the customer at the same table are ONE bill. Pay opens only when
    //    every one of them is Served (the Payment page then settles them together).
    //  - Takeaway: paid online right after checkout (Payment page).
    //  - Cancel: only while orderStatus == "Order placed" (dine-in and takeaway). A paid order is refunded.
    public class OrdersModel : PageModel
    {
        private static readonly string[] OpenStatuses = { "Order placed", "Preparing", "Served" };

        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notifications;

        public OrdersModel(ApplicationDbContext context, NotificationService notifications)
        {
            _context = context;
            _notifications = notifications;
        }

        public List<OrderDisplayItem> OpenOrders { get; set; } = new();

        // SST rate from Admin > Settings (shown in the takeaway note)
        public decimal SstPct { get; set; } = 6m;

        public class OrderDisplayItem
        {
            public int OrderId { get; set; }
            public string OrderCode { get; set; } = string.Empty;
            public bool IsDineIn { get; set; }
            public string Status { get; set; } = string.Empty;
            public DateTimeOffset OrderDateTime { get; set; }
            public DateTime? PickupDateTime { get; set; }
            public string? TableNumber { get; set; }
            public string? AreaName { get; set; }
            public string? ReservationCode { get; set; }
            public decimal TotalAmount { get; set; }                 // this order only
            public string PaymentStatus { get; set; } = "Pending";
            public bool CanPay { get; set; }
            public bool CanCancel { get; set; }
            public string PayUrl { get; set; } = string.Empty;
            public List<OrderItemDisplay> Items { get; set; } = new();

            // ---- Table bill (dine-in): every unpaid order of this customer at the same table ----
            public decimal BillTotal { get; set; }                   // what "Pay bill" will charge (takeaway = TotalAmount)
            public int BillOrderCount { get; set; }                  // orders on the bill
            public string BillOrderCodes { get; set; } = string.Empty; // "ORD-0001, ORD-0003"
            public int WaitingOrders { get; set; }                   // orders on the bill that are not Served yet
        }

        public class OrderItemDisplay
        {
            public string Name { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public string? Note { get; set; }
            public decimal Discount { get; set; }                    // promotion taken off this line
            public decimal LineTotal => Quantity * UnitPrice - Discount;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            int? custId = CurrentCustomerId();
            if (custId == null) return RedirectToPage("/Customer/Login");

            await LoadOrdersAsync(custId.Value);
            return Page();
        }

        // Live Polling API: returns current status for active customer orders
        public async Task<IActionResult> OnGetPollAsync()
        {
            int? custId = CurrentCustomerId();
            if (custId == null) return new JsonResult(new { ok = false });

            var openStatuses = OpenStatuses;

            var orders = await _context.Orders.AsNoTracking()
                .Include(o => o.Payment)
                .Where(o => o.custID == custId.Value && openStatuses.Contains(o.orderStatus))
                .Select(o => new
                {
                    orderId = o.orderID,
                    code = "ORD-" + o.orderID.ToString("0000"),
                    status = o.orderStatus,
                    isPaid = o.Payment != null && o.Payment.paymentStatus == "Paid"
                })
                .ToListAsync();

            return new JsonResult(new { ok = true, orders });
        }

        // Cancel: only the customer's own order, and only before the kitchen starts (status "Order placed")
        public async Task<IActionResult> OnPostCancelAsync(int orderId)
        {
            int? custId = CurrentCustomerId();
            if (custId == null) return RedirectToPage("/Customer/Login");

            var order = await _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.orderID == orderId && o.custID == custId.Value);

            if (order == null) return NotFound();

            string code = "ORD-" + order.orderID.ToString("0000");

            //status is checked again here, so an order the kitchen has just started cannot be cancelled
            if (order.orderStatus != "Order placed")
            {
                TempData["OrderMessage"] = $"{code} can no longer be cancelled because the kitchen has already started preparing it.";
                return RedirectToPage();
            }

            order.orderStatus = "Cancelled";

            bool refunded = order.Payment != null && order.Payment.paymentStatus == "Paid";
            if (refunded)
            {
                order.Payment!.paymentStatus = "Refunded";
            }

            await _context.SaveChangesAsync();

            await _notifications.SendAsync(
                order.custID,
                "Order",
                "Order cancelled",
                $"{code} has been cancelled.",
                "/Customer/History");

            TempData["OrderMessage"] = refunded
                ? $"{code} has been cancelled. Your payment will be refunded to your card within 3–5 working days."
                : $"{code} has been cancelled.";

            return RedirectToPage();
        }

        private async Task LoadOrdersAsync(int custId)
        {
            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            SstPct = settings?.sstPct ?? 6m;

            var openStatuses = OpenStatuses;

            var orders = await _context.Orders.AsNoTracking()
                .Include(o => o.DiningTable).ThenInclude(t => t!.DiningArea)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu)
                .Where(o => o.custID == custId && openStatuses.Contains(o.orderStatus))
                .OrderByDescending(o => o.orderDateTime)
                .ToListAsync();

            //dine-in bills: unpaid open orders grouped by table
            var tableBills = orders
                .Where(o => o.tableID != null && o.Payment?.paymentStatus != "Paid")
                .GroupBy(o => o.tableID!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(x => x.orderID).ToList());

            OpenOrders = orders.Select(o =>
            {
                bool dineIn = o.tableID != null;
                bool isPaid = o.Payment?.paymentStatus == "Paid";

                //the bill this order belongs to (takeaway / already paid = just itself)
                List<Order> bill = dineIn && !isPaid
                    ? tableBills[o.tableID!.Value]
                    : new List<Order> { o };

                int waiting = dineIn ? bill.Count(b => b.orderStatus != "Served") : 0;

                //dine-in: pay only after EVERY order at the table is served; takeaway: pay straight away
                bool canPay = dineIn
                    ? (!isPaid && waiting == 0)
                    : !isPaid;

                //before the kitchen starts, for dine-in AND takeaway (a paid takeaway is refunded)
                bool canCancel = o.orderStatus == "Order placed";

                return new OrderDisplayItem
                {
                    OrderId = o.orderID,
                    OrderCode = "ORD-" + o.orderID.ToString("0000"),
                    IsDineIn = dineIn,
                    Status = o.orderStatus,
                    OrderDateTime = o.orderDateTime,
                    PickupDateTime = o.pickupDateTime,
                    TableNumber = o.DiningTable?.tableNumber,
                    AreaName = o.DiningTable?.DiningArea?.areaName,
                    ReservationCode = o.reservationID.HasValue ? "R-" + o.reservationID.Value.ToString("0000") : null,
                    TotalAmount = o.totalAmount,
                    PaymentStatus = isPaid ? "Paid" : "Pending",
                    CanPay = canPay,
                    CanCancel = canCancel,
                    PayUrl = $"/Customer/Payment?orderId={o.orderID}",
                    BillTotal = bill.Sum(b => b.totalAmount),
                    BillOrderCount = bill.Count,
                    BillOrderCodes = string.Join(", ", bill.Select(b => "ORD-" + b.orderID.ToString("0000"))),
                    WaitingOrders = waiting,
                    Items = o.OrderItems.Select(oi => new OrderItemDisplay
                    {
                        Name = oi.Menu?.menuName ?? "Dish",
                        Quantity = oi.quantity,
                        UnitPrice = oi.unitPrice,
                        Note = oi.orderRemark,
                        Discount = oi.discountAmount
                    }).ToList()
                };
            }).ToList();
        }

        private int? CurrentCustomerId()
        {
            return HttpContext.Session.IsSignedInAs(UserRoles.Customer)
                ? HttpContext.Session.GetUserId()
                : null;
        }
    }
}
