using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Notifications: messages about the customer's reservations, orders, payments and promotions.
    // Static UI for now – backend notes:
    //  - There is no Notification table yet (ask the leader). Suggested fields:
    //      notificationID, custID, type ("Reservation", "Order", "Payment", "Promotion", "Account"),
    //      title, message, link, linkText, isRead, createdAt.
    //    Promotions can also come from Announcement rows sent to customers.
    //  - OnGet: load the customer's notifications (custID from Session), newest first.
    //  - Each row needs: class "unread" when isRead = false, data-category for the filter tabs
    //    (reservation / order / promotion / account), and the data-* values read by notifications.js.
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
