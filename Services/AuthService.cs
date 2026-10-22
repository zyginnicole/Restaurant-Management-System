using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Business logic for accounts: register, log in, lockout.
    // Pages call this service instead of using the database directly (3-tier).
    public class AuthService
    {
        // Wrong passwords in a row -> account locked for this many minutes.
        // How many wrong passwords are allowed comes from Admin > Settings > Security.
        public const int LockoutMinutes = 15;

        private const string WrongDetailsMessage = "Incorrect email or password.";

        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly SettingsService _settings;

        public AuthService(ApplicationDbContext context, PasswordService passwordService, SettingsService settings)
        {
            _context = context;
            _passwordService = passwordService;
            _settings = settings;
        }

        // Same email format everywhere: no spaces around it, all lowercase
        public static string NormaliseEmail(string? email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        // True if ANY account (customer, staff or admin) already uses this email
        public async Task<bool> IsEmailTakenAsync(string email)
        {
            string normalised = NormaliseEmail(email);

            return await _context.Customers.AnyAsync(c => c.custEmail == normalised)
                || await _context.StaffMembers.AnyAsync(s => s.staffEmail == normalised)
                || await _context.Admins.AnyAsync(a => a.adminEmail == normalised);
        }

        // ---------- Customer ----------

        // Saves a new customer. The password is hashed BEFORE it reaches the database.
        public async Task RegisterCustomerAsync(Customer customer)
        {
            customer.custID = 0; // the database picks the ID, never the form
            customer.custName = customer.custName.Trim();
            customer.custEmail = NormaliseEmail(customer.custEmail);
            customer.custPhone = customer.custPhone.Trim();
            customer.custPassword = _passwordService.HashPassword(customer.custPassword);
            customer.custCreateAt = DateTimeOffset.Now;
            customer.FailedLoginAttempts = 0;
            customer.LockoutEnd = null;

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }

        // Checks a customer's email + password and counts failed attempts.
        public async Task<LoginResult> LoginCustomerAsync(string email, string password)
        {
            string normalised = NormaliseEmail(email);

            Customer? customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.custEmail == normalised);

            // 1. No account with this email
            if (customer == null)
            {
                return LoginResult.Failed(WrongDetailsMessage);
            }

            // 2. Account is locked right now
            DateTimeOffset now = DateTimeOffset.Now;
            if (customer.LockoutEnd.HasValue && customer.LockoutEnd.Value > now)
            {
                int minutesLeft = (int)Math.Ceiling((customer.LockoutEnd.Value - now).TotalMinutes);
                return LoginResult.Failed(
                    $"This account is locked after too many failed attempts. Try again in {minutesLeft} minute(s).");
            }

            // 3. Wrong password: count it, lock when the limit from Settings is reached
            if (!_passwordService.VerifyPassword(customer.custPassword, password))
            {
                int maxAttempts = (await _settings.GetAsync()).maxFailedLogins;

                customer.FailedLoginAttempts++;

                if (customer.FailedLoginAttempts >= maxAttempts)
                {
                    customer.FailedLoginAttempts = 0;
                    customer.LockoutEnd = now.AddMinutes(LockoutMinutes);
                    await _context.SaveChangesAsync();

                    return LoginResult.Failed(
                        $"Too many failed attempts. Your account is locked for {LockoutMinutes} minutes.");
                }

                await _context.SaveChangesAsync();

                int attemptsLeft = maxAttempts - customer.FailedLoginAttempts;
                return LoginResult.Failed(
                    $"{WrongDetailsMessage} {attemptsLeft} attempt(s) left before the account is locked for {LockoutMinutes} minutes.");
            }

            // 4. Correct password: clear the counters
            customer.FailedLoginAttempts = 0;
            customer.LockoutEnd = null;
            await _context.SaveChangesAsync();

            return LoginResult.Success(customer.custID, UserRoles.Customer, customer.custName);
        }

        // ---------- Staff Portal (Staff and Administrator) ----------

        // Checks an ADMIN email + password (Administrator tab).
        public async Task<LoginResult> LoginAdminAsync(string email, string password)
        {
            string normalised = NormaliseEmail(email);

            Admin? admin = await _context.Admins
                .FirstOrDefaultAsync(a => a.adminEmail == normalised);

            if (admin == null || !_passwordService.VerifyPassword(admin.adminPassword, password))
            {
                return LoginResult.Failed(WrongDetailsMessage);
            }

            return LoginResult.Success(admin.adminID, UserRoles.Admin, admin.adminName);
        }

        // Checks a STAFF email + password (Staff tab). Deactivated staff are refused.
        public async Task<LoginResult> LoginStaffAsync(string email, string password)
        {
            string normalised = NormaliseEmail(email);

            Staff? staff = await _context.StaffMembers
                .FirstOrDefaultAsync(s => s.staffEmail == normalised);

            if (staff == null || !_passwordService.VerifyPassword(staff.staffPassword, password))
            {
                return LoginResult.Failed(WrongDetailsMessage);
            }

            // Only say "deactivated" AFTER the password is right, so strangers learn nothing
            if (!staff.staffActive)
            {
                return LoginResult.Failed("This account has been deactivated. Please contact management.");
            }

            return LoginResult.Success(staff.staffID, UserRoles.Staff, staff.staffName);
        }
    }
}