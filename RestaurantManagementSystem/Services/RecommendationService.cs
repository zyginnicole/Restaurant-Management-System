using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Chooses the dishes shown as "Popular" and "Recommended for you" on the customer menu.
    // Only dishes that are Available and in a Visible category are ever returned.
    public class RecommendationService
    {
        private const int RecentDays = 30;   // "popular" looks at the last 30 days first

        private readonly ApplicationDbContext _db;

        public RecommendationService(ApplicationDbContext db)
        {
            _db = db;
        }

        // =====================================================================
        //  POPULAR: the most ordered dishes
        // =====================================================================
        public async Task<List<Menu>> GetPopularAsync(int take = 3)
        {
            var dishes = await GetEligibleDishesAsync();
            return await PopularFromAsync(dishes, take);
        }

        // =====================================================================
        //  RECOMMENDED FOR ONE CUSTOMER
        //  custID = null (guest) or no order history -> popular dishes
        // =====================================================================
        public async Task<List<Menu>> GetRecommendedAsync(int? custID, int take = 3)
        {
            var dishes = await GetEligibleDishesAsync();
            var result = new List<Menu>();

            if (custID.HasValue)
            {
                // quantity of each dish this customer has ordered (cancelled orders ignored)
                var mine = await QuantityByDishAsync(o => true, custID);

                if (mine.Count > 0)
                {
                    var overall = await QuantityByDishAsync(o => true, null);

                    // how much this customer orders from each category
                    var ids = mine.Keys.ToList();
                    var ordered = await _db.Menus.AsNoTracking()
                        .Where(m => ids.Contains(m.menuID))
                        .Select(m => new { m.menuID, m.categoryID })
                        .ToListAsync();

                    var categoryWeight = ordered
                        .GroupBy(m => m.categoryID)
                        .ToDictionary(g => g.Key, g => g.Sum(m => mine[m.menuID]));

                    // dishes not tried yet: favourite categories first, then the most popular overall
                    result = dishes
                        .Where(d => !mine.ContainsKey(d.menuID))
                        .OrderByDescending(d => categoryWeight.GetValueOrDefault(d.categoryID))
                        .ThenByDescending(d => overall.GetValueOrDefault(d.menuID))
                        .ThenBy(d => d.menuName)
                        .Take(take)
                        .ToList();
                }
            }

            // not enough picks (guest, new customer, or they have tried most dishes): top up with popular dishes
            if (result.Count < take)
            {
                foreach (var dish in await PopularFromAsync(dishes, take + result.Count))
                {
                    if (result.Count >= take) break;
                    if (result.All(r => r.menuID != dish.menuID)) result.Add(dish);
                }
            }

            return result;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

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

        // Most ordered first: last 30 days, then all time, then (if there are no orders yet) menu order
        private async Task<List<Menu>> PopularFromAsync(List<Menu> dishes, int take)
        {
            var since = DateTimeOffset.Now.AddDays(-RecentDays);
            var recent = await QuantityByDishAsync(o => o.orderDateTime >= since, null);
            var allTime = await QuantityByDishAsync(o => true, null);

            var popular = dishes
                .Where(d => allTime.ContainsKey(d.menuID))
                .OrderByDescending(d => recent.GetValueOrDefault(d.menuID))
                .ThenByDescending(d => allTime[d.menuID])
                .ThenBy(d => d.menuName)
                .Take(take)
                .ToList();

            // no (or not enough) order history yet: fill with the available dishes in menu order
            foreach (var dish in dishes)
            {
                if (popular.Count >= take) break;
                if (popular.All(p => p.menuID != dish.menuID)) popular.Add(dish);
            }

            return popular;
        }

        // menuID -> total quantity ordered. Cancelled orders never count.
        // custID = null counts every customer.
        private async Task<Dictionary<int, int>> QuantityByDishAsync(Expression<Func<Order, bool>> filter, int? custID)
        {
            var orders = _db.Orders.AsNoTracking()
                .Where(o => o.orderStatus != "Cancelled")
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