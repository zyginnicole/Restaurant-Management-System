namespace RestaurantManagementSystem.Helpers
{
    // Small text helpers for showing the signed-in user in the sidebars.
    public static class NameFormatter
    {
        // "Sophie Clarke" -> "SC", "Madonna" -> "M", empty -> "?"
        public static string GetInitials(string? fullName)
        {
            string[] parts = (fullName ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
            {
                return "?";
            }

            string first = parts[0].Substring(0, 1);
            string last = parts.Length > 1 ? parts[^1].Substring(0, 1) : string.Empty;

            return (first + last).ToUpperInvariant();
        }
    }
}