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

            var notifications = notificationRows.Select(n => new NotificationView
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

            var announcementRows = await _db.Announcements.AsNoTracking()
                .Where(a => a.targetAudience == "All" || a.targetAudience == "Customers")
                .Select(a => new
                {
                    a.announcementTitle,
                    a.announcementMessage,
                    a.announcementDate
                })
                .ToListAsync();

            var announcements = announcementRows.Select(a => new NotificationView
            {
                Category = "Announcement",
                DataCategory = "promotion",
                Pinned = true,
                Title = a.announcementTitle,
                Message = a.announcementMessage,
                TimeText = FormatTime(a.announcementDate),
                TimeSort = a.announcementDate
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
