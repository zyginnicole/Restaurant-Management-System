using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Customer reservations: book a table and see upcoming bookings.
    // Static UI for now – backend notes:
    //  - OnGet: load the signed-in customer's upcoming reservations
    //    (custID from Session, reservationStatus "Pending" or "Confirmed" (incl. Seated), reservationDateTime >= today),
    //    and the time slots for the chosen party size / date / area.
    //  - A slot is "Full" when no table in that area has tableCapacity >= party size, is not "Maintenance",
    //    and has no overlapping Pending/Confirmed reservation at that time.
    //  - OnPost (form fields: partySize, reservationDate, reservationTime, diningArea, specialRequest):
    //    pick a free table, save a new Reservation with reservationStatus = "Pending",
    //    then RedirectToPage("/Customer/ReservationConfirmed").
    //  - OnPostCancel (field: reservationId, cancelReason): only the customer's own Pending/Confirmed booking
    //    → reservationStatus = "Cancelled".
    public class ReservationsModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: save the Reservation first, then redirect.
        public IActionResult OnPost()
        {
            return RedirectToPage("/Customer/ReservationConfirmed");
        }

        // UI flow only – backend: set reservationStatus = "Cancelled" first.
        public IActionResult OnPostCancel()
        {
            return RedirectToPage();
        }
    }
}
