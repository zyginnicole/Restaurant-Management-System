using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Services
{
    // The fields an administrator may change on My profile.
    // Property names match the form fields: Admin.adminName, Admin.adminEmail
    public class AdminProfileInput
    {
        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(100, ErrorMessage = "Full name cannot be longer than 100 characters.")]
        public string adminName { get; set; } = string.Empty;

        // The email is the sign-in name, so it must stay unique (checked in ProfileService)
        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [MaxLength(100, ErrorMessage = "Email cannot be longer than 100 characters.")]
        public string adminEmail { get; set; } = string.Empty;
    }
}