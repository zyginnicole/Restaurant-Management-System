using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Services
{
    // The fields a customer is allowed to change on their Profile page.
    // A separate class (not the Customer entity), so the form can never change the ID,
    // password, lockout fields or creation date (no overposting).
    // Property names match the form fields: Customer.custName, Customer.custEmail, ...
    public class CustomerProfileInput
    {
        // Same list as the Register page's dropdown
        public static readonly string[] DietaryOptions =
            { "None", "Vegetarian", "Vegan", "Halal", "Gluten-free", "Nut allergy", "Seafood allergy", "Other" };

        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(100, ErrorMessage = "Full name cannot be longer than 100 characters.")]
        public string custName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string custEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Phone number must be 10 to 11 digits (numbers only).")]
        public string custPhone { get; set; } = string.Empty;

        public string DietaryPref { get; set; } = "None";

        public bool promoOptIn { get; set; }
    }
}