using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestaurantManagementSystem.Model
{
    // One row only: the whole system's settings.
    public class SystemSetting
    {
        [Key]
        public int settingID { get; set; }

        // ---- General ----
        [Required(ErrorMessage = "Restaurant name is required.")]
        [StringLength(100)]
        public string restaurantName { get; set; } = "Maison Laurent";

        [StringLength(150)]
        public string? tagline { get; set; } = "Fine Dining · Est. 1998";

        [StringLength(30)]
        public string? phone { get; set; } = "+60 4-226 1998";

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [StringLength(150)]
        public string? email { get; set; } = "reservations@maisonlaurent.com";

        [StringLength(300)]
        public string? address { get; set; } = "12 Lebuh Pantai, George Town, 10300 Penang";

        [StringLength(60)]
        public string timeZone { get; set; } = "(GMT+8) Kuala Lumpur, Penang";

        // ---- Reservations ----
        [Range(30, 480, ErrorMessage = "Dining duration must be 30–480 minutes.")]
        public int diningMinutes { get; set; } = 120;

        [Range(1, 500, ErrorMessage = "Minimum party size must be at least 1.")]
        public int minPartySize { get; set; } = 1;

        [Range(1, 500, ErrorMessage = "Maximum party size must be at least 1.")]
        public int maxPartySize { get; set; } = 14;

        [Range(1, 365, ErrorMessage = "Booking window must be 1–365 days.")]
        public int bookingWindowDays { get; set; } = 60;

        [Range(0, 168, ErrorMessage = "Minimum notice must be 0–168 hours.")]
        public int minNoticeHours { get; set; } = 2;

        [Range(0, 168, ErrorMessage = "Cancellation window must be 0–168 hours.")]
        public int cancelWindowHours { get; set; } = 24;

        [Range(0, 120, ErrorMessage = "Late hold must be 0–120 minutes.")]
        public int lateHoldMinutes { get; set; } = 15;

        public bool autoConfirm { get; set; } = true;

        // ---- Billing & payments (cashless only: at least one method must stay on) ----
        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100, ErrorMessage = "Service charge must be 0–100%.")]
        public decimal serviceChargePct { get; set; } = 10;

        [Column(TypeName = "decimal(5,2)")]
        [Range(0, 100, ErrorMessage = "SST must be 0–100%.")]
        public decimal sstPct { get; set; } = 6;

        [StringLength(200)]
        public string? receiptFooter { get; set; } = "Thank you for dining with us.";

        public bool acceptCard { get; set; } = true;
        public bool acceptFpx { get; set; } = true;
        public bool acceptEwallet { get; set; } = false;

        // ---- Security ----
        [Range(6, 64, ErrorMessage = "Minimum password length must be 6–64.")]
        public int passwordMinLength { get; set; } = 8;

        public bool requireUpper { get; set; } = true;
        public bool requireNumber { get; set; } = true;
        public bool requireSymbol { get; set; } = false;

        [Range(3, 20, ErrorMessage = "Failed attempts must be 3–20.")]
        public int maxFailedLogins { get; set; } = 5;

        [Range(5, 480, ErrorMessage = "Idle timeout must be 5–480 minutes.")]
        public int idleTimeoutMinutes { get; set; } = 30;
    }
}