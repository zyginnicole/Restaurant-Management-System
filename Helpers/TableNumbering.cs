using System.Text.RegularExpressions;

namespace RestaurantManagementSystem.Helpers
{
    // Table numbers = area code + number inside the area: Indoor I-01, Outdoor O-01, VIP V-01.
    // The code makes every table number unique in the restaurant, so staff can't mix up two "T-01"s
    // and can tell the area from the number alone.
    public static class TableNumbering
    {
        public const int MaxPerArea = 99;

        // A short code for an area name that no other area uses yet:
        //   first letter ("Garden" -> G), then the initials ("Rooftop Garden" -> RG),
        //   then the first 2 or 3 letters ("Garden" -> GA, GAR), then letter + digit (G2 ... G9)
        public static string CodeFor(string areaName, IEnumerable<string> takenCodes)
        {
            var taken = new HashSet<string>(takenCodes, StringComparer.OrdinalIgnoreCase);
            var words = Regex.Matches(areaName.ToUpperInvariant(), "[A-Z0-9]+").Select(m => m.Value).ToList();
            if (words.Count == 0) words.Add("A");                   // a name with no letters or digits
            string letters = string.Concat(words);

            var candidates = new List<string> { letters[..1] };
            if (words.Count > 1) candidates.Add(string.Concat(words.Take(3).Select(w => w[0])));
            if (letters.Length >= 2) candidates.Add(letters[..2]);
            if (letters.Length >= 3) candidates.Add(letters[..3]);
            for (int i = 2; i <= 9; i++) candidates.Add(letters[..1] + i);

            return candidates.FirstOrDefault(c => !taken.Contains(c))
                ?? letters[..1] + Guid.NewGuid().ToString("N")[..2].ToUpperInvariant();   // all taken: practically never
        }

        // "I" + 3 -> "I-03"
        public static string Format(string areaCode, int number) => $"{areaCode}-{number:00}";

        // "I-07" -> 7, "V-12" -> 12, anything without digits at the end -> 0
        public static int NumberPart(string? tableNumber)
        {
            var match = Regex.Match(tableNumber ?? "", @"(\d+)\s*$");
            return match.Success && int.TryParse(match.Groups[1].Value, out var n) ? n : 0;
        }
    }
}
