using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Reservations: floor status + tonight's bookings, with the actions for each status.
    // Static UI for now – backend notes:
    //  - OnGet(DateTime? date, string? service): load the Reservations for that day (default today) with
    //    Customer + DiningTable, and all DiningTables for the floor grid. List them newest first
    //    (OrderByDescending reservationDateTime).
    //  - Clicking a booking opens the Reservation details pop-up (filled from the row's data-* values).
    //    ?view=R-0043 opens it straight away; ?status=Pending picks Pending in the status filter (Dashboard "2 need approval").
    //    The same pop-up (Pages/Staff/_ReservationDetailPopup) is also on the Staff Dashboard; its forms post here
    //    with returnUrl, so the staff member goes back to the Dashboard.
    //    Seated bookings are read-only in the pop-up (no Save / Complete) – they complete when the bill is paid.
    //  - Reservation.reservationStatus values (agreed, stored exactly like this):
    //      Pending, Confirmed, Seated, Completed, Cancelled, No-show, Declined
    //    CSS class = "is-" + status without the hyphen, lower case: is-noshow, is-declined (both grey like Cancelled).
    //  - Floor tiles: tableStatus Available → Free, Occupied → Seated, Reserved → Reserved (time of the next booking),
    //    Maintenance → Unavailable.
    //  - Walk-ins: Reservation with no online booking – see /Staff/WalkIn (needs a decision on custID).
    //  - Every action posts reservationId (and the extra fields below), then redirects back with
    //    TempData["StaffMessage"], which the page shows in the green box.
    public class ReservationsModel : PageModel
    {
        // Page to go back to after an action ("/Staff/Dashboard" when the pop-up was used there). Local URLs only.
        [BindProperty]
        public string? ReturnUrl { get; set; }

        public void OnGet()
        {
        }

        // ---- UI flow only (no saving yet) – backend: update the Reservation / DiningTable first ----

        // Details pop-up "Save changes" (fields: reservationDate, reservationTime, partySize, tableId, specialRequest;
        // a different tableId = move table). Opens the same pop-up again after saving.
        public IActionResult OnPostSave(string reservationId)
        {
            TempData["StaffMessage"] = $"{reservationId} saved.";
            return Url.IsLocalUrl(ReturnUrl)
                ? LocalRedirect($"{ReturnUrl}?view={reservationId}")
                : RedirectToPage(new { view = reservationId });
        }

        // Pending → Confirmed (staffID = signed-in staff). From the pop-up it also saves the form fields.
        public IActionResult OnPostConfirm(string reservationId) => Done($"{reservationId} confirmed.");

        // Pending → Declined (field: cancelReason – shown to the guest in a Notification); table back to Available
        public IActionResult OnPostDecline(string reservationId, string? cancelReason) => Done($"{reservationId} declined.");

        // Confirmed → Seated; table Occupied
        public IActionResult OnPostSeat(string reservationId) => Done($"{reservationId} seated.");

        // Confirmed → No-show; table back to Available (counts in the guest's no-show history)
        public IActionResult OnPostNoShow(string reservationId) => Done($"{reservationId} marked as no-show.");

        // Confirmed → Cancelled (field: cancelReason); table back to Available
        public IActionResult OnPostCancel(string reservationId, string? cancelReason) => Done($"{reservationId} cancelled.");

        // Seated → Completed has no button: it happens by itself when the order is paid (Billing OnPostPay
        // sets Reservation.reservationStatus "Completed" and the table back to Available).
        // There is no Move table either: a Pending / Confirmed booking changes table in the details pop-up (OnPostSave);
        // a Seated booking can't be changed.

        private IActionResult Done(string message)
        {
            TempData["StaffMessage"] = message;
            return Url.IsLocalUrl(ReturnUrl) ? LocalRedirect(ReturnUrl!) : RedirectToPage();
        }
    }
}
