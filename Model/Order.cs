using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Orders")]
    public class Order
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int orderID { get; set; }

        // ---- Foreign keys ----
        [ForeignKey(nameof(Customer))]
        public int custID { get; set; }

        [ForeignKey(nameof(Reservation))]
        // allow null values: takeaway orders have no reservation
        public int? reservationID { get; set; }            

        [ForeignKey(nameof(Staff))]
        public int? staffID { get; set; }                  // staff who processes the order

        // ---- Navigation properties ----
        public Customer? Customer { get; set; }
        public Reservation? Reservation { get; set; }
        public Staff? Staff { get; set; }
        public Payment? Payment { get; set; }              // one-to-one
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        public DateTimeOffset orderDateTime { get; set; } = DateTimeOffset.Now;

        [Required]
        public string orderStatus { get; set; } = "Preparing"; // Preparing, Ready to Serve, Served, Completed, Cancelled

        [Column(TypeName = "decimal(10,2)")]
        public decimal totalAmount { get; set; }
    }
}