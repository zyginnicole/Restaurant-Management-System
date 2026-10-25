using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Data
{
    // Slots are NOT stored. They are worked out from the day's OperatingHour,
    // the slot interval and the last-seating setting on Restaurant.
    public static class SlotHelper
    {
        // Start time of every slot on one day, e.g. 12:00, 12:30, 13:00 ...
        public static List<TimeSpan> Generate(OperatingHour? hour, int intervalMinutes, int lastSeatingMinutes)
        {
            var times = new List<TimeSpan>();
            if (hour == null || hour.isClosed || hour.closeTime <= hour.openTime) return times;

            var step = TimeSpan.FromMinutes(Math.Max(intervalMinutes, 5));
            var last = hour.closeTime - TimeSpan.FromMinutes(lastSeatingMinutes);   // last bookable start time

            for (var t = hour.openTime; t <= last; t += step)
                times.Add(t);

            return times;
        }

        // FOR THE CUSTOMER BOOKING PAGE:
        // slots a customer can still book on this date (open, in the future, not blocked by admin)
        public static async Task<List<DateTime>> GetBookableSlotsAsync(ApplicationDbContext db, DateTime date)
        {
            var restaurant = await db.Restaurants.AsNoTracking().OrderBy(r => r.restaurantID).FirstOrDefaultAsync();
            if (restaurant == null) return new List<DateTime>();

            var day = date.Date;
            var hour = await db.OperatingHours.AsNoTracking()
                .FirstOrDefaultAsync(h => h.restaurantID == restaurant.restaurantID && h.dayOfWeek == day.DayOfWeek);

            var blocked = await db.BlockedSlots.AsNoTracking()
                .Where(b => b.restaurantID == restaurant.restaurantID && b.slotStart >= day && b.slotStart < day.AddDays(1))
                .Select(b => b.slotStart)
                .ToListAsync();

            return Generate(hour, restaurant.slotIntervalMinutes, restaurant.lastSeatingMinutes)
                .Select(t => day + t)
                .Where(s => s > DateTime.Now && !blocked.Contains(s))
                .ToList();
        }
    }
}