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
    //  - Orders tab (connected): the customer's Completed and Cancelled orders, newest first.
    //  - Payments tab (connected): the customer's payments (Paid, Refunded, Failed). Dine-in orders paid together on
    //    one table bill are ONE row (same receipt no. as on the receipt page).
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

        // ---------- What the Orders and Payments tabs show ----------
        public List<OrderHistoryRow> Orders { get; set; } = new();
        public List<PaymentHistoryRow> Payments { get; set; } = new();
        public List<int> OrderYears { get; set; } = new();          // year filter options
        public List<int> PaymentYears { get; set; } = new();
        public decimal ServiceChargePct { get; set; } = 10m;        // Admin > Settings (labels in the order pop-up)
        public decimal SstPct { get; set; } = 6m;

        public class OrderHistoryRow
        {
            public string Code { get; set; } = string.Empty;         // "ORD-0079"
            public string Status { get; set; } = string.Empty;       // Completed / Cancelled
            public bool IsDineIn { get; set; }
            public string? Table { get; set; }                       // "T-12"
            public string? ReservationCode { get; set; }             // "R-0038"
            public string TypeText { get; set; } = string.Empty;     // "Dine-in · R-0038 · T-12" / "Takeaway · pick-up 21:00"
            public int ItemCount { get; set; }
            public DateTimeOffset Placed { get; set; }
            public DateTimeOffset? PaidAt { get; set; }
            public bool Refunded { get; set; }
            public decimal Subtotal { get; set; }                    // after promotions
            public decimal ServiceCharge { get; set; }              // dine-in only
            public decimal Sst { get; set; }
            public decimal Total { get; set; }
            public List<OrderLineRow> Lines { get; set; } = new();
        }

        public class OrderLineRow
        {
            public int Quantity { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? Note { get; set; }
            public string? PromoCode { get; set; }
            public decimal Discount { get; set; }
            public decimal LineTotal { get; set; }                   // after the promotion
        }

        public class PaymentHistoryRow
        {
            public string Code { get; set; } = string.Empty;         // "RCP-0079" (Failed payments show the order code)
            public string Status { get; set; } = string.Empty;       // Paid / Refunded / Failed
            public string OrderCodes { get; set; } = string.Empty;   // "ORD-0003, ORD-0004"
            public string TypeText { get; set; } = string.Empty;     // Dine-in / Takeaway
            public string Method { get; set; } = string.Empty;
            public DateTimeOffset Date { get; set; }
            public decimal Amount { get; set; }
        }

        public async Task OnGetAsync()
        {
            var custId = HttpContext.Session.GetUserId();
            if (custId == null) return;

            var settings = await _settings.GetAsync();
            DiningMinutes = settings.diningMinutes;
            ServiceChargePct = settings.serviceChargePct;      // labels in the order pop-up
            SstPct = settings.sstPct;
            PastReservations = await _reservations.GetHistoryForCustomerAsync(custId.Value);

            // Ratings for the orders of these visits (one Feedback per order)
            var orderIds = PastReservations.SelectMany(r => r.Orders).Select(o => o.orderID).ToList();
            var feedback = await _db.Feedbacks.AsNoTracking()
                .Where(f => f.custID == custId.Value && orderIds.Contains(f.orderID))
                .ToListAsync();
            FeedbackByOrder = feedback.GroupBy(f => f.orderID)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.feedbackDate).First());

            // Orders and Payments tabs
            await LoadOrdersAsync(custId.Value);
            await LoadPaymentsAsync(custId.Value);
            OrderYears = YearsOf(Orders.Select(o => o.Placed.Year));
            PaymentYears = YearsOf(Payments.Select(p => p.Date.Year));
        }

        // UI flow only (no saving yet) – feedback feature: save the Feedback first, then redirect.
        public IActionResult OnPostRate()
        {
            return RedirectToPage();
        }

        // ---------- helpers for the Orders and Payments tabs ----------

        private static List<int> YearsOf(IEnumerable<int> years)
        {
            var list = years.Distinct().OrderByDescending(y => y).ToList();
            if (list.Count == 0) list.Add(DateTime.Now.Year);
            return list;
        }

        // Completed and Cancelled orders, newest first
        private async Task LoadOrdersAsync(int custId)
        {
            var orders = await _db.Orders.AsNoTracking()
                .Include(o => o.DiningTable)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu)
                .Where(o => o.custID == custId && (o.orderStatus == "Completed" || o.orderStatus == "Cancelled"))
                .OrderByDescending(o => o.orderDateTime)
                .ToListAsync();

            Orders = orders.Select(o =>
            {
                bool dineIn = o.tableID != null;
                bool paid = o.Payment?.paymentStatus == "Paid";
                string? table = o.DiningTable?.tableNumber;
                string? res = o.reservationID.HasValue ? "R-" + o.reservationID.Value.ToString("0000") : null;

                var lines = o.OrderItems.Select(oi => new OrderLineRow
                {
                    Quantity = oi.quantity,
                    Name = oi.Menu?.menuName ?? "Dish",
                    Note = oi.orderRemark,
                    PromoCode = oi.promoCode,
                    Discount = oi.discountAmount,
                    LineTotal = oi.quantity * oi.unitPrice - oi.discountAmount
                }).ToList();

                decimal total = paid ? o.Payment!.paymentAmount : o.totalAmount;
                decimal subtotal = lines.Sum(l => l.LineTotal);
                decimal service = dineIn
                    ? Math.Round(subtotal * ServiceChargePct / 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new OrderHistoryRow
                {
                    Code = "ORD-" + o.orderID.ToString("0000"),
                    Status = o.orderStatus,
                    IsDineIn = dineIn,
                    Table = table,
                    ReservationCode = res,
                    TypeText = dineIn
                        ? "Dine-in" + (res != null ? " · " + res : "") + (table != null ? " · " + table : "")
                        : "Takeaway" + (o.pickupDateTime.HasValue ? " · pick-up " + o.pickupDateTime.Value.ToString("HH:mm") : ""),
                    ItemCount = o.OrderItems.Sum(oi => oi.quantity),
                    Placed = o.orderDateTime,
                    PaidAt = paid ? o.Payment!.paymentDate : null,
                    Refunded = o.Payment?.paymentStatus == "Refunded",
                    Subtotal = subtotal,
                    ServiceCharge = service,
                    Sst = total - subtotal - service,                // the rest, so the rows always add up
                    Total = total,
                    Lines = lines
                };
            }).ToList();
        }

        // Paid / Refunded / Failed payments, newest first (Pending = not paid yet, so not history)
        private async Task LoadPaymentsAsync(int custId)
        {
            var payments = await _db.Payments.AsNoTracking()
                .Include(p => p.Order)
                .Where(p => p.Order != null && p.Order.custID == custId && p.paymentStatus != "Pending")
                .OrderByDescending(p => p.paymentDate)
                .ToListAsync();

            // one table bill = every dine-in order paid at the same moment; takeaway = one order each
            var groups = payments.GroupBy(p => p.Order!.tableID != null
                ? $"T{p.Order.tableID}|{p.paymentDate.UtcTicks}|{p.paymentStatus}"
                : $"O{p.orderID}");

            Payments = groups.Select(g =>
            {
                var first = g.First();
                string status = first.paymentStatus;
                var orderCodes = g.Select(p => "ORD-" + p.orderID.ToString("0000")).OrderBy(c => c).ToList();

                return new PaymentHistoryRow
                {
                    Code = status == "Failed" ? orderCodes[0] : "RCP-" + g.Min(p => p.paymentID).ToString("0000"),
                    Status = status,
                    OrderCodes = string.Join(", ", orderCodes),
                    TypeText = first.Order!.tableID != null ? "Dine-in" : "Takeaway",
                    Method = first.paymentMethod,
                    Date = first.paymentDate,
                    Amount = g.Sum(p => p.paymentAmount)
                };
            })
            .OrderByDescending(r => r.Date)
            .ToList();
        }
    }
}