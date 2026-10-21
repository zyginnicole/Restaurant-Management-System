using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;

namespace RestaurantManagementSystem.Pages
{
    // Signs out whoever is logged in (any role), then sends them to the right login page.
    // The "Confirm sign-out?" pop-up in both layouts posts here. A POST means the antiforgery
    // token is checked, so another website cannot sign the user out with a link.
    public class LogoutModel : PageModel
    {
        // Typing /Logout in the address bar does NOT sign anyone out
        // (only the pop-up's POST does) – send the person back to their own area
        public IActionResult OnGet()
        {
            string? role = HttpContext.Session.GetUserRole();

            if (role == UserRoles.Admin) return RedirectToPage("/Admin/Dashboard");
            if (role == UserRoles.Staff) return RedirectToPage("/Staff/Dashboard");
            if (role == UserRoles.Customer) return RedirectToPage("/Customer/Dashboard");

            return RedirectToPage("/Customer/Login");
        }

        public IActionResult OnPost()
        {
            // read the role BEFORE clearing, so we know where to send them
            string? role = HttpContext.Session.GetUserRole();

            HttpContext.Session.SignOutUser();

            if (role == UserRoles.Staff || role == UserRoles.Admin)
            {
                return RedirectToPage("/Staff/Login");
            }

            return RedirectToPage("/Customer/Login");
        }
    }
}