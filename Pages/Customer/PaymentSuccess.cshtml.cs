using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Pages.Shared.Partials;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Payment successful: receipt + order tracker, built from the REAL orders and payment.
    //  - Takeaway: the receipt is the one order.
    //  - Dine-in:  the receipt is the whole table bill = every order of the customer at that table that was
    //              paid together (same table, same payment time).
    public class PaymentSuccessModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public PaymentSuccessModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public ReceiptView Receipt { get; set; } = new();
        public bool IsDineIn { get; set; }
        public string OrderCode { get; set; } = string.Empty;       // "ORD-0001, ORD-0003" for a table bill
        public string OrderStatus { get; set; } = string.Empty;     // tracker: Order placed / Preparing / Served / Completed
        public string PlacedTime { get; set; } = "—";               // when the first order was placed, "HH:mm"
        public string PaidTime { get; set; } = "—";                 // when the bill was paid, "HH:mm"

        public async Task<IActionResult> OnGetAsync(int orderId)
        {
            int? custId = CurrentCustomerId();
            if (custId == null) return RedirectToPage("/Customer/Login");

            //only the customer's own order
            var order = await _context.Orders.AsNoTracking()
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.orderID == orderId && o.custID == custId);

            if (order == null) return NotFound();

            IsDineIn = order.tableID != null;

            //not paid yet: takeaway goes back to pay, dine-in to the Orders page
            if (order.Payment == null || order.Payment.paymentStatus != "Paid")
            {
                return IsDineIn
                    ? RedirectToPage("/Customer/Orders")
                    : RedirectToPage("/Customer/Payment", new { orderId = order.orderID });
            }

            //the orders on this receipt (a dine-in bill can hold several orders paid at the same moment)
            DateTimeOffset paidAt = order.Payment.paymentDate;

            var bill = IsDineIn
                ? await _context.Orders.AsNoTracking()
                    .Include(o => o.Customer)
                    .Include(o => o.Payment)
                    .Include(o => o.DiningTable)
                    .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu)
                    .Where(o => o.custID == custId && o.tableID == order.tableID
                             && o.Payment != null && o.Payment.paymentStatus == "Paid"
                             && o.Payment.paymentDate == paidAt)
                    .OrderBy(o => o.orderID)
                    .ToListAsync()
                : await _context.Orders.AsNoTracking()
                    .Include(o => o.Customer)
                    .Include(o => o.Payment)
                    .Include(o => o.DiningTable)
                    .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu)
                    .Where(o => o.orderID == order.orderID)
                    .ToListAsync();

            var first = bill.First();

            OrderCode = string.Join(", ", bill.Select(o => "ORD-" + o.orderID.ToString("0000")));
            OrderStatus = IsDineIn ? "Completed" : first.orderStatus;
            PlacedTime = bill.Min(o => o.orderDateTime).ToString("HH:mm");
            PaidTime = paidAt.ToString("HH:mm");

            int? reservationId = bill.Select(o => o.reservationID).FirstOrDefault(id => id != null);

            //same rates as Checkout / Payment, so the receipt matches the amount charged
            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();

            Receipt = new ReceiptView
            {
                ServiceChargePct = settings?.serviceChargePct ?? 10m,
                SstPct = settings?.sstPct ?? 6m,
                ReceiptNo = "RCP-" + bill.Min(o => o.Payment!.paymentID).ToString("0000"),
                OrderCode = OrderCode,
                IsDineIn = IsDineIn,
                ReservationCode = reservationId.HasValue ? "R-" + reservationId.Value.ToString("0000") : null,
                TableCode = first.DiningTable?.tableNumber,
                PickupTime = first.pickupDateTime?.ToString("HH:mm"),
                CustomerName = first.Customer?.custName ?? string.Empty,
                PaymentMethod = order.Payment.paymentMethod,
                PaymentStatus = order.Payment.paymentStatus,
                PaidAt = paidAt,
                Items = new()
            };

            //one line per dish (same dish, price and note from different orders are added together)
            var lines = bill
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => new { oi.menuID, oi.unitPrice, oi.orderRemark, oi.promoCode });

            foreach (var g in lines)
            {
                Receipt.Items.Add(new ReceiptLine
                {
                    Quantity = g.Sum(x => x.quantity),
                    Name = g.First().Menu?.menuName ?? "Item",
                    UnitPrice = g.Key.unitPrice,
                    Note = g.Key.orderRemark,
                    PromoCode = g.Key.promoCode,
                    Discount = g.Sum(x => x.discountAmount)
                });
            }

            return Page();
        }

        private int? CurrentCustomerId()
        {
            return HttpContext.Session.IsSignedInAs(UserRoles.Customer)
                ? HttpContext.Session.GetUserId()
                : null;
        }
    }
}