using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Business logic for "My profile": reading and updating the signed-in user's own details,
    // and changing their own password, for all three roles (Customer, Staff, Admin).
    // Every Update / Change method returns null when it worked, or a message to show the user.
    public class ProfileService
    {
        private const string AccountMissingMessage = "Your account could not be found. Please sign in again.";
        private const string EmailTakenMessage = "This email is already used by another account.";

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
                return AccountMissingMessage;
            }

            // A NEW email must not belong to any other account (customer, staff or admin)
            string newEmail = AuthService.NormaliseEmail(input.custEmail);
            if (await IsNewEmailTakenAsync(customer.custEmail, newEmail))
            {
                return EmailTakenMessage;
            }

            customer.custName = input.custName.Trim();
            customer.custEmail = newEmail;
            customer.custPhone = input.custPhone.Trim();
            customer.DietaryPref = MenuVocabulary.Join(input.Prefs.Where(x => MenuVocabulary.Tags.Contains(x)));
            customer.Allergies = MenuVocabulary.Join(input.Allergies.Where(x => MenuVocabulary.Allergens.Contains(x)));
            customer.promoOptIn = input.promoOptIn;

            // Birthday: set once. A saved birthday is never changed from the Profile page.
            if (customer.custBirthday == null && input.custBirthday.HasValue)
            {
                DateTime birthday = input.custBirthday.Value.Date;
                if (birthday > MalaysiaClock.Today || birthday.Year < 1900)
                {
                    return "Please enter a valid birthday.";
                }
                customer.custBirthday = birthday;
            }

            await _context.SaveChangesAsync();
            return null;
        }

        public async Task<string?> ChangeCustomerPasswordAsync(int custId, string currentPassword, string newPassword)
        {
            Customer? customer = await _context.Customers.FirstOrDefaultAsync(c => c.custID == custId);
            if (customer == null)
            {
                return AccountMissingMessage;
            }

            string? error = await CheckPasswordChangeAsync(customer.custPassword, currentPassword, newPassword);
            if (error != null)
            {
                return error;
            }

            customer.custPassword = _passwordService.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            return null;
        }

        // ---------- Staff ----------

        public async Task<Staff?> GetStaffAsync(int staffId)
        {
            return await _context.StaffMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.staffID == staffId);
        }

        // Staff may only change their name and phone (email, job title and status belong to the admin)
        public async Task<string?> UpdateStaffProfileAsync(int staffId, StaffProfileInput input)
        {
            Staff? staff = await _context.StaffMembers.FirstOrDefaultAsync(s => s.staffID == staffId);
            if (staff == null)
            {
                return AccountMissingMessage;
            }

            staff.staffName = input.staffName.Trim();
            staff.staffPhone = input.staffPhone.Trim();

            await _context.SaveChangesAsync();
            return null;
        }

        public async Task<string?> ChangeStaffPasswordAsync(int staffId, string currentPassword, string newPassword)
        {
            Staff? staff = await _context.StaffMembers.FirstOrDefaultAsync(s => s.staffID == staffId);
            if (staff == null)
            {
                return AccountMissingMessage;
            }

            string? error = await CheckPasswordChangeAsync(staff.staffPassword, currentPassword, newPassword);
            if (error != null)
            {
                return error;
            }

            staff.staffPassword = _passwordService.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            return null;
        }

        // ---------- Admin ----------

        public async Task<Admin?> GetAdminAsync(int adminId)
        {
            return await _context.Admins
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.adminID == adminId);
        }

        // Admins may change their name and sign-in email
        public async Task<string?> UpdateAdminProfileAsync(int adminId, AdminProfileInput input)
        {
            Admin? admin = await _context.Admins.FirstOrDefaultAsync(a => a.adminID == adminId);
            if (admin == null)
            {
                return AccountMissingMessage;
            }

            string newEmail = AuthService.NormaliseEmail(input.adminEmail);
            if (await IsNewEmailTakenAsync(admin.adminEmail, newEmail))
            {
                return EmailTakenMessage;
            }

            admin.adminName = input.adminName.Trim();
            admin.adminEmail = newEmail;

            await _context.SaveChangesAsync();
            return null;
        }

        public async Task<string?> ChangeAdminPasswordAsync(int adminId, string currentPassword, string newPassword)
        {
            Admin? admin = await _context.Admins.FirstOrDefaultAsync(a => a.adminID == adminId);
            if (admin == null)
            {
                return AccountMissingMessage;
            }

            string? error = await CheckPasswordChangeAsync(admin.adminPassword, currentPassword, newPassword);
            if (error != null)
            {
                return error;
            }

            admin.adminPassword = _passwordService.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            return null;
        }

        // ---------- Shared rules ----------

        // True when the email is being CHANGED to one that another account already uses
        private async Task<bool> IsNewEmailTakenAsync(string currentEmail, string newEmail)
        {
            bool emailChanged = newEmail != AuthService.NormaliseEmail(currentEmail);
            return emailChanged && await _authService.IsEmailTakenAsync(newEmail);
        }

        // The same three checks for every role's password change
        private async Task<string?> CheckPasswordChangeAsync(string savedHash, string currentPassword, string newPassword)
        {
            // 1. Prove it's really them
            if (!_passwordService.VerifyPassword(savedHash, currentPassword))
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
            if (_passwordService.VerifyPassword(savedHash, newPassword))
            {
                return "New password must be different from your current password.";
            }

            return null;
        }
    }
}