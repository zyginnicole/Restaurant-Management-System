using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using CustomerModel = RestaurantManagementSystem.Model.Customer;

namespace RestaurantManagementSystem.Pages.Customer
{
    public class CheckoutModel : PageModel
    {
        private const int MaxQuantity = CartSessionExtensions.MaxQuantity;
        private const int PrepMinutes = 30;

        private readonly ApplicationDbContext _context;

        public CheckoutModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ---------- Form fields ----------
        [BindProperty] public string OrderType { get; set; } = "Takeaway";   // Takeaway / DineIn
        [BindProperty] public int? TableId { get; set; }                     // Selected table ID
        [BindProperty] public DateTime? PickupDateTime { get; set; }         // Takeaway pick-up time
        [BindProperty] public string? ContactPhone { get; set; }             // Takeaway contact phone

        [BindProperty] public List<CartLine> Items { get; set; } = new();

        public class CartLine
        {
            public int MenuId { get; set; }
            public int Quantity { get; set; }
            public string? ItemRemark { get; set; }
        }

        public class CartDisplayLine
        {
            public int MenuId { get; set; }
            public string Name { get; set; } = "";
            public decimal UnitPrice { get; set; }
            public int Quantity { get; set; }
            public string? Note { get; set; }
            public string? ImageUrl { get; set; }
        }

        public class PickupOption
        {
            public string Value { get; set; } = "";
            public string Label { get; set; } = "";
        }

        public List<CartDisplayLine> CartLines { get; set; } = new();
        public List<string> RemovedNotices { get; set; } = new();
        public List<PickupOption> PickupOptions { get; set; } = new();
        public List<DiningTable> AvailableTables { get; set; } = new();
        public bool HasReservation { get; set; }
        public decimal ServiceChargePct { get; set; } = 10m;
        public decimal SstPct { get; set; } = 6m;
        public string? CustomerPhone { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            int? custId = CurrentCustomerId();
            if (custId == null) return RedirectToPage("/Customer/Login");

            await LoadPageAsync(custId.Value);

            var reservedTableId = await FindOwnReservationTableIdAsync(custId.Value);
            if (reservedTableId.HasValue)
            {
                TableId = reservedTableId.Value;
                HasReservation = true;
                OrderType = "DineIn";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            int? custId = CurrentCustomerId();
            if (custId == null) return RedirectToPage("/Customer/Login");

            bool dineIn = OrderType == "DineIn";
            if (!dineIn && OrderType != "Takeaway")
            {
                return await FailAsync(custId.Value, "Please choose takeaway or dine-in.");
            }

            // 1. Sync session cart with posted items
            var cart = HttpContext.Session.GetCart();
            foreach (var line in cart)
            {
                var posted = Items.FirstOrDefault(i => i.MenuId == line.MenuId);
                if (posted == null) continue;
                line.Quantity = Math.Clamp(posted.Quantity, 0, MaxQuantity + 1);
                string? note = posted.ItemRemark?.Trim();
                line.Note = string.IsNullOrEmpty(note) ? null : (note.Length > 200 ? note.Substring(0, 200) : note);
            }

            var lines = cart.Where(i => i.Quantity > 0).ToList();
            if (lines.Count == 0)
            {
                HttpContext.Session.ClearCart();
                return RedirectToPage("/Customer/Menu");
            }

            HttpContext.Session.SaveCart(lines.Select(l => new CartItem
            {
                MenuId = l.MenuId,
                Quantity = Math.Min(l.Quantity, MaxQuantity),
                Note = l.Note
            }).ToList());

            if (lines.Any(i => i.Quantity > MaxQuantity))
            {
                return await FailAsync(custId.Value, $"You can order at most {MaxQuantity} of each dish.");
            }

            // 2. Validate menu availability and prices
            var menuIds = lines.Select(i => i.MenuId).Distinct().ToList();
            var menus = await _context.Menus
                .Include(m => m.MenuCategory)
                .Where(m => menuIds.Contains(m.menuID))
                .ToDictionaryAsync(m => m.menuID);

            foreach (var line in lines)
            {
                if (!menus.TryGetValue(line.MenuId, out var menu))
                {
                    return await FailAsync(custId.Value, "An item in your cart no longer exists.");
                }
                if (!menu.isAvailable || menu.MenuCategory?.categoryActive == false)
                {
                    return await FailAsync(custId.Value, $"{menu.menuName} is not available right now.");
                }
            }

            // 3. Order type logic & optional reservation linking
            DiningTable? table = null;
            CustomerModel? customer = null;
            int? ownReservationId = null;

            if (dineIn)
            {
                if (TableId == null)
                {
                    return await FailAsync(custId.Value, "Please select your table number.");
                }

                table = await _context.DiningTables.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.tableID == TableId && t.tableStatus != "Maintenance");

                if (table == null)
                {
                    return await FailAsync(custId.Value, "The selected table is currently unavailable.");
                }

                var today = DateTime.Today;
                ownReservationId = await _context.Reservations.AsNoTracking()
                    .Where(r => r.custID == custId.Value && r.tableID == table.tableID
                             && r.reservationDateTime >= today && r.reservationDateTime < today.AddDays(1)
                             && r.reservationStatus != "Cancelled")
                    .Select(r => (int?)r.reservationID)
                    .FirstOrDefaultAsync();
            }
            else
            {
                if (PickupDateTime == null)
                {
                    return await FailAsync(custId.Value, "Please choose a pick-up time.");
                }

                string? pickupError = await CheckPickupTimeAsync(PickupDateTime.Value);
                if (pickupError != null)
                {
                    return await FailAsync(custId.Value, pickupError);
                }

                string phone = (ContactPhone ?? string.Empty).Trim();
                if (phone.Length < 10 || phone.Length > 11 || !phone.All(char.IsDigit))
                {
                    return await FailAsync(custId.Value, "Contact phone must be 10 to 11 digits (numbers only).");
                }

                customer = await _context.Customers.FindAsync(custId.Value);
                if (customer != null && customer.custPhone != phone)
                {
                    customer.custPhone = phone;
                }
            }

            // 4. Calculate subtotal, service charge (dine-in only), and SST
            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            decimal servicePct = settings?.serviceChargePct ?? 10m;
            decimal sstPct = settings?.sstPct ?? 6m;

            decimal subtotal = lines.Sum(l => menus[l.MenuId].menuPrice * l.Quantity);
            decimal service = dineIn
                ? Math.Round(subtotal * servicePct / 100m, 2, MidpointRounding.AwayFromZero)
                : 0m;
            decimal sst = Math.Round((subtotal + service) * sstPct / 100m, 2, MidpointRounding.AwayFromZero);

            // 5. Create Order & OrderItems
            var order = new Order
            {
                custID = custId.Value,
                tableID = table?.tableID,
                reservationID = ownReservationId,
                pickupDateTime = dineIn ? null : PickupDateTime,
                orderDateTime = DateTimeOffset.Now,
                orderStatus = "Order placed",
                totalAmount = subtotal + service + sst
            };

            foreach (var line in lines)
            {
                order.OrderItems.Add(new OrderItem
                {
                    menuID = line.MenuId,
                    quantity = line.Quantity,
                    unitPrice = menus[line.MenuId].menuPrice,
                    orderRemark = line.Note
                });
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            HttpContext.Session.ClearCart();

            return dineIn
                ? RedirectToPage("/Customer/Orders")
                : RedirectToPage("/Customer/Payment", new { orderId = order.orderID });
        }

        private int? CurrentCustomerId()
        {
            return HttpContext.Session.IsSignedInAs(UserRoles.Customer)
                ? HttpContext.Session.GetUserId()
                : null;
        }

        private async Task<int?> FindOwnReservationTableIdAsync(int custId)
        {
            var today = DateTime.Today;
            return await _context.Reservations.AsNoTracking()
                .Where(r => r.custID == custId
                         && r.reservationDateTime >= today && r.reservationDateTime < today.AddDays(1)
                         && r.reservationStatus != "Cancelled")
                .OrderBy(r => r.reservationDateTime)
                .Select(r => (int?)r.tableID)
                .FirstOrDefaultAsync();
        }

        private async Task LoadPageAsync(int custId)
        {
            await LoadCartAsync();

            var settings = await _context.SystemSettings.AsNoTracking().FirstOrDefaultAsync();
            ServiceChargePct = settings?.serviceChargePct ?? 10m;
            SstPct = settings?.sstPct ?? 6m;

            AvailableTables = await _context.DiningTables.AsNoTracking()
                .Include(t => t.DiningArea)
                .Where(t => t.tableStatus != "Maintenance")
                .OrderBy(t => t.areaID).ThenBy(t => t.tableNumber)
                .ToListAsync();

            CustomerPhone = await _context.Customers.AsNoTracking()
                .Where(c => c.custID == custId)
                .Select(c => c.custPhone)
                .FirstOrDefaultAsync();

            await LoadPickupOptionsAsync();
        }

        private async Task LoadCartAsync()
        {
            CartLines = new();
            RemovedNotices = new();

            var cart = HttpContext.Session.GetCart().Where(i => i.Quantity > 0).ToList();
            if (cart.Count == 0) return;

            var ids = cart.Select(i => i.MenuId).Distinct().ToList();
            var menus = await _context.Menus.AsNoTracking()
                .Include(m => m.MenuCategory)
                .Where(m => ids.Contains(m.menuID))
                .ToDictionaryAsync(m => m.menuID);

            var kept = new List<CartItem>();
            foreach (var line in cart)
            {
                if (!menus.TryGetValue(line.MenuId, out var menu)) continue;
                if (!menu.isAvailable || menu.MenuCategory?.categoryActive == false)
                {
                    RemovedNotices.Add($"{menu.menuName} is no longer available and was removed from your cart.");
                    continue;
                }

                kept.Add(line);
                CartLines.Add(new CartDisplayLine
                {
                    MenuId = menu.menuID,
                    Name = menu.menuName,
                    UnitPrice = menu.menuPrice,
                    Quantity = line.Quantity,
                    Note = line.Note,
                    ImageUrl = menu.menuImageUrl
                });
            }

            if (kept.Count != cart.Count) HttpContext.Session.SaveCart(kept);
        }

        private async Task LoadPickupOptionsAsync()
        {
            PickupOptions = new();

            var restaurant = await _context.Restaurants.AsNoTracking().OrderBy(r => r.restaurantID).FirstOrDefaultAsync();
            if (restaurant == null) return;

            var step = TimeSpan.FromMinutes(Math.Max(restaurant.slotIntervalMinutes, 15));
            var earliest = DateTime.Now.AddMinutes(PrepMinutes);

            for (int d = 0; d < 2 && PickupOptions.Count == 0; d++)
            {
                var day = DateTime.Today.AddDays(d);
                var hour = await _context.OperatingHours.AsNoTracking()
                    .FirstOrDefaultAsync(h => h.restaurantID == restaurant.restaurantID && h.dayOfWeek == day.DayOfWeek);
                if (hour == null || hour.isClosed) continue;

                for (var t = hour.openTime; t <= hour.closeTime; t += step)
                {
                    var slot = day + t;
                    if (slot < earliest) continue;

                    PickupOptions.Add(new PickupOption
                    {
                        Value = slot.ToString("yyyy-MM-dd'T'HH:mm"),
                        Label = (d == 0 ? "Today " : "Tomorrow ") + slot.ToString("HH:mm")
                    });
                }
            }
        }

        private async Task<IActionResult> FailAsync(int custId, string message)
        {
            ModelState.AddModelError(string.Empty, message);
            await LoadPageAsync(custId);
            return Page();
        }

        private async Task<string?> CheckPickupTimeAsync(DateTime pickup)
        {
            if (pickup < DateTime.Now.AddMinutes(PrepMinutes - 1))
            {
                return "That pick-up time is too soon or has already passed. Please choose a later time.";
            }

            var restaurant = await _context.Restaurants.AsNoTracking()
                .OrderBy(r => r.restaurantID)
                .FirstOrDefaultAsync();
            if (restaurant == null) return null;

            var hour = await _context.OperatingHours.AsNoTracking()
                .FirstOrDefaultAsync(h => h.restaurantID == restaurant.restaurantID && h.dayOfWeek == pickup.DayOfWeek);
            if (hour == null) return null;

            if (hour.isClosed || pickup.TimeOfDay < hour.openTime || pickup.TimeOfDay > hour.closeTime)
            {
                return "We are closed at that time. Please choose a pick-up time within opening hours.";
            }

            return null;
        }
    }
}