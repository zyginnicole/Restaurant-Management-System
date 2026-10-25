using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Creates demo accounts so everyone in the team has the same logins on every PC
    // (each PC has its own LocalDB, so accounts typed on one PC don't exist on another).
    // Runs when the app starts (development only) and only adds an account if its email is not there yet.
    public class DemoAccountSeeder
    {
        // Demo sign-in details: for testing and the viva demo only
        public const string DemoPassword = "Demo@1234";
        public const string AdminEmail = "admin@maisondoree.my";
        public const string StaffEmail = "staff@maisondoree.my";
        public const string InactiveStaffEmail = "inactive@maisondoree.my";
        public const string CustomerEmail = "customer@maisondoree.my";

        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;

        public DemoAccountSeeder(ApplicationDbContext context, PasswordService passwordService)
        {
            _context = context;
            _passwordService = passwordService;
        }

        public async Task SeedAsync()
        {
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
    }
}