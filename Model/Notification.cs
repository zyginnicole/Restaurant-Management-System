using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    //Each notification is a single row in the NotificationLogs table.
    //Each is linked to a single customer via custID.
    [Table("Notifications")]
    public class Notification
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int notificationID { get; set; }

        // ---- Foreign key: which customer receives it ----
        [ForeignKey(nameof(Customer))]
        public int custID { get; set; }

        public Customer? Customer { get; set; }

        [Required]
        [MaxLength(30)]
        public string notificationCategory { get; set; } = "General";   // Reservation, Order, Payment, General

        [Required(ErrorMessage = "Title is required!")]
        [MaxLength(100)]
        public string notificationTitle { get; set; } = string.Empty;   // "Order ORD-0088 is being prepared"

        [Required(ErrorMessage = "Message is required!")]
        [MaxLength(1000)]
        public string notificationMessage { get; set; } = string.Empty;

        //Page the customer can open from the notification
        [MaxLength(200)]
        public string? linkUrl { get; set; }                            // page opened on click, e.g. "/Customer/Orders"

        public bool isRead { get; set; } = false;                       // false = unread (Unread tab / bell badge)

        public DateTimeOffset notificationCreateAt { get; set; } = DateTimeOffset.Now;
    }
}