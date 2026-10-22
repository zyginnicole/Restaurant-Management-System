using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Payment (SIMULATED – no real payment provider).
    //  - Takeaway: pays the one order, right after checkout.
    //  - Dine-in:  pays ONE bill for every unpaid order of the customer at the same table, and only
    //              after ALL of them are Served. Each order gets its own Payment row (same time and
    //              method) and becomes Completed.
    //  - /Customer/Payment?orderId=... works with any order of the bill.
    //  - Card number / CVC inputs have no name="" so they are never posted or saved.
    public class PaymentModel : PageModel
    {
        private static readonly string[] AllowedMethods = { "Card", "Online Banking", "E-Wallet" };
        private static readonly string[] OpenStatuses = { "Order placed", "Preparing", "Served" };

        private readonly ApplicationDbContext _context;

        public PaymentModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ---------- What the page shows ----------
        public int OrderId { get; set; }                            // the order the customer came from
        public bool IsDineIn { get; set; }
        public string OrderCodes { get; set; } = string.Empty;      // "ORD-0001, ORD-0003"
        public string? TableNumber { get; set; }
        public string? PickupTime { get; set; }                     // takeaway, e.g. "21:30"
        public string CustomerName { get; set; } = string.Empty;
        public List<BillLine> Lines { get; set; } = new();          // same dishes are added together

        public decimal Subtotal { get; set; }
        public decimal ServiceCharge { get; set; }                  // dine-in only
        public decimal Sst { get; set; }
        public decimal Total { get; set; }                          // = sum of the orders' totalAmount
        public decimal ServiceChargePct { get; set; } = 10m;
        public decimal SstPct { get; set; } = 6m;

        public class BillLine
        {
            public string Name { get; set; } = string.Empty;
            public int Quantity { get; set; }
            public string? Note { get; set; }
            public decimal LineTotal { get; set; }
        }

        // The orders being paid together (1 for takeaway, 1 or more for a dine-in table)
        private List<Order> _billOrders = new();

        public async Task<IActionResult> OnGetAsync(int orderId)
        {
            int? custId = CurrentCustomerId();
            if (custId == null) return RedirectToPage("/Customer/Login");

            IActionResult? problem = await PrepareAsync(orderId, custId.Value);
            if (problem != null) return problem;

            return Page();
        }

        // orderId comes from the URL (the hidden orderId box in the form is only a fallback)
        public async Task<IActionResult> OnPostAsync(
            [FromQuery] int orderId,
            [FromForm(Name = "orderId")] int? formOrderId,
            string? paymentMethod)
        {
            int? custId = CurrentCustomerId();
            if (custId == null) return RedirectToPage("/Customer/Login");

            if (orderId == 0) orderId = formOrderId ?? 0;

            IActionResult? problem = await PrepareAsync(orderId, custId.Value);
            if (problem != null) return problem;

            if (string.IsNullOrEmpty(paymentMethod) || !AllowedMethods.Contains(paymentMethod))
            {
                ModelState.AddModelError(string.Empty, "Please choose a payment method.");
                return Page();
            }

            bool paid = SimulatePayment(paymentMethod);
            DateTimeOffset paidAt = DateTimeOffset.Now;             // same time for every order on the bill

            foreach (var bo in _billOrders)
            {
                var payment = bo.Payment;
                if (payment == null)
                {
                    payment = new Payment { orderID = bo.orderID };
                    _context.Payments.Add(payment);
                }

                payment.paymentAmount = bo.totalAmount;
                payment.paymentMethod = paymentMethod;
                payment.paymentStatus = paid ? "Paid" : "Failed";
                payment.paymentDate = paidAt;

                //paying a served dine-in bill completes its orders (takeaway carries on to the kitchen)
                if (paid && IsDineIn)
                {
                    bo.orderStatus = "Completed";
                }
            }

            //one SaveChanges = all payments and order updates succeed or fail together
            await _context.SaveChangesAsync();

            if (!paid)
            {
                ModelState.AddModelError(string.Empty, "Your payment could not be completed. Nothing was charged. Please try again.");
                return Page();
            }

            return RedirectToPage("/Customer/PaymentSuccess", new { orderId = OrderId });
        }

        // ---------- helpers ----------

        private int? CurrentCustomerId()
        {
            return HttpContext.Session.IsSignedInAs(UserRoles.Customer)
                ? HttpContext.Session.GetUserId()
                : null;
        }

        // Finds the customer's order + the bill it belongs to. Returns a redirect / NotFound when it cannot be paid.
        private async Task<IActionResult?> PrepareAsync(int orderId, int custId)
        {
            var order = await _context.Orders
                .Include(o => o.Customer)
                .Include(o => o.Payment)
                .Include(o => o.DiningTable)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu)
                .FirstOrDefaultAsync(o => o.orderID == orderId && o.custID == custId);

            if (order == null) return NotFound();

            if (order.orderStatus == "Cancelled" || order.orderStatus == "Completed")
                return RedirectToPage("/Customer/History");

            if (order.Payment?.paymentStatus == "Paid")
                return RedirectToPage("/Customer/PaymentSuccess", new { orderId = order.orderID });

            OrderId = order.orderID;
            IsDineIn = order.tableID != null;

            if (IsDineIn)
            {
                //one bill = every unpaid open order of this customer at the same table
                var open = OpenStatuses;
                _billOrders = await _context.Orders
                    .Include(o => o.Payment)
                    .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu)
                    .Where(o => o.custID == custId && o.tableID == order.tableID
                             && open.Contains(o.orderStatus)
                             && (o.Payment == null || o.Payment.paymentStatus != "Paid"))
                    .OrderBy(o => o.orderID)
                    .ToListAsync();

                //dine-in can only be paid once EVERY order at the table has been served
                if (_billOrders.Any(o => o.orderStatus != "Served"))
                    return RedirectToPage("/Customer/Orders");
            }
            else
            {
                _billOrders = new List<Order> { order };
            }

            await FillAsync(order);
            return null;
        }

        private async Task FillAsync(Order order)
        {
            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            ServiceChargePct = settings?.serviceChargePct ?? 10m;
            SstPct = settings?.sstPct ?? 6m;

            CustomerName = order.Customer?.custName ?? string.Empty;
            TableNumber = order.DiningTable?.tableNumber;
            PickupTime = order.pickupDateTime?.ToString("HH:mm");
            OrderCodes = string.Join(", ", _billOrders.Select(o => "ORD-" + o.orderID.ToString("0000")));

            //the same dish (same price and note) from different orders is added together
            Lines = _billOrders
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => new { oi.menuID, oi.unitPrice, oi.orderRemark })
                .Select(g => new BillLine
                {
                    Name = g.First().Menu?.menuName ?? "Item",
                    Quantity = g.Sum(x => x.quantity),
                    Note = g.Key.orderRemark,
                    LineTotal = g.Sum(x => x.quantity * x.unitPrice)
                })
                .ToList();

            //totals: what is charged = the orders' saved totals; service charge worked out per order like Checkout
            Subtotal = Lines.Sum(l => l.LineTotal);
            Total = _billOrders.Sum(o => o.totalAmount);
            ServiceCharge = IsDineIn
                ? _billOrders.Sum(o => Math.Round(o.OrderItems.Sum(oi => oi.unitPrice * oi.quantity) * ServiceChargePct / 100m, 2, MidpointRounding.AwayFromZero))
                : 0m;
            Sst = Total - Subtotal - ServiceCharge;                 // the rest, so the rows always add up
        }

        // MOCK: replace with a real payment gateway call later. Return false to test the Failed path.
        private static bool SimulatePayment(string method)
        {
            return true;
        }
    }
}