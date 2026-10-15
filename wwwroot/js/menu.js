// Customer Menu page: tabs (Recommendations / Promotions / All / categories), search,
// promotion banner, cart counter and chatbot panel.
// Front-end only – saving the cart and real chatbot replies are for the team to add later.
(function () {
    // ---------- Tabs + search ----------
    var items = document.querySelectorAll('.md-menu-item');
    var tabs = document.querySelectorAll('.md-tabs [data-filter]');
    var grid = document.getElementById('menuGrid');
    var search = document.getElementById('menuSearch');
    var count = document.getElementById('menuCount');
    var empty = document.getElementById('menuEmpty');
    var activeFilter = 'Recommendations';

    // Tabs that show their own panel instead of the dish grid
    var panels = {
        Recommendations: { element: document.getElementById('recommendationsPanel'), label: 'Picked for you · 6 dishes' },
        Promotions: { element: document.getElementById('promotionsPanel'), label: 'Current promotions · 3 offers' }
    };

    function setActiveTab(filter) {
        activeFilter = filter;
        tabs.forEach(function (tab) {
            tab.classList.toggle('active', tab.dataset.filter === filter);
        });
    }

    function applyFilter() {
        // Show only the active tab's panel (if it has one)
        Object.keys(panels).forEach(function (name) {
            panels[name].element.classList.toggle('d-none', name !== activeFilter);
        });

        if (panels[activeFilter]) {
            grid.classList.add('d-none');
            empty.classList.add('d-none');
            count.textContent = panels[activeFilter].label;
            return;
        }

        grid.classList.remove('d-none');

        var text = search.value.trim().toLowerCase();
        var shown = 0;

        items.forEach(function (item) {
            var matchesCategory = activeFilter === 'All' || item.dataset.category === activeFilter;
            var matchesText = item.dataset.name.toLowerCase().indexOf(text) !== -1;
            var visible = matchesCategory && matchesText;
            item.classList.toggle('d-none', !visible);
            if (visible) shown++;
        });

        var label = activeFilter === 'All' ? 'Complete seasonal selection' : activeFilter;
        count.textContent = label + ' · ' + shown + (shown === 1 ? ' item' : ' items');
        empty.classList.toggle('d-none', shown !== 0);
    }

    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            setActiveTab(tab.dataset.filter);
            applyFilter();
        });
    });

    // ---------- Promotion banner ----------
    // Clicking a slide opens the Promotions tab, scrolls to that promotion and highlights it briefly
    document.querySelectorAll('[data-promo-target]').forEach(function (slide) {
        slide.addEventListener('click', function () {
            search.value = '';
            setActiveTab('Promotions');
            applyFilter();

            var section = document.getElementById(slide.dataset.promoTarget);
            if (!section) return;
            section.scrollIntoView({ behavior: 'smooth', block: 'start' });
            section.classList.remove('is-highlighted');
            void section.offsetWidth;            // restart the highlight animation
            section.classList.add('is-highlighted');
        });
    });

    var promoClose = document.getElementById('promoClose');
    if (promoClose) {
        promoClose.addEventListener('click', function () {
            document.getElementById('promoStrip').classList.add('d-none');
        });
    }

    // Searching from Recommendations or Promotions switches to "All" so every dish can be found
    search.addEventListener('input', function () {
        if (panels[activeFilter] && search.value.trim() !== '') {
            setActiveTab('All');
        }
        applyFilter();
    });

    // ---------- Cart counter (starts with the 2 example items in the design) ----------
    var cartCount = 2;
    var cartTotal = 204;

    document.querySelectorAll('.md-add-btn').forEach(function (button) {
        button.addEventListener('click', function () {
            cartCount++;
            cartTotal += parseFloat(button.dataset.price);
            document.getElementById('cartCount').textContent = cartCount;
            document.getElementById('cartTotal').textContent = 'RM ' + cartTotal.toFixed(2);

            var original = button.textContent;
            button.textContent = 'Added';
            setTimeout(function () { button.textContent = original; }, 1200);
        });
    });

    // ---------- Chatbot panel ----------
    var toggle = document.getElementById('chatToggle');
    var panel = document.getElementById('chatPanel');

    function setChat(open) {
        panel.classList.toggle('d-none', !open);
        toggle.classList.toggle('d-none', open);
        toggle.setAttribute('aria-expanded', open ? 'true' : 'false');
        if (open) document.getElementById('chatText').focus();
    }

    toggle.addEventListener('click', function () { setChat(true); });
    document.getElementById('chatClose').addEventListener('click', function () { setChat(false); });

    // Shows the customer's message in the panel. The assistant's reply is for the team to connect later.
    document.getElementById('chatForm').addEventListener('submit', function (event) {
        event.preventDefault();
        var input = document.getElementById('chatText');
        var text = input.value.trim();
        if (!text) return;

        var message = document.createElement('div');
        message.className = 'md-chat-msg user';
        message.textContent = text;
        var body = document.getElementById('chatBody');
        body.appendChild(message);
        body.scrollTop = body.scrollHeight;
        input.value = '';
    });
})();
