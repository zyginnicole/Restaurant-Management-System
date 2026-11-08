using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;

namespace RestaurantManagementSystem.Pages
{
    // Access denied: someone is signed in, but opened a page that belongs to another role
    // (e.g. a customer typing /Admin/Accounts, or staff opening an /Admin page).
    // RequireRoleFilter sends them here. This page is outside the protected folders, so everyone can see it.
    public class AccessDeniedModel : PageModel
    {
        public string? Role { get; set; }

        // "an administrator" / "staff" / "a customer"
        public string RoleText => Role switch
        {
            UserRoles.Admin => "an administrator",
            UserRoles.Staff => "staff",
            UserRoles.Customer => "a customer",
            _ => "another user"
        };

        // The signed-in user's own home page
        public string HomePage => Role switch
        {
            UserRoles.Admin => "/Admin/Dashboard",
            UserRoles.Staff => "/Staff/Dashboard",
            UserRoles.Customer => "/Customer/Dashboard",
            _ => "/Customer/Login"
        };

        public IActionResult OnGet()
        {
            Role = HttpContext.Session.GetUserRole();
            if (Role == null)
            {
                return RedirectToPage("/Customer/Login");   // not signed in at all
            }

            Response.StatusCode = StatusCodes.Status403Forbidden;   // 403 = signed in, but not allowed
            return Page();
        }
    }
}