namespace RestaurantManagementSystem.Services
{
    // The "Email" section of appsettings.json. UserName and Password are left empty there
    // and come from User Secrets on each computer, so the Gmail App Password is never pushed to GitHub.
    public class EmailSettings
    {
        public string Host { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public string UserName { get; set; } = string.Empty;   // the Gmail address that sends the emails
        public string Password { get; set; } = string.Empty;   // the 16-letter Gmail App Password
    }
}