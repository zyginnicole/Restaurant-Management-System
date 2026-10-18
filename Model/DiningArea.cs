using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("DiningAreas")]
    public class DiningArea
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int areaID { get; set; }

        [Required(ErrorMessage = "Area name is required!")]
        public string areaName { get; set; }              // Indoor, Outdoor, VIP

        [StringLength(200)]
        public string? areaDescription { get; set; }

        // Inactive areas are closed for reservations (Admin > Restaurant > Dining Areas switch)
        public bool areaActive { get; set; } = true;

        public ICollection<DiningTable> DiningTables { get; set; } = new List<DiningTable>();
    }
}