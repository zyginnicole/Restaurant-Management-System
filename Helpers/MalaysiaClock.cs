namespace RestaurantManagementSystem.Helpers
{
    // "Now" and "today" in Malaysia time (GMT+8), whatever time zone the server itself is set to.
    // The promotion rules (birthday month, days, time of day, campaign dates, yearly limits) all use this clock.
    public static class MalaysiaClock
    {
        private static readonly TimeZoneInfo Zone = FindZone();

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

        public static DateTime Today => Now.Date;

        // Midnight of a Malaysian date as an exact moment, for comparing with saved DateTimeOffset values
        public static DateTimeOffset StartOf(DateTime malaysiaDate) =>
            new DateTimeOffset(malaysiaDate.Date, Zone.GetUtcOffset(malaysiaDate.Date));

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
