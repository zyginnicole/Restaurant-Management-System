namespace RestaurantManagementSystem.Helpers
{
    // Shortcuts for the signed-in user. Usable in any PageModel, e.g.:
    //   int? custId = HttpContext.Session.GetUserId();
    //   if (HttpContext.Session.IsSignedInAs(UserRoles.Customer)) { ... }
    public static class SessionUserExtensions
    {
        // Call once, after the email and password have been checked.
        public static void SignInUser(this ISession session, int userId, string role, string userName)
        {
            session.Clear();   // forget any earlier sign-in in this browser first
            session.SetInt32(SessionKeys.UserId, userId);
            session.SetString(SessionKeys.Role, role);
            session.SetString(SessionKeys.UserName, userName);
        }

        // Call when the user clicks Sign out.
        public static void SignOutUser(this ISession session)
        {
            session.Clear();
        }

        public static int? GetUserId(this ISession session)
        {
            return session.GetInt32(SessionKeys.UserId);
        }

        public static string? GetUserRole(this ISession session)
        {
            return session.GetString(SessionKeys.Role);
        }

        public static string? GetUserName(this ISession session)
        {
            return session.GetString(SessionKeys.UserName);
        }

        public static bool IsSignedInAs(this ISession session, string role)
        {
            return session.GetUserRole() == role;
        }
    }
}