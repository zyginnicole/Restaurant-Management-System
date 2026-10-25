using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Pages.Admin
{
    public class SettingsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public SettingsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        // The form fields (prefix "S" in the page)
        [BindProperty]
        public SystemSetting S { get; set; } = new();

        // Which tab is open (general / reservations / billing / security)
        [BindProperty(SupportsGet = true)]
        public string? Tab { get; set; }

        public async Task OnGetAsync()
        {
            S = await LoadAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (S.minPartySize > S.maxPartySize)
                ModelState.AddModelError("S.maxPartySize", "Maximum party size must be at least the minimum.");

            if (!ModelState.IsValid)
            {
                // Stay on the page so nothing the admin typed is lost
                ViewData["Toast"] = ModelState.Values
                    .SelectMany(v => v.Errors).First().ErrorMessage;
                return Page();
            }

            var row = await LoadAsync();
            S.settingID = row.settingID;
            _db.Entry(row).CurrentValues.SetValues(S);
            await _db.SaveChangesAsync();

            TempData["Toast"] = "Settings saved";
            return RedirectToPage(null, null, new { tab = Tab });
        }

        // Gets the single settings row; creates it with the default values the first time
        private async Task<SystemSetting> LoadAsync()
        {
            var row = await _db.SystemSettings.FirstOrDefaultAsync();
            if (row == null)
            {
                row = new SystemSetting();
                _db.SystemSettings.Add(row);
                await _db.SaveChangesAsync();
            }
            return row;
        }
    }
}