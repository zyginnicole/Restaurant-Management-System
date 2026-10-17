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
    }
}