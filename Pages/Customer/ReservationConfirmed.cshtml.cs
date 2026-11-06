using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Booking confirmed: shown right after a reservation is made (Reservations OnPost redirects here with ?id=).
    // reservationStatus decides the wording: "Pending" = waiting for staff, "Confirmed" = booked.
    public class ReservationConfirmedModel : PageModel
    {
        private readonly ReservationService _reservations;
        private readonly SettingsService _settings;

        public ReservationConfirmedModel(ReservationService reservations, SettingsService settings)
        {
            _reservations = reservations;
            _settings = settings;
        }

        public Reservation Booking { get; set; } = null!;
        public SystemSetting Settings { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            var custId = HttpContext.Session.GetUserId();
            if (custId == null) return RedirectToPage("/Index");

            // Only the customer's own booking (someone else's id → 404)
            var booking = await _reservations.GetCustomerReservationAsync(id, custId.Value);
            if (booking == null) return NotFound();

            Booking = booking;
            Settings = await _settings.GetAsync();
            return Page();
        }
    }
}
