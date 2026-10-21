using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("OperatingHours")]
    public class OperatingHour
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int hourID { get; set; }

        // Foreign key: which restaurant these hours belong to
        public int restaurantID { get; set; }

        public Restaurant? Restaurant { get; set; }

        public DayOfWeek dayOfWeek { get; set; }          // Monday ... Sunday

        public TimeSpan openTime { get; set; }            // e.g. 10:00

        public TimeSpan closeTime { get; set; }           // e.g. 22:00

        public bool isClosed { get; set; } = false;       // true = closed all day
    }
}