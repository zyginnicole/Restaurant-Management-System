using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Feedbacks")]
    public class Feedback
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int feedbackID { get; set; }

        [ForeignKey(nameof(Customer))]
        public int custID { get; set; }

        [ForeignKey(nameof(Order))]
        public int orderID { get; set; }                   // feedback is for a completed dining experience

        public Customer? Customer { get; set; }
        public Order? Order { get; set; }

        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5.")]
        public int rating { get; set; }

        [MaxLength(500, ErrorMessage = "Comment cannot exceed 500 characters.")]
        public string? comment { get; set; }

        public DateTimeOffset feedbackDate { get; set; } = DateTimeOffset.Now;
    }
}