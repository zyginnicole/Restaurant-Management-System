// confirm-popup.js – fills the shared "Are you sure?" pop-up (Pages/Shared/Popups/_ConfirmPopup.cshtml)
// from the button that opened it, so one pop-up works for every record on the page.
//
//   <button data-bs-toggle="modal" data-bs-target="#cancelOrderPopup"
//           data-id="89" data-code="ORD-0089" data-details="Takeaway · RM 216.24">Cancel order</button>
//
//   data-id      → the hidden id field that is posted (e.g. orderId)
//   data-<name>  → every <span data-fill="name"> in the pop-up ({name} in the title / message / note)
// ("show.bs.modal" is Bootstrap's event name – keep it.)
(function () {
    'use strict';

    document.addEventListener('show.bs.modal', function (e) {
        var popup = e.target;
        var button = e.relatedTarget;
        if (!popup.hasAttribute('data-confirm-popup') || !button) return;

        popup.querySelectorAll('[data-fill]').forEach(function (span) {
            var value = button.dataset[span.dataset.fill];
            span.textContent = value != null && value !== '' ? value : '—';
        });

        var id = popup.querySelector('[data-confirm-id]');
        if (id) id.value = button.dataset.id || '';
    });
})();
