using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Helpers
{
    // The promotion that was applied to one cart line, e.g. WEEKEND15 – "15% off wine flight", RM 10.20 off
    public record AppliedPromo(int PromoId, string Code, string Label, string DiscountType, decimal Value, decimal LineDiscount);

    // One line of an order being priced
    public record PricedLine(int MenuId, decimal UnitPrice, int Quantity);

    // Works out promotion discounts. Which promotions may be used is decided first by PromotionService
    // (PromotionRules + usage limits); this class only does the money.
    //   Percentage    → discountValue % of the line total
    //   FixedAmount   → discountValue off EACH item (never more than the item price)
    //   Complimentary → one unit of the cheapest covered dish on the order is free
    //
    // Stacking:
    //   - a dish line gets at most ONE promotion: the one that takes the most off
    //   - a birthday promotion is never combined with another promotion on the same order;
    //     it is used only when it saves more than the other promotions together (a tie keeps the birthday use for later)
    public static class PromotionPricing
    {
        // The promotion for each line (same order as "lines"; null = none)
        public static AppliedPromo?[] Apply(IReadOnlyList<Promotion> usable, IReadOnlyList<PricedLine> lines)
        {
            var plans = usable
                .Select(p => (Promo: p, Lines: Plan(p, lines)))
                .Where(x => x.Lines.Count > 0)
                .ToList();

            // 1. regular promotions: the best one on each line
            var best = new AppliedPromo?[lines.Count];
            foreach (var (promo, plan) in plans.Where(x => !x.Promo.birthdayOnly))
            {
                foreach (var (i, discount) in plan)
                {
                    if (best[i] == null || discount > best[i]!.LineDiscount) best[i] = Make(promo, discount);
                }
            }
            decimal bestTotal = best.Sum(a => a?.LineDiscount ?? 0m);

            // 2. a birthday promotion on its own
            foreach (var (promo, plan) in plans.Where(x => x.Promo.birthdayOnly))
            {
                decimal total = plan.Values.Sum();
                if (total <= bestTotal) continue;

                best = new AppliedPromo?[lines.Count];
                foreach (var (i, discount) in plan) best[i] = Make(promo, discount);
                bestTotal = total;
            }

            return best;
        }

        // Line index → amount this promotion alone would take off
        private static Dictionary<int, decimal> Plan(Promotion p, IReadOnlyList<PricedLine> lines)
        {
            var plan = new Dictionary<int, decimal>();
            var covered = p.Menus.Select(m => m.menuID).ToHashSet();
            var eligible = Enumerable.Range(0, lines.Count)
                .Where(i => covered.Contains(lines[i].MenuId) && lines[i].Quantity > 0)
                .ToList();

            if (p.discountType == "Complimentary")
            {
                if (eligible.Count > 0)
                {
                    int cheapest = eligible.OrderBy(i => lines[i].UnitPrice).First();
                    plan[cheapest] = lines[cheapest].UnitPrice;
                }
                return plan;
            }

            foreach (int i in eligible)
            {
                decimal discount = LineDiscount(p, lines[i].UnitPrice, lines[i].Quantity);
                if (discount > 0m) plan[i] = discount;
            }
            return plan;
        }

        private static AppliedPromo Make(Promotion p, decimal discount) =>
            new(p.promoID, p.promoCode, p.promoLabel, p.discountType, p.discountValue ?? 0m, discount);

        // Amount taken off one order line (quantity × price). Same formula as the Checkout page script.
        // Complimentary: one unit free.
        public static decimal LineDiscount(Promotion p, decimal unitPrice, int quantity)
        {
            decimal gross = unitPrice * quantity;
            decimal discount = p.discountType switch
            {
                "Percentage" => Math.Round(gross * (p.discountValue ?? 0m) / 100m, 2, MidpointRounding.AwayFromZero),
                "FixedAmount" => (p.discountValue ?? 0m) * quantity,
                "Complimentary" => quantity > 0 ? unitPrice : 0m,
                _ => 0m
            };
            return Math.Clamp(discount, 0m, gross);
        }

        // Subtotal (after promotions), service charge (dine-in only) and SST of one order, rounded like Checkout
        public static (decimal Subtotal, decimal Service, decimal Sst, decimal Total) Totals(
            decimal subtotalAfterPromos, bool dineIn, decimal servicePct, decimal sstPct)
        {
            decimal service = dineIn ? Math.Round(subtotalAfterPromos * servicePct / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
            decimal sst = Math.Round((subtotalAfterPromos + service) * sstPct / 100m, 2, MidpointRounding.AwayFromZero);
            return (subtotalAfterPromos, service, sst, subtotalAfterPromos + service + sst);
        }
    }
}
