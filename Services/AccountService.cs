using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Business logic for Admin → Accounts: list staff, administrators and customers,
    // create staff / administrator accounts, edit them, deactivate / reactivate staff,
    // reset passwords and unlock customers.
    // Methods that change something return null (or a null Error) when it worked, otherwise a message.
    public class AccountService
    {
        public const string RoleStaff = "Staff";
        public const string RoleAdministrator = "Administrator";
        public const int CustomersPerPage = 20;

        private const string NotFoundMessage = "That account could not be found. Please refresh the page.";
        private const string PhoneMessage = "Phone number must be 10 to 11 digits (numbers only).";

        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly SettingsService _settingsService;
        private readonly AuthService _authService;

        public AccountService(ApplicationDbContext context, PasswordService passwordService,
                              SettingsService settingsService, AuthService authService)
        {
            _context = context;
            _passwordService = passwordService;
            _settingsService = settingsService;
            _authService = authService;
        }

        // ---------- Reading ----------

        public async Task<AccountStats> GetStatsAsync()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            DateTimeOffset weekAgo = now.AddDays(-7);

            return new AccountStats
            {
                CustomerTotal = await _context.Customers.CountAsync(),
                CustomersNewThisWeek = await _context.Customers.CountAsync(c => c.custCreateAt >= weekAgo),
                StaffActive = await _context.StaffMembers.CountAsync(s => s.staffActive),
                StaffDeactivated = await _context.StaffMembers.CountAsync(s => !s.staffActive),
                AdminTotal = await _context.Admins.CountAsync(),
                LockedCustomers = await _context.Customers.CountAsync(c => c.LockoutEnd != null && c.LockoutEnd > now)
            };
        }

        // Staff tab: administrators first, then staff (active before deactivated), each sorted by name
        public async Task<List<StaffAccountRow>> GetStaffAccountsAsync()
        {
            List<Admin> admins = await _context.Admins
                .AsNoTracking()
                .OrderBy(a => a.adminName)
                .ToListAsync();

            List<Staff> staff = await _context.StaffMembers
                .AsNoTracking()
                .OrderByDescending(s => s.staffActive)
                .ThenBy(s => s.staffName)
                .ToListAsync();

            var rows = new List<StaffAccountRow>();

            foreach (Admin a in admins)
            {
                rows.Add(new StaffAccountRow
                {
                    AccountId = $"A-{a.adminID}",
                    Name = a.adminName,
                    Email = a.adminEmail,
                    Role = RoleAdministrator,
                    Active = true
                });
            }

            foreach (Staff s in staff)
            {
                rows.Add(new StaffAccountRow
                {
                    AccountId = $"S-{s.staffID}",
                    Name = s.staffName,
                    Email = s.staffEmail,
                    Phone = s.staffPhone,
                    JobTitle = s.staffRole,
                    Role = RoleStaff,
                    Active = s.staffActive
                });
            }

            return rows;
        }

        // Customers tab: one page (newest first), optionally filtered by name or email
        public async Task<(List<CustomerAccountRow> Rows, int Total)> GetCustomersAsync(string? search, int page)
        {
            IQueryable<Customer> query = _context.Customers.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string text = search.Trim();
                query = query.Where(c => c.custName.Contains(text) || c.custEmail.Contains(text));
            }

            int total = await query.CountAsync();

            List<CustomerAccountRow> rows = await query
                .OrderByDescending(c => c.custCreateAt)
                .Skip((page - 1) * CustomersPerPage)
                .Take(CustomersPerPage)
                .Select(c => new CustomerAccountRow
                {
                    CustId = c.custID,
                    Name = c.custName,
                    Email = c.custEmail,
                    Phone = c.custPhone,
                    MemberSince = c.custCreateAt,
                    Visits = c.Reservations.Count(r => r.reservationStatus == "Completed"),
                    PromoOptIn = c.promoOptIn,
                    LockedUntil = c.LockoutEnd
                })
                .ToListAsync();

            return (rows, total);
        }

        // ---------- Changing ----------

        // Creates a Staff row or an Admin row (the system role decides the table; it can't be switched later).
        // No password is typed by the admin: a random temporary password is created, only its HASH is saved,
        // and the plain one is returned so it can be shown to the admin once.
        public async Task<(string? Error, string? TemporaryPassword)> CreateAccountAsync(
            string? name, string? email, string? phone, string? jobTitle, string? systemRole)
        {
            string cleanName = (name ?? string.Empty).Trim();
            string cleanEmail = AuthService.NormaliseEmail(email);
            string cleanPhone = (phone ?? string.Empty).Trim();
            string cleanTitle = (jobTitle ?? string.Empty).Trim();
            bool isAdmin = systemRole == RoleAdministrator;

            string? error = CheckName(cleanName);
            if (error != null)
            {
                return (error, null);
            }

            if (cleanEmail.Length == 0 || cleanEmail.Length > 100 || !new EmailAddressAttribute().IsValid(cleanEmail))
            {
                return ("Please enter a valid work email (up to 100 characters).", null);
            }

            if (await _authService.IsEmailTakenAsync(cleanEmail))
            {
                return ("This email is already used by another account.", null);
            }

            if (!isAdmin)
            {
                error = CheckPhone(cleanPhone) ?? CheckJobTitle(cleanTitle);
                if (error != null)
                {
                    return (error, null);
                }
            }

            string temporaryPassword = await GenerateTemporaryPasswordAsync();
            string hash = _passwordService.HashPassword(temporaryPassword);

            if (isAdmin)
            {
                _context.Admins.Add(new Admin
                {
                    adminName = cleanName,
                    adminEmail = cleanEmail,
                    adminPassword = hash,
                    adminCreateAt = DateTimeOffset.Now
                });
            }
            else
            {
                _context.StaffMembers.Add(new Staff
                {
                    staffName = cleanName,
                    staffEmail = cleanEmail,
                    staffPhone = cleanPhone,
                    staffRole = cleanTitle,
                    staffActive = true,
                    staffPassword = hash,
                    staffCreateAt = DateTimeOffset.Now
                });
            }

            await _context.SaveChangesAsync();
            return (null, temporaryPassword);
        }

        // Edit pop-up: staff → name, phone, job title · administrator → name only.
        // Email (sign-in name) and system role are never changed here.
        public async Task<string?> UpdateAccountAsync(string? accountId, string? name, string? phone, string? jobTitle)
        {
            string cleanName = (name ?? string.Empty).Trim();
            string? error = CheckName(cleanName);
            if (error != null)
            {
                return error;
            }

            if (!TryParseAccountId(accountId, out bool isAdmin, out int id))
            {
                return NotFoundMessage;
            }

            if (isAdmin)
            {
                Admin? admin = await _context.Admins.FirstOrDefaultAsync(a => a.adminID == id);
                if (admin == null)
                {
                    return NotFoundMessage;
                }

                admin.adminName = cleanName;
            }
            else
            {
                string cleanPhone = (phone ?? string.Empty).Trim();
                string cleanTitle = (jobTitle ?? string.Empty).Trim();
                error = CheckPhone(cleanPhone) ?? CheckJobTitle(cleanTitle);
                if (error != null)
                {
                    return error;
                }

                Staff? staff = await _context.StaffMembers.FirstOrDefaultAsync(s => s.staffID == id);
                if (staff == null)
                {
                    return NotFoundMessage;
                }

                staff.staffName = cleanName;
                staff.staffPhone = cleanPhone;
                staff.staffRole = cleanTitle;
            }

            await _context.SaveChangesAsync();
            return null;
        }

        // Deactivate (active = false) or reactivate (active = true) a STAFF account.
        // Deactivated staff can't sign in (AuthService checks staffActive); their past records stay.
        public async Task<string?> SetStaffActiveAsync(string? accountId, bool active)
        {
            if (!TryParseAccountId(accountId, out bool isAdmin, out int id) || isAdmin)
            {
                return "Only staff accounts can be deactivated or reactivated.";
            }

            Staff? staff = await _context.StaffMembers.FirstOrDefaultAsync(s => s.staffID == id);
            if (staff == null)
            {
                return NotFoundMessage;
            }

            staff.staffActive = active;
            await _context.SaveChangesAsync();
            return null;
        }

        // Gives a staff or administrator account a new random temporary password (old one stops working at once)
        public async Task<(string? Error, string? Name, string? TemporaryPassword)> ResetPasswordAsync(string? accountId)
        {
            if (!TryParseAccountId(accountId, out bool isAdmin, out int id))
            {
                return (NotFoundMessage, null, null);
            }

            string temporaryPassword = await GenerateTemporaryPasswordAsync();
            string hash = _passwordService.HashPassword(temporaryPassword);
            string name;

            if (isAdmin)
            {
                Admin? admin = await _context.Admins.FirstOrDefaultAsync(a => a.adminID == id);
                if (admin == null)
                {
                    return (NotFoundMessage, null, null);
                }

                admin.adminPassword = hash;
                name = admin.adminName;
            }
            else
            {
                Staff? staff = await _context.StaffMembers.FirstOrDefaultAsync(s => s.staffID == id);
                if (staff == null)
                {
                    return (NotFoundMessage, null, null);
                }

                staff.staffPassword = hash;
                name = staff.staffName;
            }

            await _context.SaveChangesAsync();
            return (null, name, temporaryPassword);
        }

        // Clears a customer's lockout so they can sign in again straight away
        public async Task<string?> UnlockCustomerAsync(int custId)
        {
            Customer? customer = await _context.Customers.FirstOrDefaultAsync(c => c.custID == custId);
            if (customer == null)
            {
                return NotFoundMessage;
            }

            customer.FailedLoginAttempts = 0;
            customer.LockoutEnd = null;
            await _context.SaveChangesAsync();
            return null;
        }

        // ---------- Helpers ----------

        // "S-12" → staff 12 · "A-1" → admin 1. The prefix says which table, because both start their IDs at 1.
        private static bool TryParseAccountId(string? accountId, out bool isAdmin, out int id)
        {
            isAdmin = false;
            id = 0;

            if (string.IsNullOrEmpty(accountId) || accountId.Length < 3 || accountId[1] != '-')
            {
                return false;
            }

            char prefix = accountId[0];
            if (prefix != 'S' && prefix != 'A')
            {
                return false;
            }

            isAdmin = prefix == 'A';
            return int.TryParse(accountId.Substring(2), out id);
        }

        private static string? CheckName(string name)
        {
            return name.Length == 0 || name.Length > 100
                ? "Please enter a full name (up to 100 characters)."
                : null;
        }

        // The Staff table allows at most 11 characters, so 10–11 digits like everywhere else
        private static string? CheckPhone(string phone)
        {
            return Regex.IsMatch(phone, @"^\d{10,11}$") ? null : PhoneMessage;
        }

        private static string? CheckJobTitle(string jobTitle)
        {
            return jobTitle.Length == 0 || jobTitle.Length > 50
                ? "Please enter a job title (up to 50 characters)."
                : null;
        }

        // A random password that always meets the Settings rules: at least 12 characters (or the minimum
        // length from Settings, if longer) with an uppercase letter, a lowercase letter, a number and a symbol.
        // RandomNumberGenerator is the secure random generator (System.Random is predictable).
        private async Task<string> GenerateTemporaryPasswordAsync()
        {
            SystemSetting settings = await _settingsService.GetAsync();
            int length = Math.Max(12, settings.passwordMinLength);

            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // no I or O (easy to misread)
            const string lower = "abcdefghijkmnpqrstuvwxyz";   // no l
            const string digits = "23456789";                   // no 0 or 1
            const string symbols = "@$!%*?&";
            string all = upper + lower + digits + symbols;

            var chars = new List<char>
            {
                upper[RandomNumberGenerator.GetInt32(upper.Length)],
                lower[RandomNumberGenerator.GetInt32(lower.Length)],
                digits[RandomNumberGenerator.GetInt32(digits.Length)],
                symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
            };

            while (chars.Count < length)
            {
                chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
            }

            // Shuffle, so the four required characters aren't always at the start
            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars.ToArray());
        }
    }
}