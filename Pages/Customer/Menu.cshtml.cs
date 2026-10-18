using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Pages.Customer
{
    public class MenuModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public MenuModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Menu> MenuItems { get; set; } = new();
        public List<Promotion> Promotions { get; set; } = new();
        public List<Menu> RecommendedItems { get; set; } = new();
        public List<Menu> PopularItems { get; set; } = new();

        public async Task OnGetAsync()
        {
            MenuItems = await _context.Menus
                .Include(m => m.MenuCategory)
                .OrderBy(m => m.categoryID)
                .ThenBy(m => m.menuName)
                .ToListAsync();

            var today = DateTime.Today;
            Promotions = await _context.Promotions
                .Include(p => p.Menus)
                .Where(p => p.promoActive
                         && (p.validFrom == null || p.validFrom <= today)
                         && (p.validUntil == null || p.validUntil >= today))
                .ToListAsync();

            // Popular = most ordered dishes; falls back to available dishes until orders exist
            var topIds = await _context.OrderItems
                .GroupBy(oi => oi.menuID)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Take(3)
                .ToListAsync();

            PopularItems = MenuItems.Where(m => m.isAvailable && topIds.Contains(m.menuID)).ToList();

            if (PopularItems.Count < 3)
            {
                var filler = MenuItems
                    .Where(m => m.isAvailable && !PopularItems.Contains(m))
                    .Take(3 - PopularItems.Count)
                    .ToList();
                PopularItems.AddRange(filler);
            }

            // Placeholder until personalised recommendations are built (needs the logged-in customer's order history)
            RecommendedItems = MenuItems
                .Where(m => m.isAvailable && m.MenuCategory?.categoryName == "Mains")
                .Take(3)
                .ToList();
        }
    }
}