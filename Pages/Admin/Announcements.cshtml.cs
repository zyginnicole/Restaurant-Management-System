using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Announcements: short notices for customers and/or staff (closures, opening hours, briefings).
    // Promotions are managed on Menu & Offers, not here.
    // Static UI for now – backend notes (Announcement table only, no new fields):
    //  - OnGet: Announcements (+ Admin), pinned first, then newest first (announcementDate).
    //  - Fields: announcementTitle (max 100), announcementMessage (max 500),
    //    targetAudience "All" / "Customers" / "Staff", isPinned. adminID = signed-in admin.
    //  - Post: announcementDate = now (no scheduling). Customers see "All" + "Customers"; staff see "All" + "Staff".
    //  - Every action redirects back with TempData["AdminMessage"].
    public class AnnouncementsModel : PageModel
    {
        public void OnGet()
        {
        }

        // ---- UI flow only (no saving yet) – backend: save first, then redirect ----

        // Compose form. announcementId empty = new (announcementDate = now), otherwise update that announcement.
        // Fields: announcementId, announcementTitle, announcementMessage, targetAudience, isPinned.
        public IActionResult OnPostSave(int? announcementId, string announcementTitle)
        {
            TempData["AdminMessage"] = announcementId == null
                ? $"\"{announcementTitle}\" posted."
                : $"\"{announcementTitle}\" updated.";
            return RedirectToPage();
        }

        // Pin / Unpin: isPinned = pin (pinned announcements stay at the top for customers / staff)
        public IActionResult OnPostPin(int announcementId, bool pin)
        {
            TempData["AdminMessage"] = pin ? "Announcement pinned." : "Announcement unpinned.";
            return RedirectToPage();
        }

        public IActionResult OnPostDelete(int announcementId)
        {
            TempData["AdminMessage"] = "Announcement deleted.";
            return RedirectToPage();
        }
    }
}
