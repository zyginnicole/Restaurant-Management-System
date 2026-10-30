using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Helpers
{
    // Is the restaurant open for orders right now? (Admin > Restaurant > Operating Hours)
    // Same rule as the Menu page (MenuModel.LoadOpeningHoursAsync): no hours saved yet = open.
    public static class RestaurantHours
    {
        public static async Task<bool> IsOpenAsync(ApplicationDbContext db)
        {
            var restaurant = await db.Restaurants.AsNoTracking().OrderBy(r => r.restaurantID).FirstOrDefaultAsync();
            if (restaurant == null) return true;

            var hours = await db.OperatingHours.AsNoTracking()
                .Where(h => h.restaurantID == restaurant.restaurantID)
                .ToListAsync();
            if (hours.Count == 0) return true;

            var now = DateTime.Now;
            var today = hours.FirstOrDefault(h => h.dayOfWeek == now.DayOfWeek);
            return today != null && !today.isClosed
                   && now.TimeOfDay >= today.openTime && now.TimeOfDay <= today.closeTime;
        }
    }
}
