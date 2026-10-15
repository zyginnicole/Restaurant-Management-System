using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Customers")]
    public class Customer
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int custID { get; set; }

        [Required(ErrorMessage = "Full name is required!")]
        public string custName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required!")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string custEmail { get; set; } = string.Empty;

        [Required]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Password must be at least 8 characters with one uppercase letter, one lowercase letter, one number, and one special character.")]
        [DataType(DataType.Password)]
        public string custPassword { get; set; } = string.Empty;

        [Required]
        [MaxLength(11, ErrorMessage = "Maximum 11 digits including the leading 0.")]
        public string custPhone { get; set; } = string.Empty;

        public DateTimeOffset custCreateAt { get; set; } = DateTimeOffset.Now;

        public string DietaryPref { get; set; } = "None";

        // ---- Promotions: ticked "Send me promotions and member offers" at registration ----
        public bool promoOptIn { get; set; } = false;

        // ---- Login lockout: 3 wrong passwords locks the account for 5 minutes ----
        public int failedLoginCount { get; set; } = 0;
        public DateTimeOffset? lockoutEnd { get; set; }       // null = not locked

        // Navigation (one customer -> many)
        //the List goes on the "one" side, the side that has many of something.
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    }
}