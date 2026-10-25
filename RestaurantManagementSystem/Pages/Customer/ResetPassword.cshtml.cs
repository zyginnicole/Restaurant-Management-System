using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Customer
{
    [AllowAnonymous] // must stay open: the customer can't sign in
    public class ResetPasswordModel : PageModel
    {
        private readonly PasswordResetService _resetService;
        private readonly SettingsService _settingsService;

        public ResetPasswordModel(PasswordResetService resetService, SettingsService settingsService)
        {
            _resetService = resetService;
            _settingsService = settingsService;
        }

        [BindProperty] public string Email { get; set; } = string.Empty;           // hidden field
        [BindProperty] public string Code { get; set; } = string.Empty;
        [BindProperty] public string NewPassword { get; set; } = string.Empty;
        [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? InfoMessage { get; set; }
        public string PasswordHint { get; set; } = string.Empty;

        // Runs before every handler, so the password rules are shown after an error too
        public override async Task OnPageHandlerExecutionAsync(
            PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            PasswordHint = PasswordPolicy.Describe(await _settingsService.GetAsync());
            await next();
        }

        public IActionResult OnGet()
        {
            // Peek keeps the email in TempData, so refreshing this page still works
            if (TempData.Peek("ResetEmail") is not string email || email.Length == 0)
            {
                return RedirectToPage("/Customer/ForgotPassword");
            }

            Email = email;
            InfoMessage = TempData["ResetInfo"] as string;
            return Page();
        }

        // "Reset password" button
        public async Task<IActionResult> OnPostAsync()
        {
            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "Passwords do not match.";
                return Page();
            }

            string? error = await _resetService.ResetPasswordAsync(Email, Code, NewPassword);
            if (error != null)
            {
                ErrorMessage = error;
                return Page();
            }

            TempData.Remove("ResetEmail");
            TempData["LoginMessage"] = "Your password has been changed. Please sign in with your new password.";
            return RedirectToPage("/Customer/Login");
        }

        // "Send a new code" button
        public async Task<IActionResult> OnPostResendAsync()
        {
            var (error, devCode) = await _resetService.RequestCodeAsync(Email);
            if (error != null)
            {
                ErrorMessage = error;
                return Page();
            }

            InfoMessage = devCode == null
                ? "A new code has been sent. Only the newest code works."
                : $"Email isn't set up on this computer, so here is your new code for testing: {devCode}";

            ModelState.Remove(nameof(Code));   // clear the old code from the box
            Code = string.Empty;
            return Page();
        }
    }
}