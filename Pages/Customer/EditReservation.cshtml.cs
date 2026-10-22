using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Edit reservation: change party size, date, time or special request.
    // (Cancelling is done from the Reservations page – Cancel button on each upcoming reservation.)
    // Static UI for now – backend notes:
    //  - OnGet(int id): load the Reservation; NotFound() if it isn't the signed-in customer's
    //    or its status isn't "Pending"/"Confirmed".
    //  - Availability: same slot check as the Reservations page, ignoring this booking itself.
    //  - OnPost (fields: reservationId, partySize, reservationDate, reservationTime, specialRequest):
    //    save the changes, then RedirectToPage("/Customer/Reservations").
    //    A changed date/time/party size goes back to "Pending" for staff to confirm.
    public class EditReservationModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: save the changes first, then redirect.
        public IActionResult OnPost()
        {
            return RedirectToPage("/Customer/Reservations");
        }
    }
}
