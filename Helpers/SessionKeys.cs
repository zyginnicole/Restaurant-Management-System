namespace RestaurantManagementSystem.Helpers
{
    // The names used to store the signed-in user in the session.
    // Everyone uses these constants instead of typing the text, so a typo can't break sign-in.
    public static class SessionKeys
    {
        public const string UserId = "UserId";
        public const string Role = "Role";
        public const string UserName = "UserName";
        public const string Cart = "CustomerCart";
    }

    // The three roles in the system (Role-Based Access Control).
    public static class UserRoles
    {
        public const string Customer = "Customer";
        public const string Staff = "Staff";
        public const string Admin = "Admin";
    }
}