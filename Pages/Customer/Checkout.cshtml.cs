using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Customer
{
    // Checkout: review the cart, choose takeaway or dine-in, then continue.
    // Static UI for now – backend notes:
    //  - OnGet: load the cart (e.g. Session) with Menu prices; load the customer's Confirmed reservations
    //    for today/upcoming for the dine-in dropdown.
    //  - OnPost (fields: orderType "Takeaway"/"DineIn", reservationId (dine-in), pickupDateTime + contactPhone
    //    (takeaway), items[i].menuId / items[i].quantity / items[i].itemRemark):
    //      create Order (orderStatus "Order placed", reservationID = null for takeaway,
    //      pickupDateTime = chosen time for takeaway / null for dine-in) + OrderItems
    //      (unitPrice = current Menu price, itemRemark = the note, max 200),
    //      totalAmount = subtotal (+ SST, + service charge for dine-in).
    //      Takeaway → RedirectToPage("/Customer/Payment") (paid online first).
    //      Dine-in  → RedirectToPage("/Customer/Orders") (paid on the bill at the table).
    //  - contactPhone has no field in Order: it is pre-filled from Customer.custPhone (update the customer's
    //    phone if it was changed, or leave it out).
    public class CheckoutModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend: create the Order + OrderItems first, then redirect.
        public IActionResult OnPost(string orderType)
        {
            return orderType == "DineIn"
                ? RedirectToPage("/Customer/Orders")
                : RedirectToPage("/Customer/Payment");
        }
    }
}
