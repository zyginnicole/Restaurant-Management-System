using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Portal sign-in for both Staff and Administrator.
    // - This page stays open (no login check needed to see it).
    // - The "role" value from the tabs only decides WHICH table to check.
    // - The session role always comes from the table that matched, never from the form.

    [AllowAnonymous] // must stay open: this is where staff and admins sign in
    public class LoginModel : PageModel
    {
        private readonly AuthService _authService;

        public LoginModel(AuthService authService)
        {
            _authService = authService;
        }

        // Which tab was selected: "Staff" or "Admin" (hidden field named "role")
        [BindProperty]
        public string Role { get; set; } = UserRoles.Staff;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        // The page they tried to open before signing in (set by RequireRoleFilter), e.g. /Admin/Accounts
        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        // Shown in the red alert box when sign-in fails
        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            // already signed in, go straight to the right dashboard
            if (HttpContext.Session.IsSignedInAs(UserRoles.Admin))
            {
                return RedirectToPage("/Admin/Dashboard");
            }

            if (HttpContext.Session.IsSignedInAs(UserRoles.Staff))
            {
                return RedirectToPage("/Staff/Dashboard");
            }

            // coming from an /Admin page → open the Administrator tab
            if (ReturnUrl != null && ReturnUrl.StartsWith("/Admin/", StringComparison.OrdinalIgnoreCase))
            {
                Role = UserRoles.Admin;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password))
            {
                ErrorMessage = "Please enter your email and password.";
                return Page();
            }

            // the tab only picks the table to search
            LoginResult result = Role == UserRoles.Admin
                ? await _authService.LoginAdminAsync(Email, Password)
                : await _authService.LoginStaffAsync(Email, Password);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return Page();
            }

            // clear any old session, then store user ID + role + name
            HttpContext.Session.SignInUser(result.UserId, result.Role, result.UserName);

            // where to go depends on the role from the RESULT, not the form.
            // Back to the page they wanted only if it is inside their own area of THIS site.
            string area = result.Role == UserRoles.Admin ? "/Admin/" : "/Staff/";
            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) &&
                ReturnUrl.StartsWith(area, StringComparison.OrdinalIgnoreCase))
            {
                return LocalRedirect(ReturnUrl);
            }

            if (result.Role == UserRoles.Admin)
            {
                return RedirectToPage("/Admin/Dashboard");
            }

            return RedirectToPage("/Staff/Dashboard");
        }
    }
}