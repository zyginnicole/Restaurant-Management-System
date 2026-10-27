using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Services
{
    // The fields a customer is allowed to change on their Profile page.
    // A separate class (not the Customer entity), so the form can never change the ID,
    // password, lockout fields or creation date (no overposting).
    // Property names match the form fields: Customer.custName, Customer.custEmail, ...
    public class CustomerProfileInput
    {
        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(100, ErrorMessage = "Full name cannot be longer than 100 characters.")]
        public string custName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string custEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Phone number must be 10 to 11 digits (numbers only).")]
        public string custPhone { get; set; } = string.Empty;

        // Ticked boxes (names come from MenuVocabulary). The service joins them into Customer.DietaryPref / Customer.Allergies.
        public List<string> Prefs { get; set; } = new();
        public List<string> Allergies { get; set; } = new();

        public bool promoOptIn { get; set; }

        // Birthday for birthday offers. Can only be added while the account has none; once saved it can't be
        // changed here (so nobody can move it to get the birthday promotion again). ProfileService enforces this.
        [DataType(DataType.Date)]
        public DateTime? custBirthday { get; set; }
    }
}