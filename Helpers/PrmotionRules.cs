using System.Globalization;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Helpers
{
    // ALL promotion rules in one place. The admin page badge, the customer menu and Checkout all call this class,
    // so nobody writes the rules twice.
    //
    // A promotion can be used when ALL of these are true:
    //   active + inside valid from/until + today is inside start day - end day + time is inside the time window
    //   + (birthday-only: it is the customer's birthday month)
    //   + (at Checkout) order type matches, minimum spend reached, and the usage limits are not used up.
    // "now" is always Malaysia time (MalaysiaClock). PromotionService runs these checks on the server.
    public static class PromotionRules
    {
        public const string Any = "Any";
        public const string DineIn = "DineIn";
        public const string Takeaway = "Takeaway";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // =====================================================================
        //  Schedule
        // =====================================================================

        // Fri-Sun works, and so does a range that wraps round the week (Sat-Mon).
        // The default Monday-Sunday means every day.
        public static bool IsDayAllowed(Promotion p, DayOfWeek day) =>
            p.startDay <= p.endDay
                ? day >= p.startDay && day <= p.endDay
                : day >= p.startDay || day <= p.endDay;

        // No time window = all day. A window such as 22:00-02:00 runs past midnight.
        public static bool IsTimeAllowed(Promotion p, TimeSpan time)
        {
            if (!p.startTime.HasValue || !p.endTime.HasValue) return true;
            var start = p.startTime.Value;
            var end = p.endTime.Value;
            if (start == end) return true;
            return start < end ? time >= start && time < end : time >= start || time < end;
        }

        // The whole birthday month counts (Feb 29 birthdays get all of February, leap year or not)
        public static bool IsBirthdayMonth(DateTime? birthday, DateTime now) =>
            birthday.HasValue && birthday.Value.Month == now.Month;

        public static bool OrderTypeAllowed(Promotion p, string? orderType)
        {
            bool restricted = p.orderType == DineIn || p.orderType == Takeaway;
            return !restricted || string.Equals(p.orderType, orderType, StringComparison.OrdinalIgnoreCase);
        }

        // =====================================================================
        //  Status + "can it be used right now?"
        // =====================================================================

        // Admin badge: Inactive / Expired / Scheduled / Active (by switch and dates only)
        public static string StatusOf(Promotion p, DateTime now)
        {
            var today = now.Date;
            if (!p.promoActive) return "Inactive";
            if (p.validUntil.HasValue && p.validUntil.Value.Date < today) return "Expired";
            if (p.validFrom.HasValue && p.validFrom.Value.Date > today) return "Scheduled";
            return "Active";
        }

        // null = can be used right now. Otherwise a short reason to show to people.
        // checkBirthday: false for the admin page (no customer there). forStaff: worded for staff, not the customer.
        public static string? NotAvailableReason(Promotion p, DateTime now, DateTime? birthday, bool checkBirthday = true, bool forStaff = false)
        {
            if (!p.promoActive) return "Not active";
            if (p.validUntil.HasValue && p.validUntil.Value.Date < now.Date)
                return "Ended " + p.validUntil.Value.ToString("d MMM yyyy", Inv);
            if (p.validFrom.HasValue && p.validFrom.Value.Date > now.Date)
                return "Starts " + p.validFrom.Value.ToString("d MMM yyyy", Inv);
            if (!IsDayAllowed(p, now.DayOfWeek)) return "Available " + DaysText(p);
            if (!IsTimeAllowed(p, now.TimeOfDay)) return "Available " + TimeText(p);
            if (checkBirthday && p.birthdayOnly && !IsBirthdayMonth(birthday, now))
                return birthday.HasValue ? "Birthday month only"
                     : forStaff ? "Birthday month only (this customer has no birthday saved)"
                     : "Birthday month only (add your birthday in your profile)";
            return null;
        }

        public static bool IsValidNow(Promotion p, DateTime now, DateTime? birthday) =>
            NotAvailableReason(p, now, birthday) == null;

        // =====================================================================
        //  Checkout checks (the order and payment code plugs these in)
        // =====================================================================

        // Birthday and limited promotions need a real account: a walk-in guest record has no birthday,
        // and one guest can end up with several records, so per-customer limits can't be trusted for guests.
        public static bool MembersOnly(Promotion p) => p.birthdayOnly || p.maxUsesPerCustomer.HasValue;

        // Where to start counting a customer's earlier uses of this promotion:
        // 1 January (Malaysia time) of this year if the limit resets yearly, otherwise null (count everything).
        // A use = one order with this promoID on a line, that is PAID and not "Cancelled" (PromotionService counts them),
        // so an unpaid order, a failed payment or a cancelled order never uses up the limit.
        public static DateTimeOffset? UsageWindowStart(Promotion p, DateTime now) =>
            p.limitResetsYearly ? MalaysiaClock.StartOf(new DateTime(now.Year, 1, 1)) : (DateTimeOffset?)null;

        // null = OK, otherwise the message to show the customer (or staff, when forStaff is true).
        //   orderSubtotal  = the whole order before any discount
        //   customerUses   = this customer's earlier uses (see UsageWindowStart)
        //   totalUses      = everyone's earlier uses (orders that are not Cancelled)
        public static string? CheckUsage(Promotion p, string? orderType, decimal orderSubtotal, int customerUses, int totalUses,
            bool forStaff = false)
        {
            if (!OrderTypeAllowed(p, orderType))
                return p.orderType == DineIn ? "This promotion is for dine-in orders only." : "This promotion is for takeaway orders only.";

            if (p.minSpend.HasValue && orderSubtotal < p.minSpend.Value)
                return forStaff
                    ? $"The bill must be at least RM {p.minSpend.Value:0.00} for this promotion."
                    : $"Spend at least RM {p.minSpend.Value:0.00} to use this promotion.";

            if (p.maxTotalUses.HasValue && totalUses >= p.maxTotalUses.Value)
                return "This promotion has been fully redeemed.";

            if (p.maxUsesPerCustomer.HasValue && customerUses >= p.maxUsesPerCustomer.Value)
            {
                string who = forStaff ? "This customer has" : "You have";
                return p.limitResetsYearly
                    ? $"{who} already used this promotion this year."
                    : $"{who} already used this promotion.";
            }

            return null;
        }

        // =====================================================================
        //  Text for the admin cards and the customer Promotions tab
        // =====================================================================

        public static string DaysText(Promotion p) =>
            p.startDay == DayOfWeek.Monday && p.endDay == DayOfWeek.Sunday
                ? "Every day"
                : p.startDay.ToString().Substring(0, 3) + " – " + p.endDay.ToString().Substring(0, 3);

        // "5:00 PM – 7:00 PM", or "" when the promotion has no time window
        public static string TimeText(Promotion p)
        {
            if (!p.startTime.HasValue || !p.endTime.HasValue) return "";
            return FormatTime(p.startTime.Value) + " – " + FormatTime(p.endTime.Value);
        }

        private static string FormatTime(TimeSpan t) => DateTime.Today.Add(t).ToString("h:mm tt", Inv);

        // Short chips: "5:00 PM – 7:00 PM", "Dine-in only", "Min. spend RM 80", ...
        public static string[] RulesSummary(Promotion p)
        {
            var list = new List<string>();
            var time = TimeText(p);
            if (time != "") list.Add(time);
            if (p.orderType == DineIn) list.Add("Dine-in only");
            if (p.orderType == Takeaway) list.Add("Takeaway only");
            if (p.minSpend.HasValue) list.Add($"Min. spend RM {p.minSpend.Value:0.##}");
            if (p.birthdayOnly) list.Add("Birthday month only");
            if (p.maxUsesPerCustomer.HasValue)
            {
                var n = p.maxUsesPerCustomer.Value;
                var times = n == 1 ? "Once per customer" : $"{n} uses per customer";
                list.Add(p.limitResetsYearly ? times + " per year" : times);
            }
            if (p.maxTotalUses.HasValue) list.Add($"Limited to {p.maxTotalUses.Value} redemptions");
            return list.ToArray();
        }
    }
}