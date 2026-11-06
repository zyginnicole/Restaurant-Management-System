using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Helpers;

public static class StaleOrders
{
    private static readonly string[] OpenStatuses =
        { "Order placed", "Preparing", "Served" };

    // An open order is stale when it was placed before TODAY'S OPENING TIME (Admin > Restaurant > Operating Hours):
    // it belongs to an earlier service and can no longer be completed, so it is cancelled.
    // A takeaway order whose pick-up time is still ahead of the opening time is kept (pre-order for today's service).
    public static async Task CancelStaleAsync(
        ApplicationDbContext db,
        NotificationService notifications)
    {
        var cutoffLocal = await ServiceStartAsync(db);
        var cutoff = MalaysiaClock.StartOf(cutoffLocal.Date) + cutoffLocal.TimeOfDay;

        var candidateIds = await db.Orders
            .AsNoTracking()
            .Where(o => o.orderDateTime < cutoff
                     && (o.pickupDateTime == null || o.pickupDateTime < cutoffLocal)
                     && OpenStatuses.Contains(o.orderStatus))
            .Select(o => o.orderID)
            .ToListAsync();

        var cancelled = new List<CancelledOrder>();

        foreach (var orderId in candidateIds)
        {
            // Claim the order atomically so concurrent staff pages/background runs
            // cannot cancel or notify the same order twice.
            var claimed = await db.Orders
                .Where(o => o.orderID == orderId
                         && o.orderDateTime < cutoff
                         && (o.pickupDateTime == null || o.pickupDateTime < cutoffLocal)
                         && OpenStatuses.Contains(o.orderStatus))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(o => o.orderStatus, "Cancelled"));

            if (claimed != 1)
                continue;

            var order = await db.Orders
                .Include(o => o.Payment)
                .Include(o => o.Customer)
                .SingleAsync(o => o.orderID == orderId);

            // ExecuteUpdate bypasses tracked entities, so keep the tracker aligned
            // before the single SaveChangesAsync below.
            order.orderStatus = "Cancelled";

            bool wasRefunded = order.Payment?.paymentStatus == "Paid";
            decimal refundAmount = order.Payment?.paymentAmount ?? 0m;
            if (wasRefunded)
                order.Payment!.paymentStatus = "Refunded";

            cancelled.Add(new CancelledOrder(
                order.orderID,
                order.custID,
                order.Customer?.custPassword,
                order.Customer?.custEmail,
                wasRefunded,
                refundAmount,
                order.tableID));
        }

        // Release a table only after every stale order in this run has been claimed.
        // Cancelled orders are no longer open, while any other open order keeps the
        // table occupied. Seated reservations are never changed here.
        foreach (var tableId in cancelled
                     .Where(x => x.TableId.HasValue)
                     .Select(x => x.TableId!.Value)
                     .Distinct())
        {
            var table = await db.DiningTables
                .SingleOrDefaultAsync(t => t.tableID == tableId);

            if (table == null || table.tableStatus != "Occupied")
                continue;

            bool hasOpenOrder = await db.Orders.AnyAsync(o =>
                o.tableID == tableId && OpenStatuses.Contains(o.orderStatus));
            if (hasOpenOrder)
                continue;

            bool hasSeatedReservation = await db.Reservations.AnyAsync(r =>
                r.tableID == tableId
                && r.reservationStatus == "Seated"
                && r.reservationDateTime >= MalaysiaClock.Today);
            if (!hasSeatedReservation)
                table.tableStatus = "Available";
        }

        if (cancelled.Count == 0)
            return;

        await db.SaveChangesAsync();

        foreach (var order in cancelled)
        {
            // Walk-in/guest profiles cannot sign in and must not receive notifications.
            if (string.IsNullOrEmpty(order.CustomerPassword)
                || (order.CustomerEmail?.EndsWith("@guest.local", StringComparison.OrdinalIgnoreCase) ?? false))
                continue;

            var code = $"ORD-{order.OrderId:0000}";
            var message = $"{code} was not completed during the service it was placed in, so it has been cancelled automatically.";
            if (order.WasRefunded)
                message += $" Your payment of RM {order.RefundAmount:N2} will be refunded within 3–5 working days.";

            await notifications.SendAsync(
                order.CustomerId,
                "Order",
                "Order cancelled",
                message,
                "/Customer/History");
        }
    }

    // When the CURRENT service started. Never a time in the future:
    //  - before today's opening time, the current service is still the last one (the previous open day),
    //    so orders staff create before opening are not cancelled straight away
    //  - closing after midnight (e.g. 18:00–02:00): until that closing time we are still inside yesterday's service
    //  - closed today, or no hours set up yet: the start of the day
    private static async Task<DateTime> ServiceStartAsync(ApplicationDbContext db)
    {
        var now = MalaysiaClock.Now;
        var start = await TodayServiceStartAsync(db);
        if (start <= now) return start;

        // Today's opening time hasn't come yet: go back to the last day the restaurant was open
        var restaurant = await db.Restaurants.AsNoTracking().OrderBy(r => r.restaurantID).FirstOrDefaultAsync();
        if (restaurant != null)
        {
            var hours = await db.OperatingHours.AsNoTracking()
                .Where(h => h.restaurantID == restaurant.restaurantID)
                .ToListAsync();

            for (int i = 1; i <= 7; i++)
            {
                var day = MalaysiaClock.Today.AddDays(-i);
                var h = hours.FirstOrDefault(x => x.dayOfWeek == day.DayOfWeek);
                if (h != null && !h.isClosed)
                    return day + h.openTime;
            }
        }

        // No open day found: fall back to the start of today (always in the past)
        return MalaysiaClock.Today;
    }

    // Today's opening time (or yesterday's, inside an after-midnight service). Can be later than now.
    private static async Task<DateTime> TodayServiceStartAsync(ApplicationDbContext db)
    {
        var now = MalaysiaClock.Now;
        var today = MalaysiaClock.Today;

        var restaurant = await db.Restaurants.AsNoTracking().OrderBy(r => r.restaurantID).FirstOrDefaultAsync();
        if (restaurant == null) return today;

        var hours = await db.OperatingHours.AsNoTracking()
            .Where(h => h.restaurantID == restaurant.restaurantID)
            .ToListAsync();

        var yesterday = today.AddDays(-1);
        var y = hours.FirstOrDefault(h => h.dayOfWeek == yesterday.DayOfWeek);
        if (y != null && !y.isClosed && y.closeTime < y.openTime && now.TimeOfDay < y.closeTime)
            return yesterday + y.openTime;

        var t = hours.FirstOrDefault(h => h.dayOfWeek == today.DayOfWeek);
        if (t != null && !t.isClosed)
            return today + t.openTime;

        return today;
    }

    private sealed record CancelledOrder(
        int OrderId,
        int CustomerId,
        string? CustomerPassword,
        string? CustomerEmail,
        bool WasRefunded,
        decimal RefundAmount,
        int? TableId);
}