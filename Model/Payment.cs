using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Payments")]
    public class Payment
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int paymentID { get; set; }

        [ForeignKey(nameof(Order))]
        public int orderID { get; set; }

        public Order? Order { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal paymentAmount { get; set; }

        [Required]
        public string paymentMethod { get; set; }          // Cash, Card, Online Banking, E-Wallet

        [Required]
        public string paymentStatus { get; set; } = "Pending"; // Pending, Paid, Failed, Refunded

        public DateTimeOffset paymentDate { get; set; } = DateTimeOffset.Now;
    }
}