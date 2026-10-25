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
            public decimal LineTotal { get; set; }                  // quantity × price (before the promotion)
            public string? PromoCode { get; set; }                  // promotion applied to this dish (null = none)
            public decimal Discount { get; set; }                   // amount taken off this line
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
            string? paymentMethod,
            string? cardName,
            string? cardNumber,
            string? cardExpiry,
            string? cardCvc)
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

            //card: every field is required and checked (the details are only checked here, never stored or logged)
            if (paymentMethod == "Card")
            {
                string? cardError = ValidateCard(cardName, cardNumber, cardExpiry, cardCvc);
                if (cardError != null)
                {
                    ModelState.AddModelError(string.Empty, cardError);
                    return Page();
                }
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

            //a paid dine-in bill ends the visit: the table is free again (unless other orders are still open at it)
            //and a reservation still marked Seated is completed (same as staff Billing)
            if (paid && IsDineIn)
            {
                int? tableId = _billOrders.First().tableID;
                var billIds = _billOrders.Select(o => o.orderID).ToList();
                var open = OpenStatuses;

                bool othersOpen = await _context.Orders.AnyAsync(o => o.tableID == tableId
                                                                   && open.Contains(o.orderStatus)
                                                                   && !billIds.Contains(o.orderID));
                if (!othersOpen)
                {
                    var table = await _context.DiningTables.FirstOrDefaultAsync(t => t.tableID == tableId);
                    if (table != null && table.tableStatus == "Occupied") table.tableStatus = "Available";
                }

                var reservationIds = _billOrders.Where(o => o.reservationID != null).Select(o => o.reservationID!.Value).Distinct().ToList();
                var seatedReservations = await _context.Reservations
                    .Where(r => reservationIds.Contains(r.reservationID) && r.reservationStatus == "Seated")
                    .ToListAsync();
                foreach (var r in seatedReservations) r.reservationStatus = "Completed";
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
                .GroupBy(oi => new { oi.menuID, oi.unitPrice, oi.orderRemark, oi.promoCode })
                .Select(g => new BillLine
                {
                    Name = g.First().Menu?.menuName ?? "Item",
                    Quantity = g.Sum(x => x.quantity),
                    Note = g.Key.orderRemark,
                    LineTotal = g.Sum(x => x.quantity * x.unitPrice),
                    PromoCode = g.Key.promoCode,
                    Discount = g.Sum(x => x.discountAmount)
                })
                .ToList();

            //totals: what is charged = the orders' saved totals; service charge worked out per order like Checkout
            Subtotal = Lines.Sum(l => l.LineTotal - l.Discount);    // after promotions
            Total = _billOrders.Sum(o => o.totalAmount);
            ServiceCharge = IsDineIn
                ? _billOrders.Sum(o => Math.Round(o.OrderItems.Sum(oi => oi.unitPrice * oi.quantity - oi.discountAmount) * ServiceChargePct / 100m, 2, MidpointRounding.AwayFromZero))
                : 0m;
            Sst = Total - Subtotal - ServiceCharge;                 // the rest, so the rows always add up
        }

        // ---------- Card checks (simulation, but with real rules) ----------
        private static string? ValidateCard(string? name, string? number, string? expiry, string? cvc)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "Please enter the name on your card.";

            string digits = new string((number ?? string.Empty).Where(char.IsDigit).ToArray());
            if (digits.Length < 13 || digits.Length > 19 || !PassesLuhn(digits))
                return "Please enter a valid card number.";

            if (!ExpiryIsValid(expiry))
                return "Please enter a valid expiry date (MM / YY) that has not passed.";

            string code = (cvc ?? string.Empty).Trim();
            if (code.Length < 3 || code.Length > 4 || !code.All(char.IsDigit))
                return "Please enter the 3 or 4 digit CVC.";

            return null;
        }

        // Standard card-number checksum (Luhn)
        private static bool PassesLuhn(string digits)
        {
            int sum = 0;
            bool doubleIt = false;
            for (int i = digits.Length - 1; i >= 0; i--)
            {
                int d = digits[i] - '0';
                if (doubleIt)
                {
                    d *= 2;
                    if (d > 9) d -= 9;
                }
                sum += d;
                doubleIt = !doubleIt;
            }
            return sum % 10 == 0;
        }

        // "MM / YY": month 01–12, and the card is valid until the end of that month
        private static bool ExpiryIsValid(string? expiry)
        {
            string d = new string((expiry ?? string.Empty).Where(char.IsDigit).ToArray());
            if (d.Length != 4) return false;

            int month = int.Parse(d.Substring(0, 2));
            int year = 2000 + int.Parse(d.Substring(2, 2));
            if (month < 1 || month > 12) return false;

            return new DateTime(year, month, 1).AddMonths(1) > DateTime.Now;
        }

        // MOCK: replace with a real payment gateway call later. Return false to test the Failed path.
        private static bool SimulatePayment(string method)
        {
            return true;
        }
    }
}