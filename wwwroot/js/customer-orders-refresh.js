// customer-orders-refresh.js – keeps the customer Orders page up to date without pressing refresh.
// Every few seconds it asks the page's Poll handler (Orders.cshtml.cs, OnGetPollAsync) for the customer's open orders
// and compares them with what the page was built from (data-snapshot on #ordersWatch: "code:status:paid").
// Only when something changed (an order accepted, served, completed, cancelled, paid or a new one placed)
// the page reloads, so nothing flickers while nothing happens. It never reloads while:
//   a pop-up is open (e.g. Cancel order) · the customer is typing or choosing in a field · the browser tab is in the background.
// The order that was selected in the list is remembered across the reload (see Orders.cshtml).
(function () {
    'use strict';

    var watch = document.getElementById('ordersWatch');
    if (!watch) return;

    var baseline = watch.dataset.snapshot || '';
    var seconds = 15;

    function busy() {
        if (document.visibilityState !== 'visible') return true;
        if (document.querySelector('.modal.show')) return true;
        var el = document.activeElement;
        return !!el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.tagName === 'SELECT');
    }

    // Same text the server writes into data-snapshot: sorted "ORD-0020:Preparing:1" entries
    function signature(orders) {
        return orders.map(function (o) { return o.code + ':' + o.status + ':' + (o.isPaid ? '1' : '0'); }).sort().join(',');
    }

    function check() {
        if (busy()) return;
        fetch(window.location.pathname + '?handler=Poll', {
            credentials: 'same-origin',
            headers: { 'Accept': 'application/json' }
        })
            .then(function (response) { return response.ok ? response.json() : null; })
            .then(function (data) {
                if (!data || !data.ok) return;                  // signed out or error: leave the page as it is
                if (signature(data.orders) !== baseline && !busy()) window.location.reload();
            })
            .catch(function () { /* offline: try again next time */ });
    }

    setInterval(check, seconds * 1000);
    document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'visible') check();
    });
})();
