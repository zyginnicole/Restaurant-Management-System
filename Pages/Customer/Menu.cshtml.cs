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

        // Ordered before / Signature / Top sellers / For your preference (empty list = hide that section)
        public MenuRecommendations Recs { get; set; } = new();

        // Category tabs: only the categories the admin has set to Visible, in admin order
        public List<string> CategoryNames { get; set; } = new();

        // The signed-in customer's allergies (empty = none). Used by the menu cards and the "hide dishes with my allergens" switch.
        public List<string> CustomerAllergies { get; set; } = new();

        // promoID -> why it can't be used right now (wrong day / time, birthday month...). Promotions not listed here are usable now.
        public Dictionary<int, string> PromoUnavailable { get; set; } = new();

        // ---------- Cart (kept in the session, see Helpers/Cart.cs) ----------
        public int CartCount { get; set; }                                  // total quantity, for the header button
        public decimal CartSubtotal { get; set; }                           // from database prices
        public Dictionary<int, int> CartQuantities { get; set; } = new();   // menuID -> quantity already in the cart
        public Dictionary<string, int> DishIds { get; set; } = new();       // dish name -> menuID (menu.js keys cards by name)

        public async Task<IActionResult> OnGetAsync()
        {
            // Every customer must sign in. TODO: change to your real login page path.
            if (CurrentCustomerId() is not int custId)
            {
                return RedirectToPage("/Customer/Login");
            }

            var customerInfo = await _context.Customers.AsNoTracking()
                .Where(c => c.custID == custId)
                .Select(c => new { c.Allergies, c.custBirthday })
                .FirstOrDefaultAsync();
            CustomerAllergies = MenuVocabulary.Parse(customerInfo?.Allergies).ToList();

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

            // Which promotions can be used right now (day, time of day, birthday month)
            var now = DateTime.Now;
            foreach (var promo in Promotions)
            {
                var why = PromotionRules.NotAvailableReason(promo, now, customerInfo?.custBirthday);
                if (why != null) PromoUnavailable[promo.promoID] = why;
            }

            Recs = await _recommendations.GetForCustomerAsync(custId);

            // Cart: header counter + the quantities to show on the cards
            var summary = await CartPricing.SummarizeAsync(_context, HttpContext.Session);
            CartCount = summary.Count;
            CartSubtotal = summary.Subtotal;
            CartQuantities = HttpContext.Session.GetCart().ToDictionary(i => i.MenuId, i => i.Quantity);

            var allShown = MenuItems
                .Concat(Recs.OrderedBefore)
                .Concat(Recs.Signature)
                .Concat(Recs.TopSellers)
                .Concat(Recs.ForYourPreference)
                .Concat(Promotions.SelectMany(p => p.Menus));

            foreach (var dish in allShown)
            {
                DishIds.TryAdd(dish.menuName, dish.menuID);
            }

            return Page();
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

        // The signed-in customer's ID, or null if not signed in as a customer
        private int? CurrentCustomerId()
        {
            return HttpContext.Session.IsSignedInAs(UserRoles.Customer)
                ? HttpContext.Session.GetUserId()
                : null;
        }
    }
}