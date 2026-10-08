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

        public bool promoActive { get; set; } = true;          // the ON/OFF toggle

        public DateTimeOffset promoCreateAt { get; set; } = DateTimeOffset.Now;
    }
}