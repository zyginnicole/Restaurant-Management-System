using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin Monitor: read-only overview of reservations, orders and payments across the restaurant.
    // Staff do the actions (confirm, seat, cancel, take payment) on the Staff pages.
    //  - Tabs: ?tab=reservations (default) · orders · payments.
    //  - Reservations (connected): OnGet(DateTime? from, DateTime? to) – Reservations between the two dates
    //    (default today → +4 days) with Customer + DiningTable (+ area) + Staff (ReservationService.GetBetweenAsync).
    //    Changing From / To reloads the page; status and search are filtered on screen.
    //    Clicking a row opens the details pop-up (?view=R-0041 opens it straight away);
    //    ?q=R-0041 fills the search box so only that booking is listed (Dashboard Recent activity links use both).
    // Still example data (orders / payments feature – leader):
    //  - Orders: today's Orders with Customer, Reservation (+ DiningTable), OrderItems count, totalAmount.
    //    Type = Dine-in when reservationID != null, else Takeaway. Statuses as agreed for the Staff pages:
    //    Order placed → Preparing → Served → Completed, or Cancelled. Filter: All · Active · Completed · Cancelled.
    //    Clicking an order opens the Order details pop-up: its OrderItems (quantity, Menu.menuName, unitPrice,
    //    itemRemark), subtotal, service charge 10% (dine-in only), SST 6% and total.
    //  - Payments: Payments with Order + Customer, newest first. paymentStatus Pending / Paid / Failed / Refunded.
    //    "Receipt" fills the shared _Receipt partial (ReceiptView) for Paid / Refunded payments.
    //  - Export CSV is done in the browser from the rows shown on the open tab (no backend needed).
    public class MonitorModel : PageModel
    {
        // Longest range shown at once, so the list stays quick to load
        private const int MaxRangeDays = 92;

        private readonly ReservationService _reservations;

        public MonitorModel(ReservationService reservations)
        {
            _reservations = reservations;
        }

        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public List<Reservation> Reservations { get; set; } = new();

        public async Task OnGetAsync(DateTime? from, DateTime? to)
        {
            From = (from ?? DateTime.Today).Date;
            To = (to ?? From.AddDays(4)).Date;
            if (To < From) (From, To) = (To, From);
            if ((To - From).TotalDays > MaxRangeDays) To = From.AddDays(MaxRangeDays);

            Reservations = await _reservations.GetBetweenAsync(From, To);
        }
    }
}
