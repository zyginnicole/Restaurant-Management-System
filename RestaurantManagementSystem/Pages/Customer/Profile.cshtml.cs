using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Profile: personal details, dining preferences and password.
    // Static UI for now – backend notes:
    //  - Field names follow the Register page (asp-for="Customer.xxx"), so a [BindProperty] Customer works:
    //      Customer.custName, Customer.custEmail, Customer.custPhone, Customer.promoOptIn, Customer.DietaryPref,
    //      Customer.seatingPref (one of: No preference, Indoor, Outdoor, VIP), Customer.kitchenNotes (optional, max 300)
    //  - OnGet: load the Customer for custID from Session.
    //  - OnPostProfile (one form for both cards, sent by the Edit → Save changes button):
    //    update custName / custEmail (check the email isn't used by another customer) / custPhone / promoOptIn,
    //    DietaryPref, seatingPref and kitchenNotes.
    //  - OnPostPassword (Change password pop-up; fields: currentPassword, newPassword, confirmPassword):
    //    check the current password, apply the same rules as Register, then save the new (hashed) password.
    //    On an error: ModelState.AddModelError("", "Current password is incorrect.") and return Page() –
    //    the pop-up opens again by itself and shows the message in its red box.
    //  - After saving, set TempData["ProfileMessage"] and redirect back; the page shows it in the green box.
    public class ProfileModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: save first, then redirect.
        // Personal information + Dining preferences are posted together by the Edit / Save changes button.
        public IActionResult OnPostProfile()
        {
            TempData["ProfileMessage"] = "Your profile has been saved.";
            return RedirectToPage();
        }

        public IActionResult OnPostPassword()
        {
            TempData["ProfileMessage"] = "Your password has been updated.";
            return RedirectToPage();
        }
    }
}
