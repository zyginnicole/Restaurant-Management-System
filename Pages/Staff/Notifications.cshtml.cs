using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;

namespace RestaurantManagementSystem.Pages.Staff;

// Displays administrator announcements intended for staff members.
public class NotificationsModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public List<StaffNotificationView> Items { get; private set; } = new();

    public NotificationsModel(ApplicationDbContext db)
    {
        _db = db;
    }

    // Loads announcements addressed to every user or specifically to staff.
    public async Task<IActionResult> OnGetAsync()
    {
        if (!HttpContext.Session.IsSignedInAs(UserRoles.Staff))
            return RedirectToPage("/Staff/Login");

        Items = await _db.Announcements.AsNoTracking()
            .Where(a => a.targetAudience == "All" || a.targetAudience == "Staff")
            .OrderByDescending(a => a.isPinned)
            .ThenByDescending(a => a.announcementDate)
            .Select(a => new StaffNotificationView
            {
                Title = a.announcementTitle,
                Message = a.announcementMessage,
                TimeText = a.announcementDate.ToLocalTime().ToString("dd MMM yyyy, HH:mm"),
                Pinned = a.isPinned
            })
            .ToListAsync();

        return Page();
    }

    // The staff inbox uses announcement rows because NotificationLogs belongs to customers.
    public class StaffNotificationView
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string TimeText { get; set; } = string.Empty;
        public bool Pinned { get; set; }
    }
}
