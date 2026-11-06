using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Edit reservation: change party size, date, time or special request.
    // (Cancelling is done from the Reservations page.) The area stays the same as the booked table.
    // The rules (deadline, free table, status after a change) live in ReservationService.UpdateByCustomerAsync.
    public class EditReservationModel : PageModel
    {
        private readonly ReservationService _reservations;
        private readonly SettingsService _settings;

        public EditReservationModel(ReservationService reservations, SettingsService settings)
        {
            _reservations = reservations;
            _settings = settings;
        }

        public Reservation Booking { get; set; } = null!;
        public SystemSetting Settings { get; set; } = new();

        // ---- What the form is showing (from ?partySize=&date=, or the posted form) ----
        public int PartySize { get; set; }
        public DateTime Date { get; set; }
        public string? SelectedTime { get; set; }
        public string? SpecialRequest { get; set; }

        // ---- Availability for PartySize / Date in the booking's area (this booking counts as free) ----
        public List<DateTime> AllSlots { get; set; } = new();
        public List<DateTime> FreeSlots { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(int id, int? partySize = null, DateTime? date = null)
        {
            var custId = HttpContext.Session.GetUserId();
            if (custId == null) return RedirectToPage("/Index");

            var result = await LoadBookingAsync(id, custId.Value);
            if (result != null) return result;

            // Start with the booking's own values; ?partySize=&date= after the customer changes them
            PartySize = partySize ?? Booking.partySize;
            Date = date?.Date ?? Booking.reservationDateTime.Date;
            SelectedTime = Date == Booking.reservationDateTime.Date ? Booking.reservationDateTime.ToString("HH:mm") : null;
            SpecialRequest = Booking.specialRequest;
            await LoadSlotsAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(int reservationId, int partySize, DateTime reservationDate,
            string? reservationTime, string? specialRequest)
        {
            var custId = HttpContext.Session.GetUserId();
            if (custId == null) return RedirectToPage("/Index");

            if (TimeSpan.TryParse(reservationTime, out var time))
            {
                var error = await _reservations.UpdateByCustomerAsync(reservationId, custId.Value,
                    reservationDate.Date + time, partySize, specialRequest);
                if (error == null)
                {
                    TempData["ReservationMessage"] = "R-" + reservationId.ToString("D4") + " has been updated.";
                    return RedirectToPage("/Customer/Reservations");
                }
                ErrorMessage = error;
            }
            else
            {
                ErrorMessage = "Choose a time to continue.";
            }

            // Failed: show the form again with the customer's choices and the error
            var result = await LoadBookingAsync(reservationId, custId.Value);
            if (result != null) return result;
            PartySize = partySize;
            Date = reservationDate.Date;
            SelectedTime = reservationTime;
            SpecialRequest = specialRequest;
            await LoadSlotsAsync();
            return Page();
        }

        // Loads the booking. Returns a redirect / 404 when it can't be edited, or null when it can.
        private async Task<IActionResult?> LoadBookingAsync(int id, int custId)
        {
            var booking = await _reservations.GetCustomerReservationAsync(id, custId);
            if (booking == null) return NotFound();

            Settings = await _settings.GetAsync();
            var editable = (booking.reservationStatus == ReservationStatus.Pending || booking.reservationStatus == ReservationStatus.Confirmed)
                           && DateTime.Now <= ReservationService.CancelDeadline(booking, Settings);
            if (!editable)
            {
                TempData["ReservationMessage"] = "This reservation can no longer be changed online. Please call us.";
                TempData["ReservationMessageIsError"] = true;
                return RedirectToPage("/Customer/Reservations");
            }

            Booking = booking;
            return null;
        }

        private async Task LoadSlotsAsync()
        {
            PartySize = Math.Clamp(PartySize, Settings.minPartySize, Settings.maxPartySize);
            AllSlots = await _reservations.GetCustomerSlotsAsync(Date);
            FreeSlots = await _reservations.GetAvailableSlotsAsync(Date, PartySize, Booking.DiningTable?.areaID, Booking.reservationID);
        }
    }
}
