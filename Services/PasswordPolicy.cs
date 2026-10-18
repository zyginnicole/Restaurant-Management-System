using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Checks a new password against the rules set in Admin > Settings > Security.
    public static class PasswordPolicy
    {
        // Returns null when the password is fine, otherwise the message to show
        public static string? Check(string? password, SystemSetting settings)
        {
            if (string.IsNullOrEmpty(password) || password.Length < settings.passwordMinLength)
                return $"Password must be at least {settings.passwordMinLength} characters.";

            if (settings.requireUpper && !password.Any(char.IsUpper))
                return "Password must contain an uppercase letter.";

            if (settings.requireNumber && !password.Any(char.IsDigit))
                return "Password must contain a number.";

            if (settings.requireSymbol && password.All(char.IsLetterOrDigit))
                return "Password must contain a symbol.";

            return null;
        }

        // Short sentence for the register / change-password page, e.g.
        // "Use at least 8 characters, an uppercase letter, a number."
        public static string Describe(SystemSetting settings)
        {
            var parts = new List<string> { $"at least {settings.passwordMinLength} characters" };
            if (settings.requireUpper) parts.Add("an uppercase letter");
            if (settings.requireNumber) parts.Add("a number");
            if (settings.requireSymbol) parts.Add("a symbol");
            return "Use " + string.Join(", ", parts) + ".";
        }
    }
}