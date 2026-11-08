using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Creates demo accounts so everyone in the team has the same logins on every PC
    // (each PC has its own LocalDB, so accounts typed on one PC don't exist on another).
    // Runs when the app starts (development only) and only adds an account if its email is not there yet.
    // The first administrator is created here (the "bootstrap" account); after that, only an administrator
    // can create staff or administrator accounts (Admin → Accounts).
    public class DemoAccountSeeder
    {
        // Demo sign-in details: for testing and the viva demo only
        public const string DemoPassword = "Demo@1234";
        public const string AdminEmail = "admin@lumiere.my";
        public const string StaffEmail = "staff@lumiere.my";
        public const string InactiveStaffEmail = "inactive@lumiere.my";
        public const string CustomerEmail = "customer@lumiere.my";

        // The demo emails before the restaurant was renamed to Lumière. Databases that already have them get
        // them renamed in place, so they keep the same accounts (and their bookings) instead of duplicates.
        private static readonly (string OldEmail, string NewEmail)[] RenamedEmails =
        {
            ("admin@maisondoree.my", AdminEmail),
            ("staff@maisondoree.my", StaffEmail),
            ("inactive@maisondoree.my", InactiveStaffEmail),
            ("customer@maisondoree.my", CustomerEmail)
        };

        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;

        public DemoAccountSeeder(ApplicationDbContext context, PasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        public async Task SeedAsync()
        {
            await RenameOldDemoEmailsAsync();

            // Administrator (same name the dashboard design uses)
            if (!await _context.Admins.AnyAsync(a => a.adminEmail == AdminEmail))
            {
                _context.Admins.Add(new Admin
                {
                    adminName = "Richard Beaumont",
                    adminEmail = AdminEmail,
                    adminPassword = _passwordService.HashPassword(DemoPassword),
                    adminCreateAt = DateTimeOffset.Now
                });
            }

            // Active staff member
            if (!await _context.StaffMembers.AnyAsync(s => s.staffEmail == StaffEmail))
            {
                _context.StaffMembers.Add(new Staff
                {
                    staffName = "Sophie Clarke",
                    staffEmail = StaffEmail,
                    staffPassword = _passwordService.HashPassword(DemoPassword),
                    staffPhone = "0123456789",
                    staffRole = "Senior Waiter",
                    staffActive = true,
                    staffCreateAt = DateTimeOffset.Now
                });
            }

            // Deactivated staff member: used to test that inactive staff cannot sign in
            if (!await _context.StaffMembers.AnyAsync(s => s.staffEmail == InactiveStaffEmail))
            {
                _context.StaffMembers.Add(new Staff
                {
                    staffName = "Marcus Lee",
                    staffEmail = InactiveStaffEmail,
                    staffPassword = _passwordService.HashPassword(DemoPassword),
                    staffPhone = "0129876543",
                    staffRole = "Waiter",
                    staffActive = false,
                    staffCreateAt = DateTimeOffset.Now
                });
            }

            // Customer (same name the customer pages' design uses), for testing reservations, orders, etc.
            if (!await _context.Customers.AnyAsync(c => c.custEmail == CustomerEmail))
            {
                _context.Customers.Add(new Customer
                {
                    custName = "Alexandra Beaumont",
                    custEmail = CustomerEmail,
                    custPassword = _passwordService.HashPassword(DemoPassword),
                    custPhone = "0198765432",
                    DietaryPref = "None",
                    promoOptIn = true,
                    custCreateAt = DateTimeOffset.Now
                });
            }

            await _context.SaveChangesAsync();
        }

        // admin@maisondoree.my → admin@lumiere.my, and so on (only if the new email isn't used yet)
        private async Task RenameOldDemoEmailsAsync()
        {
            foreach (var (oldEmail, newEmail) in RenamedEmails)
            {
                Admin? admin = await _context.Admins.FirstOrDefaultAsync(a => a.adminEmail == oldEmail);
                if (admin != null && !await _context.Admins.AnyAsync(a => a.adminEmail == newEmail))
                {
                    admin.adminEmail = newEmail;
                }

                Staff? staff = await _context.StaffMembers.FirstOrDefaultAsync(s => s.staffEmail == oldEmail);
                if (staff != null && !await _context.StaffMembers.AnyAsync(s => s.staffEmail == newEmail))
                {
                    staff.staffEmail = newEmail;
                }

                Customer? customer = await _context.Customers.FirstOrDefaultAsync(c => c.custEmail == oldEmail);
                if (customer != null && !await _context.Customers.AnyAsync(c => c.custEmail == newEmail))
                {
                    customer.custEmail = newEmail;
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}