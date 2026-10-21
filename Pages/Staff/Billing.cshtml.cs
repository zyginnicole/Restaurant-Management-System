using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Billing: one bill per Order – take dine-in payments at the table and show receipts.
    // Static UI for now – backend notes:
    //  - OnGet(string? day): load the Orders for the day (Tonight / Yesterday / All) with OrderItems (+ Menu),
    //    Reservation (+ DiningTable, Customer) and Payment. ?order=ORD-0083 shows that bill
    //    (the Orders page "Record payment" button and the Staff Dashboard link here like that).
    //  - Bill state shown in the list:
    //      Awaiting payment = orderStatus "Served" and no Paid Payment
    //      Still dining     = orderStatus "Order placed" / "Preparing"
    //      Paid             = Payment.paymentStatus "Paid" (takeaway: paid online on the Payment page)
    //  - Totals: subtotal − promotion discount, + service charge 10% (dine-in only), + SST 6% on (that + service charge).
    //    Use the ReceiptView rates (Pages/Shared/Partials/ReceiptView.cs) so the bill and the receipt match.
    //  - Promotions: the dropdown lists active Promotions. Leader decision needed: Order / Payment have no field for
    //    the promotion or the discount yet (e.g. Order.promotionID + discountAmount); ReceiptView would show it too.
    //  - Paid bills show the shared receipt (_Receipt partial, filled from a ReceiptView).
    public class BillingModel : PageModel
    {
        public void OnGet()
        {
        }

        // UI flow only (no saving yet) – backend first:
        //  fields: orderId, paymentMethod ("Cash" / "Card" / "E-Wallet"), promotionId (optional), amountReceived (cash)
        //  → new Payment (paymentAmount = total, paymentStatus "Paid", paymentDate = now), Order.orderStatus "Completed",
        //    and the order's Seated Reservation → reservationStatus "Completed" (automatic – there is no Complete
        //    button on the Reservations page), DiningTable.tableStatus "Available". Do it in one SaveChanges.
        //  Then show the receipt (receipt no. = "RCP-" + paymentID.ToString("0000")).
        public IActionResult OnPostPay(string orderId, string paymentMethod)
        {
            TempData["StaffMessage"] = $"Payment for {orderId} recorded ({paymentMethod}). The visit is completed and the table is free again.";
            return RedirectToPage(new { order = orderId });
        }
    }
}
