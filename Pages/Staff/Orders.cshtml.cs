using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;
using CustomerModel = RestaurantManagementSystem.Model.Customer;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Orders: every order (left), the chosen order or a new order (right).
    //  - OnGet loads ALL orders from the database. Tabs: Active = Order placed, Preparing, Served · Completed · Cancelled.
    //    The view lists the statuses that need staff first (Order placed → Preparing → Served), newest first within each.
    //    ?view=ORD-0082 shows that order on the right; ?status=placed shows the first "Order placed" order.
    //  - Order.orderStatus: Order placed → Preparing → Served → Completed (Cancelled before Served).
    //    Customers' orders arrive as "Order placed"; staff orders are sent straight to the kitchen ("Preparing").
    //  - New order: the "Seated table" dropdown lists today's seated reservations, so every dine-in order links to a reservation.
    //  - Service charge and SST are added on the bill (Billing); the order list shows the subtotal after promotions.
    public class OrdersModel : PageModel
    {
        // A guest is "seated" once staff have seated the reservation (Reservation.cs: Completed = guests seated)
        private static readonly string[] SeatedStatuses = { "Seated", "Completed" };
        private const int MaxQuantity = 20;

        private readonly ApplicationDbContext _context;
        private readonly PromotionService _promotions;

        public OrdersModel(ApplicationDbContext context, PromotionService promotions)
        {
            _context = context;
            _promotions = promotions;
        }

        // ---------- What the page shows (names match Orders.cshtml) ----------
        public string TodayLabel { get; set; } = string.Empty;
        public List<OrderRow> Orders { get; set; } = new();
        public List<SeatedRow> Seated { get; set; } = new();           // New order > Seated table
        public List<MenuRow> MenuRows { get; set; } = new();           // New order > Add items
        public List<string> Categories { get; set; } = new();          // menu category tabs
        public List<PickupOption> PickupOptions { get; set; } = new(); // New order > Takeaway pick-up

        public class OrderRow
        {
            public string Code { get; set; } = string.Empty;            // "ORD-0082"
            public string Type { get; set; } = string.Empty;            // Dine-in / Takeaway
            public string? Table { get; set; }                          // "T-12" (null for takeaway)
            public string Name { get; set; } = string.Empty;            // guest name
            public string? Res { get; set; }                            // "R-0041"
            public string Status { get; set; } = string.Empty;
            public string[] Steps { get; set; } = new string[4];        // time each tracker step was reached ("" = not recorded)
            public string? Pickup { get; set; }                         // takeaway, "21:30"
            public string? Phone { get; set; }                          // takeaway contact phone (Order.contactPhone)
            public decimal Total { get; set; }                          // amount paid, or due: items + service charge + SST (after promotions)
            public List<ItemRow> Items { get; set; } = new();
        }

        public class ItemRow
        {
            public int Qty { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }                          // price when ordered
            public string? Remark { get; set; }                         // kitchen note
            public string? Promo { get; set; }                          // promotion code (null = none)
            public decimal Discount { get; set; }                       // amount taken off this line
        }

        public class SeatedRow
        {
            public string Res { get; set; } = string.Empty;             // "R-0047" (posted as reservationId)
            public string Table { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool WalkIn { get; set; }
        }

        public class MenuRow
        {
            public int Id { get; set; }
            public string Category { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public bool Available { get; set; }
        }

        public class PickupOption
        {
            public string Value { get; set; } = string.Empty;           // "2026-10-24T21:30"
            public string Label { get; set; } = string.Empty;           // "21:30"
        }

        // items[i].menuId / items[i].quantity / items[i].itemRemark posted by the New order form
        public class NewOrderLine
        {
            public int MenuId { get; set; }
            public int Quantity { get; set; }
            public string? ItemRemark { get; set; }
        }

        public async Task OnGetAsync()
        {
            TodayLabel = DateTime.Now.ToString("dddd, d MMMM yyyy");

            // ---- all orders, newest first ----
            var orders = await _context.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.DiningTable)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems).ThenInclude(oi => oi.Menu)
                .OrderByDescending(o => o.orderDateTime)
                .ToListAsync();

            Orders = orders.Select(o => new OrderRow
            {
                Code = OrderCode(o.orderID),
                Type = o.tableID != null ? "Dine-in" : "Takeaway",
                Table = o.DiningTable?.tableNumber,
                Name = o.Customer?.custName ?? "Guest",
                Res = o.reservationID.HasValue ? "R-" + o.reservationID.Value.ToString("0000") : null,
                Status = o.orderStatus,
                // only the placed time (and the payment time once completed) are stored; other steps show "—"
                Steps = new[]
                {
                    o.orderDateTime.ToString("HH:mm"),
                    "",
                    "",
                    o.orderStatus == "Completed" && o.Payment != null ? o.Payment.paymentDate.ToString("HH:mm") : ""
                },
                Pickup = o.pickupDateTime?.ToString("HH:mm"),
                Phone = o.contactPhone,
                Total = o.Payment != null && o.Payment.paymentStatus == "Paid" ? o.Payment.paymentAmount : o.totalAmount,
                Items = o.OrderItems.Select(oi => new ItemRow
                {
                    Qty = oi.quantity,
                    Name = oi.Menu?.menuName ?? "Dish",
                    Price = oi.unitPrice,
                    Remark = oi.orderRemark,
                    Promo = oi.promoCode,
                    Discount = oi.discountAmount
                }).ToList()
            }).ToList();

            // ---- New order: today's seated reservations ----
            Seated = (await LoadSeatedAsync()).Select(r => new SeatedRow
            {
                Res = "R-" + r.reservationID.ToString("0000"),
                Table = r.DiningTable?.tableNumber ?? "—",
                Name = r.Customer?.custName ?? "Guest",
                WalkIn = false
            }).ToList();

            // ---- New order: menu (only categories shown to customers) ----
            var menus = await _context.Menus.AsNoTracking()
                .Include(m => m.MenuCategory)
                .Where(m => m.MenuCategory != null && m.MenuCategory.categoryActive)
                .OrderBy(m => m.categoryID).ThenBy(m => m.menuName)
                .ToListAsync();

            MenuRows = menus.Select(m => new MenuRow
            {
                Id = m.menuID,
                Category = m.MenuCategory!.categoryName,
                Name = m.menuName,
                Price = m.menuPrice,
                Available = m.isAvailable
            }).ToList();

            Categories = MenuRows.Select(m => m.Category).Distinct().ToList();

            // ---- New order: takeaway pick-up times, every 15 minutes from 15 minutes ahead ----
            var first = DateTime.Now.AddMinutes(15);
            first = new DateTime(first.Year, first.Month, first.Day, first.Hour, first.Minute / 15 * 15, 0).AddMinutes(15);
            for (int i = 0; i < 4; i++)
            {
                var t = first.AddMinutes(15 * i);
                PickupOptions.Add(new PickupOption { Value = t.ToString("yyyy-MM-dd'T'HH:mm"), Label = t.ToString("HH:mm") });
            }
        }

        // ---------- New order ----------
        // Fields: orderType "DineIn"/"Takeaway", reservationId (dine-in), guestName, guestPhone, pickupDateTime (takeaway),
        // items[i].menuId, items[i].quantity, items[i].itemRemark.
        // Creates the Order (orderStatus "Preparing", staffID = signed-in staff) + OrderItems (unitPrice = current Menu price).
        public async Task<IActionResult> OnPostCreateAsync(
            string orderType,
            string? reservationId,
            string? guestName,
            string? guestPhone,
            DateTime? pickupDateTime,
            List<NewOrderLine>? items)
        {
            int? staffId = CurrentStaffId();
            if (staffId == null) return RedirectToPage("/Staff/Login");

            bool dineIn = orderType == "DineIn";
            if (!dineIn && orderType != "Takeaway") return Done("Please choose dine-in or takeaway.", null);

            //1. items (quantity 0 = not ordered)
            var lines = (items ?? new List<NewOrderLine>()).Where(i => i.Quantity > 0).ToList();
            if (lines.Count == 0) return Done("Add at least one item before sending the order.", null);
            if (lines.Any(i => i.Quantity > MaxQuantity)) return Done($"At most {MaxQuantity} of each dish per order.", null);

            var menuIds = lines.Select(l => l.MenuId).Distinct().ToList();
            var menus = await _context.Menus
                .Include(m => m.MenuCategory)
                .Where(m => menuIds.Contains(m.menuID))
                .ToDictionaryAsync(m => m.menuID);

            foreach (var line in lines)
            {
                if (!menus.TryGetValue(line.MenuId, out var menu)) return Done("An item in the order no longer exists.", null);
                if (!menu.isAvailable || menu.MenuCategory?.categoryActive == false) return Done($"{menu.menuName} is sold out.", null);
            }

            //2. who the order is for
            int custId = 0;
            CustomerModel? guest = null;
            int? tableId = null;
            int? reservationKey = null;
            DateTime? pickup = null;
            string? contactPhone = null;

            if (dineIn)
            {
                //a seated reservation of today → the customer and the table come from it
                int resId = ParseId(reservationId);
                var seated = (await LoadSeatedAsync()).FirstOrDefault(r => r.reservationID == resId);
                if (seated == null) return Done("Choose a table with a seated reservation.", null);

                custId = seated.custID;
                tableId = seated.tableID;
                reservationKey = seated.reservationID;
            }
            else
            {
                string name = (guestName ?? string.Empty).Trim();
                string phone = NormalisePhone(guestPhone);
                if (name.Length == 0) return Done("Enter the guest name for the takeaway order.", null);
                if (phone.Length < 10 || phone.Length > 11) return Done("Enter a valid guest phone number (10 to 11 digits).", null);
                if (pickupDateTime == null || pickupDateTime < DateTime.Now.AddMinutes(-5)) return Done("Choose a pick-up time that has not passed.", null);

                pickup = pickupDateTime;
                contactPhone = phone;                               // saved on the order

                // custID is required, and a walk-in guest has no account: use a guest record created by staff
                // (the same name + phone is reused). The phone is also saved on the order (contactPhone).
                guest = await _context.Customers.FirstOrDefaultAsync(c => c.custEmail.EndsWith("@guest.local")
                                                                       && c.custPhone == phone && c.custName == name);
                if (guest == null)
                {
                    guest = new CustomerModel
                    {
                        custName = name.Length > 100 ? name.Substring(0, 100) : name,
                        custEmail = $"walkin-{Guid.NewGuid():N}@guest.local",
                        custPassword = "!" + Guid.NewGuid().ToString("N"),
                        custPhone = phone
                    };
                    _context.Customers.Add(guest);
                }
            }

            //3. promotions + totals (same server-side rules as the customer Checkout).
            //   A walk-in takeaway guest is not a member, so birthday and limited promotions don't apply.
            var buyer = guest != null ? PromoBuyer.Guest : await _promotions.GetBuyerAsync(custId);
            var applied = await _promotions.PriceNewOrderAsync(buyer, orderType,
                lines.Select(l => new PricedLine(l.MenuId, menus[l.MenuId].menuPrice, l.Quantity)).ToList());
            var priced = lines.Select((l, i) => new
            {
                Line = l,
                Menu = menus[l.MenuId],
                Promo = applied[i]
            }).ToList();

            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            decimal servicePct = settings?.serviceChargePct ?? 10m;
            decimal sstPct = settings?.sstPct ?? 6m;

            decimal subtotal = priced.Sum(x => x.Menu.menuPrice * x.Line.Quantity - (x.Promo?.LineDiscount ?? 0m));
            decimal service = dineIn ? Math.Round(subtotal * servicePct / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
            decimal sst = Math.Round((subtotal + service) * sstPct / 100m, 2, MidpointRounding.AwayFromZero);

            //4. save
            var order = new Order
            {
                custID = custId,
                reservationID = reservationKey,
                tableID = tableId,
                staffID = staffId,
                pickupDateTime = pickup,
                contactPhone = contactPhone,
                orderDateTime = DateTimeOffset.Now,
                orderStatus = "Preparing",                          // staff orders go straight to the kitchen
                totalAmount = subtotal + service + sst
            };
            if (guest != null) order.Customer = guest;

            foreach (var x in priced)
            {
                string? note = x.Line.ItemRemark?.Trim();
                if (string.IsNullOrEmpty(note)) note = null;
                else if (note.Length > 200) note = note.Substring(0, 200);

                order.OrderItems.Add(new OrderItem
                {
                    menuID = x.Line.MenuId,
                    quantity = x.Line.Quantity,
                    unitPrice = x.Menu.menuPrice,
                    orderRemark = note,
                    promoCode = x.Promo?.Code,
                    promoID = x.Promo?.PromoId,
                    discountAmount = x.Promo?.LineDiscount ?? 0m
                });
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            string code = OrderCode(order.orderID);
            return Done($"New order {code} sent to the kitchen.", code);
        }

        // Order placed → Preparing (staffID = signed-in staff)
        public async Task<IActionResult> OnPostAcceptAsync(string orderId)
        {
            int? staffId = CurrentStaffId();
            if (staffId == null) return RedirectToPage("/Staff/Login");

            var order = await FindOrderAsync(orderId);
            if (order == null) return Done("Order not found.", null);

            string code = OrderCode(order.orderID);
            if (order.orderStatus != "Order placed")
                return Done($"{code} cannot be accepted: it is already {order.orderStatus}.", code);

            order.orderStatus = "Preparing";
            order.staffID = staffId;
            await _context.SaveChangesAsync();

            return Done($"{code} accepted. The kitchen is preparing it.", code);
        }

        // Preparing → Served
        public async Task<IActionResult> OnPostServeAsync(string orderId)
        {
            if (CurrentStaffId() == null) return RedirectToPage("/Staff/Login");

            var order = await FindOrderAsync(orderId);
            if (order == null) return Done("Order not found.", null);

            string code = OrderCode(order.orderID);
            if (order.orderStatus != "Preparing")
                return Done($"{code} cannot be marked as served: it is {order.orderStatus}.", code);

            order.orderStatus = "Served";
            await _context.SaveChangesAsync();

            return Done($"{code} marked as served.", code);
        }

        // Order placed / Preparing → Cancelled (field: cancelReason); a paid takeaway is refunded
        public async Task<IActionResult> OnPostCancelOrderAsync(string orderId, string? cancelReason)
        {
            if (CurrentStaffId() == null) return RedirectToPage("/Staff/Login");

            var order = await FindOrderAsync(orderId);
            if (order == null) return Done("Order not found.", null);

            string code = OrderCode(order.orderID);
            if (order.orderStatus != "Order placed" && order.orderStatus != "Preparing")
                return Done($"{code} cannot be cancelled: it is already {order.orderStatus}.", code);

            order.orderStatus = "Cancelled";

            bool refunded = order.Payment != null && order.Payment.paymentStatus == "Paid";
            if (refunded) order.Payment!.paymentStatus = "Refunded";

            await _context.SaveChangesAsync();

            return Done(refunded ? $"{code} cancelled. The payment is refunded to the guest." : $"{code} cancelled.", code);
        }

        // ---------- helpers ----------

        private int? CurrentStaffId()
        {
            return HttpContext.Session.IsSignedInAs(UserRoles.Staff)
                ? HttpContext.Session.GetUserId()
                : null;
        }

        private static string OrderCode(int orderId) => "ORD-" + orderId.ToString("0000");

        // "ORD-0082" / "R-0041" → 82 / 41 (0 when it is not a number)
        private static int ParseId(string? code)
        {
            string digits = new string((code ?? string.Empty).Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out int id) ? id : 0;
        }

        // "+60 12-345 6789" → "0123456789"
        private static string NormalisePhone(string? phone)
        {
            string digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.StartsWith("60") ? "0" + digits.Substring(2) : digits;
        }

        private Task<Order?> FindOrderAsync(string orderCode)
        {
            int id = ParseId(orderCode);
            return _context.Orders
                .Include(o => o.Payment)
                .FirstOrDefaultAsync(o => o.orderID == id);
        }

        // Today's seated reservations (the only ones that can get a dine-in order)
        private async Task<List<Reservation>> LoadSeatedAsync()
        {
            var today = DateTime.Today;
            var statuses = SeatedStatuses;

            return await _context.Reservations.AsNoTracking()
                .Include(r => r.Customer)
                .Include(r => r.DiningTable)
                .Where(r => r.reservationDateTime >= today && r.reservationDateTime < today.AddDays(1)
                         && statuses.Contains(r.reservationStatus))
                .OrderBy(r => r.reservationDateTime)
                .ToListAsync();
        }

        private IActionResult Done(string message, string? orderCode)
        {
            TempData["StaffMessage"] = message;
            return orderCode == null ? RedirectToPage() : RedirectToPage(new { view = orderCode });
        }
    }
}