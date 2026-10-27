using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Data
{
    // Inserts sample data on startup. Each block only runs when its table is empty,
    // so restarting the app never creates duplicates.
    public static class DbSeeder
    {
        public static void Seed(ApplicationDbContext db)
        {
            // ---------- 1. Menu categories (same names as the Menu page tabs) ----------
            if (!db.MenuCategories.Any())
            {
                db.MenuCategories.AddRange(
                    new MenuCategory { categoryName = "Starters" },
                    new MenuCategory { categoryName = "Mains" },
                    new MenuCategory { categoryName = "Desserts" },
                    new MenuCategory { categoryName = "Beverages" });
                db.SaveChanges();
            }

            // ---------- 2. Dining areas (the code starts each table number, e.g. I-01) ----------
            if (!db.DiningAreas.Any())
            {
                db.DiningAreas.AddRange(
                    new DiningArea { areaName = "Indoor", areaCode = "I" },
                    new DiningArea { areaName = "Outdoor", areaCode = "O" },
                    new DiningArea { areaName = "VIP", areaCode = "V" });
                db.SaveChanges();
            }

            // ---------- 3. Menu items (placeholder dishes from Menu.cshtml) ----------
            if (!db.Menus.Any())
            {
                var cat = db.MenuCategories.ToDictionary(c => c.categoryName, c => c.categoryID);

                db.Menus.AddRange(
                    Item("Oyster Selection", "Six Brittany oysters, mignonette, lemon", cat["Starters"], 68m, "oyster-selection.jpg"),
                    Item("Caviar & Soft Egg", "Oscietra caviar, 63°C egg, crème fraîche", cat["Starters"], 85m, "caviar-soft-egg.jpg"),

                    Item("Wagyu Beef Tenderloin", "A5 wagyu, truffle jus, bone marrow", cat["Mains"], 168m, "wagyu-beef-tenderloin.jpg"),
                    Item("Duck à l'Orange", "Roasted duck,Blood orange reduction, pommes dauphine", cat["Mains"], 92m, "duck-orange.jpg"),
                    Item("Pan-Seared Sea Bass", "Wild-caught sea bass, saffron beurre blanc, fennel confit", cat["Mains"], 98m, "pan-seared-sea-bass.jpg"),
                    Item("Lobster Thermidor", "Half lobster, cognac cream, gruyère", cat["Mains"], 128m, "lobster-thermidor.jpg", available: false),

                    Item("Mille-Feuille", "Caramelised pastry, custard, seasonal berries", cat["Desserts"], 32m, "mille-feuille.jpg"),
                    Item("Valrhona Chocolate Fondant", "Valrhona dark chocolate, vanilla ice cream, praline", cat["Desserts"], 36m, "valrhona-chocolate-fondant.jpg"),

                    Item("Château Margaux 2018 · glass", "Bordeaux red, served by the glass", cat["Beverages"], 120m, "chateau-margaux-glass.jpg"),
                    Item("Sparkling Water", "Chilled sparkling mineral water, 750 ml", cat["Beverages"], 18m, "sparkling-water.jpg"));
                db.SaveChanges();
            }

            // ---------- 4. Tables: area code + number inside the area (TableNumbering) ----------
            if (!db.DiningTables.Any())
            {
                var area = db.DiningAreas.ToDictionary(a => a.areaName, a => a.areaID);

                db.DiningTables.AddRange(
                    Table("I-01", 2, area["Indoor"]),
                    Table("I-02", 2, area["Indoor"]),
                    Table("I-03", 4, area["Indoor"]),
                    Table("I-04", 4, area["Indoor"]),

                    Table("O-01", 4, area["Outdoor"]),
                    Table("O-02", 4, area["Outdoor"]),
                    Table("O-03", 6, area["Outdoor"]),

                    Table("V-01", 8, area["VIP"]),
                    Table("V-02", 12, area["VIP"]));
                db.SaveChanges();
            }

            // ---------- 5. Restaurant + operating hours ----------
            if (!db.Restaurants.Any())
            {
                var restaurant = new Restaurant
                {
                    restaurantName = "Restaurant Name",          // TODO: replace with your real name
                    restaurantPhone = "+60 4-261 0000"
                };

                foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
                {
                    restaurant.OperatingHours.Add(new OperatingHour
                    {
                        dayOfWeek = day,
                        openTime = new TimeSpan(12, 0, 0),
                        closeTime = new TimeSpan(22, 0, 0)
                    });
                }

                db.Restaurants.Add(restaurant);
                db.SaveChanges();
            }

            // ---------- 6. Promotions (need an existing Admin, so skipped if there is none yet) ----------
            if (!db.Promotions.Any())
            {
                var admin = db.Admins.FirstOrDefault();
                if (admin != null)
                {
                    db.Promotions.AddRange(
                        new Promotion
                        {
                            Admin = admin,
                            promoTitle = "Weekend Tasting Menu",
                            promoDescription = "The chef's eight-course tasting experience with optional wine pairing.",
                            promoLabel = "15% off wine flight",
                            promoCode = "WEEKEND15",
                            discountType = "Percentage",
                            discountValue = 15m,
                            autoApply = false,
                            startDay = DayOfWeek.Friday,
                            endDay = DayOfWeek.Sunday
                        },
                        new Promotion
                        {
                            Admin = admin,
                            promoTitle = "Early Dinner",
                            promoDescription = "15% off selected mains when you dine before 18:30.",
                            promoLabel = "15% off before 18:30",
                            promoCode = "EARLY15",
                            discountType = "Percentage",
                            discountValue = 15m,
                            autoApply = true,
                            startDay = DayOfWeek.Tuesday,
                            endDay = DayOfWeek.Sunday,
                            startTime = new TimeSpan(12, 0, 0),    // from opening ...
                            endTime = new TimeSpan(18, 30, 0)      // ... until 18:30, as the label says
                        },
                        new Promotion
                        {
                            Admin = admin,
                            promoTitle = "Birthday Dessert",
                            promoDescription = "A complimentary dessert for members during their birthday month.",
                            promoLabel = "Free in birthday month",
                            promoCode = "BIRTHDAY",
                            discountType = "Complimentary",
                            discountValue = null,
                            autoApply = false,
                            birthdayOnly = true,           // only in the customer's birthday month
                            maxUsesPerCustomer = 1,        // once ...
                            limitResetsYearly = true       // ... per year
                        });
                    db.SaveChanges();
                }
            }

            // ---------- 7. Link promotions to their dishes (runs once) ----------
            var promos = db.Promotions.Include(p => p.Menus).ToList();
            if (promos.Count > 0 && promos.All(p => p.Menus.Count == 0))
            {
                var dishes = db.Menus.ToDictionary(m => m.menuName);

                void Link(string code, params string[] dishNames)
                {
                    var promo = promos.FirstOrDefault(p => p.promoCode == code);
                    if (promo == null) return;
                    foreach (var name in dishNames)
                        if (dishes.TryGetValue(name, out var dish)) promo.Menus.Add(dish);
                }

                Link("EARLY15", "Duck à l'Orange", "Pan-Seared Sea Bass");
                Link("BIRTHDAY", "Mille-Feuille", "Valrhona Chocolate Fondant");
                db.SaveChanges();
            }

            // ---------- 8. Eight-Course Tasting Menu (linked to Weekend Tasting) ----------
            var weekend = db.Promotions.Include(p => p.Menus).FirstOrDefault(p => p.promoCode == "WEEKEND15");
            if (weekend != null && weekend.Menus.Count == 0)
            {
                var setMenu = db.MenuCategories.FirstOrDefault(c => c.categoryName == "Set Menu")
                              ?? new MenuCategory { categoryName = "Set Menu" };

                weekend.Menus.Add(new Menu
                {
                    menuName = "Eight-Course Tasting Menu",
                    menuDescription = "Seasonal courses chosen by the chef · per guest · weekends only",
                    MenuCategory = setMenu,
                    menuPrice = 388m,
                    isAvailable = true,
                    menuImageUrl = "/images/menu/eight-course-tasting-menu.jpg"
                });
                db.SaveChanges();
            }

            // ---------- 9. Tags, allergens and signature dishes (used by the customer recommendations) ----------
            // Fills only what is still empty, so anything the admin set by hand is never overwritten.
            // Signature dishes are only seeded while no dish is marked signature yet.
            // CHECK: allergens are best guesses from the dish names. The chef must confirm them before real customers use this.
            // null allergens = unknown: the dish stays hidden from customers who have allergies.
            {
                var info = new Dictionary<string, (bool Signature, string? Tags, string? Allergens)>
                {
                    ["Oyster Selection"] = (false, "Seafood", "Shellfish"),
                    ["Caviar & Soft Egg"] = (false, "Seafood", "Fish,Eggs,Dairy"),
                    ["Wagyu Beef Tenderloin"] = (true, "Beef", "None"),                  // CHECK: jus may contain gluten or dairy
                    ["Duck à l'Orange"] = (true, null, "Gluten,Eggs,Dairy"),     // pommes dauphine = choux pastry
                    ["Pan-Seared Sea Bass"] = (false, "Seafood", "Fish,Dairy"),            // beurre blanc = butter
                    ["Lobster Thermidor"] = (false, "Seafood", "Shellfish,Dairy,Eggs"),
                    ["Mille-Feuille"] = (false, "Vegetarian,Sweet", "Gluten,Dairy,Eggs"),
                    ["Valrhona Chocolate Fondant"] = (true, "Vegetarian,Sweet", "Gluten,Dairy,Eggs,Tree nuts,Soy"), // praline, chocolate
                    ["Château Margaux 2018 · glass"] = (false, null, "None"),
                    ["Sparkling Water"] = (false, "Vegan,Vegetarian,Halal", "None"),
                    ["Eight-Course Tasting Menu"] = (true, null, null)                     // CHECK: courses change, ask the chef
                };

                bool seedSignature = !db.Menus.Any(m => m.isSignature);

                foreach (var dish in db.Menus.ToList())
                {
                    if (!info.TryGetValue(dish.menuName, out var x)) continue;

                    if (dish.menuTags == null) dish.menuTags = x.Tags;
                    if (dish.menuAllergens == null) dish.menuAllergens = x.Allergens;
                    if (seedSignature && x.Signature) dish.isSignature = true;
                }
                db.SaveChanges();
            }
        }

        // ---------- helpers ----------
        private static Menu Item(string name, string description, int categoryId, decimal price,
                                 string image, bool available = true) => new Menu
                                 {
                                     menuName = name,
                                     menuDescription = description,
                                     categoryID = categoryId,
                                     menuPrice = price,
                                     isAvailable = available,
                                     menuImageUrl = "/images/menu/" + image
                                 };

        private static DiningTable Table(string number, int capacity, int areaId) => new DiningTable
        {
            tableNumber = number,
            tableCapacity = capacity,
            areaID = areaId
        };
    }
}