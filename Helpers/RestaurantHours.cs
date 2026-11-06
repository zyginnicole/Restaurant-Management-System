using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Helpers
{
    // Is the restaurant taking orders right now? (Admin > Restaurant > Operating Hours)
    // Orders are taken from opening time until the LAST CALL (closing time minus Restaurant.lastCallMinutes).
    // Same rule as the Menu page (MenuModel.LoadOpeningHoursAsync): no hours saved yet = open.
    public static class RestaurantHours
    {
        // Choices offered on the admin page (minutes before closing)
        public static readonly int[] LastCallChoices = { 0, 15, 30, 45, 60 };

        // Last time an order can be placed on a day: closing time minus the last call minutes,
        // never earlier than opening time.
        public static TimeSpan LastCall(OperatingHour hour, int lastCallMinutes)
        {
            var last = hour.closeTime - TimeSpan.FromMinutes(lastCallMinutes);
            return last < hour.openTime ? hour.openTime : last;
        }

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
                   && now.TimeOfDay >= today.openTime
                   && now.TimeOfDay <= LastCall(today, restaurant.lastCallMinutes);
        }
    }
}
