using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services;

/// Handles in-system notifications only.
/// No email or SMTP logic belongs in this service.
public class NotificationService
{
    private readonly ApplicationDbContext _db;

    public NotificationService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// Sends one system notification to one customer.
    public async Task SendAsync(
        int customerId,
        string category,
        string title,
        string message,
        string? linkUrl = null)
    {
        var customerExists = await _db.Customers
            .AnyAsync(c => c.custID == customerId);

        if (!customerExists)
        {
            return;
        }

        var notification = new Notification
        {
            custID = customerId,
            notificationCategory = Limit(category, 30),
            notificationTitle = Limit(title, 100),
            notificationMessage = Limit(message, 1000),
            linkUrl = LimitNullable(linkUrl, 200),
            isRead = false,
            notificationCreateAt = DateTimeOffset.Now
        };

        _db.NotificationLogs.Add(notification);
        await _db.SaveChangesAsync();
    }

    /// Sends the same system notification to every customer.
    /// Used by administrators for announcements or promotions.
    public async Task BroadcastToCustomersAsync(
        string category,
        string title,
        string message,
        string? linkUrl = null)
    {
        var customerIds = await _db.Customers
            .Select(c => c.custID)
            .ToListAsync();

        var notifications = customerIds.Select(customerId => new Notification
        {
            custID = customerId,
            notificationCategory = Limit(category, 30),
            notificationTitle = Limit(title, 100),
            notificationMessage = Limit(message, 1000),
            linkUrl = LimitNullable(linkUrl, 200),
            isRead = false,
            notificationCreateAt = DateTimeOffset.Now
        });

        _db.NotificationLogs.AddRange(notifications);
        await _db.SaveChangesAsync();
    }

    /// Returns the number of unread notifications for one customer.
    public Task<int> UnreadCountAsync(int customerId)
    {
        return _db.NotificationLogs
            .CountAsync(n =>
                n.custID == customerId &&
                !n.isRead);
    }

    /// Marks one notification as read.
    /// The customer can only update their own notification.
    public async Task MarkReadAsync(
        int customerId,
        int notificationId)
    {
        var notification = await _db.NotificationLogs
            .FirstOrDefaultAsync(n =>
                n.notificationID == notificationId &&
                n.custID == customerId);

        if (notification == null)
        {
            return;
        }

        notification.isRead = true;
        await _db.SaveChangesAsync();
    }

    /// Marks all notifications belonging to one customer as read.
    public async Task MarkAllReadAsync(int customerId)
    {
        var notifications = await _db.NotificationLogs
            .Where(n =>
                n.custID == customerId &&
                !n.isRead)
            .ToListAsync();

        if (notifications.Count == 0)
        {
            return;
        }

        foreach (var notification in notifications)
        {
            notification.isRead = true;
        }

        await _db.SaveChangesAsync();
    }


    /// Sends an administrator-created announcement as an in-system
    /// notification to all customers.
    public Task SendAdminAnnouncementAsync(
        string title,
        string message,
        string? linkUrl = "/Customer/Notifications")
    {
        return BroadcastToCustomersAsync(
            category: "General",
            title: title,
            message: message,
            linkUrl: linkUrl);
    }

    private static string Limit(string value, int maxLength)
    {
        value = value?.Trim() ?? string.Empty;

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }

    private static string? LimitNullable(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        return value.Length <= maxLength
            ? value
            : value[..maxLength];
    }
}