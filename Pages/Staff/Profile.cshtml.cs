using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;
using StaffEntity = RestaurantManagementSystem.Model.Staff;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff My profile: personal details (editable) and work details (read-only), plus change password.
    // Only signed-in staff reach this page (RequireRoleFilter on /Staff);
    // the staff ID always comes from the session, never from the URL or the form.
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
        public StaffProfileInput Input { get; set; } = new StaffProfileInput();

        // Read-only details shown on the page (always the saved values)
        public string DisplayName { get; set; } = string.Empty;
        public string StaffCode { get; set; } = string.Empty;     // e.g. STF-0001
        public string StaffSince { get; set; } = string.Empty;
        public string StaffEmail { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string PasswordHint { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            return await LoadPageAsync(fillForm: true) ? Page() : SignOutAndGoToLogin();
        }

        // Edit → Save changes. [Bind(Prefix = "Staff")] reads the fields named Staff.xxx
        public async Task<IActionResult> OnPostProfileAsync([Bind(Prefix = "Staff")] StaffProfileInput input)
        {
            Input = input;

            int? staffId = HttpContext.Session.GetUserId();
            if (staffId == null)
            {
                return SignOutAndGoToLogin();
            }

            if (ModelState.IsValid)
            {
                string? error = await _profileService.UpdateStaffProfileAsync(staffId.Value, input);
                if (error == null)
                {
                    HttpContext.Session.UpdateUserName(input.staffName.Trim());

                    TempData["StaffMessage"] = "Your profile has been saved.";
                    return RedirectToPage();
                }

                ModelState.AddModelError(string.Empty, error);
            }

            return await LoadPageAsync(fillForm: false) ? Page() : SignOutAndGoToLogin();
        }

        // Change password pop-up
        public async Task<IActionResult> OnPostPasswordAsync(string? currentPassword, string? newPassword, string? confirmPassword)
        {
            int? staffId = HttpContext.Session.GetUserId();
            if (staffId == null)
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
                string? error = await _profileService.ChangeStaffPasswordAsync(staffId.Value, currentPassword, newPassword);
                if (error != null)
                {
                    ModelState.AddModelError(string.Empty, error);
                }
            }

            if (!ModelState.IsValid)
            {
                return await LoadPageAsync(fillForm: true) ? Page() : SignOutAndGoToLogin();
            }

            TempData["StaffMessage"] = "Your password has been updated.";
            return RedirectToPage();
        }

        // Loads what every version of the page needs. Returns false if the account no longer exists.
        private async Task<bool> LoadPageAsync(bool fillForm)
        {
            int? staffId = HttpContext.Session.GetUserId();
            if (staffId == null)
            {
                return false;
            }

            StaffEntity? staff = await _profileService.GetStaffAsync(staffId.Value);
            if (staff == null)
            {
                return false;
            }

            DisplayName = staff.staffName;
            StaffCode = $"STF-{staff.staffID:D4}";
            StaffSince = staff.staffCreateAt.ToString("MMM yyyy");
            StaffEmail = staff.staffEmail;
            JobTitle = staff.staffRole;
            PasswordHint = PasswordPolicy.Describe(await _settingsService.GetAsync());

            if (fillForm)
            {
                Input = new StaffProfileInput
                {
                    staffName = staff.staffName,
                    staffPhone = staff.staffPhone
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