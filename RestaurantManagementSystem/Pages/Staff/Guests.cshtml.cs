using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Guests: customer profiles to prepare each visit (read-only for staff, except staff notes).
    // Static UI for now – backend notes:
    //  - OnGet: load Customers with their Reservations (+ DiningTable) and Orders (+ Payment).
    //    Per guest: visits = Completed reservations, no-shows = "No-show" reservations,
    //    average spend = average Payment.paymentAmount of their Paid orders, last visit = latest Completed reservation.
    //    Tabs: All · Tonight (a Pending / Confirmed / Seated reservation today) · Regulars (3 or more visits).
    //    ?guest=12 shows that customer on the right.
    //  - Profile fields: custName, custPhone, custEmail, custCreateAt, seatingPref, DietaryPref, kitchenNotes.
    //    Customers edit these themselves on their Profile page; staff can't change them here.
    //  - Walk-ins without an account are not customers, so they are not listed (see the walk-in decision for the leader).
    //  - Staff notes have no field yet (ask the leader, e.g. Customer.staffNotes, max 500) – OnPostNote is UI only.
    public class GuestsModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only – backend: save Customer.staffNotes first (fields: custId, staffNote)
        public IActionResult OnPostNote(int custId, string? staffNote)
        {
            TempData["StaffMessage"] = "Staff note saved.";
            return RedirectToPage(new { guest = custId });
        }
    }
}
