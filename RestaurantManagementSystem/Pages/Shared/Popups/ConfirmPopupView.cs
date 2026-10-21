namespace RestaurantManagementSystem.Pages.Shared.Popups
{
    // Settings for the shared "Are you sure?" pop-up (_ConfirmPopup.cshtml).
    // Used for cancel / delete / sign out confirmations on every module.
    //
    //   @{ var cancelOrderPopup = new ConfirmPopupView { Id = "cancelOrderPopup", Title = "Cancel {code}?", ... }; }
    //   <partial name="~/Pages/Shared/Popups/_ConfirmPopup.cshtml" model="cancelOrderPopup" />
    //   <button type="button" data-bs-toggle="modal" data-bs-target="#cancelOrderPopup"
    //           data-id="89" data-code="ORD-0089" data-details="Takeaway · RM 216.24">Cancel order</button>
    //
    // {name} in Title / Message / Note is filled from the clicked button's data-name
    // (e.g. {code} ← data-code). data-id goes into the hidden IdField that is posted to the handler.
    // The filling is done by wwwroot/js/confirm-popup.js (loaded by the layout).
    public class ConfirmPopupView
    {
        public string Id { get; set; } = "";              // element id, e.g. "cancelOrderPopup"
        public string? Eyebrow { get; set; }              // small gold label above the title
        public string Title { get; set; } = "";           // e.g. "Cancel {code}?"
        public string? Message { get; set; }              // e.g. "{details}"
        public string? Note { get; set; }                 // blue info box under the message

        // Where the form posts: Page = another page (e.g. "/Customer/Logout"), Handler = OnPost{Handler}.
        // Leave Page empty to post to the current page.
        public string? Page { get; set; }
        public string? Handler { get; set; }
        public string? IdField { get; set; }              // hidden input name, e.g. "orderId" (value from data-id)

        // Optional "Reason" dropdown (posted as ReasonField)
        public string[]? Reasons { get; set; }
        public string ReasonField { get; set; } = "cancelReason";
        public string ReasonLabel { get; set; } = "Reason (optional)";

        public string BackText { get; set; } = "Go back";
        public string ConfirmText { get; set; } = "Confirm";
        public bool Danger { get; set; }                  // true = red confirm button (cancel / delete)
    }
}
