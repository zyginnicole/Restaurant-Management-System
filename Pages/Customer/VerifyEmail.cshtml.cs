using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Customer
{
    [AllowAnonymous] // must stay open: the account doesn't exist yet
    public class VerifyEmailModel : PageModel
    {
        private readonly SignUpVerificationService _verificationService;

        public VerifyEmailModel(SignUpVerificationService verificationService)
        {
            _verificationService = verificationService;
        }

        [BindProperty] public string Email { get; set; } = string.Empty;   // hidden field
        [BindProperty] public string Code { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? InfoMessage { get; set; }

        public IActionResult OnGet()
        {
            // Peek keeps the email in TempData, so refreshing this page still works
            if (TempData.Peek("VerifyEmail") is not string email || email.Length == 0)
            {
                return RedirectToPage("/Customer/Register");
            }

            Email = email;
            InfoMessage = TempData["VerifyInfo"] as string;
            return Page();
        }

        // "Verify and create account" button
        public async Task<IActionResult> OnPostAsync()
        {
            var (error, wasGuestProfile) = await _verificationService.CompleteAsync(Email, Code);
            if (error != null)
            {
                ErrorMessage = error;
                return Page();
            }

            TempData.Remove("VerifyEmail");
            TempData["LoginMessage"] = wasGuestProfile
                ? "Your email is verified and your account is ready. The bookings our staff made for you are already in it. You can now sign in."
                : "Your email is verified and your account has been created. You can now sign in.";
            return RedirectToPage("/Customer/Login");
        }

        // "Send a new code" button
        public async Task<IActionResult> OnPostResendAsync()
        {
            var (error, devCode) = await _verificationService.ResendAsync(Email);
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