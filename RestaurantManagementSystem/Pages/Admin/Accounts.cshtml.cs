using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Accounts: customer and staff accounts, roles and access.
    // Static UI for now – backend notes:
    //  - OnGet: load Customers, Staff and Admins.
    //    Stat cards: Customers (count, new in the last 7 days from custCreateAt) · Staff (active / deactivated from isActive)
    //    · Administrators (Admin rows) · Locked (Customers with lockoutEnd > now).
    //  - Staff tab = Staff rows + Admin rows (system role "Administrator"). Fields: staffName, staffEmail, staffPhone,
    //    staffPosition (job title, display only), isActive. Admin has adminName / adminEmail only (no phone or job title).
    //    "Last sign-in" has no field yet – ask the leader (e.g. Staff.lastLoginAt / Admin.lastLoginAt), or remove the column.
    //  - Customers tab: custName, custEmail, custPhone, custCreateAt, promoOptIn, completed visits (Reservations),
    //    locked = lockoutEnd > now. List 20 per page (?page=2); the search box filters by name or email (?q=).
    //  - The system role is chosen only when the account is created (Staff table or Admin table);
    //    it can't be switched later because they are two different tables.
    //  - Every action redirects back with TempData["AdminMessage"] (green box) and ?tab=staff / ?tab=customers.
    public class AccountsModel : PageModel
    {
        public void OnGet()
        {
        }

        // ---- UI flow only (no saving yet) – backend: save first, then redirect ----

        // Create staff account (fields: staffName, staffEmail, staffPhone, staffPosition, systemRole "Staff" / "Administrator",
        // sendSetPasswordLink). Check the email isn't used yet. Role "Administrator" → new Admin row, else new Staff row.
        // No password is typed here: send a set-password link, or the backend sets a temporary password (PasswordService).
        public IActionResult OnPostCreate(string staffName, string systemRole)
        {
            TempData["AdminMessage"] = $"{(systemRole == "Administrator" ? "Administrator" : "Staff")} account created for {staffName}.";
            return RedirectToPage(new { tab = "staff" });
        }

        // Edit pop-up "Save changes" (fields: accountId, staffName, staffPhone, staffPosition).
        // accountId is "S-12" for a Staff row or "A-1" for an Admin row.
        public IActionResult OnPostEdit(string accountId, string staffName)
        {
            TempData["AdminMessage"] = $"{staffName}'s account saved.";
            return RedirectToPage(new { tab = "staff" });
        }

        // Staff.isActive = false (they can't sign in any more; their past reservations and orders stay)
        public IActionResult OnPostDeactivate(string accountId) => Done("Account deactivated.", "staff");

        // Staff.isActive = true
        public IActionResult OnPostReactivate(string accountId) => Done("Account reactivated.", "staff");

        // Send a set-password link (or set a temporary password with PasswordService)
        public IActionResult OnPostResetPassword(string accountId) => Done("Password reset link sent.", "staff");

        // Customer: failedLoginCount = 0, lockoutEnd = null
        public IActionResult OnPostUnlock(int custId) => Done("Customer account unlocked.", "customers");

        private IActionResult Done(string message, string tab)
        {
            TempData["AdminMessage"] = message;
            return RedirectToPage(new { tab });
        }
    }
}
