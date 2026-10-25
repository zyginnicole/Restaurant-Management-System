using System.ComponentModel.DataAnnotations;

namespace RestaurantManagementSystem.Services
{
    // The fields a staff member may change on My profile. Email, job title and active status
    // are managed by an administrator, so they are not in this class and can't be posted.
    // Property names match the form fields: Staff.staffName, Staff.staffPhone
    public class StaffProfileInput
    {
        [Required(ErrorMessage = "Full name is required.")]
        [MaxLength(100, ErrorMessage = "Full name cannot be longer than 100 characters.")]
        public string staffName { get; set; } = string.Empty;

        // The Staff table allows at most 11 characters, so the same 10–11 digit rule as customers
        [Required(ErrorMessage = "Phone number is required.")]
        [RegularExpression(@"^\d{10,11}$", ErrorMessage = "Phone number must be 10 to 11 digits (numbers only).")]
        public string staffPhone { get; set; } = string.Empty;
    }
}