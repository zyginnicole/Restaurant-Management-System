# Pages/Shared – layouts, pop-ups and shared partials

## Folders

```
Pages/Shared/
├─ _CustomerLayout.cshtml        customer pages (sidebar, header, bell, Sign out pop-up)
├─ _CustomerAuthLayout.cshtml    customer Login / Register
├─ _DashboardLayout.cshtml       Staff + Admin pages
├─ _StaffAuthLayout.cshtml       Staff / Admin login
├─ Popups/                       shared pop-ups
│  ├─ _ConfirmPopup.cshtml       every "Are you sure?" pop-up (cancel, delete, sign out)
│  ├─ ConfirmPopupView.cs        its settings (title, message, buttons, where it posts)
│  └─ _NotificationPopup.cshtml  notification details (filled by wwwroot/js/notifications.js)
└─ Partials/                     other shared blocks
   ├─ _Receipt.cshtml            till-style receipt (dine-in + takeaway)
   └─ ReceiptView.cs             its data + service charge / SST / total
```

`Popups` and `Partials` are already in `Pages/_ViewImports.cshtml`, so `ConfirmPopupView` and
`ReceiptView` can be used on any page without an `@using`.

## Naming: "popup", not "modal"

Everything we name ourselves says **popup** (`_ConfirmPopup`, `cancelOrderPopup`, `.md-popup`, `.md-popup-title`),
so it is never mixed up with the `Model` folder.

The word **modal** only appears where Bootstrap needs it – do not rename these, or the pop-ups stop working:
`class="modal …"`, `modal-dialog`, `modal-content`, `modal-header`, `modal-body`, `modal-footer`, `modal-title`,
`data-bs-toggle="modal"`, `data-bs-dismiss="modal"`, the events `show.bs.modal` / `hidden.bs.modal`
and `bootstrap.Modal` in JavaScript.

## Confirm pop-up (`_ConfirmPopup`)

1. Describe the pop-up once on the page:

   ```cshtml
   @{
       var cancelOrderPopup = new ConfirmPopupView
       {
           Id = "cancelOrderPopup", Eyebrow = "Order", Title = "Cancel {code}?", Message = "{details}",
           Note = "The payment is refunded within 3–5 working days.",
           Handler = "Cancel", IdField = "orderId",            // posts orderId to OnPostCancel
           BackText = "Keep order", ConfirmText = "Cancel order", Danger = true
       };
   }
   <partial name="~/Pages/Shared/Popups/_ConfirmPopup.cshtml" model="cancelOrderPopup" />
   ```

2. Give each record's button the values – one pop-up then works for every row:

   ```html
   <button type="button" data-bs-toggle="modal" data-bs-target="#cancelOrderPopup"
           data-id="@order.orderID" data-code="ORD-@order.orderID.ToString("0000")" data-details="…">Cancel order</button>
   ```

   - `data-id` → the hidden field named in `IdField` (posted to the handler)
   - `data-code`, `data-details`, … → `{code}`, `{details}`, … in Title / Message / Note
   - Filling is done by `wwwroot/js/confirm-popup.js` (loaded by `_CustomerLayout`).

3. Write the handler on the PageModel, e.g. `public IActionResult OnPostCancel(int orderId)`.

Options: `Page` (post to another page, e.g. `/Customer/Logout`), `Reasons` (adds a "Reason" dropdown,
posted as `cancelReason`), `Danger` (red button for cancel / delete).

## Pop-ups that stay on their own page

These have their own form fields, so they are written on the page that uses them (each has a comment
naming its handler and fields):

| Page | Pop-up | Posts to |
|---|---|---|
| Reservations | `reviewReservationPopup` (check booking) | `OnPost` (the booking form) |
| Reservations | `reservationDetailPopup` (details, Edit / Cancel) | – |
| History | `historyDetailPopup` (details) | – |
| History | `ratePopup` (rate your visit) | `OnPostRate` |
| Profile | `changePasswordPopup` | `OnPostPassword` |
| Register | `registerSuccessPopup` | – |

All pop-ups use the same look: `class="modal fade md-popup"` (add `md-popup-narrow` for the reservation layout).

## Receipt (`_Receipt`)

Fill a `ReceiptView` from the Order + Payment and pass it in:

```cshtml
<partial name="~/Pages/Shared/Partials/_Receipt.cshtml" model="receipt" />
```

`ReceiptView` works out the service charge (10%, dine-in only = `reservationID != null`), SST (6% of subtotal
+ service charge) and the total, so every page shows the same numbers. Mapping to the database is in
`ReceiptView.cs`. The Download PDF button uses `data-pdf-target="#receiptPaper"` (`wwwroot/js/pdf-download.js`).

## Other shared scripts (wwwroot/js)

| File | Does |
|---|---|
| `confirm-popup.js` | fills `_ConfirmPopup` from the clicked button |
| `notifications.js` | opens `_NotificationPopup` with a notification's details |
| `reservation-popup.js` | lines up the details grid in the reservation pop-ups |
| `pdf-download.js` | "Download … (PDF)" buttons (`data-pdf-target`, `data-pdf-name`) |
| `function.js` | password eye buttons, pop-ups with `data-auto-show` |
| `menu.js` | Menu page tabs, search, cart, quantity stepper, chatbot |
