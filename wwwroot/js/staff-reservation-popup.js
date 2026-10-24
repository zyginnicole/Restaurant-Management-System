// staff-reservation-popup.js – Staff "Reservation details" pop-up (Pages/Staff/_ReservationDetailPopup.cshtml).
// Used by /Staff/Reservations and /Staff/Dashboard. A booking row opens it when it is an element with:
//   data-id="43" (reservationID, posted) data-code="R-0043" data-name="Sophia Haines" data-guests="6"
//   data-date="2026-10-25" data-time="20:00" data-table="T-20" data-table-id="20" (tableID, posted)
//   data-status="Pending" data-seated="19:04" data-walkin="false" data-booked="Requested online on …"
//   data-request="…" data-details="Sophia Haines · 20:00 · 6 guests · T-20 VIP" data-free="T-20 is held 20:00–22:00 …"
//   and a class is-pending / is-confirmed / is-seated / … (the pill colour)
// and contains a link with data-open-detail (href="?view=R-0043", so it also works without the script).
// ?view=R-0043 in the address opens that booking's pop-up when the page loads.
(function () {
    'use strict';

    var popupEl = document.getElementById('reservationDetailPopup');
    if (!popupEl) return;
    var popup = new bootstrap.Modal(popupEl);
    var ended = ['Completed', 'Cancelled', 'No-show', 'Declined'];
    var part = function (name) { return popupEl.querySelector('[data-detail="' + name + '"]'); };
    var timeInput = document.getElementById('detailTime');
    var tableSelect = document.getElementById('detailTable');
    var form = document.getElementById('detailForm');

    // ---------- Unsaved changes: date, time, guests and table, compared with the values the pop-up opened with ----------
    var fields = [
        { label: 'Date', el: document.getElementById('detailDate') },
        { label: 'Time', el: timeInput },
        { label: 'Guests', el: document.getElementById('detailGuests') },
        { label: 'Table', el: tableSelect }
    ];
    var original = [], currentCode = '', submitting = false, forceHide = false;

    function shown(f) {   // text for the summary: the option label for the table, the value otherwise
        return f.el.tagName === 'SELECT' && f.el.selectedIndex >= 0 ? f.el.options[f.el.selectedIndex].text.split(' · ')[0] : f.el.value;
    }
    function remember() { original = fields.map(function (f) { return { value: f.el.value, text: shown(f) }; }); }
    function changes() {
        if (part('fields').disabled) return [];   // read-only booking
        return fields.filter(function (f, i) { return f.el.value !== original[i].value; })
                     .map(function (f) { var i = fields.indexOf(f); return f.label + ' ' + original[i].text + ' → ' + shown(f); });
    }

    // Save changes: dark while nothing is changed, solid green once something is
    function updateSaveButton() {
        var changed = changes().length > 0;
        part('save').classList.toggle('btn-md-dark', !changed);
        part('save').classList.toggle('btn-md-green', changed);
    }
    form.addEventListener('input', updateSaveButton);
    form.addEventListener('change', updateSaveButton);

    var discardEl = document.getElementById('discardEditPopup');
    var discardPopup = discardEl ? new bootstrap.Modal(discardEl) : null;
    var afterDiscard = null, discarded = false;

    // Close the details pop-up and ask "Discard changes?"; next = what to do after discarding (e.g. open the Cancel pop-up)
    function askDiscard(next) {
        document.getElementById('discardEditSummary').textContent = currentCode + ': ' + changes().join(' · ');
        afterDiscard = next;
        discarded = false;
        forceHide = true;
        popupEl.addEventListener('hidden.bs.modal', function () { forceHide = false; discardPopup.show(); }, { once: true });
        popup.hide();
    }

    if (discardPopup) {
        document.getElementById('discardEditConfirm').addEventListener('click', function () {
            discarded = true;
            discardPopup.hide();
        });
        // Discard changes → go on (or just stay closed). Keep editing / × → back to the details pop-up, changes kept.
        discardEl.addEventListener('hidden.bs.modal', function () {
            if (discarded) { var next = afterDiscard; afterDiscard = null; if (next) next(); }
            else popup.show();
        });
    }

    // ×, Esc or a click outside the details pop-up: ask first when something was changed
    popupEl.addEventListener('hide.bs.modal', function (e) {
        if (forceHide || submitting || !discardPopup || changes().length === 0) return;
        e.preventDefault();
        askDiscard(null);
    });

    // Save / Confirm booking / Seat guests send the changes, so no warning
    form.addEventListener('submit', function () { submitting = true; });

    // Refresh, browser back or closing the tab with unsaved changes: the browser shows its own "Leave site?" message
    window.addEventListener('beforeunload', function (e) {
        if (popupEl.classList.contains('show') && !submitting && changes().length > 0) { e.preventDefault(); e.returnValue = ''; }
    });

    function openDetail(row) {
        // Seated bookings are read-only too: they become Completed by themselves when the order is paid (Billing)
        var d = row.dataset, status = d.status, seated = status === 'Seated', readOnly = seated || ended.indexOf(status) !== -1;
        var statusClass = (row.className.match(/\bis-\w+/) || ['is-pending'])[0];
        part('eyebrow').textContent = 'Reservation ' + d.code + (d.walkin === 'true' ? ' · walk-in' : '');
        part('title').textContent = d.name + ' · ' + d.guests + (d.guests === '1' ? ' guest' : ' guests');
        part('pill').className = 'md-pill ' + statusClass;
        part('pill').textContent = status + (d.seated ? ' ' + d.seated : '');
        part('id').value = d.id || d.code;   // reservationID posted to the handlers
        part('booked').textContent = d.booked || '';

        if (d.date) document.getElementById('detailDate').value = d.date;
        timeInput.value = d.time;
        document.getElementById('detailGuests').value = d.guests;
        // Table: every table except out-of-service ones (the booking's own table always); the server checks it is free
        var tableValue = d.tableId || d.table;
        Array.prototype.forEach.call(tableSelect.options, function (o) {
            o.disabled = o.value !== tableValue && o.dataset.state !== 'free';
        });
        tableSelect.value = tableValue;
        tableSelect.dispatchEvent(new Event('md-refresh'));   // update the custom dropdown (md-select.js)
        document.getElementById('detailRequest').value = d.request || '';
        part('free').textContent = d.free || '';

        // Read-only for seated and ended bookings; otherwise show the buttons for this status
        part('fields').disabled = readOnly;
        part('free').classList.toggle('d-none', readOnly || !d.free);
        part('readonly').classList.toggle('d-none', !readOnly);
        part('readonly').textContent = seated
            ? 'Status: Seated. The guests are at the table, so this booking can no longer be changed. It is completed automatically when the bill is paid.'
            : 'Status: ' + status + '. This booking can no longer be changed.';
        part('footer').classList.toggle('d-none', readOnly);
        popupEl.querySelectorAll('[data-for]').forEach(function (group) {
            group.classList.toggle('d-none', group.dataset.for !== status);
        });
        // Buttons that open a confirm pop-up carry this booking's values (read by confirm-popup.js)
        popupEl.querySelectorAll('[data-open-popup]').forEach(function (b) {
            b.dataset.id = d.id || d.code; b.dataset.code = d.code; b.dataset.details = d.details; b.dataset.table = d.table;
        });
        currentCode = d.code;
        submitting = false;
        remember();   // starting values for "Discard changes?"
        updateSaveButton();
        popup.show();
    }

    // Decline / Cancel / No-show inside the pop-up: close it, then open the confirm pop-up
    // (these don't save the edited fields, so unsaved changes are asked about first)
    popupEl.querySelectorAll('[data-open-popup]').forEach(function (button) {
        button.addEventListener('click', function () {
            var target = bootstrap.Modal.getOrCreateInstance(document.getElementById(button.dataset.openPopup));
            var open = function () { target.show(button); };
            if (discardPopup && changes().length > 0) { askDiscard(open); return; }
            forceHide = true;
            popupEl.addEventListener('hidden.bs.modal', function () { forceHide = false; open(); }, { once: true });
            popup.hide();
        });
    });

    var rows = document.querySelectorAll('[data-code][data-status]');
    rows.forEach(function (row) {
        var link = row.querySelector('[data-open-detail]');
        if (!link) return;
        link.addEventListener('click', function (e) {
            e.preventDefault();
            openDetail(row);
        });
    });

    // /Staff/…?view=R-0043 opens that booking's pop-up
    var view = new URLSearchParams(window.location.search).get('view');
    if (view) {
        rows.forEach(function (row) { if (row.dataset.code === view) openDetail(row); });
    }
})();
