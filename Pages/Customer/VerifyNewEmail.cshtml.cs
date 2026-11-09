using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;
using CustomerModel = RestaurantManagementSystem.Model.Customer;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Confirm new email: the customer changed their email on the Profile page and enters the code
    // that was sent to the NEW address. Only then is the new email saved (EmailChangeService).
    // Signed-in customers only (RequireRoleFilter on /Customer); the customer ID comes from the session.
    public class VerifyNewEmailModel : PageModel
    {
        private readonly EmailChangeService _emailChangeService;
        private readonly ProfileService _profileService;

        public VerifyNewEmailModel(EmailChangeService emailChangeService, ProfileService profileService)
        {
            _emailChangeService = emailChangeService;
            _profileService = profileService;
        }

        [BindProperty]
        public string Code { get; set; } = string.Empty;

        public string CurrentEmail { get; set; } = string.Empty;
        public string? NewEmail { get; set; }          // null = no email change waiting

        public string? ErrorMessage { get; set; }
        public string? InfoMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!await LoadAsync())
            {
                return SignOutAndGoToLogin();
            }

            InfoMessage = TempData["VerifyNewEmailInfo"] as string;
            return Page();
        }

        // "Confirm new email" button
        public async Task<IActionResult> OnPostAsync()
        {
            int? custId = HttpContext.Session.GetUserId();
            if (custId == null)
            {
                return SignOutAndGoToLogin();
            }

            var (error, newEmail) = await _emailChangeService.CompleteAsync(custId.Value, Code);
            if (error == null)
            {
                TempData["ProfileMessage"] = $"Your email has been changed to {newEmail}. Please use it the next time you sign in.";
                return RedirectToPage("/Customer/Profile");
            }

            ErrorMessage = error;
            return await LoadAsync() ? Page() : SignOutAndGoToLogin();
        }

        // "Send a new code" button
        public async Task<IActionResult> OnPostResendAsync()
        {
            int? custId = HttpContext.Session.GetUserId();
            if (custId == null)
            {
                return SignOutAndGoToLogin();
            }

            var (error, devCode) = await _emailChangeService.ResendAsync(custId.Value);
            if (error != null)
            {
                ErrorMessage = error;
            }
            else
            {
                InfoMessage = devCode == null
                    ? "A new code has been sent. Only the newest code works."
                    : $"Email isn't set up on this computer, so here is your new code for testing: {devCode}";
            }

            ModelState.Remove(nameof(Code));   // clear the old code from the box
            Code = string.Empty;
            return await LoadAsync() ? Page() : SignOutAndGoToLogin();
        }

        // "Keep my current email" button
        public IActionResult OnPostCancel()
        {
            int? custId = HttpContext.Session.GetUserId();
            if (custId == null)
            {
                return SignOutAndGoToLogin();
            }

            _emailChangeService.Cancel(custId.Value);
            TempData["ProfileMessage"] = "Email change cancelled. Your email has not been changed.";
            return RedirectToPage("/Customer/Profile");
        }

        // Current email from the database, new email from the waiting change. False if the account no longer exists.
        private async Task<bool> LoadAsync()
        {
            int? custId = HttpContext.Session.GetUserId();
            if (custId == null)
            {
                return false;
            }

            CustomerModel? customer = await _profileService.GetCustomerAsync(custId.Value);
            if (customer == null)
            {
                return false;
            }

            CurrentEmail = customer.custEmail;
            NewEmail = _emailChangeService.GetPendingEmail(custId.Value);
            return true;
        }

        private IActionResult SignOutAndGoToLogin()
        {
            HttpContext.Session.SignOutUser();
            return RedirectToPage("/Customer/Login");
        }
    }
}