using System.Reflection;

namespace RestaurantManagementSystem.Helpers
{
    // "Now" and "today" in Malaysia time (GMT+8), whatever time zone the server itself is set to.
    // The promotion rules (birthday month, days, time of day, campaign dates, yearly limits) all use this clock.
    public static class MalaysiaClock
    {
        private static readonly TimeSpan MalaysiaOffset = TimeSpan.FromHours(8);   // no daylight saving in Malaysia
        private static Timer? _recheckTimer;

        public static readonly TimeZoneInfo Zone = FindZone();

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

        public static DateTime Today => Now.Date;

        // Midnight of a Malaysian date as an exact moment, for comparing with saved DateTimeOffset values
        public static DateTimeOffset StartOf(DateTime malaysiaDate) =>
            new DateTimeOffset(malaysiaDate.Date, Zone.GetUtcOffset(malaysiaDate.Date));

        // The rest of the app uses DateTime.Now / DateTime.Today / DateTimeOffset.Now, which follow the SERVER's
        // time zone. On a hosting server outside Malaysia (MonsterASP's is in Europe) opening hours, reservation
        // slots, "today" and reports would all be hours off. .NET has no setting to choose the app's local time
        // zone, so this replaces the two cached values those properties read:
        //   _localTimeZone       - used by DateTimeOffset.Now, ToLocalTime, ToUniversalTime
        //   _oneYearLocalFromUtc - used by DateTime.Now / DateTime.Today (Windows refills it from the server's
        //                          setting at the start of each year, hence the re-check every minute)
        // On a PC already set to GMT+8 nothing is changed. Call once, first thing in Program.cs.
        public static void UseAsServerLocalTime()
        {
            if (!ApplyIfNeeded()) return;
            _recheckTimer = new Timer(_ => ApplyIfNeeded(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        // True when the server is not on Malaysia time (so it had to be switched)
        private static bool ApplyIfNeeded()
        {
            var nowOffset = TimeSpan.FromMinutes(Math.Round((DateTime.Now - DateTime.UtcNow).TotalMinutes));
            if (nowOffset == MalaysiaOffset && TimeZoneInfo.Local.BaseUtcOffset == MalaysiaOffset)
            {
                return _recheckTimer != null;   // already Malaysia time (keep the timer if we switched earlier)
            }

            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                object? cache = typeof(TimeZoneInfo)
                    .GetField("s_cachedData", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                FieldInfo? localZone = cache?.GetType().GetField("_localTimeZone", flags);
                FieldInfo? yearOffset = cache?.GetType().GetField("_oneYearLocalFromUtc", flags);
                Type? offsetAndRule = typeof(TimeZoneInfo).GetNestedType("OffsetAndRule", BindingFlags.NonPublic);

                if (cache == null || localZone == null || yearOffset == null || offsetAndRule == null)
                {
                    Console.Error.WriteLine("Could not switch the app to Malaysia time: times follow the server's time zone.");
                    return false;
                }

                // new OffsetAndRule(year, +8h, no daylight-saving rule)
                object yearEntry = Activator.CreateInstance(offsetAndRule, flags | BindingFlags.Public, null,
                    new object?[] { DateTime.UtcNow.Year, MalaysiaOffset, null }, null)!;

                lock (cache)
                {
                    localZone.SetValue(cache, Zone);
                    yearOffset.SetValue(cache, yearEntry);
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not switch the app to Malaysia time: {ex.Message}");
                return false;
            }
        }

        // IANA id (Linux, and Windows with ICU), then the Windows id; Malaysia has no daylight saving, so a fixed +8 also works
        private static TimeZoneInfo FindZone()
        {
            foreach (var id in new[] { "Asia/Kuala_Lumpur", "Singapore Standard Time" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("MYT", TimeSpan.FromHours(8), "Malaysia Time", "Malaysia Time");
        }
    }
}
