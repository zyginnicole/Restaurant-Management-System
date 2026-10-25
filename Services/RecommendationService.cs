using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // The four lists the customer menu can show. An empty list = hide that section.
    public class MenuRecommendations
    {
        public List<Menu> OrderedBefore { get; set; } = new();       // dishes this customer has ordered
        public List<Menu> Signature { get; set; } = new();           // admin-ticked signature dishes
        public List<Menu> TopSellers { get; set; } = new();          // most ordered by everyone
        public List<Menu> ForYourPreference { get; set; } = new();   // dish tags match the customer's preferences

        // One list for the dashboard hero carousel, in the same order as the Menu page:
        // Order again -> Based on your preferences -> Popular -> Signature. No dish appears twice; at most `max` slides.
        public List<HeroSlide> ToHeroSlides(int max = 6)
        {
            var slides = new List<HeroSlide>();
            var seen = new HashSet<int>();

            void Add(IEnumerable<Menu> dishes, string label)
            {
                foreach (var d in dishes)
                {
                    if (slides.Count >= max) return;
                    if (!seen.Add(d.menuID)) continue;
                    slides.Add(new HeroSlide
                    {
                        Name = d.menuName,
                        Price = d.menuPrice,
                        Label = label,
                        Category = d.MenuCategory?.categoryName ?? "",
                        ImageUrl = d.menuImageUrl
                    });
                }
            }

            Add(OrderedBefore, "Order again");
            Add(ForYourPreference, "Based on your preferences");
            Add(TopSellers, "Popular selection");
            Add(Signature, "Signature dish");
            return slides;
        }
    }

    // One slide of the dashboard hero carousel
    public class HeroSlide
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public string Label { get; set; } = "";       // e.g. "Based on your preferences"
        public string Category { get; set; } = "";    // e.g. "Mains"
        public string? ImageUrl { get; set; }
    }

    // Every customer is signed in, so there is no guest path.
    //
    // Each section follows its own rule, so a dish can appear in more than one section.
    // Display order (empty list = section hidden):
    //   Order again -> Based on your preferences -> Popular dishes -> Signature Dishes
    //
    //   New customer, no preference      -> Popular (once there are Completed orders) + Signature
    //   New customer, has preference     -> Preferences + Popular + Signature
    //   Ordered before (any preference)  -> Order again + (Preferences if set) + Popular + Signature
    //
    // Allergies are a hard filter on every list.
    public class RecommendationService
    {
        private const int RecentDays = 30;   // top sellers look at the last 30 days first

        private readonly ApplicationDbContext _db;

        public RecommendationService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<MenuRecommendations> GetForCustomerAsync(int custID, int take = 3)
        {
            var customer = await _db.Customers.AsNoTracking()
                .Where(c => c.custID == custID)
                .Select(c => new { c.DietaryPref, c.Allergies })
                .FirstOrDefaultAsync();

            var prefs = MenuVocabulary.Parse(customer?.DietaryPref);
            var allergies = MenuVocabulary.Parse(customer?.Allergies);

            // Available + visible category + safe for this customer's allergies
            var dishes = (await GetEligibleDishesAsync())
                .Where(d => IsSafe(d, allergies))
                .ToList();

            var mine = await QuantityByDishAsync(o => true, custID);        // this customer's orders
            var overall = await QuantityByDishAsync(o => true, null);       // everyone's orders
            var since = DateTimeOffset.Now.AddDays(-RecentDays);
            var recent = await QuantityByDishAsync(o => o.orderDateTime >= since, null);

            var result = new MenuRecommendations();

            // 1. Ordered before: most ordered by this customer first
            result.OrderedBefore = dishes
                .Where(d => mine.ContainsKey(d.menuID))
                .OrderByDescending(d => mine[d.menuID])
                .ThenBy(d => d.menuName)
                .Take(take)
                .ToList();

            // 2. Preference: dishes whose tags match, most matching tags first
            //    (skips dishes the customer already ordered - those are in "Order again")
            if (prefs.Count > 0)
            {
                result.ForYourPreference = dishes
                    .Where(d => !mine.ContainsKey(d.menuID))
                    .Select(d => new { Dish = d, Score = MenuVocabulary.Parse(d.menuTags).Count(t => prefs.Contains(t)) })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => overall.GetValueOrDefault(x.Dish.menuID))
                    .ThenBy(x => x.Dish.menuName)
                    .Take(take)
                    .Select(x => x.Dish)
                    .ToList();
            }

            // 3. Popular: last 30 days first, then all time (Completed orders only).
            //    Dishes nobody has ordered are not popular, so with 0 Completed orders this list is empty and the section is hidden.
            //    Always the real top sellers: a dish may also appear in another section.
            result.TopSellers = dishes
                .Where(d => overall.ContainsKey(d.menuID))
                .OrderByDescending(d => recent.GetValueOrDefault(d.menuID))
                .ThenByDescending(d => overall[d.menuID])
                .ThenBy(d => d.menuName)
                .Take(take)
                .ToList();

            // 4. Signature: always the admin's own picks
            result.Signature = dishes
                .Where(d => d.isSignature)
                .OrderBy(d => d.menuName)
                .Take(take)
                .ToList();

            return result;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        // A dish is safe if it lists none of the customer's allergens.
        // If the customer has allergies and the dish's allergens were never filled in, we treat it as NOT safe.
        private static bool IsSafe(Menu dish, HashSet<string> allergies)
        {
            if (allergies.Count == 0) return true;
            if (string.IsNullOrWhiteSpace(dish.menuAllergens)) return false;
            return !MenuVocabulary.Parse(dish.menuAllergens).Overlaps(allergies);
        }

        // Dishes that can be shown: Available, and in a category the admin has set to Visible
        private async Task<List<Menu>> GetEligibleDishesAsync()
        {
            return await _db.Menus.AsNoTracking()
                .Include(m => m.MenuCategory)
                .Where(m => m.isAvailable && m.MenuCategory!.categoryActive)
                .OrderBy(m => m.categoryID)
                .ThenBy(m => m.menuName)
                .ToListAsync();
        }

        // menuID -> total quantity ordered. Only Completed orders count.
        // custID = null counts every customer.
        private async Task<Dictionary<int, int>> QuantityByDishAsync(Expression<Func<Order, bool>> filter, int? custID)
        {
            var orders = _db.Orders.AsNoTracking()
                .Where(o => o.orderStatus == "Completed")
                .Where(filter);

            if (custID.HasValue)
                orders = orders.Where(o => o.custID == custID.Value);

            return await orders
                .SelectMany(o => o.OrderItems)
                .GroupBy(oi => oi.menuID)
                .Select(g => new { menuID = g.Key, qty = g.Sum(oi => oi.quantity) })
                .ToDictionaryAsync(x => x.menuID, x => x.qty);
        }
    }
}