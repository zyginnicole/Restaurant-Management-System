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

    // Quantity per dish (keyed by dish name, so the same dish shown in Recommendations,
    // Promotions and All keeps one quantity). Backend: key by itemID and save to the cart instead.
    var quantities = {};

    function dishName(button) {
        return button.closest('.md-menu-card').querySelector('h3').textContent.trim();
    }

    function updateCart(price, change) {
        cartCount += change;
        cartTotal += price * change;
        document.getElementById('cartCount').textContent = cartCount;
        document.getElementById('cartTotal').textContent = 'RM ' + cartTotal.toFixed(2);
    }

    // Show the stepper with the quantity on every card of this dish (hidden at 0)
    function showQuantity(name) {
        var qty = quantities[name] || 0;
        document.querySelectorAll('.md-add-row').forEach(function (row) {
            if (row.dataset.dish !== name) return;
            row.querySelector('.md-qty').classList.toggle('d-none', qty === 0);
            row.querySelector('[data-qty]').textContent = qty;
        });
    }

    document.querySelectorAll('.md-add-btn').forEach(function (button) {
        var name = dishName(button);
        var price = parseFloat(button.dataset.price);

        // Wrap the button in a row with a − qty + stepper (hidden until the dish is added)
        var row = document.createElement('div');
        row.className = 'md-add-row';
        row.dataset.dish = name;
        button.parentNode.insertBefore(row, button);
        row.innerHTML =
            '<div class="md-qty d-none">' +
            '<button type="button" data-step="-1" aria-label="Remove one ' + name + '">&minus;</button>' +
            '<span data-qty aria-live="polite">0</span>' +
            '<button type="button" data-step="1" aria-label="Add one more ' + name + '">+</button>' +
            '</div>';
        row.appendChild(button);

        row.querySelectorAll('[data-step]').forEach(function (step) {
            step.addEventListener('click', function () {
                var change = parseInt(step.dataset.step, 10);
                if ((quantities[name] || 0) + change < 0) return;
                quantities[name] = (quantities[name] || 0) + change;
                updateCart(price, change);
                showQuantity(name);
            });
        });

        button.addEventListener('click', function () {
            quantities[name] = (quantities[name] || 0) + 1;
            updateCart(price, 1);
            showQuantity(name);

            // "Added" on green for a moment, then back to the white "Add to order"
            clearTimeout(button.addedTimer);
            button.classList.remove('is-reset');
            button.classList.add('is-added');
            button.textContent = 'Added';
            button.addedTimer = setTimeout(function () {
                button.classList.remove('is-added');
                button.classList.add('is-reset');      // stays white even while the mouse is still on it
                button.textContent = 'Add to order';
                button.blur();
            }, 1200);
        });
        button.addEventListener('mouseleave', function () {
            if (!button.classList.contains('is-added')) button.classList.remove('is-reset');
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
