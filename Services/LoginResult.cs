namespace RestaurantManagementSystem.Services
{
    // What AuthService sends back to a login page after checking an email + password.
    public class LoginResult
    {
        public bool Succeeded { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;

        // Only filled in when Succeeded is true
        public int UserId { get; private set; }
        public string Role { get; private set; } = string.Empty;
        public string UserName { get; private set; } = string.Empty;

        public static LoginResult Success(int userId, string role, string userName)
        {
            return new LoginResult
            {
                Succeeded = true,
                UserId = userId,
                Role = role,
                UserName = userName
            };
        }

        public static LoginResult Failed(string errorMessage)
        {
            return new LoginResult
            {
                Succeeded = false,
                ErrorMessage = errorMessage
            };
        }
    }
}