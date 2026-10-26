using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    // Junction table: resolves the many-to-many relationship between Orders and Menus.
    [Table("OrderItems")]
    public class OrderItem
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int orderItemID { get; set; }

        [ForeignKey(nameof(Order))]
        public int orderID { get; set; }

        [ForeignKey(nameof(Menu))]
        public int menuID { get; set; }

        public Order? Order { get; set; }
        public Menu? Menu { get; set; }

        [Range(1, 100)]
        public int quantity { get; set; } = 1;

        // Price at time of ordering (so later menu price changes don't alter old orders)
        [Column(TypeName = "decimal(10,2)")]
        public decimal unitPrice { get; set; }

        // Kitchen note for this dish, e.g. "Medium rare" (entered on Checkout)
        [MaxLength(200)]
        public string? orderRemark { get; set; }

        // Promotion applied to this line at checkout (null = none): its code, and the amount taken off the
        // line total (quantity × unitPrice). Saved here so old receipts never change if the promotion changes.
        [MaxLength(20)]
        public string? promoCode { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal discountAmount { get; set; } = 0m;
    }
}