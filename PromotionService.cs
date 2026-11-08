using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services
{
    // Who an order is for, as far as promotions care
    public record PromoBuyer(int? CustId, bool IsMember, DateTime? Birthday)
    {
        public static readonly PromoBuyer Guest = new(null, false, null);
    }

    // Server-side promotion checks. Customer Checkout, staff Orders, staff Billing and the Payment page all use this,
    // so a rule hidden in the page can't be skipped by posting the form by hand.
    //   1. PromotionRules: on/off, campaign dates, days, time of day, birthday month (Malaysia time)
    //   2. members only: birthday and limited promotions need a real customer account
    //   3. order type and minimum spend (subtotal before discounts)
    //   4. usage limits: only PAID orders that are not Cancelled count (see PromotionRules.UsageWindowStart)
    // Usage is checked again when the bill is paid (RecheckLimitsAsync), because two unpaid orders could both
    // have taken the last use.
    public class PromotionService
    {
        private readonly ApplicationDbContext _context;

        public PromotionService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PromoBuyer> GetBuyerAsync(int? custId)
        {
            if (custId is not int id || id <= 0) return PromoBuyer.Guest;

            var c = await _context.Customers.AsNoTracking()
                .Where(x => x.custID == id)
                .Select(x => new { x.custEmail, x.custPassword, x.custBirthday })
                .FirstOrDefaultAsync();
            if (c == null) return PromoBuyer.Guest;

            // Walk-in guests: a reservation guest profile has no password yet, a staff takeaway guest uses an @guest.local email
            bool member = !string.IsNullOrEmpty(c.custPassword) && !c.custEmail.EndsWith("@guest.local", StringComparison.OrdinalIgnoreCase);
            return new PromoBuyer(id, member, member ? c.custBirthday : null);
        }

        // Every promotion that is switched on, with its dishes (the list the checks run over)
        public Task<List<Promotion>> ActiveAsync() =>
            _context.Promotions.AsNoTracking()
                .Include(p => p.Menus)
                .Where(p => p.promoActive)
                .ToListAsync();

        // null = this buyer may use the promotion now. Otherwise the reason.
        //   orderType / subtotal: null on the Menu page (no order yet) → those two checks are skipped
        //   billOrderIds: orders being paid right now, not counted as earlier uses
        //   forStaff: word the reason for staff ("This customer has ...") instead of the customer ("You have ...")
        public async Task<string?> WhyNotAsync(Promotion p, PromoBuyer buyer, DateTime now,
            string? orderType = null, decimal? subtotal = null, ICollection<int>? billOrderIds = null, bool forStaff = false)
        {
            string? why = PromotionRules.NotAvailableReason(p, now, buyer.Birthday, forStaff: forStaff);
            if (why != null) return why;

            if (PromotionRules.MembersOnly(p) && !buyer.IsMember)
                return "For members with an account only";

            var (customerUses, totalUses) = await CountUsesAsync(p, buyer, now, billOrderIds);
            return PromotionRules.CheckUsage(p, orderType ?? p.orderType, subtotal ?? decimal.MaxValue, customerUses, totalUses, forStaff);
        }

        // The promotions this buyer may use on this order right now
        public async Task<List<Promotion>> UsableAsync(IEnumerable<Promotion> candidates, PromoBuyer buyer, DateTime now,
            string orderType, decimal subtotal, ICollection<int>? billOrderIds = null)
        {
            var usable = new List<Promotion>();
            foreach (var p in candidates)
            {
                if (await WhyNotAsync(p, buyer, now, orderType, subtotal, billOrderIds) == null) usable.Add(p);
            }
            return usable;
        }

        // Auto-apply promotions for a new order (customer Checkout, staff new order), one entry per line.
        // enteredCode: a promotion the customer unlocked by typing its code at Checkout. It must already have
        // passed CheckCodeAsync; it then competes with the auto-apply promotions (best discount per line wins).
        public async Task<AppliedPromo?[]> PriceNewOrderAsync(PromoBuyer buyer, string orderType, IReadOnlyList<PricedLine> lines,
            Promotion? enteredCode = null)
        {
            var now = MalaysiaClock.Now;
            decimal subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
            var auto = (await ActiveAsync()).Where(p => p.autoApply);
            var usable = await UsableAsync(auto, buyer, now, orderType, subtotal);
            if (enteredCode != null && usable.All(p => p.promoID != enteredCode.promoID)) usable.Add(enteredCode);
            return PromotionPricing.Apply(usable, lines);
        }

        // A promo code typed by the customer at Checkout. Rules for the code:
        //   1. it must belong to a promotion (codes are stored in upper case, so typing is not case-sensitive)
        //   2. at least one dish in the cart must be one of the promotion's own dishes
        //   3. every normal promotion rule must pass for this customer and order (WhyNotAsync): switched on, campaign
        //      dates, days, time of day, birthday month, members only, order type, minimum spend, and the usage limits.
        //      Used-code rule: a code this customer has already used up to "uses per customer" (paid, not cancelled
        //      orders), or that is fully redeemed, is rejected. It is checked again at payment (RecheckLimitsAsync).
        // Returns the promotion when it may be used, otherwise the message to show. An empty code = (null, null).
        public async Task<(Promotion? Promo, string? Error)> CheckCodeAsync(string? code, PromoBuyer buyer, string orderType,
            IReadOnlyList<PricedLine> lines)
        {
            code = code?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(code)) return (null, null);

            var promo = await _context.Promotions.AsNoTracking()
                .Include(p => p.Menus)
                .FirstOrDefaultAsync(p => p.promoCode == code);
            if (promo == null) return (null, $"{code} is not a valid promo code.");

            var covered = promo.Menus.Select(m => m.menuID).ToHashSet();
            if (!lines.Any(l => l.Quantity > 0 && covered.Contains(l.MenuId)))
                return (null, $"{code} does not apply to any dish in your cart.");

            decimal subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
            string? why = await WhyNotAsync(promo, buyer, MalaysiaClock.Now, orderType, subtotal);
            return why == null ? (promo, null) : (null, $"{code} cannot be used: {why}");
        }

        // Right before a bill is paid: a promotion whose limit is now used up (by other paid orders, or by an
        // earlier order on this same bill) is taken off the later lines, and those orders are priced again.
        // Returns the codes that were removed (empty = nothing changed). Does not save.
        public async Task<List<string>> RecheckLimitsAsync(List<Order> bill, decimal servicePct, decimal sstPct)
        {
            var removed = new List<string>();
            var promoIds = bill.SelectMany(o => o.OrderItems).Where(oi => oi.promoID != null)
                .Select(oi => oi.promoID!.Value).Distinct().ToList();
            if (promoIds.Count == 0) return removed;

            var now = MalaysiaClock.Now;
            var billIds = bill.Select(o => o.orderID).ToList();
            var promos = await _context.Promotions.AsNoTracking().Where(p => promoIds.Contains(p.promoID)).ToListAsync();

            foreach (var p in promos.Where(p => p.maxUsesPerCustomer.HasValue || p.maxTotalUses.HasValue))
            {
                // each order on the bill that carries the promotion is one use, oldest first
                foreach (var order in bill.OrderBy(o => o.orderID).Where(o => o.OrderItems.Any(oi => oi.promoID == p.promoID)))
                {
                    var buyer = await GetBuyerAsync(order.custID);
                    var earlierOnBill = bill.Where(o => o.orderID < order.orderID
                                                     && o.OrderItems.Any(oi => oi.promoID == p.promoID)).ToList();
                    var (customerUses, totalUses) = await CountUsesAsync(p, buyer, now, billIds);
                    customerUses += earlierOnBill.Count(o => o.custID == order.custID);
                    totalUses += earlierOnBill.Count;

                    bool overLimit = (p.maxTotalUses.HasValue && totalUses >= p.maxTotalUses.Value)
                                  || (p.maxUsesPerCustomer.HasValue && customerUses >= p.maxUsesPerCustomer.Value);
                    if (!overLimit) continue;

                    foreach (var line in order.OrderItems.Where(oi => oi.promoID == p.promoID))
                    {
                        line.promoID = null;
                        line.promoCode = null;
                        line.discountAmount = 0m;
                    }
                    decimal subtotal = order.OrderItems.Sum(oi => oi.unitPrice * oi.quantity - oi.discountAmount);
                    order.totalAmount = PromotionPricing.Totals(subtotal, order.tableID != null, servicePct, sstPct).Total;
                    if (!removed.Contains(p.promoCode)) removed.Add(p.promoCode);
                }
            }
            return removed;
        }

        // Earlier uses: orders that are paid and not cancelled, inside the usage window
        private async Task<(int CustomerUses, int TotalUses)> CountUsesAsync(Promotion p, PromoBuyer buyer, DateTime now, ICollection<int>? billOrderIds)
        {
            if (!p.maxUsesPerCustomer.HasValue && !p.maxTotalUses.HasValue) return (0, 0);

            var exclude = billOrderIds ?? Array.Empty<int>();
            var uses = _context.Orders.AsNoTracking()
                .Where(o => o.orderStatus != "Cancelled"
                         && o.Payment != null && o.Payment.paymentStatus == "Paid"
                         && !exclude.Contains(o.orderID)
                         && o.OrderItems.Any(oi => oi.promoID == p.promoID));

            int total = p.maxTotalUses.HasValue ? await uses.CountAsync() : 0;

            int mine = 0;
            if (p.maxUsesPerCustomer.HasValue && buyer.CustId is int custId)
            {
                var since = PromotionRules.UsageWindowStart(p, now);
                var myUses = uses.Where(o => o.custID == custId);
                if (since.HasValue) myUses = myUses.Where(o => o.orderDateTime >= since.Value);
                mine = await myUses.CountAsync();
            }
            return (mine, total);
        }
    }
}
