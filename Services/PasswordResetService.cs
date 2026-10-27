using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Forgot password for CUSTOMERS, using a 6-digit code (OTP) sent to their email.
    // - The code is kept in server memory (IMemoryCache) for 10 minutes, stored only as a HASH.
    // - 5 wrong tries and the code is cancelled; a new code can be requested once a minute.
    // - An email with no account behaves exactly like a real one, so nobody can use this page
    //   to find out which emails are registered.
    // No database change is needed. (Staff/admin passwords are reset by an administrator in Admin → Accounts.)
    public class PasswordResetService
    {
        public const int CodeMinutes = 10;
        public const int MaxCodeAttempts = 5;
        public const int ResendSeconds = 60;

        private const string ExpiredMessage = "This code has expired or is no longer valid. Please request a new code.";

        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly SettingsService _settingsService;
        private readonly EmailService _emailService;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<PasswordResetService> _logger;

        public PasswordResetService(ApplicationDbContext context, PasswordService passwordService,
                                    SettingsService settingsService, EmailService emailService,
                                    IMemoryCache cache, IWebHostEnvironment environment,
                                    ILogger<PasswordResetService> logger)
        {
            _context = context;
            _passwordService = passwordService;
            _settingsService = settingsService;
            _emailService = emailService;
            _cache = cache;
            _environment = environment;
            _logger = logger;
        }

        // What is kept in memory for one email address
        private class ResetCode
        {
            public string CodeHash { get; set; } = string.Empty;
            public int Attempts { get; set; }
        }

        private static string CodeKey(string email) => "pwreset-code:" + email;
        private static string WaitKey(string email) => "pwreset-wait:" + email;

        // Step 1: create a code and email it.
        // Error = message to show. DevCode = the code, ONLY on a development computer without email set up
        // (so teammates without the Gmail App Password can still test).
        public async Task<(string? Error, string? DevCode)> RequestCodeAsync(string? email)
        {
            string cleanEmail = AuthService.NormaliseEmail(email);
            if (cleanEmail.Length == 0)
            {
                return ("Please enter your email address.", null);
            }

            // Stop the button being used to flood someone's inbox
            if (_cache.TryGetValue(WaitKey(cleanEmail), out _))
            {
                return ("A code was sent less than a minute ago. Please check your inbox and Spam folder, or wait a moment before asking again.", null);
            }
            _cache.Set(WaitKey(cleanEmail), true, TimeSpan.FromSeconds(ResendSeconds));

            // Random 6-digit code from the secure random generator, saved as a hash (like a password)
            string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            _cache.Set(CodeKey(cleanEmail),
                       new ResetCode { CodeHash = _passwordService.HashPassword(code) },
                       TimeSpan.FromMinutes(CodeMinutes));

            Customer? customer = await _context.Customers
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.custEmail == cleanEmail);

            if (customer == null)
            {
                return (null, null);   // no email is sent, but the page looks exactly the same
            }

            if (!_emailService.IsConfigured)
            {
                if (_environment.IsDevelopment())
                {
                    return (null, code);   // development fallback: show the code on screen
                }

                return ("Email is not available right now. Please contact the restaurant.", null);
            }

            SystemSetting settings = await _settingsService.GetAsync();
            string brand = WebUtility.HtmlEncode(settings.restaurantName);
            string name = WebUtility.HtmlEncode(customer.custName);

            string body = $@"
<div style=""font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#1a1a1a"">
  <h2 style=""font-weight:normal;letter-spacing:1px"">{brand}</h2>
  <p>Hi {name},</p>
  <p>We received a request to reset the password for your {brand} account. Your verification code is:</p>
  <p style=""font-size:32px;font-weight:bold;letter-spacing:8px;margin:24px 0"">{code}</p>
  <p>This code expires in {CodeMinutes} minutes. Never share it with anyone. Our staff will never ask for it.</p>
  <p style=""color:#777;font-size:13px"">If you didn't ask to reset your password, you can ignore this email. Your password will not change.</p>
</div>";

            try
            {
                await _emailService.SendAsync(customer.custEmail, $"Your {settings.restaurantName} password reset code", body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Password reset email could not be sent to {Email}", cleanEmail);
                _cache.Remove(CodeKey(cleanEmail));
                _cache.Remove(WaitKey(cleanEmail));
                return ("We couldn't send the email right now. Please check your internet connection and try again.", null);
            }

            return (null, null);
        }

        // Step 2: check the code, then save the new password. Returns null when it worked, otherwise the message.
        public async Task<string?> ResetPasswordAsync(string? email, string? code, string? newPassword)
        {
            string cleanEmail = AuthService.NormaliseEmail(email);
            string cleanCode = (code ?? string.Empty).Trim();

            if (!_cache.TryGetValue(CodeKey(cleanEmail), out ResetCode? entry) || entry == null)
            {
                return ExpiredMessage;
            }

            if (cleanCode.Length != 6 || !cleanCode.All(char.IsDigit))
            {
                return "Please enter the 6-digit code from the email.";
            }

            if (!_passwordService.VerifyPassword(entry.CodeHash, cleanCode))
            {
                entry.Attempts++;
                int triesLeft = MaxCodeAttempts - entry.Attempts;

                if (triesLeft <= 0)
                {
                    _cache.Remove(CodeKey(cleanEmail));
                    return "Too many incorrect codes. Please request a new code.";
                }

                return $"Incorrect code. {triesLeft} {(triesLeft == 1 ? "try" : "tries")} left.";
            }

            // Code is correct. A weak password doesn't use up the code, so they can just try again.
            string? policyError = PasswordPolicy.Check(newPassword, await _settingsService.GetAsync());
            if (policyError != null)
            {
                return policyError;
            }

            Customer? customer = await _context.Customers.FirstOrDefaultAsync(c => c.custEmail == cleanEmail);
            if (customer == null)
            {
                _cache.Remove(CodeKey(cleanEmail));
                return ExpiredMessage;
            }

            if (_passwordService.VerifyPassword(customer.custPassword, newPassword!))
            {
                return "Your new password must be different from your current one.";
            }

            customer.custPassword = _passwordService.HashPassword(newPassword!);
            customer.FailedLoginAttempts = 0;   // a locked-out customer can sign in straight away
            customer.LockoutEnd = null;
            await _context.SaveChangesAsync();

            _cache.Remove(CodeKey(cleanEmail));   // the code works only once
            return null;
        }
    }
}