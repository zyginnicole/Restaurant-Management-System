using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;

namespace RestaurantManagementSystem.Pages.Customer
{
    public class NotificationsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public List<NotificationView> Items { get; private set; } = new();
        public int UnreadCount => Items.Count(x => x.Unread);

        public NotificationsModel(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> OnGetAsync()
        {
            var custId = CurrentCustomerId();
            if (custId == null) return RedirectToPage("/Customer/Login");

            var notificationRows = await _db.NotificationLogs.AsNoTracking()
                .Where(n => n.custID == custId.Value)
                .Select(n => new
                {
                    n.notificationID,
                    n.notificationCategory,
                    n.isRead,
                    n.notificationTitle,
                    n.notificationMessage,
                    n.notificationCreateAt,
                    n.linkUrl
                })
                .ToListAsync();

            var announcementRows = await _db.Announcements.AsNoTracking()
                .Where(a => a.targetAudience == "All" || a.targetAudience == "Customers")
                .Select(a => new
                {
                    a.announcementID,
                    a.announcementTitle,
                    a.announcementMessage,
                    a.announcementDate,
                    a.isPinned
                })
                .ToListAsync();

            // An admin announcement is stored in Announcements and is also broadcast into
            // NotificationLogs so each customer has read/unread state. Show the announcement
            // once, while using the matching log only for its read state and link.
            var announcementNotificationIds = new HashSet<int>();
            var announcements = new List<NotificationView>();

            foreach (var announcement in announcementRows)
            {
                var matchingLogs = notificationRows
                    .Where(n => n.notificationCategory == "General"
                                && n.notificationTitle == announcement.announcementTitle
                                && n.notificationMessage == announcement.announcementMessage)
                    .OrderByDescending(n => n.notificationCreateAt)
                    .ToList();

                foreach (var log in matchingLogs)
                    announcementNotificationIds.Add(log.notificationID);

                var matchingLog = matchingLogs.FirstOrDefault();
                announcements.Add(new NotificationView
                {
                    NotificationId = matchingLog?.notificationID,
                    Category = "Announcement",
                    DataCategory = "promotion",
                    Pinned = announcement.isPinned,
                    Unread = matchingLog != null && !matchingLog.isRead,
                    Title = announcement.announcementTitle,
                    Message = announcement.announcementMessage,
                    TimeText = FormatTime(announcement.announcementDate),
                    LinkUrl = matchingLog?.linkUrl ?? "/Customer/Notifications",
                    TimeSort = announcement.announcementDate
                });
            }

            var notifications = notificationRows
                .Where(n => !announcementNotificationIds.Contains(n.notificationID))
                .Select(n => new NotificationView
                {
                    NotificationId = n.notificationID,
                    Category = n.notificationCategory,
                    DataCategory = ToDataCategory(n.notificationCategory),
                    Unread = !n.isRead,
                    Title = n.notificationTitle,
                    Message = n.notificationMessage,
                    TimeText = FormatTime(n.notificationCreateAt),
                    LinkUrl = n.linkUrl,
                    TimeSort = n.notificationCreateAt
                });

            Items = notifications.Concat(announcements)
                .OrderByDescending(x => x.Pinned)
                .ThenByDescending(x => x.TimeSort)
                .ToList();

            return Page();
        }

        // Triggered via a form POST when a user clicks on a specific notification.
        public async Task<IActionResult> OnPostReadAsync(int notificationId)
        {
            var custId = CurrentCustomerId();
            if (custId == null) return Unauthorized();

            await _db.NotificationLogs
                .Where(n => n.notificationID == notificationId && n.custID == custId.Value && !n.isRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.isRead, true));

            return new EmptyResult();
        }

        // Triggered when a user clicks a "Mark All as Read" button.
        public async Task<IActionResult> OnPostReadAllAsync()
        {
            var custId = CurrentCustomerId();
            if (custId == null) return RedirectToPage("/Customer/Login");

            await _db.NotificationLogs.Where(n => n.custID == custId.Value && !n.isRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.isRead, true));
            return RedirectToPage();
        }

        private int? CurrentCustomerId() => HttpContext.Session.IsSignedInAs(UserRoles.Customer) ? HttpContext.Session.GetUserId() : null;

        private static string ToDataCategory(string? category) => category?.Trim().ToLowerInvariant() switch
        {
            "reservation" => "reservation",
            "order" or "payment" => "order",
            "promotion" or "announcement" => "promotion",
            _ => "account"
        };

        private static string FormatTime(DateTimeOffset date) => date.ToLocalTime().ToString("dd MMM yyyy, HH:mm");

        public class NotificationView
        {
            public int? NotificationId { get; set; }
            public string Category { get; set; } = "Notification";
            public string DataCategory { get; set; } = "account";
            public bool Unread { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string TimeText { get; set; } = string.Empty;
            public string? LinkUrl { get; set; }
            public string LinkText { get; set; } = "View";
            public DateTimeOffset TimeSort { get; set; }
            public bool Pinned { get; set; }
        }
    }
}
