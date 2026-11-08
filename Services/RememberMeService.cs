using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // "Remember me" on the customer Sign in page: keeps the customer signed in on this device for 30 days,
    // even after the browser is closed or the session ends.
    // - The cookie holds only "customer ID | password fingerprint", encrypted and signed with ASP.NET Data
    //   Protection and valid for 30 days. It can't be read or changed by the user (a changed cookie is rejected).
    // - The password fingerprint means the cookie stops working as soon as the password is changed or reset.
    // - It is HttpOnly (JavaScript can't read it) and Secure (only sent over HTTPS).
    // - Customers only: staff and admins often use shared restaurant computers, so they always sign in.
    // - Signing out deletes the cookie. A deleted or locked account is never signed back in.
    public class RememberMeService
    {
        public const string CookieName = "Lumiere.RememberMe";
        public const int RememberDays = 30;

        private readonly ApplicationDbContext _context;
        private readonly ITimeLimitedDataProtector _protector;

        public RememberMeService(ApplicationDbContext context, IDataProtectionProvider dataProtection)
        {
            _context = context;
            _protector = dataProtection.CreateProtector("RROMS.RememberMe.v1").ToTimeLimitedDataProtector();
        }

        // After a successful customer sign-in with "Remember me" ticked
        public async Task IssueAsync(HttpContext http, int custId)
        {
            Customer? customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.custID == custId);
            if (customer == null)
            {
                return;
            }

            string token = _protector.Protect($"{customer.custID}|{Fingerprint(customer.custPassword)}",
                                              TimeSpan.FromDays(RememberDays));

            http.Response.Cookies.Append(CookieName, token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Expires = DateTimeOffset.UtcNow.AddDays(RememberDays)
            });
        }

        // Runs on every request (Program.cs): no one signed in + a valid cookie → sign the customer in again
        public async Task TryRestoreAsync(HttpContext http)
        {
            if (http.Session.GetUserRole() != null)
            {
                return;   // someone is already signed in
            }

            if (!http.Request.Cookies.TryGetValue(CookieName, out string? token) || string.IsNullOrEmpty(token))
            {
                return;
            }

            Customer? customer = null;
            try
            {
                string[] parts = _protector.Unprotect(token).Split('|');
                if (parts.Length == 2 && int.TryParse(parts[0], out int custId))
                {
                    customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.custID == custId);

                    // the password was changed or reset since the cookie was made → no longer valid
                    if (customer != null && Fingerprint(customer.custPassword) != parts[1])
                    {
                        customer = null;
                    }
                }
            }
            catch (CryptographicException)
            {
                // changed by hand, expired after 30 days, or made by another computer
            }

            bool locked = customer?.LockoutEnd != null && customer.LockoutEnd > DateTimeOffset.Now;
            if (customer == null || locked || AccountService.IsClosedEmail(customer.custEmail))
            {
                Clear(http);
                return;
            }

            http.Session.SignInUser(customer.custID, UserRoles.Customer, customer.custName);
        }

        // Sign out, or a sign-in without "Remember me"
        public void Clear(HttpContext http)
        {
            http.Response.Cookies.Delete(CookieName, new CookieOptions { Secure = true, SameSite = SameSiteMode.Lax });
        }

        // A short fingerprint of the saved password HASH (never the password itself)
        private static string Fingerprint(string passwordHash)
        {
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(passwordHash));
            return Convert.ToHexString(hash, 0, 16);
        }
    }
}