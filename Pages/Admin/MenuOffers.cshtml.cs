using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

// If Visual Studio generated a different namespace line in your own file, keep yours.
namespace RestaurantManagementSystem.Pages.Admin
{
    public class MenuOffersModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        // ASP.NET gives us the database and the wwwroot location automatically (dependency injection)
        public MenuOffersModel(ApplicationDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        // The page (.cshtml) reads these lists through "Model"
        public List<CategoryRow> Categories { get; set; } = new();
        public List<MenuItemRow> Items { get; set; } = new();
        public List<PromoRow> Promos { get; set; } = new();

        private static readonly string[] AllowedImageTypes = { ".jpg", ".jpeg", ".png", ".webp" };

        // =====================================================================
        //  READ: runs when the page is opened
        // =====================================================================
        public async Task OnGetAsync()
        {
            Categories = await _db.MenuCategories
                .OrderBy(c => c.categoryName)
                .Select(c => new CategoryRow { Id = c.categoryID, Name = c.categoryName })
                .ToListAsync();

            var menus = await _db.Menus
                .Include(m => m.MenuCategory)
                .OrderBy(m => m.categoryID).ThenBy(m => m.menuName)
                .ToListAsync();

            Items = menus.Select(m => new MenuItemRow
            {
                Id = m.menuID,
                Name = m.menuName,
                CatId = m.categoryID,
                Cat = m.MenuCategory?.categoryName ?? "",
                Price = m.menuPrice,
                Avail = m.isAvailable,
                Image = ImageFile(m.menuImageUrl),
                Desc = m.menuDescription ?? "",
                Sig = m.isSignature,
                TagList = MenuVocabulary.Parse(m.menuTags).ToArray(),
                AllergenList = MenuVocabulary.Parse(m.menuAllergens).ToArray(),
                AllergensSet = !string.IsNullOrWhiteSpace(m.menuAllergens)
            }).ToList();

            var promotions = await _db.Promotions
                .Include(p => p.Menus)
                .ToListAsync();

            Promos = promotions
                .OrderByDescending(p => p.promoCreateAt)
                .Select(p => new PromoRow
                {
                    Id = p.promoID,
                    Name = p.promoTitle,
                    Code = p.promoCode,
                    AutoApply = p.autoApply,
                    Value = DiscountText(p),
                    Unit = DaysText(p),
                    Dates = DateText(p.validFrom, p.validUntil),
                    Status = StatusOf(p),
                    Availability = AvailabilityText(p),
                    Rules = PromotionRules.RulesSummary(p),
                    Dishes = p.Menus.Select(m => m.menuName).ToArray(),
                    Edit = new
                    {
                        id = p.promoID,
                        title = p.promoTitle,
                        code = p.promoCode,
                        label = p.promoLabel,
                        desc = p.promoDescription ?? "",
                        type = p.discountType,
                        value = p.discountValue,
                        auto = p.autoApply,
                        startDay = p.startDay.ToString(),
                        endDay = p.endDay.ToString(),
                        from = p.validFrom?.ToString("yyyy-MM-dd") ?? "",
                        until = p.validUntil?.ToString("yyyy-MM-dd") ?? "",
                        active = p.promoActive,
                        startTime = p.startTime?.ToString(@"hh\:mm") ?? "",
                        endTime = p.endTime?.ToString(@"hh\:mm") ?? "",
                        orderType = p.orderType == PromotionRules.DineIn || p.orderType == PromotionRules.Takeaway ? p.orderType : PromotionRules.Any,
                        minSpend = p.minSpend,
                        perCustomer = p.maxUsesPerCustomer,
                        yearly = p.limitResetsYearly,
                        totalUses = p.maxTotalUses,
                        birthday = p.birthdayOnly,
                        dishIds = p.Menus.Select(m => m.menuID).ToArray()
                    }
                }).ToList();
        }

        // =====================================================================
        //  DISHES: add / edit, delete, sold-out switch
        // =====================================================================
        public async Task<IActionResult> OnPostSaveDishAsync(int? id, string? name, int categoryId, decimal price,
            string? description, bool available, IFormFile? image,
            bool signature, string[]? tags, string[]? allergens, bool noAllergens)
        {
            name = name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || price <= 0)
                return Fail("Please enter a dish name and a price above 0.");
            if (name.Length > 100)
                return Fail("The dish name can be at most 100 characters.");
            if (!await _db.MenuCategories.AnyAsync(c => c.categoryID == categoryId))
                return Fail("Please choose a category.");

            Menu? dish;
            if (id.HasValue)
            {
                dish = await _db.Menus.FindAsync(id.Value);
                if (dish == null) return Fail("That dish no longer exists.");
            }
            else
            {
                dish = new Menu { menuImageUrl = "" };
                _db.Menus.Add(dish);
            }

            dish.menuName = name;
            dish.categoryID = categoryId;
            dish.menuPrice = price;
            dish.menuDescription = description?.Trim() ?? "";
            dish.isAvailable = available;
            dish.isSignature = signature;

            var tagList = (tags ?? Array.Empty<string>()).Where(x => MenuVocabulary.Tags.Contains(x)).ToList();
            dish.menuTags = tagList.Count == 0 ? null : MenuVocabulary.Join(tagList);

            // allergens ticked -> those; "No common allergens" ticked -> "None"; neither -> null (unknown, hidden from customers with allergies)
            var allergenList = (allergens ?? Array.Empty<string>()).Where(x => MenuVocabulary.Allergens.Contains(x)).ToList();
            dish.menuAllergens = allergenList.Count > 0 ? MenuVocabulary.Join(allergenList)
                               : noAllergens ? "None"
                               : null;

            // Optional photo: saved into wwwroot/images/menu
            if (image != null && image.Length > 0)
            {
                var ext = Path.GetExtension(image.FileName).ToLowerInvariant();
                if (!AllowedImageTypes.Contains(ext) || image.Length > 5 * 1024 * 1024)
                    return Fail("The photo must be a .jpg, .png or .webp file under 5 MB.");

                var folder = Path.Combine(_env.WebRootPath, "images", "menu");
                Directory.CreateDirectory(folder);
                var fileName = Guid.NewGuid().ToString("N") + ext;
                await using (var stream = System.IO.File.Create(Path.Combine(folder, fileName)))
                {
                    await image.CopyToAsync(stream);
                }
                // Store it the same way your existing rows do ("duck.jpg" or "/images/menu/duck.jpg")
                dish.menuImageUrl = await UsesFullImagePathsAsync() ? "/images/menu/" + fileName : fileName;
            }

            await _db.SaveChangesAsync();
            TempData["Toast"] = id.HasValue ? "Dish updated" : "Dish added";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteDishAsync(int id)
        {
            var dish = await _db.Menus.FindAsync(id);
            if (dish == null) return Fail("That dish no longer exists.");

            // Past orders keep their dishes, so a dish that was ordered can't be deleted
            if (await _db.OrderItems.AnyAsync(oi => oi.menuID == id))
                return Fail($"\"{dish.menuName}\" has past orders, so it can't be deleted. Mark it as sold out instead.");

            _db.Menus.Remove(dish);
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Dish deleted";
            return RedirectToPage();
        }

        // Called by the available / sold-out switch in the table (no page reload)
        public async Task<IActionResult> OnPostToggleAvailabilityAsync(int id, bool available)
        {
            var dish = await _db.Menus.FindAsync(id);
            if (dish == null) return NotFound();
            dish.isAvailable = available;
            await _db.SaveChangesAsync();
            return new OkResult();
        }

        // =====================================================================
        //  PROMOTIONS: add / edit, delete
        // =====================================================================
        public async Task<IActionResult> OnPostSavePromoAsync(int? id, string? title, string? code, string? label,
            string? description, string? discountType, decimal? discountValue, bool autoApply,
            DayOfWeek startDay, DayOfWeek endDay, DateTime? validFrom, DateTime? validUntil,
            bool active, int[]? dishIds,
            TimeSpan? startTime, TimeSpan? endTime, string? orderType, decimal? minSpend,
            int? maxUsesPerCustomer, bool limitResetsYearly, int? maxTotalUses, bool birthdayOnly)
        {
            title = title?.Trim();
            code = code?.Trim().ToUpperInvariant();
            label = label?.Trim();

            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(label))
                return Fail("Please fill in the title, promo code and label.", "promos");
            if (code.Length > 20)
                return Fail("The promo code can be at most 20 characters.", "promos");
            if (discountType != "Percentage" && discountType != "FixedAmount" && discountType != "Complimentary")
                return Fail("Please choose a discount type.", "promos");

            if (discountType == "Complimentary")
            {
                discountValue = null;
            }
            else
            {
                if (discountValue == null || discountValue <= 0)
                    return Fail("Please enter a discount value above 0.", "promos");
                if (discountType == "Percentage" && discountValue > 100)
                    return Fail("A percentage discount can't be more than 100.", "promos");
            }

            if (validFrom.HasValue && validUntil.HasValue && validUntil < validFrom)
                return Fail("\"Valid until\" must be on or after \"Valid from\".", "promos");

            // Time of day: both or neither. An end earlier than the start means "past midnight".
            if (startTime.HasValue != endTime.HasValue)
                return Fail("Please fill in both the start time and the end time, or leave both empty.", "promos");
            if (startTime.HasValue && startTime == endTime)
                return Fail("The start time and end time can't be the same.", "promos");

            if (minSpend.HasValue && minSpend <= 0)
                return Fail("Minimum spend must be above 0, or leave it empty.", "promos");
            if (maxUsesPerCustomer.HasValue && maxUsesPerCustomer < 1)
                return Fail("Uses per customer must be at least 1, or leave it empty.", "promos");
            if (maxTotalUses.HasValue && maxTotalUses < 1)
                return Fail("Total uses must be at least 1, or leave it empty.", "promos");
            if (maxTotalUses.HasValue && maxUsesPerCustomer.HasValue && maxTotalUses < maxUsesPerCustomer)
                return Fail("Total uses can't be lower than uses per customer.", "promos");

            // A day name that can't be read binds as Sunday, so check the binding errors as well
            if (!Enum.IsDefined(startDay) || !Enum.IsDefined(endDay)
                || ModelState[nameof(startDay)]?.Errors.Count > 0 || ModelState[nameof(endDay)]?.Errors.Count > 0)
                return Fail("Please choose a valid start day and end day.", "promos");

            // Birthday promotions: a limit per customer that starts again every year, otherwise one customer
            // could use it again and again all month, or only once in their life
            if (birthdayOnly && !maxUsesPerCustomer.HasValue)
                return Fail("A birthday promotion needs a \"uses per customer\" limit (for example 1).", "promos");
            if (birthdayOnly && !limitResetsYearly)
                return Fail("A birthday promotion's limit must reset every year (tick \"Per-customer limit resets every year\").", "promos");

            string orderKind = orderType == PromotionRules.DineIn || orderType == PromotionRules.Takeaway
                ? orderType!
                : PromotionRules.Any;
            if (!maxUsesPerCustomer.HasValue) limitResetsYearly = false;   // nothing to reset

            var thisId = id ?? 0;
            if (await _db.Promotions.AnyAsync(p => p.promoCode == code && p.promoID != thisId))
                return Fail($"The promo code {code} is already used by another promotion.", "promos");

            Promotion? promo;
            if (id.HasValue)
            {
                promo = await _db.Promotions.Include(p => p.Menus).FirstOrDefaultAsync(p => p.promoID == id.Value);
                if (promo == null) return Fail("That promotion no longer exists.", "promos");
            }
            else
            {
                // A new promotion belongs to the signed-in admin (RequireRoleFilter only lets admins reach this page)
                int? adminId = HttpContext.Session.GetUserId();
                var admin = await _db.Admins.FirstOrDefaultAsync(a => a.adminID == adminId);
                if (admin == null) return Fail("Your admin account could not be found. Please sign in again.", "promos");

                promo = new Promotion { Admin = admin };
                _db.Promotions.Add(promo);
            }

            promo.promoTitle = title;
            promo.promoCode = code;
            promo.promoLabel = label;
            promo.promoDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            promo.discountType = discountType;
            promo.discountValue = discountValue;
            promo.autoApply = autoApply;
            promo.startDay = startDay;
            promo.endDay = endDay;
            promo.validFrom = validFrom;
            promo.validUntil = validUntil;
            promo.promoActive = active;
            promo.startTime = startTime;
            promo.endTime = endTime;
            promo.orderType = orderKind;
            promo.minSpend = minSpend;
            promo.birthdayOnly = birthdayOnly;
            promo.maxUsesPerCustomer = maxUsesPerCustomer;
            promo.limitResetsYearly = limitResetsYearly;
            promo.maxTotalUses = maxTotalUses;

            // This promotion's own dishes
            var ids = dishIds ?? Array.Empty<int>();
            var chosen = await _db.Menus.Where(m => ids.Contains(m.menuID)).ToListAsync();
            promo.Menus.Clear();
            foreach (var dish in chosen) promo.Menus.Add(dish);

            await _db.SaveChangesAsync();
            TempData["Toast"] = id.HasValue ? "Promotion updated" : "Promotion added";
            return RedirectToPage(null, null, new { tab = "promos" });
        }

        public async Task<IActionResult> OnPostDeletePromoAsync(int id)
        {
            var promo = await _db.Promotions.FindAsync(id);
            if (promo == null) return Fail("That promotion no longer exists.", "promos");

            _db.Promotions.Remove(promo);
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Promotion deleted";
            return RedirectToPage(null, null, new { tab = "promos" });
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        // Shows a message at the bottom of the page after the redirect
        private IActionResult Fail(string message, string tab = "items")
        {
            TempData["Toast"] = message;
            return RedirectToPage(null, null, new { tab });
        }

        // Looks at the existing dishes to see how image paths are stored
        private async Task<bool> UsesFullImagePathsAsync()
        {
            var sample = await _db.Menus
                .Where(m => m.menuImageUrl != null && m.menuImageUrl != "")
                .Select(m => m.menuImageUrl)
                .FirstOrDefaultAsync();
            return sample != null && (sample.Contains('/') || sample.Contains('\\'));
        }

        // The page shows ~/images/menu/<file>, so keep only the file name
        private static string ImageFile(string? url) =>
            string.IsNullOrWhiteSpace(url) ? "" : Path.GetFileName(url.Trim());

        private static string DiscountText(Promotion p) => p.discountType switch
        {
            "Percentage" => $"{p.discountValue:0.##}% off",
            "FixedAmount" => $"RM {p.discountValue:0.##} off",
            _ => "Complimentary"
        };

        private static string DaysText(Promotion p) => PromotionRules.DaysText(p);

        // "Valid right now" / "Not valid right now · Available Fri – Sun" (only for promotions that are Active by date)
        private static string AvailabilityText(Promotion p)
        {
            if (StatusOf(p) != "Active") return "";
            var why = PromotionRules.NotAvailableReason(p, MalaysiaClock.Now, null, checkBirthday: false);
            return why == null ? "Valid right now" : "Not valid right now · " + why;
        }

        private static string DateText(DateTime? start, DateTime? end)
        {
            if (start.HasValue && end.HasValue) return $"{start.Value:d MMM yyyy} – {end.Value:d MMM yyyy}";
            if (start.HasValue) return $"From {start.Value:d MMM yyyy}";
            if (end.HasValue) return $"Until {end.Value:d MMM yyyy}";
            return "No end date";
        }

        // Badge text on the card: Inactive / Expired / Scheduled / Active
        private static string StatusOf(Promotion p) => PromotionRules.StatusOf(p, MalaysiaClock.Now);

        // =====================================================================
        //  Simple classes the page loops over
        // =====================================================================
        public class CategoryRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        public class MenuItemRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int CatId { get; set; }
            public string Cat { get; set; } = "";
            public decimal Price { get; set; }
            public bool Avail { get; set; }
            public string Image { get; set; } = "";
            public string Desc { get; set; } = "";
            public bool Sig { get; set; }
            public string[] TagList { get; set; } = Array.Empty<string>();
            public string[] AllergenList { get; set; } = Array.Empty<string>();
            public bool AllergensSet { get; set; }          // false = admin has not filled in the allergens yet

            // Used by the Edit button to fill the form
            public string Json => JsonSerializer.Serialize(new
            {
                id = Id,
                name = Name,
                catId = CatId,
                price = Price,
                desc = Desc,
                avail = Avail,
                sig = Sig,
                tags = TagList,
                allergens = AllergenList,
                noAllergens = AllergensSet && AllergenList.Length == 0
            });
        }

        public class PromoRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Code { get; set; } = "";
            public bool AutoApply { get; set; }
            public string Value { get; set; } = "";
            public string Unit { get; set; } = "";
            public string Dates { get; set; } = "";
            public string Status { get; set; } = "";
            public string Availability { get; set; } = "";
            public string[] Rules { get; set; } = Array.Empty<string>();
            public string[] Dishes { get; set; } = Array.Empty<string>();
            public object Edit { get; set; } = new { };

            // Used by the Edit button to fill the form
            public string Json => JsonSerializer.Serialize(Edit);
        }
    }
}