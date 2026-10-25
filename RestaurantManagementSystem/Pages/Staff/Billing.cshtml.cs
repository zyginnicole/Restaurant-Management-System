using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Billing: take dine-in payments at the table and show receipts.
    //  - OnGet loads ALL orders (except Cancelled) and turns them into bills. The page filters them by day
    //    (Tonight / Yesterday / All). ?order=ORD-0083 shows that bill (Orders page + Staff Dashboard link here).
    //  - One bill = one table visit, the same rule as the customer's Payment page:
    //      dine-in  → every unpaid open order at the same table is ONE bill (shown under its lowest order code);
    //                 paid dine-in orders that were paid together are one receipt
    //      takeaway → one bill per order
    //  - Bill state:
    //      Awaiting payment = every order on the bill is "Served" and none is Paid
    //      Still dining     = an order is still "Order placed" / "Preparing"
    //      Paid             = Payment.paymentStatus "Paid" (takeaway: usually paid online on the Payment page)
    //  - Totals: subtotal after promotions, + service charge (dine-in only) + SST on (that + service charge).
    //    Rates come from Admin > Settings, rounded per order exactly like Checkout / Payment, so the numbers match.
    //  - Promotions: the dropdown lists promotions running today that apply to dishes on the bill that have no
    //    promotion yet. The chosen one is saved on those OrderItem lines (promoCode, discountAmount).
    //  - Paying: one Payment per order (same time and method), orders → Completed, the table → Available, all in
    //    one SaveChanges. The reservation process already ended when the guests were seated (the table became
    //    Occupied on DiningTable), so the reservation is only touched if it is still marked "Seated".
    public class BillingModel : PageModel
    {
        private static readonly string[] OpenStatuses = { "Order placed", "Preparing", "Served" };

        private readonly ApplicationDbContext _context;

        public BillingModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ---------- What the page shows (names match Billing.cshtml) ----------
        public string TodayLabel { get; set; } = string.Empty;
        public List<BillRow> Bills { get; set; } = new();

        // "Tonight at a glance"
        public decimal CollectedTonight { get; set; }
        public int PaidBills { get; set; }
        public int PaidDineIn { get; set; }
        public int PaidTakeaway { get; set; }
        public decimal AwaitingTotal { get; set; }
        public int AwaitingCount { get; set; }
        public List<string> AwaitingTables { get; set; } = new();
        public List<string> DiningTables { get; set; } = new();          // tables still dining (dine-in, not served yet)
        public int CoversTonight { get; set; }                           // guests of today's reservations that are seated

        public class BillRow
        {
            public string Code { get; set; } = string.Empty;             // "ORD-0083" – lowest order on the bill (posted as orderId)
            public string Codes { get; set; } = string.Empty;            // "ORD-0083, ORD-0084" – every order on the bill
            public string Day { get; set; } = "tonight";                 // tonight / yesterday / earlier
            public string DateText { get; set; } = string.Empty;         // "12 Oct" (for earlier days)
            public string? Table { get; set; }                           // "T-09" (null for takeaway)
            public string Name { get; set; } = string.Empty;
            public string? Res { get; set; }                             // "R-0039" when the order came from a reservation
            public string State { get; set; } = "open";                  // awaiting / open / paid
            public string StateText { get; set; } = string.Empty;
            public string OrderStatus { get; set; } = string.Empty;      // least advanced status on the bill
            public string Placed { get; set; } = string.Empty;           // "18:12"
            public string PlacedFull { get; set; } = string.Empty;       // "16 Sep 2026, 18:12"
            public string? PaidWith { get; set; }                        // "Card", "Card (online)"
            public string? PaidAt { get; set; }                          // "19:45"
            public DateTimeOffset? PaidAtFull { get; set; }
            public string? ReceiptNo { get; set; }                       // "RCP-0012" (paid)
            public string? Pickup { get; set; }                          // takeaway "21:30"
            public string? Phone { get; set; }                           // takeaway contact phone (Order.contactPhone)
            public bool CanCollect { get; set; }                         // paid takeaway that is ready: "Mark collected"
            public decimal Subtotal { get; set; }                        // after promotions already on the lines
            public decimal Service { get; set; }
            public decimal Sst { get; set; }
            public decimal Total { get; set; }
            public List<BillLine> Items { get; set; } = new();
            public List<BillPromo> Promos { get; set; } = new();         // awaiting bills only
        }

        public class BillLine
        {
            public int Qty { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Cat { get; set; } = string.Empty;
            public decimal Price { get; set; }                           // price when ordered
            public string? Remark { get; set; }                          // kitchen note
            public string? Promo { get; set; }                           // promotion code (null = none)
            public decimal Discount { get; set; }                        // amount taken off this line
        }

        // One entry of the Promotion dropdown with the bill totals that result from choosing it
        public class BillPromo
        {
            public string Id { get; set; } = string.Empty;               // Promotion.promoID
            public string Title { get; set; } = string.Empty;
            public decimal Discount { get; set; }
            public decimal Service { get; set; }
            public decimal Sst { get; set; }
            public decimal Total { get; set; }
        }

        private record OrderPrice(decimal Subtotal, decimal Service, decimal Sst, decimal Total);

        public async Task OnGetAsync()
        {
            var now = DateTime.Now;
            var today = now.Date;
            TodayLabel = now.ToString("dddd, d MMMM yyyy");

            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            decimal servicePct = settings?.serviceChargePct ?? 10m;
            decimal sstPct = settings?.sstPct ?? 6m;

            // ---- every order that can have a bill (Cancelled ones have none) ----
            var orders = (await _context.Orders.AsNoTracking()
                    .Include(o => o.Customer)
                    .Include(o => o.DiningTable)
                    .Include(o => o.Payment)
                    .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu).ThenInclude(m => m!.MenuCategory)
                    .Where(o => o.orderStatus != "Cancelled")
                    .OrderBy(o => o.orderID)
                    .ToListAsync())
                .Where(o => !(o.orderStatus == "Completed" && !IsPaid(o)))      // completed without a payment: nothing to bill
                .ToList();

            // ---- promotions running today (also those that are not auto-applied) ----
            var promotions = (await _context.Promotions.AsNoTracking()
                    .Include(p => p.Menus)
                    .Where(p => p.promoActive)
                    .ToListAsync())
                .Where(p => PromotionPricing.IsRunning(p, today))
                .OrderBy(p => p.promoTitle)
                .ToList();

            // ---- group the orders into bills ----
            var groups = new List<List<Order>>();
            groups.AddRange(orders.Where(o => o.tableID == null).Select(o => new List<Order> { o }));          // takeaway: one each
            var dineIn = orders.Where(o => o.tableID != null).ToList();
            groups.AddRange(dineIn.Where(o => !IsPaid(o)).GroupBy(o => o.tableID!.Value).Select(g => g.ToList()));
            groups.AddRange(dineIn.Where(IsPaid).GroupBy(o => (o.tableID!.Value, o.Payment!.paymentDate)).Select(g => g.ToList()));

            var bills = groups.Select(g => BuildBill(g, today, servicePct, sstPct, promotions)).ToList();

            // awaiting payment first, then still dining, then paid; newest first within each
            Bills = bills
                .OrderBy(b => b.State == "awaiting" ? 0 : b.State == "open" ? 1 : 2)
                .ThenByDescending(b => b.Code)
                .ToList();

            // ---- tonight at a glance ----
            var tonight = Bills.Where(b => b.Day == "tonight").ToList();
            var paid = tonight.Where(b => b.State == "paid").ToList();
            var awaiting = tonight.Where(b => b.State == "awaiting").ToList();

            CollectedTonight = paid.Sum(b => b.Total);
            PaidBills = paid.Count;
            PaidDineIn = paid.Count(b => b.Table != null);
            PaidTakeaway = paid.Count(b => b.Table == null);
            AwaitingTotal = awaiting.Sum(b => b.Total);
            AwaitingCount = awaiting.Count;
            AwaitingTables = awaiting.Where(b => b.Table != null).Select(b => b.Table!).OrderBy(t => t).ToList();
            DiningTables = tonight.Where(b => b.State == "open" && b.Table != null).Select(b => b.Table!).OrderBy(t => t).ToList();

            CoversTonight = await _context.Reservations.AsNoTracking()
                .Where(r => r.reservationDateTime >= today && r.reservationDateTime < today.AddDays(1)
                         && (r.reservationStatus == "Completed" || r.reservationStatus == "Seated"))
                .SumAsync(r => (int?)r.partySize) ?? 0;
        }

        // ---------- Record payment ----------
        // Fields: orderId (any order on the bill), paymentMethod ("Cash" / "Card" / "E-Wallet"),
        // promotionId (optional), amountReceived (cash).
        // → one Payment per order on the bill (paymentAmount = that order's total, "Paid", same time),
        //   Order.orderStatus "Completed", DiningTable.tableStatus "Available". One SaveChanges.
        public async Task<IActionResult> OnPostPayAsync(string orderId, string? paymentMethod, int? promotionId, decimal? amountReceived)
        {
            int? staffId = CurrentStaffId();
            if (staffId == null) return RedirectToPage("/Staff/Login");

            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            decimal servicePct = settings?.serviceChargePct ?? 10m;
            decimal sstPct = settings?.sstPct ?? 6m;

            //1. payment method (Admin > Settings can switch methods off)
            bool methodOk = paymentMethod switch
            {
                "Cash" => settings?.acceptCash ?? true,
                "Card" => settings?.acceptCard ?? true,
                "E-Wallet" => settings?.acceptEwallet ?? true,
                _ => false
            };
            if (!methodOk) return Done("Choose a payment method that is accepted (Cash, Card or E-Wallet).", orderId);

            //2. the order and its bill
            var order = await _context.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.orderID == ParseId(orderId));
            if (order == null) return Done("Order not found.", null);

            string code = OrderCode(order.orderID);
            if (order.orderStatus is "Cancelled" or "Completed" || IsPaid(order))
                return Done($"{code} is already settled.", code);

            bool isDineIn = order.tableID != null;
            var bill = isDineIn
                ? await BillQuery()
                    .Where(o => o.tableID == order.tableID
                             && OpenStatuses.Contains(o.orderStatus)
                             && (o.Payment == null || o.Payment.paymentStatus != "Paid"))
                    .OrderBy(o => o.orderID).ToListAsync()
                : await BillQuery().Where(o => o.orderID == order.orderID).ToListAsync();

            //the bill opens only when EVERY order on it has been served
            var waiting = bill.Where(o => o.orderStatus != "Served").ToList();
            if (waiting.Count > 0)
                return Done($"{code} cannot be paid yet: {string.Join(", ", waiting.Select(o => OrderCode(o.orderID)))} not served yet.", code);

            //3. promotion (optional): saved on the dish lines it applies to
            var plan = new Dictionary<OrderItem, (string Code, decimal Discount)>();
            if (promotionId != null)
            {
                var promo = await _context.Promotions.AsNoTracking().Include(p => p.Menus)
                    .FirstOrDefaultAsync(p => p.promoID == promotionId);
                if (promo == null || !promo.promoActive || !PromotionPricing.IsRunning(promo, DateTime.Today))
                    return Done("That promotion is not available today.", code);

                plan = PlanPromotion(promo, bill);
                if (plan.Count == 0)
                    return Done($"{promo.promoCode} does not apply to any dish on this bill.", code);

                foreach (var line in bill.SelectMany(o => o.OrderItems))
                {
                    if (plan.TryGetValue(line, out var applied))
                    {
                        line.promoCode = applied.Code;
                        line.discountAmount = applied.Discount;
                    }
                }
            }

            //4. totals (current rates; each order rounded on its own like Checkout)
            foreach (var o in bill)
            {
                o.totalAmount = PriceOrder(o, isDineIn, servicePct, sstPct, null).Total;
            }
            decimal total = bill.Sum(o => o.totalAmount);

            if (paymentMethod == "Cash" && (amountReceived ?? 0m) < total)
                return Done($"The amount received is less than the total (RM {total:#,##0.00}).", code);

            //5. payments + orders + table: all saved together
            var paidAt = DateTimeOffset.Now;
            foreach (var o in bill)
            {
                var payment = o.Payment;
                if (payment == null)
                {
                    payment = new Payment { orderID = o.orderID };
                    _context.Payments.Add(payment);
                }

                payment.paymentAmount = o.totalAmount;
                payment.paymentMethod = paymentMethod!;
                payment.paymentStatus = "Paid";
                payment.paymentDate = paidAt;

                o.orderStatus = "Completed";
                o.staffID ??= staffId;
            }

            if (isDineIn)
            {
                var table = await _context.DiningTables.FirstOrDefaultAsync(t => t.tableID == order.tableID);
                if (table != null && table.tableStatus == "Occupied") table.tableStatus = "Available";

                //a reservation that is still marked Seated ends with the visit
                var reservationIds = bill.Where(o => o.reservationID != null).Select(o => o.reservationID!.Value).Distinct().ToList();
                var seatedReservations = await _context.Reservations
                    .Where(r => reservationIds.Contains(r.reservationID) && r.reservationStatus == "Seated")
                    .ToListAsync();
                foreach (var r in seatedReservations) r.reservationStatus = "Completed";
            }

            await _context.SaveChangesAsync();

            string codes = string.Join(", ", bill.Select(o => OrderCode(o.orderID)));
            string change = paymentMethod == "Cash" && amountReceived > total ? $" Change due: RM {(amountReceived.Value - total):#,##0.00}." : "";
            string tableText = isDineIn ? " The table is free again." : "";
            return Done($"Payment for {codes} recorded ({paymentMethod}, RM {total:#,##0.00}). The order is completed.{tableText}{change}", code);
        }

        // Paid takeaway that is served (ready) → Completed once the guest has collected it
        public async Task<IActionResult> OnPostCollectAsync(string orderId)
        {
            if (CurrentStaffId() == null) return RedirectToPage("/Staff/Login");

            var order = await _context.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.orderID == ParseId(orderId));
            if (order == null) return Done("Order not found.", null);

            string code = OrderCode(order.orderID);
            if (order.tableID != null || !IsPaid(order) || order.orderStatus != "Served")
                return Done($"{code} cannot be marked as collected: it must be a paid takeaway that has been served.", code);

            order.orderStatus = "Completed";
            await _context.SaveChangesAsync();

            return Done($"{code} collected. The order is completed.", code);
        }

        // ---------- building a bill ----------

        private static BillRow BuildBill(List<Order> group, DateTime today, decimal servicePct, decimal sstPct, List<Promotion> promotions)
        {
            var first = group.OrderBy(o => o.orderID).First();
            bool dineIn = first.tableID != null;
            bool paid = group.All(IsPaid);
            bool allServed = group.All(o => o.orderStatus == "Served");

            string state = paid ? "paid" : allServed ? "awaiting" : "open";
            string orderStatus = group.Any(o => o.orderStatus == "Order placed") ? "Order placed"
                               : group.Any(o => o.orderStatus == "Preparing") ? "Preparing"
                               : group.OrderBy(o => o.orderID).First().orderStatus;

            var placed = group.Min(o => o.orderDateTime);
            var placedDate = placed.LocalDateTime.Date;
            string day = placedDate == today ? "tonight" : placedDate == today.AddDays(-1) ? "yesterday" : "earlier";

            // totals: unpaid bills use the current rates; paid bills show what was really charged
            var prices = group.Select(o => PriceOrder(o, dineIn, servicePct, sstPct, null)).ToList();
            decimal subtotal = prices.Sum(p => p.Subtotal);
            decimal service = prices.Sum(p => p.Service);
            decimal total = paid ? group.Sum(o => o.totalAmount) : prices.Sum(p => p.Total);

            var payment = paid ? group.Select(o => o.Payment!).OrderBy(p => p.paymentID).First() : null;
            bool paidOnline = paid && !dineIn && group.Any(o => o.orderStatus != "Completed");

            var bill = new BillRow
            {
                Code = OrderCode(first.orderID),
                Codes = string.Join(", ", group.OrderBy(o => o.orderID).Select(o => OrderCode(o.orderID))),
                Day = day,
                DateText = placed.ToString("d MMM"),
                Table = first.DiningTable?.tableNumber,
                Name = first.Customer?.custName ?? "Guest",
                Res = group.Where(o => o.reservationID != null).Select(o => "R-" + o.reservationID!.Value.ToString("0000")).FirstOrDefault(),
                State = state,
                StateText = state == "awaiting" ? "Awaiting payment"
                          : state == "open" ? (dineIn ? "Still dining" : "In progress")
                          : paidOnline ? "Paid online" : "Paid",
                OrderStatus = orderStatus,
                Placed = placed.ToString("HH:mm"),
                PlacedFull = placed.ToString("d MMM yyyy, HH:mm"),
                Pickup = first.pickupDateTime?.ToString("HH:mm"),
                Phone = first.contactPhone,
                Subtotal = subtotal,
                Service = service,
                Sst = total - subtotal - service,                                  // the rest, so the rows always add up
                Total = total,
                Items = group.OrderBy(o => o.orderID)
                    .SelectMany(o => o.OrderItems)
                    .GroupBy(oi => new { oi.menuID, oi.unitPrice, oi.orderRemark, oi.promoCode })
                    .Select(g => new BillLine
                    {
                        Qty = g.Sum(x => x.quantity),
                        Name = g.First().Menu?.menuName ?? "Dish",
                        Cat = g.First().Menu?.MenuCategory?.categoryName ?? "",
                        Price = g.Key.unitPrice,
                        Remark = g.Key.orderRemark,
                        Promo = g.Key.promoCode,
                        Discount = g.Sum(x => x.discountAmount)
                    }).ToList()
            };

            if (payment != null)
            {
                bill.PaidWith = paidOnline ? payment.paymentMethod + " (online)" : payment.paymentMethod;
                bill.PaidAt = payment.paymentDate.ToString("HH:mm");
                bill.PaidAtFull = payment.paymentDate;
                bill.ReceiptNo = "RCP-" + payment.paymentID.ToString("0000");
                bill.CanCollect = !dineIn && group.Any(o => o.orderStatus == "Served");
            }

            // Promotion dropdown: each promotion that applies, with the totals it would give
            if (state == "awaiting")
            {
                bill.Promos.Add(new BillPromo { Id = "", Title = "None", Discount = 0m, Service = bill.Service, Sst = bill.Sst, Total = bill.Total });

                foreach (var promo in promotions)
                {
                    var plan = PlanPromotion(promo, group);
                    if (plan.Count == 0) continue;

                    var withPromo = group.Select(o => PriceOrder(o, dineIn, servicePct, sstPct, plan)).ToList();
                    bill.Promos.Add(new BillPromo
                    {
                        Id = promo.promoID.ToString(),
                        Title = $"{promo.promoTitle} · {promo.promoLabel}",
                        Discount = plan.Values.Sum(v => v.Discount),
                        Service = withPromo.Sum(p => p.Service),
                        Sst = withPromo.Sum(p => p.Sst),
                        Total = withPromo.Sum(p => p.Total)
                    });
                }
            }

            return bill;
        }

        // The dish lines a promotion would be applied to on this bill (lines that have no promotion yet):
        //   Percentage / FixedAmount → every covered line; Complimentary → one unit of the cheapest covered dish free
        private static Dictionary<OrderItem, (string Code, decimal Discount)> PlanPromotion(Promotion promo, IEnumerable<Order> orders)
        {
            var plan = new Dictionary<OrderItem, (string Code, decimal Discount)>();
            var covered = promo.Menus.Select(m => m.menuID).ToHashSet();

            var eligible = orders.SelectMany(o => o.OrderItems)
                .Where(oi => covered.Contains(oi.menuID) && string.IsNullOrEmpty(oi.promoCode) && oi.discountAmount == 0m)
                .ToList();

            if (promo.discountType == "Complimentary")
            {
                var cheapest = eligible.OrderBy(oi => oi.unitPrice).FirstOrDefault();
                if (cheapest != null) plan[cheapest] = (promo.promoCode, cheapest.unitPrice);
            }
            else
            {
                foreach (var oi in eligible)
                {
                    decimal discount = PromotionPricing.LineDiscount(promo, oi.unitPrice, oi.quantity);
                    if (discount > 0m) plan[oi] = (promo.promoCode, discount);
                }
            }

            return plan;
        }

        // One order's subtotal / service charge / SST / total (same rounding as Checkout and the Payment page).
        // "plan" = extra promotion discounts to try out; null = the discounts already saved on the lines.
        private static OrderPrice PriceOrder(Order o, bool dineIn, decimal servicePct, decimal sstPct, Dictionary<OrderItem, (string Code, decimal Discount)>? plan)
        {
            decimal subtotal = o.OrderItems.Sum(oi =>
                oi.unitPrice * oi.quantity - (plan != null && plan.TryGetValue(oi, out var extra) ? extra.Discount : oi.discountAmount));
            decimal service = dineIn ? Round(subtotal * servicePct / 100m) : 0m;
            decimal sst = Round((subtotal + service) * sstPct / 100m);
            return new OrderPrice(subtotal, service, sst, subtotal + service + sst);
        }

        // ---------- helpers ----------

        // Orders with their lines, dishes, payment and table (tracked: used when recording a payment)
        private IQueryable<Order> BillQuery() =>
            _context.Orders
                .Include(o => o.Payment)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu);

        private static bool IsPaid(Order o) => o.Payment?.paymentStatus == "Paid";

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

        private int? CurrentStaffId()
        {
            return HttpContext.Session.IsSignedInAs(UserRoles.Staff)
                ? HttpContext.Session.GetUserId()
                : null;
        }

        private static string OrderCode(int orderId) => "ORD-" + orderId.ToString("0000");

        // "ORD-0082" → 82 (0 when it is not a number)
        private static int ParseId(string? code)
        {
            string digits = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int id) ? id : 0;
        }

        private IActionResult Done(string message, string? orderCode)
        {
            TempData["StaffMessage"] = message;
            return string.IsNullOrEmpty(orderCode) ? RedirectToPage() : RedirectToPage(new { order = orderCode });
        }
    }
}