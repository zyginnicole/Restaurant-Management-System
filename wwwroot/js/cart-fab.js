// cart-fab.js – Menu page: floating cart button above the chatbot button.
// It appears only when the header's Cart button (.md-cart-btn) has scrolled out of view,
// shows the same item count as #cartCount, and hides while the chat panel is open.
(function () {
    'use strict';

    var fab = document.getElementById('cartFab');
    var headerCart = document.querySelector('.md-cart-btn');
    if (!fab || !headerCart) return;

    var count = document.getElementById('cartCount');
    var total = document.getElementById('cartTotal');
    var fabCount = document.getElementById('cartFabCount');
    var chatPanel = document.getElementById('chatPanel');
    var headerVisible = true;

    function chatOpen() { return chatPanel && !chatPanel.classList.contains('d-none'); }

    function update() {
        var show = !headerVisible && !chatOpen();
        fab.classList.toggle('is-hidden', !show);
        fab.setAttribute('aria-hidden', show ? 'false' : 'true');
        fab.tabIndex = show ? 0 : -1;
    }

    // Same number as the header ("Cart · 2 items · RM 204.00")
    function syncCount() {
        var n = count ? count.textContent.trim() : '0';
        fabCount.textContent = n;
        fabCount.classList.toggle('d-none', n === '0');
        var label = 'Open cart, ' + n + (n === '1' ? ' item' : ' items') + (total ? ', ' + total.textContent.trim() : '');
        fab.setAttribute('aria-label', label);
        fab.title = label;
    }

    // Header Cart button in view → hide the floating one
    new IntersectionObserver(function (entries) {
        headerVisible = entries[0].isIntersecting;
        update();
    }).observe(headerCart);

    // menu.js updates #cartCount / #cartTotal when dishes are added; the chat panel toggles d-none
    if (count) new MutationObserver(syncCount).observe(count, { childList: true, characterData: true, subtree: true });
    if (total) new MutationObserver(syncCount).observe(total, { childList: true, characterData: true, subtree: true });
    if (chatPanel) new MutationObserver(update).observe(chatPanel, { attributes: true, attributeFilter: ['class'] });

    syncCount();
    update();
})();
