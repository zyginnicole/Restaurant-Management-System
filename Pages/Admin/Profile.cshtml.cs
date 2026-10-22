using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin My profile: name, sign-in email and password of the signed-in administrator.
    // Static UI for now – backend notes:
    //  - OnGet: load the Admin for adminID from Session.
    //    Shown: adminName, adminEmail (editable) · adminID as "ADM-0001", adminCreateAt as "Administrator since".
    //    System role is always "Administrator" (Admin table). "Last sign-in" has no field yet (see the Accounts page).
    //  - Field names allow a [BindProperty] Admin: Admin.adminName (max 100), Admin.adminEmail (max 100).
    //  - OnPostProfile (Edit → Save changes): update adminName and adminEmail.
    //    adminEmail is the sign-in name: check no other Admin or Staff uses it; on a clash
    //    ModelState.AddModelError("Admin.adminEmail", "...") and return Page().
    //  - OnPostPassword (Change password pop-up; fields: currentPassword, newPassword, confirmPassword):
    //    check the current password with PasswordService, apply the same rules as Register, then save the new hash.
    //    On an error: ModelState.AddModelError("", "Current password is incorrect.") and return Page() –
    //    the pop-up opens again by itself and shows the message in its red box.
    //  - After saving, set TempData["AdminMessage"] and redirect back; the page shows it in the green box.
    public class ProfileModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: save first, then redirect.
        public IActionResult OnPostProfile()
        {
            TempData["AdminMessage"] = "Your profile has been saved.";
            return RedirectToPage();
        }

        public IActionResult OnPostPassword()
        {
            TempData["AdminMessage"] = "Your password has been updated.";
            return RedirectToPage();
        }
    }
}
