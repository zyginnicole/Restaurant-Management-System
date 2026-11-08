using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Accounts: customer and staff accounts, roles and access (create, edit, deactivate, reset password,
    // unlock locked accounts, delete unused staff accounts, delete customer accounts).
    // Only administrators reach this page (RequireRoleFilter on /Admin).
    // Every action redirects back with TempData["AdminMessage"] (green box) or TempData["AdminError"] (red box)
    // and ?tab=staff / ?tab=customers, so refreshing the page never repeats an action.
    public class AccountsModel : PageModel
    {
        private readonly AccountService _accountService;

        public AccountsModel(AccountService accountService)
        {
            _accountService = accountService;
        }

        public AccountStats Stats { get; set; } = new AccountStats();
        public List<StaffAccountRow> StaffAccounts { get; set; } = new List<StaffAccountRow>();
        public List<CustomerAccountRow> Customers { get; set; } = new List<CustomerAccountRow>();

        // Customers tab paging and search (?page=2, ?q=lim)
        public int CustomerMatches { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public string? Search { get; set; }

        public async Task OnGetAsync(int page = 1, string? q = null)
        {
            Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
            CurrentPage = Math.Max(1, page);

            Stats = await _accountService.GetStatsAsync();
            StaffAccounts = await _accountService.GetStaffAccountsAsync();

            var result = await _accountService.GetCustomersAsync(Search, CurrentPage);
            Customers = result.Rows;
            CustomerMatches = result.Total;
            TotalPages = Math.Max(1, (int)Math.Ceiling(CustomerMatches / (double)AccountService.CustomersPerPage));
        }

        // Create staff account pop-up. Role "Administrator" → new Admin row, otherwise a new Staff row.
        public async Task<IActionResult> OnPostCreateAsync(string? staffName, string? staffEmail, string? staffPhone,
                                                           string? staffPosition, string? systemRole)
        {
            var (error, temporaryPassword) = await _accountService.CreateAccountAsync(
                staffName, staffEmail, staffPhone, staffPosition, systemRole);

            if (error != null)
            {
                return Failed(error, "staff");
            }

            string roleText = systemRole == AccountService.RoleAdministrator ? "Administrator" : "Staff";
            return Done($"{roleText} account created for {staffName?.Trim()}. " +
                        $"Temporary password: {temporaryPassword} (shown only once). " +
                        "Give it to them privately; they can change it in My profile.", "staff");
        }

        // Edit pop-up "Save changes" (accountId is "S-12" for a Staff row or "A-1" for an Admin row)
        public async Task<IActionResult> OnPostEditAsync(string? accountId, string? staffName, string? staffPhone, string? staffPosition)
        {
            string? error = await _accountService.UpdateAccountAsync(accountId, staffName, staffPhone, staffPosition);
            return error == null
                ? Done($"{staffName?.Trim()}'s account saved.", "staff")
                : Failed(error, "staff");
        }

        public async Task<IActionResult> OnPostDeactivateAsync(string? accountId)
        {
            string? error = await _accountService.SetStaffActiveAsync(accountId, active: false);
            return error == null ? Done("Account deactivated. They can no longer sign in.", "staff") : Failed(error, "staff");
        }

        public async Task<IActionResult> OnPostReactivateAsync(string? accountId)
        {
            string? error = await _accountService.SetStaffActiveAsync(accountId, active: true);
            return error == null ? Done("Account reactivated. They can sign in again.", "staff") : Failed(error, "staff");
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(string? accountId)
        {
            var (error, name, temporaryPassword) = await _accountService.ResetPasswordAsync(accountId);
            if (error != null)
            {
                return Failed(error, "staff");
            }

            return Done($"New temporary password for {name}: {temporaryPassword} (shown only once). " +
                        "Their old password no longer works.", "staff");
        }

        public async Task<IActionResult> OnPostUnlockAsync(int custId)
        {
            string? error = await _accountService.UnlockCustomerAsync(custId);
            return error == null ? Done("Customer account unlocked.", "customers") : Failed(error, "customers");
        }

        // Staff tab "Unlock" (accountId is "S-12" for a Staff row or "A-1" for an Admin row)
        public async Task<IActionResult> OnPostUnlockStaffAsync(string? accountId)
        {
            var (error, name) = await _accountService.UnlockStaffAsync(accountId);
            return error == null
                ? Done($"{name}'s account unlocked. They can sign in again straight away.", "staff")
                : Failed(error, "staff");
        }

        // Staff "Delete" (Edit pop-up or deactivated row): only for staff with no reservations or orders
        public async Task<IActionResult> OnPostDeleteStaffAsync(string? accountId)
        {
            var (error, name) = await _accountService.DeleteStaffAsync(accountId);
            return error == null
                ? Done($"{name}'s staff account has been deleted.", "staff")
                : Failed(error, "staff");
        }

        // Customers tab "Delete": removes the customer's personal details (records stay for reports)
        public async Task<IActionResult> OnPostDeleteCustomerAsync(int custId)
        {
            var (error, name) = await _accountService.CloseCustomerAccountAsync(custId, closedByAdmin: true);
            return error == null
                ? Done($"{name}'s account has been deleted. Their personal details were removed; past bookings and orders stay in the records.", "customers")
                : Failed(error, "customers");
        }

        private IActionResult Done(string message, string tab)
        {
            TempData["AdminMessage"] = message;
            return RedirectToPage(new { tab });
        }

        private IActionResult Failed(string message, string tab)
        {
            TempData["AdminError"] = message;
            return RedirectToPage(new { tab });
        }
    }
}