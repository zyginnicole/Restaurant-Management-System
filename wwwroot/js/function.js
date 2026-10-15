// function.js - shared page helpers (Register, Login, ...)
(function () {
    'use strict';

    // ---------- Show / hide password ----------
    // Any button with data-toggle-password="<input id>" toggles that input
    document.addEventListener('click', function (e) {
        const btn = e.target.closest('[data-toggle-password]');
        if (!btn) return;

        const input = document.getElementById(btn.dataset.togglePassword);
        if (!input) return;

        const show = input.type === 'password';
        input.type = show ? 'text' : 'password';

        const icon = btn.querySelector('i');
        if (icon) {
            icon.classList.toggle('bi-eye', !show);
            icon.classList.toggle('bi-eye-slash', show);
        }
        btn.setAttribute('aria-pressed', show);
        btn.setAttribute('aria-label', show ? 'Hide password' : 'Show password');
    });

    // ---------- Load Bootstrap JS only if the layout has not loaded it ----------
    function withBootstrap(callback) {
        if (window.bootstrap) { callback(); return; }
        const s = document.createElement('script');
        s.src = 'https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/js/bootstrap.bundle.min.js';
        s.onload = callback;
        document.head.appendChild(s);
    }

    // ---------- Auto-open pop-ups ----------
    // Any modal marked data-auto-show opens when the page loads (used for success messages)
    document.addEventListener('DOMContentLoaded', function () {
        const modals = document.querySelectorAll('.modal[data-auto-show]');
        if (modals.length === 0) return;

        withBootstrap(function () {
            modals.forEach(function (el) {
                bootstrap.Modal.getOrCreateInstance(el).show();
            });
        });
    });
})();