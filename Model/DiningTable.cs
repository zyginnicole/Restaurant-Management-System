using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    // Class is named DiningTable (not "Table") to avoid clashing with [Table] attribute and the SQL keyword.
    [Table("Tables")]
    public class DiningTable
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int tableID { get; set; }

        [Required]
        public string tableNumber { get; set; }          // e.g. "T01"

        [Range(1, 20, ErrorMessage = "Capacity must be between 1 and 20.")]
        public int tableCapacity { get; set; }

        [Required]
        public int areaID { get; set; }
        public DiningArea? DiningArea { get; set; } 

        [Required]
        public string tableStatus { get; set; } = "Available"; // Available, Occupied, Reserved, Maintenance

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
