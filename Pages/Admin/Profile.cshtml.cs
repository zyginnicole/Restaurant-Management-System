using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;
using AdminEntity = RestaurantManagementSystem.Model.Admin;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin My profile: name and sign-in email (editable), account details (read-only), change password.
    // Only signed-in admins reach this page (RequireRoleFilter on /Admin);
    // the admin ID always comes from the session, never from the URL or the form.
    public class ProfileModel : PageModel
    {
        private readonly ProfileService _profileService;
        private readonly SettingsService _settingsService;

        public ProfileModel(ProfileService profileService, SettingsService settingsService)
        {
            _profileService = profileService;
            _settingsService = settingsService;
        }

        // Editable fields: the saved values, or what the user typed after a failed save
        public AdminProfileInput Input { get; set; } = new AdminProfileInput();

        // Read-only details shown on the page (always the saved values)
        public string DisplayName { get; set; } = string.Empty;
        public string AdminCode { get; set; } = string.Empty;     // e.g. ADM-0001
        public string AdminSince { get; set; } = string.Empty;
        public string PasswordHint { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            return await LoadPageAsync(fillForm: true) ? Page() : SignOutAndGoToLogin();
        }

        // Edit → Save changes. [Bind(Prefix = "Admin")] reads the fields named Admin.xxx
        public async Task<IActionResult> OnPostProfileAsync([Bind(Prefix = "Admin")] AdminProfileInput input)
        {
            Input = input;

            int? adminId = HttpContext.Session.GetUserId();
            if (adminId == null)
            {
                return SignOutAndGoToLogin();
            }

            if (ModelState.IsValid)
            {
                string? error = await _profileService.UpdateAdminProfileAsync(adminId.Value, input);
                if (error == null)
                {
                    HttpContext.Session.UpdateUserName(input.adminName.Trim());

                    TempData["AdminMessage"] = "Your profile has been saved.";
                    return RedirectToPage();
                }

                ModelState.AddModelError("Admin.adminEmail", error);
            }

            return await LoadPageAsync(fillForm: false) ? Page() : SignOutAndGoToLogin();
        }

        // Change password pop-up
        public async Task<IActionResult> OnPostPasswordAsync(string? currentPassword, string? newPassword, string? confirmPassword)
        {
            int? adminId = HttpContext.Session.GetUserId();
            if (adminId == null)
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
                string? error = await _profileService.ChangeAdminPasswordAsync(adminId.Value, currentPassword, newPassword);
                if (error != null)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }

            if (!ModelState.IsValid)
            {
                return await LoadPageAsync(fillForm: true) ? Page() : SignOutAndGoToLogin();
            }

            TempData["AdminMessage"] = "Your password has been updated.";
            return RedirectToPage();
        }

        // Loads what every version of the page needs. Returns false if the account no longer exists.
        private async Task<bool> LoadPageAsync(bool fillForm)
        {
            int? adminId = HttpContext.Session.GetUserId();
            if (adminId == null)
            {
                return false;
            }

            AdminEntity? admin = await _profileService.GetAdminAsync(adminId.Value);
            if (admin == null)
            {
                return false;
            }

            DisplayName = admin.adminName;
            AdminCode = $"ADM-{admin.adminID:D4}";
            AdminSince = admin.adminCreateAt.ToString("MMM yyyy");
            PasswordHint = PasswordPolicy.Describe(await _settingsService.GetAsync());

            if (fillForm)
            {
                Input = new AdminProfileInput
                {
                    adminName = admin.adminName,
                    adminEmail = admin.adminEmail
                };
            }

            return true;
        }

        private IActionResult SignOutAndGoToLogin()
        {
            HttpContext.Session.SignOutUser();
            return RedirectToPage("/Staff/Login");
        }
    }
}