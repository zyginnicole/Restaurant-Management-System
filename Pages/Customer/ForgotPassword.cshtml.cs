using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Customer
{
    [AllowAnonymous] // must stay open: the customer can't sign in
    public class ForgotPasswordModel : PageModel
    {
        private readonly PasswordResetService _resetService;

        public ForgotPasswordModel(PasswordResetService resetService)
        {
            _resetService = resetService;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.IsSignedInAs(UserRoles.Customer))
            {
                return RedirectToPage("/Customer/Dashboard");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var (error, devCode) = await _resetService.RequestCodeAsync(Email);
            if (error != null)
            {
                ErrorMessage = error;
                return Page();
            }

            // The email is passed in TempData (not the URL), so it doesn't show in the address bar or history
            TempData["ResetEmail"] = AuthService.NormaliseEmail(Email);
            TempData["ResetInfo"] = devCode == null
                ? $"If an account exists for {Email.Trim()}, we've sent a 6-digit code to it. It expires in {PasswordResetService.CodeMinutes} minutes."
                : $"Email isn't set up on this computer, so here is your code for testing: {devCode}";

            return RedirectToPage("/Customer/ResetPassword");
        }
    }
}