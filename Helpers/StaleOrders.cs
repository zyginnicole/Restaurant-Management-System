using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;

namespace RestaurantManagementSystem.Helpers
{
    // Orders are only worked on the day they are placed. An order that is still "Order placed", "Preparing" or
    // "Served" when that day has passed was never completed, so it is cancelled (a paid one is refunded, like a
    // normal cancellation). Called when a staff page loads, so no background job is needed.
    //   await StaleOrders.CancelPreviousDaysAsync(_context);
    public static class StaleOrders
    {
        private static readonly string[] OpenStatuses = { "Order placed", "Preparing", "Served" };

        // Returns how many orders were cancelled
        public static async Task<int> CancelPreviousDaysAsync(ApplicationDbContext db)
        {
            var startOfToday = new DateTimeOffset(DateTime.Today);      // local midnight
            var open = OpenStatuses;

            var stale = await db.Orders
                .Include(o => o.Payment)
                .Where(o => open.Contains(o.orderStatus) && o.orderDateTime < startOfToday)
                .ToListAsync();
            if (stale.Count == 0) return 0;

            foreach (var order in stale)
            {
                order.orderStatus = "Cancelled";
                if (order.Payment != null && order.Payment.paymentStatus == "Paid")
                {
                    order.Payment.paymentStatus = "Refunded";
                }
            }

            await db.SaveChangesAsync();
            return stale.Count;
        }
    }
}
