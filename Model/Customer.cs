using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    [Table("Customer")]
    public class Customer : ILockable
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int custID { get; set; }

        [Required(ErrorMessage = "Full name is required!")]
        public string custName { get; set; }

        [Required(ErrorMessage = "Email is required!")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string custEmail { get; set; }

        [Required(ErrorMessage = "Password is required!")]
        [DataType(DataType.Password)]
        public string custPassword { get; set; }

        [Required]
        [MaxLength(11, ErrorMessage = "Maximum 11 digits including the leading 0.")]
        public string custPhone { get; set; }

        public DateTimeOffset custCreateAt { get; set; } = DateTimeOffset.Now;

        // Optional. Used for birthday promotions (valid during the customer's birthday month)
        [DataType(DataType.Date)]
        [Column(TypeName = "date")]
        public DateTime? custBirthday { get; set; }

        // Comma-separated dish tags the customer likes (MenuVocabulary.Tags), e.g. "Vegetarian,Spicy". "None" = no preference
        public string DietaryPref { get; set; } = "None";

        // Comma-separated allergens (MenuVocabulary.Allergens). Dishes containing them are never recommended. "None" = no allergies
        public string Allergies { get; set; } = "None";

        public bool promoOptIn { get; set; } = false;

        public int FailedLoginAttempts { get; set; } = 0;
        public DateTimeOffset? LockoutEnd { get; set; }

        // Navigation (one customer -> many)
        //the List goes on the "one" side, the side that has many of something.
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
    }
}