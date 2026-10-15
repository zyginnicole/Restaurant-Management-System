using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Sign out: posted from the confirmation pop-up in _CustomerLayout ("Sign out" in the sidebar).
    // A POST (not a link) so another site can't sign the customer out, and the antiforgery token is checked.
    public class LogoutModel : PageModel
    {
        // Opening /Customer/Logout directly does nothing – go back to the Overview
        public IActionResult OnGet() => RedirectToPage("/Customer/Dashboard");

        public IActionResult OnPost()
        {
            // Removes CustomerLoggedIn, CustID and CustName (set by the Login page)
            HttpContext.Session.Clear();
            return RedirectToPage("/Customer/Login");
        }
    }
}
