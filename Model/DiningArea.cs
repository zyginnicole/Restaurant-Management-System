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

        public ICollection<DiningTable> DiningTables { get; set; } = new List<DiningTable>();
    }
}