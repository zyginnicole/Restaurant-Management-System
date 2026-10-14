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
        // 5 wrong passwords in a row -> account locked for 15 minutes
        public const int MaxFailedAttempts = 5;
        public const int LockoutMinutes = 15;

        private const string WrongDetailsMessage = "Incorrect email or password.";

        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;

        public AuthService(ApplicationDbContext context, PasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
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

        // Saves a new customer. The password is hashed BEFORE it reaches the database.
        public async Task RegisterCustomerAsync(Customer customer)
        {
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

            // 3. Wrong password: count it, lock on the 5th
            if (!_passwordService.VerifyPassword(customer.custPassword, password))
            {
                customer.FailedLoginAttempts++;

                if (customer.FailedLoginAttempts >= MaxFailedAttempts)
                {
                    customer.FailedLoginAttempts = 0;
                    customer.LockoutEnd = now.AddMinutes(LockoutMinutes);
                    await _context.SaveChangesAsync();

                    return LoginResult.Failed(
                        $"Too many failed attempts. Your account is locked for {LockoutMinutes} minutes.");
                }

                await _context.SaveChangesAsync();

                int attemptsLeft = MaxFailedAttempts - customer.FailedLoginAttempts;
                return LoginResult.Failed(
                    $"{WrongDetailsMessage} {attemptsLeft} attempt(s) left before the account is locked for {LockoutMinutes} minutes.");
            }

            // 4. Correct password: clear the counters
            customer.FailedLoginAttempts = 0;
            customer.LockoutEnd = null;
            await _context.SaveChangesAsync();

            return LoginResult.Success(customer.custID, UserRoles.Customer, customer.custName);
        }
    }
}