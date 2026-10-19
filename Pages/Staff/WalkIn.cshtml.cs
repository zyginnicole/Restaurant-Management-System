using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff – Book reservations: book a table for a guest on any date and time.
    // Opened from a table on the Reservations floor (pop-up → Book Table):
    //   /Staff/WalkIn?table=T-05   → that table is chosen
    //   /Staff/WalkIn?guest=12     → that customer is filled in (Guests page)
    // Static UI for now – backend notes:
    //  - OnGet: load the DiningTables that are not Maintenance, with the Pending / Confirmed / Seated bookings
    //    of the chosen day as "start-end" windows (data-busy; booking time + 90 min + 15 min buffer, from Settings).
    //    The page warns about a clash on screen; check it again in OnPost.
    //  - Find existing guest: search Customer by custName / custPhone; choosing one fills custId, name and phone.
    //  - OnPost ("Book here") – fields: reservationDate, reservationTime ("HH:mm"), tableId, partySize,
    //      custId (existing guest, optional), guestName + guestPhone (required), specialRequest
    //    Check the table has enough seats (tableCapacity >= partySize) and no booking overlaps, then
    //    new Reservation for that date/time, reservationStatus = "Confirmed" (staff made it), staffID = signed-in staff.
    //    When the guests arrive, staff press Seat on the Reservations page (→ "Seated", table "Occupied").
    //  - Leader decision needed: Reservation.custID is required, but a walk-in may have no account
    //    (e.g. one "Walk-in guest" Customer row, or a nullable custID + guestName / guestPhone).
    public class WalkInModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: create the Reservation and update the table first.
        public IActionResult OnPost(string tableId, string? guestName, DateOnly reservationDate, string reservationTime)
        {
            string who = string.IsNullOrWhiteSpace(guestName) ? "the guest" : guestName.Trim();
            TempData["StaffMessage"] = $"Booking for {who} confirmed at {tableId} on " +
                $"{reservationDate.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}, {reservationTime}.";
            return RedirectToPage("/Staff/Reservations");
        }
    }
}
