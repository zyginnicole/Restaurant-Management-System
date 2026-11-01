using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Helpers;

public static class StaleOrders
{
    private static readonly string[] OpenStatuses =
        { "Order placed", "Preparing", "Served" };

    public static async Task CancelStaleAsync(
        ApplicationDbContext db,
        NotificationService notifications)
    {
        var cutoff = MalaysiaClock.StartOf(MalaysiaClock.Today);

        var candidateIds = await db.Orders
            .AsNoTracking()
            .Where(o => o.orderDateTime < cutoff && OpenStatuses.Contains(o.orderStatus))
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
            var message = $"{code} was not completed on the day it was placed, so it has been cancelled automatically.";
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

    private sealed record CancelledOrder(
        int OrderId,
        int CustomerId,
        string? CustomerPassword,
        string? CustomerEmail,
        bool WasRefunded,
        decimal RefundAmount,
        int? TableId);
}
