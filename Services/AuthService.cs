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

        // ---------- Lockout (shared by customer, staff and admin) ----------

        // Returns a "locked" result if the account is locked right now, otherwise null
        private static LoginResult? CheckLocked(ILockable account)
        {
            DateTimeOffset now = DateTimeOffset.Now;
            if (account.LockoutEnd.HasValue && account.LockoutEnd.Value > now)
            {
                int minutesLeft = (int)Math.Ceiling((account.LockoutEnd.Value - now).TotalMinutes);
                return LoginResult.Failed(
                    $"This account is locked after too many failed attempts. Try again in {minutesLeft} minute(s).");
            }

            return null;
        }

        // Counts one wrong password. Locks the account when the limit from Settings is reached.
        private async Task<LoginResult> RegisterFailureAsync(ILockable account)
        {
            int maxAttempts = (await _settings.GetAsync()).maxFailedLogins;

            account.FailedLoginAttempts++;

            if (account.FailedLoginAttempts >= maxAttempts)
            {
                account.FailedLoginAttempts = 0;
                account.LockoutEnd = DateTimeOffset.Now.AddMinutes(LockoutMinutes);
                await _context.SaveChangesAsync();

                return LoginResult.Failed(
                    $"Too many failed attempts. Your account is locked for {LockoutMinutes} minutes.");
            }

            await _context.SaveChangesAsync();

            int attemptsLeft = maxAttempts - account.FailedLoginAttempts;
            return LoginResult.Failed(
                $"{WrongDetailsMessage} {attemptsLeft} attempt(s) left before the account is locked for {LockoutMinutes} minutes.");
        }

        // Correct password: clear the counters
        private async Task ClearFailuresAsync(ILockable account)
        {
            account.FailedLoginAttempts = 0;
            account.LockoutEnd = null;
            await _context.SaveChangesAsync();
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

        // ---------- Guest profiles (walk-ins booked by staff) ----------
        // Staff › Book reservations saves a walk-in guest as a Customer with name, email, phone and an EMPTY password
        // (ReservationService.CreateWalkInAsync). It can't sign in until the guest registers with that email.

        public static bool IsGuestProfile(Customer customer) => string.IsNullOrEmpty(customer.custPassword);

        // The guest profile that uses this email, or null
        public async Task<Customer?> FindGuestProfileAsync(string email)
        {
            string normalised = NormaliseEmail(email);
            Customer? customer = await _context.Customers.FirstOrDefaultAsync(c => c.custEmail == normalised);
            return customer != null && IsGuestProfile(customer) ? customer : null;
        }

        // Register with the email of a guest profile: turn that same row into a full account,
        // so the bookings staff made for this guest are already in it.
        public async Task ActivateGuestProfileAsync(Customer guestProfile, Customer form)
        {
            guestProfile.custName = form.custName.Trim();
            guestProfile.custPhone = form.custPhone.Trim();
            guestProfile.DietaryPref = form.DietaryPref;
            guestProfile.promoOptIn = form.promoOptIn;
            guestProfile.custPassword = _passwordService.HashPassword(form.custPassword);
            guestProfile.FailedLoginAttempts = 0;
            guestProfile.LockoutEnd = null;
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

            // 1b. Guest profile made by staff for a walk-in: no password yet (not counted as a failed attempt)
            if (IsGuestProfile(customer))
            {
                return LoginResult.Failed(
                    "Our staff made a booking with this email. Please register with it to set your password.");
            }

            // 2. Account is locked right now
            LoginResult? locked = CheckLocked(customer);
            if (locked != null)
            {
                return locked;
            }

            // 3. Wrong password: count it, lock when the limit from Settings is reached
            if (!_passwordService.VerifyPassword(customer.custPassword, password))
            {
                return await RegisterFailureAsync(customer);
            }

            // 4. Correct password: clear the counters
            await ClearFailuresAsync(customer);

            return LoginResult.Success(customer.custID, UserRoles.Customer, customer.custName);
        }

        // ---------- Staff Portal (Staff and Administrator) ----------

        // Checks an ADMIN email + password (Administrator tab). Counts failed attempts like customers.
        public async Task<LoginResult> LoginAdminAsync(string email, string password)
        {
            string normalised = NormaliseEmail(email);

            Admin? admin = await _context.Admins
                .FirstOrDefaultAsync(a => a.adminEmail == normalised);

            if (admin == null)
            {
                return LoginResult.Failed(WrongDetailsMessage);
            }

            LoginResult? locked = CheckLocked(admin);
            if (locked != null)
            {
                return locked;
            }

            if (!_passwordService.VerifyPassword(admin.adminPassword, password))
            {
                return await RegisterFailureAsync(admin);
            }

            await ClearFailuresAsync(admin);

            return LoginResult.Success(admin.adminID, UserRoles.Admin, admin.adminName);
        }

        // Checks a STAFF email + password (Staff tab). Counts failed attempts like customers.
        // Deactivated staff are refused.
        public async Task<LoginResult> LoginStaffAsync(string email, string password)
        {
            string normalised = NormaliseEmail(email);

            Staff? staff = await _context.StaffMembers
                .FirstOrDefaultAsync(s => s.staffEmail == normalised);

            if (staff == null)
            {
                return LoginResult.Failed(WrongDetailsMessage);
            }

            LoginResult? locked = CheckLocked(staff);
            if (locked != null)
            {
                return locked;
            }

            if (!_passwordService.VerifyPassword(staff.staffPassword, password))
            {
                return await RegisterFailureAsync(staff);
            }

            // Only say "deactivated" AFTER the password is right, so strangers learn nothing
            if (!staff.staffActive)
            {
                return LoginResult.Failed("This account has been deactivated. Please contact management.");
            }

            await ClearFailuresAsync(staff);

            return LoginResult.Success(staff.staffID, UserRoles.Staff, staff.staffName);
        }
    }
}