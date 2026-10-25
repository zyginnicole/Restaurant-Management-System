// Small helpers shared by the hardcoded admin setup pages.
// Remove or replace these once the pages are wired to the backend.
(function () {
    function toast(message) {
        let host = document.getElementById('aToasts');
        if (!host) {
            host = document.createElement('div');
            host.id = 'aToasts';
            host.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            document.body.appendChild(host);
        }
        const el = document.createElement('div');
        el.className = 'toast a-toast border-0';
        el.setAttribute('role', 'status');
        el.innerHTML = '<div class="d-flex"><div class="toast-body"></div>' +
            '<button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button></div>';
        el.querySelector('.toast-body').textContent = message;
        host.appendChild(el);
        el.addEventListener('hidden.bs.toast', function () { el.remove(); });
        new bootstrap.Toast(el, { delay: 2500 }).show();
    }

    document.addEventListener('click', function (e) {
        // data-remove=".selector" removes the closest matching element (demo delete)
        const rm = e.target.closest('[data-remove]');
        if (rm) {
            const target = rm.closest(rm.dataset.remove);
            if (target) target.remove();
        }
        // data-toast="Message" shows a confirmation toast
        const t = e.target.closest('[data-toast]');
        if (t) toast(t.dataset.toast);
    });

    // Switches with data-on / data-off update the [data-switch-text] label in the same [data-switch-scope]
    document.addEventListener('change', function (e) {
        const sw = e.target.closest('input.a-switch[data-on]');
        if (!sw) return;
        const scope = sw.closest('[data-switch-scope]');
        const label = scope && scope.querySelector('[data-switch-text]');
        if (!label) return;
        label.textContent = sw.checked ? sw.dataset.on : sw.dataset.off;
        if (label.classList.contains('a-badge')) {
            label.className = 'a-badge ' + (sw.checked ? sw.dataset.on : sw.dataset.off).toLowerCase().replace(/\s+/g, '-');
        }
    });

    // Slot chips: click to turn a booking slot on/off
    document.addEventListener('click', function (e) {
        const chip = e.target.closest('.a-chip');
        if (!chip) return;
        chip.classList.toggle('on');
        chip.setAttribute('aria-pressed', chip.classList.contains('on'));
    });

    window.adm = { toast: toast };
})();