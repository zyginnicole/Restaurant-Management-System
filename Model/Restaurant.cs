using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Restaurants")]
    public class Restaurant
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int restaurantID { get; set; }

        [Required(ErrorMessage = "Restaurant name is required!")]
        public string restaurantName { get; set; }

        public string? restaurantAddress { get; set; }

        public string? restaurantPhone { get; set; }

        public ICollection<OperatingHour> OperatingHours { get; set; } = new List<OperatingHour>();

        // ---- Booking slot settings (slots are generated from OperatingHours) ----
        public int slotIntervalMinutes { get; set; } = 30;      // 15, 30 or 60
        public int lastSeatingMinutes { get; set; } = 60;       // last booking = closing time minus this

        public ICollection<BlockedSlot> BlockedSlots { get; set; } = new List<BlockedSlot>();
    }
}