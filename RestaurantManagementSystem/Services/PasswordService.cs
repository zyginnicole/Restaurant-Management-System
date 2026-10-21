using Microsoft.AspNetCore.Identity;

namespace RestaurantManagementSystem.Services
{
    // Turns passwords into one-way hashes before they are saved, and checks a typed
    // password against the saved hash at sign-in. Plain passwords are never stored.
    // Uses ASP.NET Core's built-in PasswordHasher (PBKDF2 with a random salt per password).
    public class PasswordService
    {
        private readonly PasswordHasher<object> _hasher = new PasswordHasher<object>();

        // PasswordHasher asks "which user is this for?" but doesn't use it,
        // so we pass the same empty placeholder every time.
        private static readonly object NoUser = new object();

        // Used when registering or changing a password: returns the hash to save.
        public string HashPassword(string plainPassword)
        {
            return _hasher.HashPassword(NoUser, plainPassword);
        }

        // Used at sign-in: true if the typed password matches the saved hash.
        public bool VerifyPassword(string savedHash, string typedPassword)
        {
            try
            {
                PasswordVerificationResult result =
                    _hasher.VerifyHashedPassword(NoUser, savedHash, typedPassword);

                // SuccessRehashNeeded = correct password, saved with older settings
                return result == PasswordVerificationResult.Success
                    || result == PasswordVerificationResult.SuccessRehashNeeded;
            }
            catch (FormatException)
            {
                // The saved value isn't a hash (e.g. a test account made before hashing existed)
                return false;
            }
        }
    }
}