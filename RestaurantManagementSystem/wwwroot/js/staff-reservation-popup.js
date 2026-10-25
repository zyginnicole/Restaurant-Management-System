// staff-reservation-popup.js – Staff "Reservation details" pop-up (Pages/Staff/_ReservationDetailPopup.cshtml).
// Used by /Staff/Reservations and /Staff/Dashboard. A booking row opens it when it is an element with:
//   data-code="R-0043" data-name="Sophia Haines" data-guests="6" data-time="20:00" data-table="T-20"
//   data-status="Pending" data-seated="19:04" data-walkin="false" data-booked="Requested online on …"
//   data-request="…" data-details="Sophia Haines · 20:00 · 6 guests · T-20 VIP"
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
    var timeSelect = document.getElementById('detailTime');
    var tableSelect = document.getElementById('detailTable');

    function addMinutes(time, minutes) {
        var p = time.split(':').map(Number), total = p[0] * 60 + p[1] + minutes;
        return String(Math.floor(total / 60) % 24).padStart(2, '0') + ':' + String(total % 60).padStart(2, '0');
    }

    function openDetail(row) {
        // Seated bookings are read-only too: they become Completed by themselves when the order is paid (Billing)
        var d = row.dataset, status = d.status, seated = status === 'Seated', readOnly = seated || ended.indexOf(status) !== -1;
        var statusClass = (row.className.match(/\bis-\w+/) || ['is-pending'])[0];
        part('eyebrow').textContent = 'Reservation ' + d.code + (d.walkin === 'true' ? ' · walk-in' : '');
        part('title').textContent = d.name + ' · ' + d.guests + (d.guests === '1' ? ' guest' : ' guests');
        part('pill').className = 'md-pill ' + statusClass;
        part('pill').textContent = status + (d.seated ? ' ' + d.seated : '');
        part('id').value = d.code;
        part('booked').textContent = d.booked || '';

        // Time: add the booking's own time if it is not one of the choices
        if (!Array.prototype.some.call(timeSelect.options, function (o) { return o.value === d.time; })) {
            timeSelect.add(new Option(d.time, d.time));
        }
        timeSelect.value = d.time;
        document.getElementById('detailGuests').value = d.guests;
        // Table: free tables + the booking's own table can be chosen
        Array.prototype.forEach.call(tableSelect.options, function (o) {
            o.disabled = o.value !== d.table && o.dataset.state !== 'free';
        });
        tableSelect.value = d.table;
        tableSelect.dispatchEvent(new Event('md-refresh'));   // update the custom dropdown (md-select.js)
        document.getElementById('detailRequest').value = d.request || '';
        part('free').textContent = d.table + ' is free ' + d.time + '–' + addMinutes(d.time, 105) +
            ' (90 min + 15 min buffer). Next booking on this table: none tonight.';

        // Read-only for seated and ended bookings; otherwise show the buttons for this status
        part('fields').disabled = readOnly;
        part('free').classList.toggle('d-none', readOnly);
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
            b.dataset.id = d.code; b.dataset.code = d.code; b.dataset.details = d.details; b.dataset.table = d.table;
        });
        popup.show();
    }

    // Decline / Cancel / No-show inside the pop-up: close it, then open the confirm pop-up
    popupEl.querySelectorAll('[data-open-popup]').forEach(function (button) {
        button.addEventListener('click', function () {
            var target = bootstrap.Modal.getOrCreateInstance(document.getElementById(button.dataset.openPopup));
            popupEl.addEventListener('hidden.bs.modal', function () { target.show(button); }, { once: true });
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
