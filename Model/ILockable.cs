namespace RestaurantManagementSystem.Model
{
    // Anything that can sign in and be locked after too many wrong passwords
    // (Customer, Staff, Admin). AuthService uses this for one shared lockout routine.
    public interface ILockable
    {
        int FailedLoginAttempts { get; set; }
        DateTimeOffset? LockoutEnd { get; set; }
    }
}