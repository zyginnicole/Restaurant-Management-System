using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Business logic for "My profile": reading and updating the signed-in user's own details,
    // and changing their own password. Customer for now; Staff and Admin are added the same way later.
    // Every method returns null when it worked, or a message to show the user.
    public class ProfileService
    {
        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly SettingsService _settingsService;
        private readonly AuthService _authService;

        public ProfileService(ApplicationDbContext context, PasswordService passwordService,
                              SettingsService settingsService, AuthService authService)
        {
            _context = context;
            _passwordService = passwordService;
            _settingsService = settingsService;
            _authService = authService;
        }

        // ---------- Customer ----------

        // Read-only copy of the customer, for showing the Profile page
        public async Task<Customer?> GetCustomerAsync(int custId)
        {
            return await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.custID == custId);
        }

        // Saves the fields the customer may edit
        public async Task<string?> UpdateCustomerProfileAsync(int custId, CustomerProfileInput input)
        {
            Customer? customer = await _context.Customers.FirstOrDefaultAsync(c => c.custID == custId);
            if (customer == null)
            {
                return "Your account could not be found. Please sign in again.";
            }

            // A NEW email must not belong to any other account (customer, staff or admin)
            string newEmail = AuthService.NormaliseEmail(input.custEmail);
            bool emailChanged = newEmail != AuthService.NormaliseEmail(customer.custEmail);
            if (emailChanged && await _authService.IsEmailTakenAsync(newEmail))
            {
                return "This email is already used by another account.";
            }

            customer.custName = input.custName.Trim();
            customer.custEmail = newEmail;
            customer.custPhone = input.custPhone.Trim();
            customer.DietaryPref = input.DietaryPref;
            customer.promoOptIn = input.promoOptIn;

            await _context.SaveChangesAsync();
            return null;
        }

        // Changes the password after checking the current one
        public async Task<string?> ChangeCustomerPasswordAsync(int custId, string currentPassword, string newPassword)
        {
            Customer? customer = await _context.Customers.FirstOrDefaultAsync(c => c.custID == custId);
            if (customer == null)
            {
                return "Your account could not be found. Please sign in again.";
            }

            // 1. Prove it's really them
            if (!_passwordService.VerifyPassword(customer.custPassword, currentPassword))
            {
                return "Current password is incorrect.";
            }

            // 2. Same rules as Register (from Admin > Settings > Security)
            string? ruleError = PasswordPolicy.Check(newPassword, await _settingsService.GetAsync());
            if (ruleError != null)
            {
                return ruleError;
            }

            // 3. Must actually be a new password
            if (_passwordService.VerifyPassword(customer.custPassword, newPassword))
            {
                return "New password must be different from your current password.";
            }

            // 4. Save it hashed, never as plain text
            customer.custPassword = _passwordService.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            return null;
        }
    }
}