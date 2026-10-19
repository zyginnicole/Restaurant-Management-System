using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff My profile: personal details and password of the signed-in staff member.
    // Static UI for now – backend notes:
    //  - OnGet: load the Staff for staffID from Session.
    //    Shown: staffName, staffPhone (editable) · staffEmail, staffPosition (read-only) · staffID as "STF-0042",
    //    staffCreateAt as "Staff since". System role is always "Staff" (admins are in the Admin table).
    //    The shift line ("Evening service") has no field yet – ask the leader, or remove it.
    //  - Field names allow a [BindProperty] Staff: Staff.staffName (max 100), Staff.staffPhone (max 20).
    //  - OnPostProfile (Edit → Save changes): update only staffName and staffPhone.
    //    staffEmail, staffPosition and isActive are changed by an administrator on the Admin Staff page –
    //    don't read them from the form.
    //  - OnPostPassword (Change password pop-up; fields: currentPassword, newPassword, confirmPassword):
    //    check the current password with PasswordService, apply the same rules as Register, then save the new hash.
    //    On an error: ModelState.AddModelError("", "Current password is incorrect.") and return Page() –
    //    the pop-up opens again by itself and shows the message in its red box.
    //  - After saving, set TempData["StaffMessage"] and redirect back; the page shows it in the green box.
    public class ProfileModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: save first, then redirect.
        public IActionResult OnPostProfile()
        {
            TempData["StaffMessage"] = "Your profile has been saved.";
            return RedirectToPage();
        }

        public IActionResult OnPostPassword()
        {
            TempData["StaffMessage"] = "Your password has been updated.";
            return RedirectToPage();
        }
    }
}
