using System.Globalization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Pages.Shared.Partials;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Monitor: read-only overview of reservations, orders and payments across the restaurant.
    // Staff do the actions (confirm, seat, cancel, take payment) on the Staff pages.
    //  - Tabs: ?tab=reservations (default) · orders · payments.
    //  - Reservations: OnGet(DateTime? from, DateTime? to) – Reservations between the two dates
    //    (default today → +4 days) with Customer + DiningTable (+ area) + Staff (ReservationService.GetBetweenAsync).
    //    Changing From / To reloads the page; status and search are filtered on screen.
    //    Clicking a row opens the details pop-up (?view=R-0041 opens it straight away);
    //    ?q=R-0041 fills the search box so only that booking is listed (Dashboard Recent activity links use both).
    //  - Orders: today's Orders (placed today) with Customer, DiningTable, Payment and OrderItems.
    //    Type = Dine-in when the order has a table (tableID != null, the rule used by every other page:
    //    a walk-in dine-in order has a table but no reservation), otherwise Takeaway.
    //    Statuses: Order placed → Preparing → Served → Completed, or Cancelled.
    //    Filter: All · Active (placed / preparing / served) · Completed · Cancelled.
    //    Clicking an order opens the Order details pop-up: its OrderItems (quantity, Menu.menuName, unitPrice,
    //    orderRemark, promotion), subtotal after promotions, service charge (dine-in only), SST and total.
    //    The total is the amount really charged (Payment.paymentAmount when Paid, otherwise Order.totalAmount);
    //    SST is what is left of the total, so the rows always add up (same rule as the Payment page and History).
    //  - Payments: Payments with Order + Customer, newest first (the latest MaxPayments rows).
    //    paymentStatus Pending / Paid / Failed / Refunded. Dine-in orders paid together at one table are ONE row,
    //    exactly like the customer History page and the receipt (receipt no. = the lowest payment ID of the bill).
    //    "Receipt" fills the shared _Receipt partial (ReceiptView) for Paid / Refunded payments.
    //  - Export CSV is done in the browser from the rows shown on the open tab (no backend needed).
    public class MonitorModel : PageModel
    {
        // Longest range shown at once, so the list stays quick to load
        private const int MaxRangeDays = 92;

        // Newest payment rows loaded for the Payments tab (keeps the page quick on a large history)
        private const int MaxPayments = 200;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly ApplicationDbContext _db;
        private readonly ReservationService _reservations;
        private readonly SettingsService _settings;

        public MonitorModel(ApplicationDbContext db, ReservationService reservations, SettingsService settings)
        {
            _db = db;
            _reservations = reservations;
            _settings = settings;
        }

        // ---------- Reservations tab ----------
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<Reservation> Reservations { get; set; } = new();

        // ---------- Orders and Payments tabs ----------
        public List<OrderRow> Orders { get; set; } = new();
        public List<PaymentRow> Payments { get; set; } = new();

        // Rates from Admin > Settings (labels in the order pop-up, and the receipts)
        public decimal ServicePct { get; set; } = 10m;
        public decimal SstPct { get; set; } = 6m;

        // One order in the Orders tab (+ its details pop-up)
        public class OrderRow
        {
            public string Code { get; set; } = string.Empty;            // "ORD-0085"
            public string Type { get; set; } = string.Empty;            // Dine-in / Takeaway
            public bool IsDineIn { get; set; }
            public string? Table { get; set; }                          // "I-03" (null = takeaway)
            public string? ReservationCode { get; set; }                // "R-0041" when it came from a reservation
            public string CustomerName { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string Group { get; set; } = "active";               // active / completed / cancelled (the filter buttons)
            public string PlacedTime { get; set; } = string.Empty;      // "20:31"
            public string PaymentStatus { get; set; } = "Pending";      // Pending / Paid / Failed / Refunded
            public int ItemCount { get; set; }                          // total quantity
            public decimal Subtotal { get; set; }                       // after promotions
            public decimal ServiceCharge { get; set; }                  // dine-in only
            public decimal Sst { get; set; }
            public decimal Total { get; set; }
            public List<OrderLineRow> Lines { get; set; } = new();
        }

        public class OrderLineRow
        {
            public int Qty { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }                      // price when ordered
            public string? Remark { get; set; }                         // kitchen note
            public string? Promo { get; set; }                          // promotion code (null = none)
            public decimal Discount { get; set; }                       // amount taken off this line
            public decimal LineTotal => Qty * UnitPrice - Discount;     // after the promotion
        }

        // One payment row in the Payments tab (a table bill paid together is one row)
        public class PaymentRow
        {
            public string Code { get; set; } = string.Empty;            // "PAY-0061"
            public string OrderCodes { get; set; } = string.Empty;      // "ORD-0083, ORD-0084"
            public string CustomerName { get; set; } = string.Empty;
            public string Method { get; set; } = string.Empty;          // Card / Online Banking / E-Wallet
            public string Status { get; set; } = string.Empty;          // Pending / Paid / Failed / Refunded
            public string DateText { get; set; } = string.Empty;        // "16 Sep, 20:26"
            public decimal Amount { get; set; }
            public ReceiptView? Receipt { get; set; }                   // only Paid / Refunded
        }

        public async Task OnGetAsync(DateTime? from, DateTime? to)
        {
            From = (from ?? DateTime.Today).Date;
            To = (to ?? From.AddDays(4)).Date;
            if (To < From) (From, To) = (To, From);
            if ((To - From).TotalDays > MaxRangeDays) To = From.AddDays(MaxRangeDays);

            Reservations = await _reservations.GetBetweenAsync(From, To);

            var settings = await _settings.GetAsync();
            ServicePct = settings.serviceChargePct;
            SstPct = settings.sstPct;

            await LoadOrdersAsync();
            await LoadPaymentsAsync();
        }

        // ---------- Orders: placed today, newest first ----------
        private async Task LoadOrdersAsync()
        {
            var start = new DateTimeOffset(DateTime.Today);
            var end = start.AddDays(1);

            var orders = await _db.Orders.AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.Customer)
                .Include(o => o.DiningTable)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu)
                .Where(o => o.orderDateTime >= start && o.orderDateTime < end)
                .OrderByDescending(o => o.orderDateTime)
                .ToListAsync();

            Orders = orders.Select(o =>
            {
                bool dineIn = o.tableID != null;
                bool paid = o.Payment?.paymentStatus == "Paid";

                var lines = o.OrderItems
                    .OrderBy(oi => oi.orderItemID)
                    .Select(oi => new OrderLineRow
                    {
                        Qty = oi.quantity,
                        Name = oi.Menu?.menuName ?? "Dish",
                        UnitPrice = oi.unitPrice,
                        Remark = oi.orderRemark,
                        Promo = oi.promoCode,
                        Discount = oi.discountAmount
                    }).ToList();

                decimal subtotal = lines.Sum(l => l.LineTotal);
                decimal service = dineIn ? Round(subtotal * ServicePct / 100m) : 0m;
                decimal total = paid ? o.Payment!.paymentAmount : o.totalAmount;

                return new OrderRow
                {
                    Code = OrderCode(o.orderID),
                    Type = dineIn ? "Dine-in" : "Takeaway",
                    IsDineIn = dineIn,
                    Table = o.DiningTable?.tableNumber,
                    ReservationCode = o.reservationID.HasValue ? ReservationCode(o.reservationID.Value) : null,
                    CustomerName = o.Customer?.custName ?? "Guest",
                    Status = o.orderStatus,
                    Group = o.orderStatus == "Completed" ? "completed" : o.orderStatus == "Cancelled" ? "cancelled" : "active",
                    PlacedTime = o.orderDateTime.ToLocalTime().ToString("HH:mm", Inv),
                    PaymentStatus = o.Payment?.paymentStatus ?? "Pending",
                    ItemCount = lines.Sum(l => l.Qty),
                    Subtotal = subtotal,
                    ServiceCharge = service,
                    Sst = total - subtotal - service,           // the rest, so the rows always add up
                    Total = total,
                    Lines = lines
                };
            }).ToList();
        }

        // ---------- Payments: newest first; a table bill paid together is one row ----------
        private async Task LoadPaymentsAsync()
        {
            var payments = await _db.Payments.AsNoTracking()
                .AsSplitQuery()
                .Include(p => p.Order).ThenInclude(o => o!.Customer)
                .Include(p => p.Order).ThenInclude(o => o!.DiningTable)
                .Include(p => p.Order).ThenInclude(o => o!.OrderItems).ThenInclude(oi => oi.Menu)
                .Where(p => p.Order != null)
                .OrderByDescending(p => p.paymentDate).ThenByDescending(p => p.paymentID)
                .Take(MaxPayments)
                .ToListAsync();

            // one table bill = every dine-in order paid at the same moment with the same status; takeaway = one order each
            var groups = payments.GroupBy(p => p.Order!.tableID != null
                ? $"T{p.Order.tableID}|{p.paymentDate.UtcTicks}|{p.paymentStatus}"
                : $"O{p.orderID}");

            Payments = groups.Select(g =>
            {
                var first = g.OrderBy(p => p.paymentID).First();
                var billOrders = g.Select(p => p.Order!).OrderBy(o => o.orderID).ToList();
                var firstOrder = billOrders[0];
                bool dineIn = firstOrder.tableID != null;
                string status = first.paymentStatus;
                int number = g.Min(p => p.paymentID);
                var paidAt = first.paymentDate.ToLocalTime();

                var row = new PaymentRow
                {
                    Code = "PAY-" + number.ToString("D4"),
                    OrderCodes = string.Join(", ", billOrders.Select(o => OrderCode(o.orderID))),
                    CustomerName = firstOrder.Customer?.custName ?? "Guest",
                    Method = first.paymentMethod,
                    Status = status,
                    DateText = paidAt.ToString("d MMM, HH:mm", Inv),
                    Amount = g.Sum(p => p.paymentAmount)
                };

                // Receipts exist for Paid and Refunded payments only (same lines as the customer receipt)
                if (status == "Paid" || status == "Refunded")
                {
                    var items = billOrders
                        .SelectMany(o => o.OrderItems)
                        .GroupBy(oi => new { oi.menuID, oi.unitPrice, oi.orderRemark, oi.promoCode })
                        .Select(x => new ReceiptLine
                        {
                            Quantity = x.Sum(i => i.quantity),
                            Name = x.First().Menu?.menuName ?? "Dish",
                            UnitPrice = x.Key.unitPrice,
                            Note = x.Key.orderRemark,
                            PromoCode = x.Key.promoCode,
                            Discount = x.Sum(i => i.discountAmount)
                        }).ToList();

                    int? reservationId = billOrders.Select(o => o.reservationID).FirstOrDefault(id => id != null);

                    row.Receipt = new ReceiptView
                    {
                        ServiceChargePct = ServicePct,
                        SstPct = SstPct,
                        ReceiptNo = "RCP-" + number.ToString("D4"),
                        OrderCode = row.OrderCodes,
                        IsDineIn = dineIn,
                        ReservationCode = reservationId.HasValue ? ReservationCode(reservationId.Value) : null,
                        TableCode = firstOrder.DiningTable?.tableNumber,
                        PickupTime = firstOrder.pickupDateTime?.ToString("HH:mm", Inv),
                        CustomerName = row.CustomerName,
                        Items = items,
                        PaymentMethod = first.paymentMethod,
                        PaymentStatus = status,
                        PaidAt = first.paymentDate
                    };
                }

                return (Row: row, When: first.paymentDate);
            })
            .OrderByDescending(x => x.When)
            .Select(x => x.Row)
            .ToList();
        }

        // ---------- helpers ----------
        private static string OrderCode(int orderId) => "ORD-" + orderId.ToString("D4");

        private static string ReservationCode(int reservationId) => "R-" + reservationId.ToString("D4");

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}