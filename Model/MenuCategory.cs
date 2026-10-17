using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("MenuCategories")]
    public class MenuCategory
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int categoryID { get; set; }

        [Required(ErrorMessage = "Category name is required!")]
        public string categoryName { get; set; }          // Appetizer, Main, Dessert, Beverage

        public ICollection<Menu> Menus { get; set; } = new List<Menu>();
    }
}