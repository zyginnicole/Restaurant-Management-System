using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Staff")]
    public class Staff : ILockable
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int staffID { get; set; }

        [Required(ErrorMessage = "Full name is required!")]
        public string staffName { get; set; }

        [Required(ErrorMessage = "Email is required!")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string staffEmail { get; set; }

        [Required(ErrorMessage = "Password is required!")]
        [DataType(DataType.Password)]
        public string staffPassword { get; set; }

        [Required]
        [MaxLength(11, ErrorMessage = "Maximum 11 digits including the leading 0.")]
        public string staffPhone { get; set; }

        public string staffRole { get; set; }

        public bool staffActive { get; set; } = true;

        public DateTimeOffset staffCreateAt { get; set; } = DateTimeOffset.Now;

        // Lockout: wrong passwords in a row (limit from Admin > Settings > Security)
        public int FailedLoginAttempts { get; set; } = 0;
        public DateTimeOffset? LockoutEnd { get; set; }

        // Navigation (one staff handles many reservations / orders)
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}