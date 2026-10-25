using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Helpers
{
    // The promotion that was applied to one cart line, e.g. WEEKEND15 – "15% off wine flight", RM 10.20 off
    public record AppliedPromo(string Code, string Label, string DiscountType, decimal Value, decimal LineDiscount);

    // Works out promotion discounts. Used by Checkout (price shown + saved on the order lines)
    // and the Menu page (only promotions that are really running today are listed).
    //   Percentage  → discountValue % of the line total
    //   FixedAmount → discountValue off EACH item (never more than the item price)
    //   Complimentary → nothing is taken off the price
    public static class PromotionPricing
    {
        // ON, inside its campaign dates, and today is inside its Mon–Sun window (e.g. Fri–Sun, Sat–Mon)
        public static bool IsRunning(Promotion p, DateTime today)
        {
            if (!p.promoActive) return false;
            if (p.validFrom != null && p.validFrom.Value.Date > today.Date) return false;
            if (p.validUntil != null && p.validUntil.Value.Date < today.Date) return false;

            DayOfWeek d = today.DayOfWeek;
            return p.startDay <= p.endDay
                ? d >= p.startDay && d <= p.endDay
                : d >= p.startDay || d <= p.endDay;        // window that wraps over the weekend
        }

        // Promotions that apply by themselves ("Auto-apply enabled") and are running today, with the dishes they cover
        public static async Task<List<Promotion>> RunningAsync(ApplicationDbContext db, DateTime today)
        {
            var list = await db.Promotions.AsNoTracking()
                .Include(p => p.Menus)
                .Where(p => p.promoActive && p.autoApply
                         && (p.validFrom == null || p.validFrom <= today)
                         && (p.validUntil == null || p.validUntil >= today))
                .ToListAsync();

            return list.Where(p => IsRunning(p, today)).ToList();
        }

        // The best promotion for one dish (the one that takes the most off), or null if none applies
        public static AppliedPromo? BestFor(IEnumerable<Promotion> running, int menuId, decimal unitPrice, int quantity)
        {
            Promotion? best = null;
            decimal bestPerItem = 0m;

            foreach (var p in running)
            {
                if (!p.Menus.Any(m => m.menuID == menuId)) continue;

                decimal perItem = PerItemDiscount(p, unitPrice);
                if (perItem > bestPerItem)
                {
                    best = p;
                    bestPerItem = perItem;
                }
            }

            if (best == null) return null;

            decimal line = LineDiscount(best, unitPrice, quantity);
            return line > 0
                ? new AppliedPromo(best.promoCode, best.promoLabel, best.discountType, best.discountValue ?? 0m, line)
                : null;
        }

        private static decimal PerItemDiscount(Promotion p, decimal unitPrice) => p.discountType switch
        {
            "Percentage" => unitPrice * (p.discountValue ?? 0m) / 100m,
            "FixedAmount" => Math.Min(p.discountValue ?? 0m, unitPrice),
            _ => 0m
        };

        // Amount taken off one order line (quantity × price). Same formula as the Checkout page script.
        public static decimal LineDiscount(Promotion p, decimal unitPrice, int quantity)
        {
            decimal gross = unitPrice * quantity;
            decimal discount = p.discountType switch
            {
                "Percentage" => Math.Round(gross * (p.discountValue ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero),
                "FixedAmount" => (p.discountValue ?? 0m) * quantity,
                _ => 0m
            };
            return Math.Clamp(discount, 0m, gross);
        }
    }
}
