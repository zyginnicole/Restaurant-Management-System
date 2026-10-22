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