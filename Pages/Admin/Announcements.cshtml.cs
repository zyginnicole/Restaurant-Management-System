using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Announcements: short notices for customers and/or staff (closures, opening hours, briefings).
    // Promotions are managed on Menu & Offers, not here.
    //  - OnGet: Announcements, pinned first, then newest first (announcementDate).
    //    Audience labels show how many people each reaches: customers, active staff, or both.
    //  - Fields: announcementTitle (max 100), announcementMessage (max 500),
    //    targetAudience "All" / "Customers" / "Staff", isPinned. adminID = signed-in admin.
    //  - Post: announcementDate = now (no scheduling); editing keeps the original date.
    //    Customers see "All" + "Customers"; staff see "All" + "Staff".
    //  - Every action redirects back with TempData["AdminMessage"] (+ AdminMessageIsError when it was refused).
    public class AnnouncementsModel : PageModel
    {
        public const int MaxTitle = 100;
        public const int MaxMessage = 500;
        public static readonly string[] AudienceValues = { "All", "Customers", "Staff" };

        private readonly ApplicationDbContext _db;
        private readonly NotificationService _notifications;

        public AnnouncementsModel(ApplicationDbContext db, NotificationService notifications)
        {
            _db = db;
            _notifications = notifications;
        }

        public List<Announcement> Items { get; set; } = new();
        public List<(string Value, string Label, int Count)> Audiences { get; set; } = new();

        public async Task OnGetAsync()
        {
            Items = await _db.Announcements.AsNoTracking()
                .OrderByDescending(a => a.isPinned)
                .ThenByDescending(a => a.announcementDate)
                .ToListAsync();

            int customers = await _db.Customers.CountAsync();
            int staff = await _db.StaffMembers.CountAsync(s => s.staffActive);
            Audiences = new()
            {
                ("All", "Everyone", customers + staff),
                ("Customers", "Customers", customers),
                ("Staff", "Staff", staff)
            };
        }

        // Compose form. announcementId empty = new (announcementDate = now), otherwise update that announcement.
        public async Task<IActionResult> OnPostSaveAsync(int? announcementId, string? announcementTitle,
            string? announcementMessage, string? targetAudience, bool isPinned)
        {
            var title = announcementTitle?.Trim() ?? "";
            var message = announcementMessage?.Trim() ?? "";

            if (title.Length == 0) return Fail("Please enter a title.");
            if (title.Length > MaxTitle) return Fail($"The title can be at most {MaxTitle} characters.");
            if (message.Length == 0) return Fail("Please enter a message.");
            if (message.Length > MaxMessage) return Fail($"The message can be at most {MaxMessage} characters.");
            if (!AudienceValues.Contains(targetAudience)) return Fail("Please choose who should see it.");

            Announcement? item;
            if (announcementId == null)
            {
                var adminId = HttpContext.Session.GetUserId();
                if (adminId == null || !await _db.Admins.AnyAsync(a => a.adminID == adminId))
                    return Fail("Please sign in again as an administrator.");

                item = new Announcement { adminID = adminId.Value, announcementDate = DateTimeOffset.Now };
                _db.Announcements.Add(item);
            }
            else
            {
                item = await _db.Announcements.FindAsync(announcementId.Value);
                if (item == null) return Fail("That announcement no longer exists.");
            }

            item.announcementTitle = title;
            item.announcementMessage = message;
            item.targetAudience = targetAudience!;
            item.isPinned = isPinned;
            await _db.SaveChangesAsync();

            if (announcementId == null && (item.targetAudience == "All" || item.targetAudience == "Customers"))
            {
                await _notifications.SendAdminAnnouncementAsync(
                    item.announcementTitle,
                    item.announcementMessage,
                    "/Customer/Notifications");
            }

            TempData["AdminMessage"] = announcementId == null ? $"\"{title}\" posted." : $"\"{title}\" updated.";
            return RedirectToPage();
        }

        // Pin / Unpin: isPinned = pin (pinned announcements stay at the top for customers / staff)
        public async Task<IActionResult> OnPostPinAsync(int announcementId, bool pin)
        {
            var item = await _db.Announcements.FindAsync(announcementId);
            if (item == null) return Fail("That announcement no longer exists.");

            item.isPinned = pin;
            await _db.SaveChangesAsync();

            TempData["AdminMessage"] = pin ? $"\"{item.announcementTitle}\" pinned." : $"\"{item.announcementTitle}\" unpinned.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int announcementId)
        {
            var item = await _db.Announcements.FindAsync(announcementId);
            if (item == null) return Fail("That announcement no longer exists.");

            _db.Announcements.Remove(item);
            await _db.SaveChangesAsync();

            TempData["AdminMessage"] = $"\"{item.announcementTitle}\" deleted.";
            return RedirectToPage();
        }

        private IActionResult Fail(string message)
        {
            TempData["AdminMessage"] = message;
            TempData["AdminMessageIsError"] = true;
            return RedirectToPage();
        }
    }
}
