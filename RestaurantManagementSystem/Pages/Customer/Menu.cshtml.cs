using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Customer
{
    public class MenuModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly RecommendationService _recommendations;

        public MenuModel(ApplicationDbContext context, RecommendationService recommendations)
        {
            _context = context;
            _recommendations = recommendations;
        }

        public List<Menu> MenuItems { get; set; } = new();
        public List<Promotion> Promotions { get; set; } = new();
        public List<Menu> RecommendedItems { get; set; } = new();
        public List<Menu> PopularItems { get; set; } = new();

        // Category tabs: only the categories the admin has set to Visible, in admin order
        public List<string> CategoryNames { get; set; } = new();

        // ---------- Cart (kept in the session, see Helpers/Cart.cs) ----------
        public int CartCount { get; set; }                                  // total quantity, for the header button
        public decimal CartSubtotal { get; set; }                           // from database prices
        public Dictionary<int, int> CartQuantities { get; set; } = new();   // menuID -> quantity already in the cart
        public Dictionary<string, int> DishIds { get; set; } = new();       // dish name -> menuID (menu.js keys cards by name)

        public async Task OnGetAsync()
        {
            var activeCategories = await _context.MenuCategories
                .Where(cat => cat.categoryActive)
                .OrderBy(cat => cat.categoryID)
                .Select(cat => new { cat.categoryID, cat.categoryName })
                .ToListAsync();

            CategoryNames = activeCategories.Select(cat => cat.categoryName).ToList();
            var activeIds = activeCategories.Select(cat => cat.categoryID).ToList();

            // Dishes in a Hidden category are not shown anywhere on this page
            MenuItems = await _context.Menus
                .Include(m => m.MenuCategory)
                .Where(m => activeIds.Contains(m.categoryID))
                .OrderBy(m => m.categoryID)
                .ThenBy(m => m.menuName)
                .ToListAsync();

            var today = DateTime.Today;
            Promotions = await _context.Promotions
                .Include(p => p.Menus.Where(m => activeIds.Contains(m.categoryID)))
                .Where(p => p.promoActive
                         && (p.validFrom == null || p.validFrom <= today)
                         && (p.validUntil == null || p.validUntil >= today))
                .ToListAsync();

            // Popular = most ordered dishes; Recommended = based on the signed-in customer's order history
            PopularItems = await _recommendations.GetPopularAsync();
            RecommendedItems = await _recommendations.GetRecommendedAsync(CurrentCustomerId());

            // Cart: header counter + the quantities to show on the cards
            var summary = await CartPricing.SummarizeAsync(_context, HttpContext.Session);
            CartCount = summary.Count;
            CartSubtotal = summary.Subtotal;
            CartQuantities = HttpContext.Session.GetCart().ToDictionary(i => i.MenuId, i => i.Quantity);

            foreach (var dish in MenuItems.Concat(RecommendedItems).Concat(PopularItems).Concat(Promotions.SelectMany(p => p.Menus)))
            {
                DishIds.TryAdd(dish.menuName, dish.menuID);
            }
        }

        // Called by menu.js (and Checkout) when a quantity changes: ?handler=SetQuantity
        // Sets the quantity of one dish in the session cart (0 removes it) and returns the new totals.
        // Only the dish ID and quantity come from the browser - the price is read from the database.
        public async Task<IActionResult> OnPostSetQuantityAsync(int menuId, int quantity, string? note = null)
        {
            quantity = Math.Clamp(quantity, 0, CartSessionExtensions.MaxQuantity);

            if (quantity > 0)
            {
                bool orderable = await CartPricing.Orderable(_context).AsNoTracking().AnyAsync(m => m.menuID == menuId);
                if (!orderable)
                {
                    return new JsonResult(new { ok = false, message = "Sorry, this dish is not available right now." }) { StatusCode = 400 };
                }
            }

            HttpContext.Session.SetCartQuantity(menuId, quantity, note);

            var summary = await CartPricing.SummarizeAsync(_context, HttpContext.Session);
            int saved = HttpContext.Session.GetCart().FirstOrDefault(i => i.MenuId == menuId)?.Quantity ?? 0;

            return new JsonResult(new { ok = true, quantity = saved, count = summary.Count, total = summary.Subtotal });
        }

        // The signed-in customer's ID, or null for a guest / staff / admin (they see popular dishes)
        private int? CurrentCustomerId()
        {
            return HttpContext.Session.IsSignedInAs(UserRoles.Customer)
                ? HttpContext.Session.GetUserId()
                : null;
        }
    }
}