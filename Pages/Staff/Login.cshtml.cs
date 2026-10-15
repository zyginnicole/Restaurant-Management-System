using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Portal sign-in for Staff and Administrator. Static UI for now – sign-in logic to be added by the team.
    //
    // Notes for the sign-in logic:
    // - This page must stay open (no login check), even when the rest of /Staff and /Admin is protected.
    // - The form's "role" value only decides which table to check (Staff or Admin).
    //   Set the session role from the table that matched, never straight from the form.
    // - Store both the user ID and the role in the session, e.g. Session["UserId"] and Session["Role"].
    // - After sign-in: Staff -> /Staff/Dashboard, Admin -> /Admin/Dashboard.
    public class LoginModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
