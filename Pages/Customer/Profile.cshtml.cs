using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;
using CustomerModel = RestaurantManagementSystem.Model.Customer;

namespace RestaurantManagementSystem.Pages.Customer
{
    // My profile for customers: personal details, dining preferences and password.
    // Only signed-in customers reach this page (RequireRoleFilter on /Customer),
    // and the customer ID always comes from the session, never from the URL or the form.
    public class ProfileModel : PageModel
    {
        private readonly ProfileService _profileService;
        private readonly SettingsService _settingsService;

        public ProfileModel(ProfileService profileService, SettingsService settingsService)
        {
            _profileService = profileService;
            _settingsService = settingsService;
        }

        // What the form shows: the saved values, or what the user typed after a failed save
        public CustomerProfileInput Input { get; set; } = new CustomerProfileInput();

        public string MemberSince { get; set; } = string.Empty;

        // Password rule sentence for the Change password pop-up (from Admin > Settings > Security)
        public string PasswordHint { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            return await LoadPageAsync(fillForm: true) ? Page() : SignOutAndGoToLogin();
        }

        // Edit → Save changes (both cards). [Bind(Prefix = "Customer")] reads the fields named Customer.xxx
        public async Task<IActionResult> OnPostProfileAsync([Bind(Prefix = "Customer")] CustomerProfileInput input)
        {
            Input = input;

            int? custId = HttpContext.Session.GetUserId();
            if (custId == null)
            {
                return SignOutAndGoToLogin();
            }

            if (ModelState.IsValid)
            {
                string? error = await _profileService.UpdateCustomerProfileAsync(custId.Value, input);
                if (error == null)
                {
                    // keep the sidebar and dashboard name in step with the new name
                    HttpContext.Session.UpdateUserName(input.custName.Trim());

                    TempData["ProfileMessage"] = "Your profile has been saved.";
                    return RedirectToPage();
                }

                ModelState.AddModelError(string.Empty, error);
            }

            // Show the page again with the typed values and the errors (fillForm: false keeps Input)
            return await LoadPageAsync(fillForm: false) ? Page() : SignOutAndGoToLogin();
        }

        // Change password pop-up
        public async Task<IActionResult> OnPostPasswordAsync(string? currentPassword, string? newPassword, string? confirmPassword)
        {
            int? custId = HttpContext.Session.GetUserId();
            if (custId == null)
            {
                return SignOutAndGoToLogin();
            }

            if (string.IsNullOrEmpty(currentPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            {
                ModelState.AddModelError(string.Empty, "Please fill in all three password fields.");
            }
            else if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "The new passwords do not match.");
            }
            else
            {
                string? error = await _profileService.ChangeCustomerPasswordAsync(custId.Value, currentPassword, newPassword);
                if (error != null)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }

            if (!ModelState.IsValid)
            {
                // the pop-up opens again by itself and shows the error in its red box
                return await LoadPageAsync(fillForm: true) ? Page() : SignOutAndGoToLogin();
            }

            TempData["ProfileMessage"] = "Your password has been updated.";
            return RedirectToPage();
        }

        // Loads what every version of the page needs. Returns false if the account no longer exists.
        private async Task<bool> LoadPageAsync(bool fillForm)
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

            MemberSince = customer.custCreateAt.ToString("MMM yyyy");
            PasswordHint = PasswordPolicy.Describe(await _settingsService.GetAsync());

            if (fillForm)
            {
                Input = new CustomerProfileInput
                {
                    custName = customer.custName,
                    custEmail = customer.custEmail,
                    custPhone = customer.custPhone,
                    Prefs = MenuVocabulary.Parse(customer.DietaryPref).ToList(),
                    Allergies = MenuVocabulary.Parse(customer.Allergies).ToList(),
                    promoOptIn = customer.promoOptIn
                };
            }

            return true;
        }

        private IActionResult SignOutAndGoToLogin()
        {
            HttpContext.Session.SignOutUser();
            return RedirectToPage("/Customer/Login");
        }
    }
}