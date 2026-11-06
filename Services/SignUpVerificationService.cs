using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Sign-up email verification for CUSTOMERS.
    // The account is NOT saved when the Register form is sent. The details wait in server memory
    // (password already hashed) and a 6-digit code is emailed. Only the correct code creates the account,
    // so an email nobody can open (fake or mistyped) never becomes an account.
    // Same rules as Forgot password: code stored as a hash, 10 minutes, 5 tries, a new code once a minute.
    public class SignUpVerificationService
    {
        public const int CodeMinutes = 10;
        public const int MaxCodeAttempts = 5;
        public const int ResendSeconds = 60;

        private const string ExpiredMessage = "This code has expired. Please register again.";

        private readonly ApplicationDbContext _context;
        private readonly PasswordService _passwordService;
        private readonly SettingsService _settingsService;
        private readonly EmailService _emailService;
        private readonly AuthService _authService;
        private readonly IMemoryCache _cache;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<SignUpVerificationService> _logger;

        public SignUpVerificationService(ApplicationDbContext context, PasswordService passwordService,
                                         SettingsService settingsService, EmailService emailService,
                                         AuthService authService, IMemoryCache cache,
                                         IWebHostEnvironment environment, ILogger<SignUpVerificationService> logger)
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

        // The Register form details waiting for the code (kept in memory, never in the database)
        private class PendingSignUp
        {
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public DateTime? Birthday { get; set; }
            public string DietaryPref { get; set; } = "None";
            public string Allergies { get; set; } = "None";
            public bool PromoOptIn { get; set; }
            public string PasswordHash { get; set; } = string.Empty;   // hashed straight away, like a saved password
            public string CodeHash { get; set; } = string.Empty;
            public int Attempts { get; set; }
        }

        private static string PendingKey(string email) => "signup-pending:" + email;
        private static string WaitKey(string email) => "signup-wait:" + email;

        // Step 1: the Register form passed all checks → keep the details and email a code.
        // The page has already copied the ticked boxes into DietaryPref / Allergies and set promoOptIn.
        // Error = message to show. DevCode = the code, ONLY on a development computer without email set up.
        public async Task<(string? Error, string? DevCode)> StartAsync(Customer form)
        {
            var pending = new PendingSignUp
            {
                Name = (form.custName ?? string.Empty).Trim(),
                Email = AuthService.NormaliseEmail(form.custEmail),
                Phone = (form.custPhone ?? string.Empty).Trim(),
                Birthday = form.custBirthday,
                DietaryPref = string.IsNullOrEmpty(form.DietaryPref) ? "None" : form.DietaryPref,
                Allergies = string.IsNullOrEmpty(form.Allergies) ? "None" : form.Allergies,
                PromoOptIn = form.promoOptIn,
                PasswordHash = _passwordService.HashPassword(form.custPassword)
            };

            return await SendCodeAsync(pending);
        }

        // "Send a new code" on the Verify email page
        public async Task<(string? Error, string? DevCode)> ResendAsync(string? email)
        {
            string cleanEmail = AuthService.NormaliseEmail(email);
            if (!_cache.TryGetValue(PendingKey(cleanEmail), out PendingSignUp? pending) || pending == null)
            {
                return (ExpiredMessage, null);
            }

            return await SendCodeAsync(pending);
        }

        private async Task<(string? Error, string? DevCode)> SendCodeAsync(PendingSignUp pending)
        {
            // Stop the button being used to flood someone's inbox
            if (_cache.TryGetValue(WaitKey(pending.Email), out _))
            {
                return ("A code was sent less than a minute ago. Please check your inbox and Spam folder, or wait a moment before asking again.", null);
            }

            // Random 6-digit code from the secure random generator, saved only as a hash
            string code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            pending.CodeHash = _passwordService.HashPassword(code);
            pending.Attempts = 0;

            if (!_emailService.IsConfigured)
            {
                if (!_environment.IsDevelopment())
                {
                    return ("Email is not available right now, so new accounts can't be created. Please try again later.", null);
                }

                Save(pending);
                return (null, code);   // development fallback: show the code on screen
            }

            SystemSetting settings = await _settingsService.GetAsync();
            string brand = WebUtility.HtmlEncode(settings.restaurantName);
            string name = WebUtility.HtmlEncode(pending.Name);

            string body = $@"
<div style=""font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#1a1a1a"">
  <h2 style=""font-weight:normal;letter-spacing:1px"">{brand}</h2>
  <p>Hi {name},</p>
  <p>Thank you for registering with {brand}. To confirm this is your email address, enter this code:</p>
  <p style=""font-size:32px;font-weight:bold;letter-spacing:8px;margin:24px 0"">{code}</p>
  <p>This code expires in {CodeMinutes} minutes. Your account is created once the code is entered.</p>
  <p style=""color:#777;font-size:13px"">If you didn't register with us, you can ignore this email. No account will be created.</p>
</div>";

            try
            {
                await _emailService.SendAsync(pending.Email, $"Verify your email for {settings.restaurantName}", body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sign-up verification email could not be sent to {Email}", pending.Email);
                return ("We couldn't send the verification email. Please check the email address and your internet connection, then try again.", null);
            }

            Save(pending);
            return (null, null);
        }

        private void Save(PendingSignUp pending)
        {
            _cache.Set(PendingKey(pending.Email), pending, TimeSpan.FromMinutes(CodeMinutes));
            _cache.Set(WaitKey(pending.Email), true, TimeSpan.FromSeconds(ResendSeconds));
        }

        // Step 2: correct code → create the account, or activate the guest profile staff made for this email.
        // Returns Error (null when it worked) and whether it was a guest profile (for the message on Sign in).
        public async Task<(string? Error, bool WasGuestProfile)> CompleteAsync(string? email, string? code)
        {
            string cleanEmail = AuthService.NormaliseEmail(email);
            string cleanCode = (code ?? string.Empty).Trim();

            if (!_cache.TryGetValue(PendingKey(cleanEmail), out PendingSignUp? pending) || pending == null)
            {
                return (ExpiredMessage, false);
            }

            if (cleanCode.Length != 6 || !cleanCode.All(char.IsDigit))
            {
                return ("Please enter the 6-digit code from the email.", false);
            }

            if (!_passwordService.VerifyPassword(pending.CodeHash, cleanCode))
            {
                pending.Attempts++;
                int triesLeft = MaxCodeAttempts - pending.Attempts;

                if (triesLeft <= 0)
                {
                    _cache.Remove(PendingKey(cleanEmail));
                    return ("Too many incorrect codes. Please register again.", false);
                }

                return ($"Incorrect code. {triesLeft} {(triesLeft == 1 ? "try" : "tries")} left.", false);
            }

            _cache.Remove(PendingKey(cleanEmail));   // the code works only once

            // Walk-in guest profile with this email (staff booked for them): it becomes their full account,
            // so the bookings staff made are already in it (same fields as AuthService.ActivateGuestProfileAsync)
            Customer? guestProfile = await _authService.FindGuestProfileAsync(cleanEmail);
            if (guestProfile != null)
            {
                guestProfile.custName = pending.Name;
                guestProfile.custPhone = pending.Phone;
                guestProfile.DietaryPref = pending.DietaryPref;
                guestProfile.Allergies = pending.Allergies;
                guestProfile.custBirthday ??= pending.Birthday;   // staff never enter a birthday
                guestProfile.promoOptIn = pending.PromoOptIn;
                guestProfile.custPassword = pending.PasswordHash;
                guestProfile.FailedLoginAttempts = 0;
                guestProfile.LockoutEnd = null;
                await _context.SaveChangesAsync();
                return (null, true);
            }

            // Someone registered this email while the code was waiting
            if (await _authService.IsEmailTakenAsync(cleanEmail))
            {
                return ("This email is already registered. Please sign in, or use Forgot password.", false);
            }

            _context.Customers.Add(new Customer
            {
                custName = pending.Name,
                custEmail = pending.Email,
                custPhone = pending.Phone,
                custBirthday = pending.Birthday,
                DietaryPref = pending.DietaryPref,
                Allergies = pending.Allergies,
                promoOptIn = pending.PromoOptIn,
                custPassword = pending.PasswordHash,
                custCreateAt = DateTimeOffset.Now,
                FailedLoginAttempts = 0,
                LockoutEnd = null
            });
            await _context.SaveChangesAsync();
            return (null, false);
        }
    }
}