using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    // A booking slot the admin has stopped customers from booking.
    // Existing reservations at the slot are NOT touched.
    [Table("BlockedSlots")]
    public class BlockedSlot
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int blockedSlotID { get; set; }

        // Foreign key: which restaurant this block belongs to
        public int restaurantID { get; set; }

        public Restaurant? Restaurant { get; set; }

        public DateTime slotStart { get; set; }           // date + start time, e.g. 2026-10-24 19:30
    }
}