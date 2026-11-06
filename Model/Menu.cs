using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Menus")]
    public class Menu
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int menuID { get; set; }

        [Required(ErrorMessage = "Item name is required!")]
        public string menuName { get; set; }

        public string menuDescription { get; set; }

        [Required]
        public int categoryID { get; set; }
        public MenuCategory? MenuCategory { get; set; }          // Appetizer, Main, Dessert, Beverage

        [Range(0.01, 10000, ErrorMessage = "Price must be greater than 0.")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal menuPrice { get; set; }

        public bool isAvailable { get; set; } = true;

        // Admin ticks this for the restaurant's signature dishes
        public bool isSignature { get; set; } = false;

        // Comma-separated, from MenuVocabulary.Tags, e.g. "Vegetarian,Spicy". null = not tagged yet
        public string? menuTags { get; set; }

        // Comma-separated, from MenuVocabulary.Allergens. null = not filled in yet (unknown), "None" = checked, no allergens
        public string? menuAllergens { get; set; }

        [Display(Name = "Menu Image")]
        public string menuImageUrl { get; set; }

        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
    }
}