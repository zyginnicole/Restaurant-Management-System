using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Notifications: messages about the customer's reservations, orders, payments and promotions.
    // Static UI for now – backend notes:
    //  - Data: Notification (one customer each: notificationCategory, notificationTitle, notificationMessage,
    //    linkUrl, isRead, notificationCreateAt) + Announcement rows for everyone (targetAudience "All" / "Customers").
    //  - OnGet: load the customer's notifications (custID from Session) and the customer announcements, newest first.
    //  - Each row needs: class "unread" when isRead = false, data-category for the filter tabs, and the
    //    data-* values read by notifications.js (data-title, data-message, data-time, data-link = linkUrl).
    //      notificationCategory "Reservation"       → data-category "reservation"  (Reservations tab)
    //      notificationCategory "Order" / "Payment" → data-category "order"        (Orders tab)
    //      Announcement                             → data-category "promotion"    (Promotions tab, never unread)
    //      notificationCategory "General"           → data-category "account"      (All tab only)
    //    data-link-text has no field – choose it from the category, e.g. "View reservation", "Track order".
    //  - OnPostRead (field: notificationId) – mark one as read when it is opened.
    //  - OnPostReadAll – mark all of this customer's notifications as read.
    //  - The unread count is also shown in the sidebar and the bell (_CustomerLayout).
    public class NotificationsModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: set isRead = true for all, then redirect.
        public IActionResult OnPostReadAll()
        {
            return RedirectToPage();
        }
    }
}
