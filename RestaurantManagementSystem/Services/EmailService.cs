using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace RestaurantManagementSystem.Services
{
    // Sends emails through Gmail (SMTP). Shared: any module can use it (password reset, promotions, ...).
    // The sender name is the restaurant name from Admin > Settings.
    public class EmailService
    {
        private readonly EmailSettings _settings;
        private readonly SettingsService _settingsService;

        public EmailService(IOptions<EmailSettings> options, SettingsService settingsService)
        {
            _settings = options.Value;
            _settingsService = settingsService;
        }

        // False on computers where the Gmail address / App Password haven't been added to User Secrets
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_settings.UserName) && !string.IsNullOrWhiteSpace(_settings.Password);

        // Throws an exception if the email can't be sent (no internet, wrong App Password, ...)
        public async Task SendAsync(string toEmail, string subject, string htmlBody)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("Email is not set up on this computer (see User Secrets).");
            }

            string restaurantName = (await _settingsService.GetAsync()).restaurantName;

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.UserName, restaurantName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = true,   // Gmail only accepts encrypted connections
                // Google shows the App Password with spaces ("abcd efgh ..."), so remove them
                Credentials = new NetworkCredential(_settings.UserName, _settings.Password.Replace(" ", ""))
            };

            await client.SendMailAsync(message);
        }
    }
}