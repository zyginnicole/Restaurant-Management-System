using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // TEMPORARY sign out, used by the "Confirm sign-out?" pop-up in both layouts.
    // It will be replaced by Sophia's Pages/Logout (feature/auth-login), which signs out any role:
    // once that is merged, point both pop-ups to "/Logout" and delete this page.
    //
    // A POST (not a link) so another site can't sign the user out, and the antiforgery token is checked.
    public class LogoutModel : PageModel
    {
        // Opening /Customer/Logout directly does nothing – go back to the Overview
        public IActionResult OnGet() => RedirectToPage("/Customer/Dashboard");

        // portal = "staff" is sent by the Staff/Admin layout's Sign out button (data-id="staff").
        // "Role" is the session key Sophia's login uses (Helpers/SessionKeys.Role).
        public IActionResult OnPost(string? portal)
        {
            string? role = HttpContext.Session.GetString("Role");
            bool staffPortal = portal == "staff" || role == "Staff" || role == "Admin";

            HttpContext.Session.Clear();

            return staffPortal
                ? RedirectToPage("/Staff/Login")
                : RedirectToPage("/Customer/Login");
        }
    }
}
