using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Changing a CUSTOMER's email from the Profile page.
    // The new email is NOT saved straight away: a 6-digit code is sent to the NEW address and the email
    // only changes after the customer enters it. So a verified account can never be switched to a fake
    // or mistyped email (which would also undo the sign-up verification).
    // After the change, a security notice goes to the OLD address.
    // Same code rules as sign-up and Forgot password: stored as a hash, 10 minutes, 5 tries, new code once a minute.
    public class EmailChangeService
    {
        public const int CodeMinutes = 10;
        public const int MaxCodeAttempts = 5;
        public const int ResendSeconds = 60;

        private const string ExpiredMessage = "This code has expired. Please change your email on the Profile page again.";

        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly SettingsService _settingsService;
        private readonly EmailService _emailService;
        private readonly AuthService _authService;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<EmailChangeService> _logger;

        public EmailChangeService(ApplicationDbContext context, PasswordService passwordService,
                                  SettingsService settingsService, EmailService emailService,
                                  AuthService authService, IMemoryCache cache,
                                  IWebHostEnvironment environment, ILogger<EmailChangeService> logger)
        {
            _context = context;
            _passwordService = passwordService;
            _settingsService = settingsService;
            _emailService = emailService;
            _authService = authService;
            _cache = cache;
            _environment = environment;
            _logger = logger;
        }

        // The new email waiting for its code (kept in memory, never in the database)
        private class PendingEmailChange
        {
            public string NewEmail { get; set; } = string.Empty;
            public string CodeHash { get; set; } = string.Empty;
            public int Attempts { get; set; }
        }

        // One pending change per customer (the ID comes from the session, never from the form)
        private static string PendingKey(int custId) => "emailchange:" + custId;
        private static string WaitKey(int custId) => "emailchange-wait:" + custId;

        // Checked BEFORE anything is saved: a valid email that no other account (customer, staff or admin) uses
        public async Task<string?> CheckNewEmailAsync(string? newEmail)
        {
            string cleanEmail = AuthService.NormaliseEmail(newEmail);

            if (cleanEmail.Length == 0 || cleanEmail.Length > 100 || !new EmailAddressAttribute().IsValid(cleanEmail))
            {
                return "Please enter a valid email address.";
            }

            if (await _authService.IsEmailTakenAsync(cleanEmail))
            {
                return "This email is already used by another account.";
            }

            return null;
        }

        // The new email waiting for confirmation, or null when there is none
        public string? GetPendingEmail(int custId)
        {
            return _cache.TryGetValue(PendingKey(custId), out PendingEmailChange? pending) ? pending?.NewEmail : null;
        }

        // Step 1: email a code to the NEW address.
        // Error = message to show. DevCode = the code, ONLY on a development computer without email set up.
        public async Task<(string? Error, string? DevCode)> StartAsync(int custId, string? newEmail)
        {
            var pending = new PendingEmailChange { NewEmail = AuthService.NormaliseEmail(newEmail) };
            return await SendCodeAsync(custId, pending);
        }

        // "Send a new code"
        public async Task<(string? Error, string? DevCode)> ResendAsync(int custId)
        {
            if (!_cache.TryGetValue(PendingKey(custId), out PendingEmailChange? pending) || pending == null)
            {
                return (ExpiredMessage, null);
            }

            return await SendCodeAsync(custId, pending);
        }

        // "Keep my current email"
        public void Cancel(int custId)
        {
            _cache.Remove(PendingKey(custId));
        }

        private async Task<(string? Error, string? DevCode)> SendCodeAsync(int custId, PendingEmailChange pending)
        {
            // Stop the button being used to flood someone's inbox
            if (_cache.TryGetValue(WaitKey(custId), out _))
            {
                return ("A code was sent less than a minute ago. Please check your inbox and Spam folder, or wait a moment before asking again.", null);
            }

            Customer? customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.custID == custId);
            if (customer == null)
            {
                return ("Your account could not be found. Please sign in again.", null);
            }

            // Random 6-digit code from the secure random generator, saved only as a hash
            string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            pending.CodeHash = _passwordService.HashPassword(code);
            pending.Attempts = 0;

            if (!_emailService.IsConfigured)
            {
                if (!_environment.IsDevelopment())
                {
                    return ("Email is not available right now, so your email can't be changed. Please try again later.", null);
                }

                Save(custId, pending);
                return (null, code);   // development fallback: show the code on screen
            }

            SystemSetting settings = await _settingsService.GetAsync();
            string brand = WebUtility.HtmlEncode(settings.restaurantName);
            string name = WebUtility.HtmlEncode(customer.custName);

            string body = $@"
<div style=""font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#1a1a1a"">
  <h2 style=""font-weight:normal;letter-spacing:1px"">{brand}</h2>
  <p>Hi {name},</p>
  <p>You asked to use this address for your {brand} account. To confirm it, enter this code on the Confirm new email page:</p>
  <p style=""font-size:32px;font-weight:bold;letter-spacing:8px;margin:24px 0"">{code}</p>
  <p>This code expires in {CodeMinutes} minutes. Until you enter it, your account keeps your current email.</p>
  <p style=""color:#777;font-size:13px"">If you didn't ask for this, you can ignore this email. Nothing will change.</p>
</div>";

            try
            {
                await _emailService.SendAsync(pending.NewEmail, $"Confirm your new email for {settings.restaurantName}", body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email change code could not be sent to {Email}", pending.NewEmail);
                return ("We couldn't send the code to your new email. Please check the address and your internet connection, then try again.", null);
            }

            Save(custId, pending);
            return (null, null);
        }

        private void Save(int custId, PendingEmailChange pending)
        {
            _cache.Set(PendingKey(custId), pending, TimeSpan.FromMinutes(CodeMinutes));
            _cache.Set(WaitKey(custId), true, TimeSpan.FromSeconds(ResendSeconds));
        }

        // Step 2: correct code → save the new email and tell the OLD address.
        // Returns Error (null when it worked) and the new email.
        public async Task<(string? Error, string? NewEmail)> CompleteAsync(int custId, string? code)
        {
            string cleanCode = (code ?? string.Empty).Trim();

            if (!_cache.TryGetValue(PendingKey(custId), out PendingEmailChange? pending) || pending == null)
            {
                return (ExpiredMessage, null);
            }

            if (cleanCode.Length != 6 || !cleanCode.All(char.IsDigit))
            {
                return ("Please enter the 6-digit code from the email.", null);
            }

            if (!_passwordService.VerifyPassword(pending.CodeHash, cleanCode))
            {
                pending.Attempts++;
                int triesLeft = MaxCodeAttempts - pending.Attempts;

                if (triesLeft <= 0)
                {
                    _cache.Remove(PendingKey(custId));
                    return ("Too many incorrect codes. Your email was not changed. Please try again from the Profile page.", null);
                }

                return ($"Incorrect code. {triesLeft} {(triesLeft == 1 ? "try" : "tries")} left.", null);
            }

            _cache.Remove(PendingKey(custId));   // the code works only once

            // Someone may have registered this email while the code was waiting
            if (await _authService.IsEmailTakenAsync(pending.NewEmail))
            {
                return ("This email is now used by another account. Your email was not changed.", null);
            }

            Customer? customer = await _context.Customers.FirstOrDefaultAsync(c => c.custID == custId);
            if (customer == null)
            {
                return ("Your account could not be found. Please sign in again.", null);
            }

            string oldEmail = customer.custEmail;
            customer.custEmail = pending.NewEmail;
            await _context.SaveChangesAsync();

            await SendChangedNoticeAsync(oldEmail, customer.custName, pending.NewEmail);
            return (null, pending.NewEmail);
        }

        // Security notice to the OLD address, so the real owner notices if someone else changed it.
        // Best effort: the email change has already been saved, so a failure here is only logged.
        private async Task SendChangedNoticeAsync(string oldEmail, string customerName, string newEmail)
        {
            if (!_emailService.IsConfigured)
            {
                return;
            }

            try
            {
                SystemSetting settings = await _settingsService.GetAsync();
                string brand = WebUtility.HtmlEncode(settings.restaurantName);
                string name = WebUtility.HtmlEncode(customerName);
                string maskedNewEmail = WebUtility.HtmlEncode(MaskEmail(newEmail));

                string body = $@"
<div style=""font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#1a1a1a"">
  <h2 style=""font-weight:normal;letter-spacing:1px"">{brand}</h2>
  <p>Hi {name},</p>
  <p>The email address for your {brand} account was changed to <strong>{maskedNewEmail}</strong>.
     From now on, please sign in with the new address.</p>
  <p style=""color:#777;font-size:13px"">If you didn't make this change, please contact the restaurant straight away.</p>
</div>";

                await _emailService.SendAsync(oldEmail, $"Your {settings.restaurantName} email was changed", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Email change notice could not be sent to the old address {Email}", oldEmail);
            }
        }

        // "sophia@gmail.com" → "s***@gmail.com" (the notice shouldn't show the full new address)
        private static string MaskEmail(string email)
        {
            int at = email.IndexOf('@');
            return at <= 1 ? email : email[0] + "***" + email.Substring(at);
        }
    }
}