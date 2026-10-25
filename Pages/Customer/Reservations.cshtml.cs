using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Customer reservations: book a table (left) and see upcoming bookings (right).
    // The rules (times, free tables, cancelling) live in ReservationService.
    public class ReservationsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ReservationService _reservations;
        private readonly SettingsService _settings;

        public ReservationsModel(ApplicationDbContext db, ReservationService reservations, SettingsService settings)
        {
            _db = db;
            _reservations = reservations;
            _settings = settings;
        }

        // ---- What the form is showing (from ?partySize=&date=&area=, or the posted form) ----
        public int PartySize { get; set; } = 2;
        public DateTime Date { get; set; } = DateTime.Today;
        public string Area { get; set; } = "Any";
        public string? SelectedTime { get; set; }   // "19:00", kept when the booking fails
        public string? SpecialRequest { get; set; }

        // ---- Data for the page ----
        public SystemSetting Settings { get; set; } = new();
        public List<string> AreaNames { get; set; } = new();
        public List<DateTime> AllSlots { get; set; } = new();   // every bookable time that day (too-soon times left out)
        public List<DateTime> FreeSlots { get; set; } = new();  // the ones with a free table for this party
        public List<Reservation> Upcoming { get; set; } = new();
        public string CustomerName { get; set; } = "";   // shown in the review pop-up
        public string CustomerPhone { get; set; } = "";
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int partySize = 2, DateTime? date = null, string area = "Any")
        {
            var custId = HttpContext.Session.GetUserId();
            if (custId == null) return RedirectToPage("/Index");

            PartySize = partySize;
            Date = date?.Date ?? DateTime.Today;
            Area = area;

            // No date chosen yet: start on the first day that still has bookable times (e.g. late at night → tomorrow)
            if (date == null)
                for (var i = 0; i < 7 && (await _reservations.GetCustomerSlotsAsync(Date)).Count == 0; i++)
                    Date = Date.AddDays(1);
            await LoadAsync(custId.Value);
            return Page();
        }

        // Book: validate, pick a free table, save, then show the confirmation page
        public async Task<IActionResult> OnPostAsync(int partySize, DateTime reservationDate, string? reservationTime,
            string diningArea, string? specialRequest)
        {
            var custId = HttpContext.Session.GetUserId();
            if (custId == null) return RedirectToPage("/Index");

            if (TimeSpan.TryParse(reservationTime, out var time))
            {
                var start = reservationDate.Date + time;
                var areaId = await FindAreaIdAsync(diningArea);
                var (booking, error) = await _reservations.CreateAsync(custId.Value, start, partySize, areaId, specialRequest);
                if (booking != null)
                    return RedirectToPage("/Customer/ReservationConfirmed", new { id = booking.reservationID });
                ErrorMessage = error;
            }
            else
            {
                ErrorMessage = "Choose a time to continue.";
            }

            // Failed: show the same page again with the customer's choices and the error
            PartySize = partySize;
            Date = reservationDate.Date;
            Area = diningArea;
            SelectedTime = reservationTime;
            SpecialRequest = specialRequest;
            await LoadAsync(custId.Value);
            return Page();
        }

        // Cancel from the pop-up (reservationId comes from the pop-up's hidden field)
        public async Task<IActionResult> OnPostCancelAsync(int reservationId)
        {
            var custId = HttpContext.Session.GetUserId();
            if (custId == null) return RedirectToPage("/Index");

            var error = await _reservations.CancelByCustomerAsync(reservationId, custId.Value);
            TempData["ReservationMessage"] = error ?? "Your reservation has been cancelled.";
            TempData["ReservationMessageIsError"] = error != null;
            return RedirectToPage();
        }

        // Loads everything the page shows for the current PartySize / Date / Area
        private async Task LoadAsync(int custId)
        {
            Settings = await _settings.GetAsync();
            PartySize = Math.Clamp(PartySize, Settings.minPartySize, Settings.maxPartySize);

            AreaNames = await _db.DiningAreas.AsNoTracking()
                .Where(a => a.areaActive)
                .OrderBy(a => a.areaID)
                .Select(a => a.areaName)
                .ToListAsync();
            if (Area != "Any" && !AreaNames.Contains(Area)) Area = "Any";

            var areaId = await FindAreaIdAsync(Area);
            AllSlots = await _reservations.GetCustomerSlotsAsync(Date);
            FreeSlots = await _reservations.GetAvailableSlotsAsync(Date, PartySize, areaId);
            Upcoming = await _reservations.GetUpcomingForCustomerAsync(custId);

            var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.custID == custId);
            CustomerName = customer?.custName ?? "";
            CustomerPhone = customer?.custPhone ?? "";
        }

        // The form sends the area NAME ("Indoor"); the service needs its ID. "Any" = no preference (null).
        private async Task<int?> FindAreaIdAsync(string? areaName)
        {
            if (string.IsNullOrEmpty(areaName) || areaName == "Any") return null;
            var area = await _db.DiningAreas.AsNoTracking().FirstOrDefaultAsync(a => a.areaName == areaName);
            return area?.areaID;
        }
    }
}
