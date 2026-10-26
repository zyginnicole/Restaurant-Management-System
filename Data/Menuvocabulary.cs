namespace RestaurantManagementSystem.Helpers
{
    // One list of tags / allergens used by the admin dish form, Register, Profile and the recommendation engine.
    // Stored in the database as comma-separated text, e.g. "Vegetarian,Spicy". "None" = nothing selected.
    public static class MenuVocabulary
    {
        // Dish tags = what a customer can pick as a preference (Customer.DietaryPref)
        public static readonly string[] Tags =
            { "Vegetarian", "Vegan", "Halal", "Spicy", "Seafood", "Chicken", "Beef", "Sweet" };

        // Allergens = what a dish can contain (Menu.menuAllergens) and what a customer can be allergic to (Customer.Allergies)
        public static readonly string[] Allergens =
            { "Peanuts", "Tree nuts", "Dairy", "Eggs", "Gluten", "Soy", "Shellfish", "Fish", "Sesame" };

        // "Vegetarian, Spicy" -> {Vegetarian, Spicy}. null / "" / "None" -> empty set.
        public static HashSet<string> Parse(string? csv)
        {
            return (csv ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !x.Equals("None", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        // {Vegetarian, Spicy} -> "Vegetarian,Spicy". Empty -> "None".
        public static string Join(IEnumerable<string> items)
        {
            var list = items.Distinct().ToList();
            return list.Count == 0 ? "None" : string.Join(",", list);
        }
    }
}