using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Admin")]
    public class Admin
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int adminID { get; set; }

        [Required(ErrorMessage = "Full name is required!")]
        public string adminName { get; set; }

        [Required(ErrorMessage = "Email is required!")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string adminEmail { get; set; }

        [Required(ErrorMessage = "Password is required!")]
        [DataType(DataType.Password)]
        public string adminPassword { get; set; } 

        public DateTimeOffset adminCreateAt { get; set; } = DateTimeOffset.Now;

        // Navigation (one admin -> many)
        public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
        public ICollection<Announcement> Announcements { get; set; } = new List<Announcement>();
    }
}