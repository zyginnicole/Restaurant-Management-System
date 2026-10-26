using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Reservations")]
    public class Reservation
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int reservationID { get; set; }

        // ---- Foreign keys ----
        [ForeignKey(nameof(Customer))]
        public int custID { get; set; }

        [ForeignKey(nameof(DiningTable))]
        public int tableID { get; set; }

        [ForeignKey(nameof(Staff))]
        // allow null: assigned when staff confirms
        public int? staffID { get; set; }

        // ---- Navigation properties ---- 
        public Customer? Customer { get; set; }
        public DiningTable? DiningTable { get; set; }
        public Staff? Staff { get; set; }

        [Required]
        public DateTime reservationDateTime { get; set; }

        [Range(1, 20, ErrorMessage = "Party size must be between 1 and 20.")]
        public int partySize { get; set; }

        public string? specialRequest { get; set; }

        public string reservationStatus { get; set; } = "Pending"; // Pending, Confirmed, Cancelled, Completed (the process ends when guests are seated; the table becomes Occupied on DiningTable)

        public DateTimeOffset reservationCreateAt { get; set; } = DateTimeOffset.Now;

        //allow users to order when making reservation
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}