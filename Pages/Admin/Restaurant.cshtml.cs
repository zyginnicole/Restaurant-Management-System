using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Pages.Admin
{
    public class RestaurantModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public RestaurantModel(ApplicationDbContext db)
        {
            _db = db;
        }

        // The page (.cshtml) reads these lists through "Model"
        public List<AreaRow> Areas { get; set; } = new();
        public List<TableRow> Tables { get; set; } = new();
        public List<HourRow> Hours { get; set; } = new();
        public List<CategoryRow> Categories { get; set; } = new();
        public int SlotInterval { get; set; } = 30;
        public int LastSeating { get; set; } = 60;
        public string Today => DateTime.Today.ToString("yyyy-MM-dd");
        public string SlotDate { get; set; } = "";     // date shown in Booking Slots (kept after a save)

        private static readonly string[] Statuses = { "Available", "Occupied", "Reserved", "Maintenance" };

        private static readonly DayOfWeek[] DayOrder =
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };

        // =====================================================================
        //  READ: runs when the page is opened
        // =====================================================================
        public async Task OnGetAsync()
        {
            // keep the date the admin was looking at in Booking Slots after saving hours / slot settings
            SlotDate = DateTime.TryParse(TempData["SlotDate"] as string, out var keptDate) && keptDate.Date >= DateTime.Today
                ? keptDate.ToString("yyyy-MM-dd")
                : Today;

            var areas = await _db.DiningAreas
                .Include(a => a.DiningTables)
                .OrderBy(a => a.areaName)
                .ToListAsync();

            Areas = areas.Select(a => new AreaRow
            {
                Id = a.areaID,
                Name = a.areaName,
                Code = a.areaCode,
                Description = a.areaDescription ?? "",
                Active = a.areaActive,
                TableCount = a.DiningTables.Count,
                Covers = a.DiningTables.Sum(t => t.tableCapacity)   // total pax the area can seat
            }).ToList();

            Tables = areas
                .SelectMany(a => a.DiningTables.Select(t => new TableRow
                {
                    Id = t.tableID,
                    Number = t.tableNumber,
                    Seats = t.tableCapacity,
                    Status = t.tableStatus,
                    AreaId = a.areaID,
                    Area = a.areaName
                }))
                .OrderBy(t => t.Area)
                .ThenBy(t => TableNumbering.NumberPart(t.Number))
                .ThenBy(t => t.Number)
                .ToList();

            // ---- Operating hours & slot settings ----
            var restaurant = await GetRestaurantAsync();
            await EnsureHoursAsync(restaurant);
            SlotInterval = restaurant.slotIntervalMinutes;
            LastSeating = restaurant.lastSeatingMinutes;

            var saved = await _db.OperatingHours
                .Where(h => h.restaurantID == restaurant.restaurantID)
                .ToListAsync();

            Hours = DayOrder.Select(d =>
            {
                var h = saved.First(x => x.dayOfWeek == d);
                return new HourRow
                {
                    Day = d,
                    Open = !h.isClosed,
                    From = h.openTime.ToString(@"hh\:mm"),
                    To = h.closeTime.ToString(@"hh\:mm")
                };
            }).ToList();

            // ---- Menu categories ----
            Categories = await _db.MenuCategories
                .OrderBy(c => c.categoryID)
                .Select(c => new CategoryRow
                {
                    Id = c.categoryID,
                    Name = c.categoryName,
                    Active = c.categoryActive,
                    Items = c.Menus.Count
                })
                .ToListAsync();
        }

        // =====================================================================
        //  OPERATING HOURS: save the 7 weekdays
        // =====================================================================
        public async Task<IActionResult> OnPostSaveHoursAsync(List<HourInput> hours, DateTime? date)
        {
            if (date.HasValue) TempData["SlotDate"] = date.Value.ToString("yyyy-MM-dd");

            if (hours == null || hours.Count != 7 || hours.Select(h => h.Day).Distinct().Count() != 7)
                return BackTo("tab-hours", "The opening hours were not received. Please try again.");

            foreach (var h in hours)
                if (h.Open && h.To <= h.From)
                    return BackTo("tab-hours", $"{h.Day}: closing time must be after opening time.");

            var restaurant = await GetRestaurantAsync();
            await EnsureHoursAsync(restaurant);
            var rows = await _db.OperatingHours
                .Where(x => x.restaurantID == restaurant.restaurantID)
                .ToListAsync();

            foreach (var h in hours)
            {
                var row = rows.First(x => x.dayOfWeek == h.Day);
                row.isClosed = !h.Open;
                row.openTime = h.From;
                row.closeTime = h.To;
            }
            await _db.SaveChangesAsync();

            // Existing reservations are never changed, but tell the admin if some now fall outside the hours
            var upcoming = await _db.Reservations
                .Where(r => r.reservationDateTime >= DateTime.Now
                         && r.reservationStatus != "Cancelled" && r.reservationStatus != "Completed")
                .Select(r => r.reservationDateTime)
                .ToListAsync();

            int outside = upcoming.Count(dt =>
            {
                var row = rows.First(x => x.dayOfWeek == dt.DayOfWeek);
                return row.isClosed || dt.TimeOfDay < row.openTime || dt.TimeOfDay > row.closeTime;
            });

            return BackTo("tab-hours", outside == 0
                ? "Opening hours saved"
                : $"Opening hours saved. {outside} upcoming reservation(s) fall outside the new hours.");
        }

        // =====================================================================
        //  SLOTS: interval settings, list slots of a date, block / unblock a slot
        // =====================================================================
        public async Task<IActionResult> OnPostSaveSlotSettingsAsync(int interval, int lastSeating, DateTime? date)
        {
            if (date.HasValue) TempData["SlotDate"] = date.Value.ToString("yyyy-MM-dd");

            if (!new[] { 15, 30, 60 }.Contains(interval) || !new[] { 30, 60, 90 }.Contains(lastSeating))
                return BackTo("tab-hours", "Please choose a valid slot interval and last seating time.");

            var restaurant = await GetRestaurantAsync();
            bool intervalChanged = restaurant.slotIntervalMinutes != interval;

            restaurant.slotIntervalMinutes = interval;
            restaurant.lastSeatingMinutes = lastSeating;

            // Slot times change with a new interval, so old blocks would no longer line up: clear the upcoming ones
            if (intervalChanged)
            {
                var old = await _db.BlockedSlots
                    .Where(b => b.restaurantID == restaurant.restaurantID && b.slotStart >= DateTime.Today)
                    .ToListAsync();
                _db.BlockedSlots.RemoveRange(old);
            }

            await _db.SaveChangesAsync();
            return BackTo("tab-hours", intervalChanged
                ? "Slot settings saved. Upcoming blocked slots were cleared because the interval changed."
                : "Slot settings saved");
        }

        // Called by the page script: slots of one date (no page reload)
        public async Task<IActionResult> OnGetSlotsAsync(DateTime date)
        {
            var day = date.Date;
            var next = day.AddDays(1);
            var restaurant = await GetRestaurantAsync();
            await EnsureHoursAsync(restaurant);

            var hour = await _db.OperatingHours.AsNoTracking()
                .FirstOrDefaultAsync(h => h.restaurantID == restaurant.restaurantID && h.dayOfWeek == day.DayOfWeek);
            var times = SlotHelper.Generate(hour, restaurant.slotIntervalMinutes, restaurant.lastSeatingMinutes);

            var blocked = await _db.BlockedSlots.AsNoTracking()
                .Where(b => b.restaurantID == restaurant.restaurantID && b.slotStart >= day && b.slotStart < next)
                .Select(b => b.slotStart)
                .ToListAsync();

            var booked = await _db.Reservations.AsNoTracking()
                .Where(r => r.reservationDateTime >= day && r.reservationDateTime < next
                         && r.reservationStatus != "Cancelled")
                .Select(r => r.reservationDateTime)
                .ToListAsync();

            var slots = times.Select(t =>
            {
                var start = day + t;
                return new
                {
                    time = t.ToString(@"hh\:mm"),
                    blocked = blocked.Contains(start),
                    reservations = booked.Count(b => b == start),
                    past = start <= DateTime.Now
                };
            }).ToList();

            return new JsonResult(new { closed = times.Count == 0, slots });
        }

        // Called by clicking a slot: blocked = true stops new bookings, false allows them again
        public async Task<IActionResult> OnPostToggleSlotAsync(DateTime date, string time, bool blocked)
        {
            if (!TimeSpan.TryParse(time, out var t)) return BadRequest();

            var restaurant = await GetRestaurantAsync();
            var day = date.Date;

            var hour = await _db.OperatingHours.AsNoTracking()
                .FirstOrDefaultAsync(h => h.restaurantID == restaurant.restaurantID && h.dayOfWeek == day.DayOfWeek);

            // only a real slot of that day, and not one that has already started
            if (!SlotHelper.Generate(hour, restaurant.slotIntervalMinutes, restaurant.lastSeatingMinutes).Contains(t))
                return BadRequest();
            var start = day + t;
            if (start <= DateTime.Now) return BadRequest();

            var existing = await _db.BlockedSlots
                .FirstOrDefaultAsync(b => b.restaurantID == restaurant.restaurantID && b.slotStart == start);

            if (blocked && existing == null)
                _db.BlockedSlots.Add(new BlockedSlot { restaurantID = restaurant.restaurantID, slotStart = start });
            else if (!blocked && existing != null)
                _db.BlockedSlots.Remove(existing);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // someone else blocked the same slot at the same moment: it is blocked either way
            }
            return new OkResult();
        }

        // =====================================================================
        //  MENU CATEGORIES: add / edit, visible switch, delete
        // =====================================================================
        public async Task<IActionResult> OnPostSaveCategoryAsync(int? id, string? name, bool active)
        {
            name = name?.Trim();

            if (string.IsNullOrWhiteSpace(name))
                return BackTo("tab-categories", "Please enter a category name.");
            if (name.Length > 50)
                return BackTo("tab-categories", "The category name can be at most 50 characters.");

            var thisId = id ?? 0;
            if (await _db.MenuCategories.AnyAsync(c => c.categoryName == name && c.categoryID != thisId))
                return BackTo("tab-categories", $"A category called \"{name}\" already exists.");

            MenuCategory? category;
            if (id.HasValue)
            {
                category = await _db.MenuCategories.FindAsync(id.Value);
                if (category == null) return BackTo("tab-categories", "That category no longer exists.");
            }
            else
            {
                category = new MenuCategory();
                _db.MenuCategories.Add(category);
            }

            category.categoryName = name;
            category.categoryActive = active;

            await _db.SaveChangesAsync();
            return BackTo("tab-categories", id.HasValue ? "Category updated" : "Category added");
        }

        // Called by the Visible / Hidden switch on a category row (no page reload)
        public async Task<IActionResult> OnPostToggleCategoryAsync(int id, bool active)
        {
            var category = await _db.MenuCategories.FindAsync(id);
            if (category == null) return NotFound();

            category.categoryActive = active;
            await _db.SaveChangesAsync();
            return new OkResult();
        }

        public async Task<IActionResult> OnPostDeleteCategoryAsync(int id)
        {
            var category = await _db.MenuCategories.FindAsync(id);
            if (category == null) return BackTo("tab-categories", "That category no longer exists.");

            if (await _db.Menus.AnyAsync(m => m.categoryID == id))
                return BackTo("tab-categories", $"\"{category.categoryName}\" still has menu items. Move or delete them first, or mark it Hidden.");

            _db.MenuCategories.Remove(category);
            await _db.SaveChangesAsync();
            return BackTo("tab-categories", "Category deleted");
        }

        // =====================================================================
        //  TABLES: add / edit, delete
        // =====================================================================
        public async Task<IActionResult> OnPostSaveTableAsync(int? id, int areaId, int capacity, string? status)
        {
            if (capacity < 1 || capacity > 20)
                return Fail("Seats must be between 1 and 20.");
            if (status == null || !Statuses.Contains(status))
                return Fail("Please choose a valid status.");
            if (!await _db.DiningAreas.AnyAsync(a => a.areaID == areaId))
                return Fail("Please choose a dining area.");

            DiningTable? table;
            if (id.HasValue)
            {
                table = await _db.DiningTables.FindAsync(id.Value);
                if (table == null) return Fail("That table no longer exists.");

                // Moved to another area: it gets the next number there. Otherwise it keeps its number.
                if (table.areaID != areaId)
                {
                    var number = await NextNumberAsync(areaId);
                    if (number == null) return Fail("That area already has 99 tables, the highest table number.");
                    table.areaID = areaId;
                    table.tableNumber = number;
                }
            }
            else
            {
                var number = await NextNumberAsync(areaId);
                if (number == null) return Fail("That area already has 99 tables, the highest table number.");

                table = new DiningTable { areaID = areaId, tableNumber = number };
                _db.DiningTables.Add(table);
            }

            table.tableCapacity = capacity;
            table.tableStatus = status;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // The unique index on the table number stopped a duplicate, e.g. two admins adding at once
                return Fail("That table number was just taken. Please try again.");
            }

            TempData["Toast"] = id.HasValue ? $"Table {table.tableNumber} updated" : $"Table {table.tableNumber} added";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDeleteTableAsync(int id)
        {
            var table = await _db.DiningTables.FindAsync(id);
            if (table == null) return Fail("That table no longer exists.");

            // Reservations keep their table, so a table that was booked can't be deleted
            if (await _db.Reservations.AnyAsync(r => r.tableID == id))
                return Fail($"Table {table.tableNumber} has reservations, so it can't be deleted. Set it to Maintenance instead.");

            _db.DiningTables.Remove(table);
            await _db.SaveChangesAsync();
            TempData["Toast"] = $"Table {table.tableNumber} deleted";
            return RedirectToPage();
        }

        // =====================================================================
        //  DINING AREAS: add / edit, active switch, delete
        // =====================================================================
        public async Task<IActionResult> OnPostSaveAreaAsync(int? id, string? name, string? description, bool active,
            int tableCount, int seatsPerTable)
        {
            name = name?.Trim();
            description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

            if (string.IsNullOrWhiteSpace(name))
                return Fail("Please enter an area name.");
            if (name.Length > 50)
                return Fail("The area name can be at most 50 characters.");
            if (description != null && description.Length > 200)
                return Fail("The description can be at most 200 characters.");

            // Only a NEW area can start with tables (existing areas are changed in the Tables section)
            if (!id.HasValue)
            {
                if (tableCount < 0 || tableCount > 99)
                    return Fail("Number of tables must be between 0 and 99.");
                if (tableCount > 0 && (seatsPerTable < 1 || seatsPerTable > 20))
                    return Fail("Seats per table must be between 1 and 20.");
            }

            var thisId = id ?? 0;
            if (await _db.DiningAreas.AnyAsync(a => a.areaName == name && a.areaID != thisId))
                return Fail($"An area called \"{name}\" already exists.");

            DiningArea? area;
            if (id.HasValue)
            {
                area = await _db.DiningAreas.Include(a => a.DiningTables).FirstOrDefaultAsync(a => a.areaID == id.Value);
                if (area == null) return Fail("That area no longer exists.");
            }
            else
            {
                area = new DiningArea();
                _db.DiningAreas.Add(area);
            }

            // The area code starts every table number (I-01 ...). A new name can give a new code:
            // then this area's tables keep their numbers but get the new code (G-02 -> T-02 for "Terrace").
            string oldCode = area.areaCode;
            bool renamed = area.areaName != name;
            if (!id.HasValue || renamed)
            {
                var otherCodes = await _db.DiningAreas.Where(a => a.areaID != thisId).Select(a => a.areaCode).ToListAsync();
                string firstLetter = TableNumbering.CodeFor(name, Array.Empty<string>())[..1];

                // keep the code while it still starts with the name's first letter, so a small rename doesn't renumber tables
                if (!id.HasValue || !oldCode.StartsWith(firstLetter, StringComparison.OrdinalIgnoreCase))
                    area.areaCode = TableNumbering.CodeFor(name, otherCodes);
            }

            area.areaName = name;
            area.areaDescription = description;
            area.areaActive = active;

            if (!id.HasValue)
            {
                // New area: create its tables I-01, I-02 ... (its own code) with the chosen pax
                for (int i = 1; i <= tableCount; i++)
                {
                    area.DiningTables.Add(new DiningTable
                    {
                        tableNumber = TableNumbering.Format(area.areaCode, i),
                        tableCapacity = seatsPerTable,
                        tableStatus = "Available"
                    });
                }
            }
            else if (area.areaCode != oldCode)
            {
                foreach (var t in area.DiningTables)
                    t.tableNumber = TableNumbering.Format(area.areaCode, TableNumbering.NumberPart(t.tableNumber));
            }

            await _db.SaveChangesAsync();
            TempData["Toast"] = id.HasValue
                ? area.areaCode != oldCode && area.DiningTables.Count > 0
                    ? $"Dining area updated. Its tables now start with {area.areaCode}- (e.g. {area.DiningTables.OrderBy(t => t.tableNumber).First().tableNumber})."
                    : "Dining area updated"
                : tableCount == 1 ? $"Dining area added with table {TableNumbering.Format(area.areaCode, 1)}"
                : tableCount > 1 ? $"Dining area added with tables {TableNumbering.Format(area.areaCode, 1)} to {TableNumbering.Format(area.areaCode, tableCount)}"
                : $"Dining area added. Its tables will be numbered {area.areaCode}-01, {area.areaCode}-02 ...";
            return RedirectToPage();
        }

        // Called by the Active / Inactive switch on an area card (no page reload)
        public async Task<IActionResult> OnPostToggleAreaAsync(int id, bool active)
        {
            var area = await _db.DiningAreas.FindAsync(id);
            if (area == null) return NotFound();

            area.areaActive = active;
            await _db.SaveChangesAsync();
            return new OkResult();
        }

        public async Task<IActionResult> OnPostDeleteAreaAsync(int id)
        {
            var area = await _db.DiningAreas.FindAsync(id);
            if (area == null) return Fail("That area no longer exists.");

            if (await _db.DiningTables.AnyAsync(t => t.areaID == id))
                return Fail($"\"{area.areaName}\" still has tables. Move or delete them first, or mark the area Inactive.");

            _db.DiningAreas.Remove(area);
            await _db.SaveChangesAsync();
            TempData["Toast"] = "Dining area deleted";
            return RedirectToPage();
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        // Shows a message at the bottom of the page after the redirect
        private IActionResult Fail(string message)
        {
            TempData["Toast"] = message;
            return RedirectToPage();
        }

        // Shows a message and re-opens the tab the admin was on (tab-layout, tab-hours, tab-categories)
        private IActionResult BackTo(string tab, string message)
        {
            TempData["Toast"] = message;
            TempData["Tab"] = tab;
            return RedirectToPage();
        }

        // The restaurant this system manages (the first one). Created once if the table is empty.
        private async Task<Restaurant> GetRestaurantAsync()
        {
            var restaurant = await _db.Restaurants.OrderBy(r => r.restaurantID).FirstOrDefaultAsync();
            if (restaurant == null)
            {
                restaurant = new Restaurant { restaurantName = "My Restaurant" };
                _db.Restaurants.Add(restaurant);
                await _db.SaveChangesAsync();
            }
            return restaurant;
        }

        // Makes sure the restaurant has one OperatingHour row for each weekday (default 12:00 - 22:30)
        private async Task EnsureHoursAsync(Restaurant restaurant)
        {
            var have = await _db.OperatingHours
                .Where(h => h.restaurantID == restaurant.restaurantID)
                .Select(h => h.dayOfWeek)
                .ToListAsync();

            var missing = DayOrder.Where(d => !have.Contains(d)).ToList();
            if (missing.Count == 0) return;

            foreach (var d in missing)
                _db.OperatingHours.Add(new OperatingHour
                {
                    restaurantID = restaurant.restaurantID,
                    dayOfWeek = d,
                    openTime = new TimeSpan(12, 0, 0),
                    closeTime = new TimeSpan(22, 30, 0),
                    isClosed = false
                });
            await _db.SaveChangesAsync();
        }

        // Next table number inside one area: its code + (the highest number there + 1), e.g. I-05.
        // Returns null when the area already has number 99.
        private async Task<string?> NextNumberAsync(int areaId)
        {
            var code = await _db.DiningAreas.Where(a => a.areaID == areaId).Select(a => a.areaCode).FirstAsync();
            var numbers = await _db.DiningTables
                .Where(t => t.areaID == areaId)
                .Select(t => t.tableNumber)
                .ToListAsync();

            int max = numbers.Count == 0 ? 0 : numbers.Max(TableNumbering.NumberPart);
            return max >= TableNumbering.MaxPerArea ? null : TableNumbering.Format(code, max + 1);
        }

        // =====================================================================
        //  Simple classes the page loops over
        // =====================================================================
        public class AreaRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Code { get; set; } = "";          // starts the area's table numbers, e.g. "I" -> I-01
            public string Description { get; set; } = "";
            public bool Active { get; set; }
            public int TableCount { get; set; }
            public int Covers { get; set; }

            // Used by the Edit button to fill the form
            public string Json => JsonSerializer.Serialize(new { id = Id, name = Name, desc = Description, active = Active });
        }

        public class HourRow
        {
            public DayOfWeek Day { get; set; }
            public string Name => Day.ToString();
            public bool Open { get; set; }
            public string From { get; set; } = "12:00";
            public string To { get; set; } = "22:30";
        }

        // What the Save hours form posts for each weekday
        public class HourInput
        {
            public DayOfWeek Day { get; set; }
            public bool Open { get; set; }
            public TimeSpan From { get; set; }
            public TimeSpan To { get; set; }
        }

        public class CategoryRow
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public bool Active { get; set; }
            public int Items { get; set; }

            // Used by the Edit button to fill the form
            public string Json => JsonSerializer.Serialize(new { id = Id, name = Name });
        }

        public class TableRow
        {
            public int Id { get; set; }
            public string Number { get; set; } = "";
            public int Seats { get; set; }
            public string Status { get; set; } = "";
            public int AreaId { get; set; }
            public string Area { get; set; } = "";
        }
    }
}