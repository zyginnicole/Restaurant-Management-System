using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;

namespace RestaurantManagementSystem.Pages
{
    // Signs out whoever is logged in, then sends them to the right login page.
    public class LogoutModel : PageModel
    {
        public IActionResult OnGet()
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