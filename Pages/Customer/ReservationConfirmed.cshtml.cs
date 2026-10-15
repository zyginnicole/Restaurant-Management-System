using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Booking confirmed: shown right after a reservation is made (Reservations OnPost redirects here).
    // Static UI for now – backend notes:
    //  - OnGet(int id): load the Reservation (+ DiningTable, Customer). Return NotFound() if it doesn't
    //    exist or doesn't belong to the signed-in customer (custID from Session).
    //  - reservationStatus decides the wording: "Pending" = waiting for staff, "Confirmed" = booked.
    //  - Time range = reservationDateTime to + dining duration (90 minutes in the example).
    public class ReservationConfirmedModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
