using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Announcements")]
    public class Announcement
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int announcementID { get; set; }

        // ---- Foreign key: which admin posted it ----
        [ForeignKey(nameof(Admin))]
        public int adminID { get; set; }

        public Admin? Admin { get; set; }

        [Required(ErrorMessage = "Title is required!")]
        [MaxLength(100)]
        public string announcementTitle { get; set; }       // "Kitchen Deep Clean — Monday Closure"

        [Required(ErrorMessage = "Message is required!")]
        [MaxLength(1000)]
        public string announcementMessage { get; set; }

        [Required]
        public string targetAudience { get; set; } = "All"; // All, Staff, Customers

        public bool isPinned { get; set; } = false;         // Pin / Unpin button

        public DateTimeOffset announcementDate { get; set; } = DateTimeOffset.Now;
    }
}