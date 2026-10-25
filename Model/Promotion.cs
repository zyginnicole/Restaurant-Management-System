using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace RestaurantManagementSystem.Model
{
    [Table("Promotions")]
    [Index(nameof(promoCode), IsUnique = true)]          // no two promotions can share a code
    public class Promotion
    {
        [Key] // Sets this as the Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Enforces Auto-Increment
        public int promoID { get; set; }

        // Foreign key: which admin created it(?
        [ForeignKey(nameof(Admin))]
        public int adminID { get; set; }

        public Admin? Admin { get; set; }

        [Required(ErrorMessage = "Promotion title is required!")]
        public string promoTitle { get; set; }              // "Weekend Tasting Menu"

        public string? promoDescription { get; set; }       // "5-course tasting experience..."

        [Required]
        public string promoLabel { get; set; }              // text badge: "15% off wine flight"

        [Required(ErrorMessage = "Promo code is required!")]
        [MaxLength(20)]
        public string promoCode { get; set; }               // "WEEKEND15"

        // ---- Discount logic (used when auto-applying at order time) ----
        public string discountType { get; set; } = "Percentage";   // Percentage, FixedAmount, Complimentary

        [Column(TypeName = "decimal(10,2)")]
        public decimal? discountValue { get; set; }         // 15 (%) or 20.00 ($); null for complimentary

        public bool autoApply { get; set; } = false;        // "Auto-apply enabled / off"

        // ---- Availability, default Monday to Sun, can define yourself
        public DayOfWeek startDay { get; set; } = DayOfWeek.Monday;
        public DayOfWeek endDay { get; set; } = DayOfWeek.Sunday;

        public DateTime? validFrom { get; set; }            // optional campaign dates
        public DateTime? validUntil { get; set; }

        // ---- Time of day (optional: fill in both or neither). An end earlier than the start means "past midnight".
        public TimeSpan? startTime { get; set; }
        public TimeSpan? endTime { get; set; }

        // ---- Extra conditions (all optional)
        [MaxLength(10)]
        public string orderType { get; set; } = "Any";      // Any, DineIn, Takeaway (anything else is treated as Any)

        [Column(TypeName = "decimal(10,2)")]
        public decimal? minSpend { get; set; }              // minimum order subtotal (RM) before the discount; null = none

        public bool birthdayOnly { get; set; } = false;     // only valid in the customer's birthday month

        // ---- Usage limits (null = unlimited)
        public int? maxUsesPerCustomer { get; set; }        // e.g. 1 = each customer can use it once
        public bool limitResetsYearly { get; set; } = false;// true = the per-customer count starts again every 1 January
        public int? maxTotalUses { get; set; }              // across all customers

        public bool promoActive { get; set; } = true;          // the ON/OFF toggle

        public DateTimeOffset promoCreateAt { get; set; } = DateTimeOffset.Now;
        public ICollection<Menu> Menus { get; set; } = new List<Menu>();
    }
}