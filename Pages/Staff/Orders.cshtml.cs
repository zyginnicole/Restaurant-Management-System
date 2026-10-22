using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Orders: tonight's orders (left), the chosen order or a new order (right).
    // Static UI for now – backend notes:
    //  - OnGet: load today's Orders with OrderItems (+ Menu), Reservation (+ DiningTable, Customer), newest first.
    //    Tabs: Active = Order placed, Preparing, Served · Completed · Cancelled.
    //    List the statuses that need staff first: Order placed → Preparing → Served, newest first within each.
    //    ?view=ORD-0082 shows that order on the right (the Staff Dashboard links here like that);
    //    ?status=placed shows the first "Order placed" order (Dashboard "1 to accept").
    //  - Order.orderStatus values: Order placed → Preparing → Served → Completed (Cancelled before Served).
    //    Customers' orders arrive as "Order placed"; staff orders are sent straight to the kitchen ("Preparing").
    //    "Draft" is only on screen while staff build a new order – it is never saved.
    //  - New order: the "Seated table" dropdown lists only Reservations with reservationStatus "Seated",
    //    so every dine-in order links to a reservation. Menu grid = Menu items (isAvailable = false → Sold out).
    //  - Service charge and SST are added on the bill (Billing), not here – the order shows the subtotal.
    public class OrdersModel : PageModel
    {
        public void OnGet()
        {
        }

        // ---- UI flow only (no saving yet) – backend: save first, then redirect ----

        // New order (fields: orderType "DineIn"/"Takeaway", reservationId (dine-in), guestName, guestPhone,
        // pickupDateTime (takeaway), items[i].menuId, items[i].quantity, items[i].itemRemark):
        // create Order (orderStatus "Preparing", staffID = signed-in staff, totalAmount = subtotal) + OrderItems
        // (unitPrice = current Menu price).
        public IActionResult OnPostCreate(string orderType) => Done("New order sent to the kitchen.", null);

        // Order placed → Preparing (staffID = signed-in staff)
        public IActionResult OnPostAccept(string orderId) => Done($"{orderId} accepted. The kitchen is preparing it.", orderId);

        // Preparing → Served
        public IActionResult OnPostServe(string orderId) => Done($"{orderId} marked as served.", orderId);

        // Order placed / Preparing → Cancelled (field: cancelReason); a paid takeaway is refunded
        public IActionResult OnPostCancelOrder(string orderId, string? cancelReason) => Done($"{orderId} cancelled.", orderId);

        private IActionResult Done(string message, string? orderId)
        {
            TempData["StaffMessage"] = message;
            return orderId == null ? RedirectToPage() : RedirectToPage(new { view = orderId });
        }
    }
}
