// staff-auto-refresh.js – reloads a staff page (Reservations, Dashboard) every minute, so new bookings and
// colleagues' changes show up without pressing refresh. It never reloads while:
//   a pop-up is open · staff are typing or choosing in a field · the browser tab is in the background.
// ?view=… is dropped on reload, so a pop-up that was opened from the address doesn't open again.
// Filters on the page keep their values (each page saves them in sessionStorage).
(function () {
    'use strict';

    var seconds = 60;

    function busy() {
        if (document.visibilityState !== 'visible') return true;
        if (document.querySelector('.modal.show')) return true;
        var el = document.activeElement;
        return !!el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.tagName === 'SELECT');
    }

    setInterval(function () {
        if (busy()) return;
        var url = new URL(window.location.href);
        url.searchParams.delete('view');
        window.location.replace(url.toString());
    }, seconds * 1000);
})();
