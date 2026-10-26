// Customer Menu page: tabs (Recommendations / Promotions / All / categories), search,
// promotion banner, cart counter and chatbot panel.
// The cart is saved in the session on the server (Menu.cshtml.cs, handler SetQuantity), so Checkout can read it.
// Real chatbot replies are for the team to add later.
(function () {
    // ---------- Tabs + search ----------
    var items = document.querySelectorAll('.md-menu-item');
    var tabs = document.querySelectorAll('.md-tabs [data-filter]');
    var grid = document.getElementById('menuGrid');
    var search = document.getElementById('menuSearch');
    var count = document.getElementById('menuCount');
    var empty = document.getElementById('menuEmpty');
    var activeFilter = 'Recommendations';
    var selectedTags = [];       // tag chips that are switched on (a dish must have all of them)
    var hideUnsafe = false;      // "Hide dishes with my allergens" switch (set below)

    function csv(value) { return (value || '').split(',').filter(Boolean); }

    // Tabs that show their own panel instead of the dish grid
    var panels = {
        Recommendations: {
            element: document.getElementById('recommendationsPanel'),
            label: function () {
                var n = document.querySelectorAll('#recommendationsPanel .md-menu-card').length;
                return 'Picked for you · ' + n + (n === 1 ? ' dish' : ' dishes');
            }
        },
        Promotions: {
            element: document.getElementById('promotionsPanel'),
            label: function () {
                var n = document.querySelectorAll('#promotionsPanel .md-promo-section').length;
                return 'Current promotions · ' + n + (n === 1 ? ' offer' : ' offers');
            }
        }
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

        // Tag chips only apply to the dish grid (All + category tabs)
        var chipBar = document.getElementById('tagChips');
        if (chipBar) chipBar.classList.toggle('d-none', !!panels[activeFilter]);

        if (panels[activeFilter]) {
            grid.classList.add('d-none');
            empty.classList.add('d-none');
            if (count) count.textContent = panels[activeFilter].label();
            return;
        }

        grid.classList.remove('d-none');

        var text = search.value.trim().toLowerCase();
        var shown = 0;

        items.forEach(function (item) {
            var matchesCategory = activeFilter === 'All' || item.dataset.category === activeFilter;
            var matchesText = item.dataset.name.toLowerCase().indexOf(text) !== -1;
            var card = item.querySelector('.md-menu-card');
            var dishTags = card ? csv(card.dataset.tags) : [];
            var matchesTags = selectedTags.every(function (tag) { return dishTags.indexOf(tag) !== -1; });
            var hiddenByAllergy = hideUnsafe && card && card.classList.contains('is-unsafe');
            var visible = matchesCategory && matchesText && matchesTags && !hiddenByAllergy;
            item.classList.toggle('d-none', !visible);
            if (visible) shown++;
        });

        var label = activeFilter === 'All' ? 'Complete seasonal selection' : activeFilter;
        if (count) count.textContent = label + ' · ' + shown + (shown === 1 ? ' item' : ' items');
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

    // ---------- Cart (saved on the server) ----------
    var MAX_QTY = 20;                                              // same limit as Checkout

    // Set by Menu.cshtml: ids = dish name -> menuID, quantities = menuID -> quantity already in the cart
    var cartData = window.MD_CART || { ids: {}, quantities: {} };
    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');

    // Quantity per dish (keyed by dish name, so the same dish shown in Recommendations,
    // Promotions and All keeps one quantity). Starts from the cart saved in the session.
    var quantities = {};
    Object.keys(cartData.ids).forEach(function (name) {
        var qty = cartData.quantities[cartData.ids[name]];
        if (qty) quantities[name] = qty;
    });

    function dishName(button) {
        return button.closest('.md-menu-card').querySelector('h3').textContent.trim();
    }

    // Header button: "Cart · 3 items · RM 204.00" (numbers come from the server)
    function showCart(totalItems, total) {
        document.getElementById('cartCount').textContent = totalItems;
        document.getElementById('cartTotal').textContent = 'RM ' + Number(total).toFixed(2);
    }

    // Show the stepper and keep button as "Added" when quantity >= 1
    function showQuantity(name) {
        var qty = quantities[name] || 0;
        document.querySelectorAll('.md-add-row').forEach(function (row) {
            if (row.dataset.dish !== name) return;

            // Toggle quantity stepper (- qty +)
            row.querySelector('.md-qty').classList.toggle('d-none', qty === 0);
            row.querySelector('[data-qty]').textContent = qty;

            // Maintain "Added" state on the button whenever quantity >= 1
            var button = row.querySelector('.md-add-btn');
            if (button) {
                if (qty > 0) {
                    button.classList.remove('is-reset');
                    button.classList.add('is-added');
                    button.textContent = 'Added';
                } else {
                    button.classList.remove('is-added', 'is-reset');
                    button.textContent = 'Add to order';
                }
            }
        });
    }

    // Change one dish to a new quantity: update the cards at once, save on the server,
    // then use the server's answer for the quantity and the header totals.
    function changeQuantity(name, qty) {
        var menuId = cartData.ids[name];
        if (!menuId) return;

        var previous = quantities[name] || 0;
        qty = Math.max(0, Math.min(MAX_QTY, qty));
        if (qty === previous) return;
        quantities[name] = qty;
        showQuantity(name);

        fetch(window.location.pathname + '?handler=SetQuantity', {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded',
                'RequestVerificationToken': tokenInput ? tokenInput.value : ''
            },
            body: 'menuId=' + encodeURIComponent(menuId) + '&quantity=' + qty
        })
            .then(function (response) {
                return response.json().catch(function () { return { ok: false }; });
            })
            .then(function (data) {
                if (!data.ok) throw new Error(data.message || '');
                quantities[name] = data.quantity;
                showQuantity(name);
                showCart(data.count, data.total);
            })
            .catch(function (error) {
                quantities[name] = previous;
                showQuantity(name);
                window.alert(error.message || 'We could not update your cart. Please try again.');
            });
    }

    document.querySelectorAll('.md-add-btn').forEach(function (button) {
        var name = dishName(button);

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
                changeQuantity(name, (quantities[name] || 0) + parseInt(step.dataset.step, 10));
            });
        });

        button.addEventListener('click', function () {
            var current = quantities[name] || 0;
            if (current === 0 && !confirmAllergen(button)) return;   // ask once, when the dish is first added
            changeQuantity(name, current + 1);
        });
    });

    // Cards of dishes that are already in the saved cart show their state straight away
    Object.keys(quantities).forEach(showQuantity);

    // No reservation today: the menu is browse-only, the add buttons are locked
    if (cartData.locked) {
        document.querySelectorAll('.md-add-btn').forEach(function (button) {
            button.disabled = true;
            button.textContent = 'Reservation required';
            button.title = cartData.lockMessage || '';
        });
    }

    // ---------- Allergens + tags ----------
    // Allergies of the signed-in customer (set by Menu.cshtml)
    var myAllergies = ((window.MD_USER && window.MD_USER.allergies) || []).map(function (a) { return a.toLowerCase(); });
    var hideSwitch = document.getElementById('hideAllergens');
    hideUnsafe = !!hideSwitch && hideSwitch.checked;

    // Mark every dish card: has-allergen = contains one of my allergens,
    // allergen-unknown = the admin never filled in the allergens. Both are "unsafe" (same rule as the recommendations).
    if (myAllergies.length) {
        document.querySelectorAll('.md-menu-card').forEach(function (card) {
            var known = card.dataset.allergensKnown === 'yes';
            var conflicts = csv(card.dataset.allergens).filter(function (a) {
                return myAllergies.indexOf(a.toLowerCase()) !== -1;
            });
            card.dataset.conflicts = conflicts.join(',');

            if (conflicts.length) {
                card.classList.add('has-allergen', 'is-unsafe');
                card.querySelectorAll('[data-allergen]').forEach(function (el) {
                    if (conflicts.indexOf(el.dataset.allergen) !== -1) el.classList.add('is-mine');
                });
                var mine = card.querySelector('[data-allergen-mine]');
                if (mine) {
                    mine.textContent = 'Contains your allergen: ' + conflicts.join(', ');
                    mine.classList.remove('d-none');
                }
            } else if (!known) {
                card.classList.add('is-unsafe', 'allergen-unknown');
            }
        });
    }

    // Hide / show the unsafe dishes (the card's grid column is hidden, so no gaps are left)
    function applyAllergenHide() {
        document.querySelectorAll('.md-menu-card.is-unsafe').forEach(function (card) {
            var column = card.closest('[class*="col-"]') || card;
            column.classList.toggle('md-allergen-hidden', hideUnsafe);
        });
    }

    if (hideSwitch) {
        hideSwitch.addEventListener('change', function () {
            hideUnsafe = hideSwitch.checked;
            applyAllergenHide();
            applyFilter();
        });
    }

    // Ask before adding a dish that has the customer's allergen (or has no allergen info at all)
    function confirmAllergen(button) {
        var card = button.closest('.md-menu-card');
        if (!card) return true;
        if (card.dataset.conflicts) {
            return window.confirm('This dish contains ' + card.dataset.conflicts.split(',').join(', ') +
                ', which you listed as an allergy.\n\nAdd it to your order anyway?');
        }
        if (card.classList.contains('allergen-unknown')) {
            return window.confirm('Allergen information for this dish is not available, and you listed allergies. ' +
                'Please check with our staff.\n\nAdd it to your order anyway?');
        }
        return true;
    }

    // Tag chips (All + category tabs): a dish must have every chip that is switched on
    var chipBar = document.getElementById('tagChips');
    if (chipBar) {
        chipBar.addEventListener('click', function (event) {
            var chip = event.target.closest('[data-tag]');
            var clear = event.target.closest('[data-clear-tags]');
            if (!chip && !clear) return;

            if (clear) {
                chipBar.querySelectorAll('[data-tag]').forEach(function (c) {
                    c.classList.remove('active');
                    c.setAttribute('aria-pressed', 'false');
                });
            } else {
                var on = !chip.classList.contains('active');
                chip.classList.toggle('active', on);
                chip.setAttribute('aria-pressed', on ? 'true' : 'false');
            }

            selectedTags = Array.prototype.map.call(chipBar.querySelectorAll('[data-tag].active'), function (c) { return c.dataset.tag; });
            chipBar.querySelector('[data-clear-tags]').classList.toggle('d-none', selectedTags.length === 0);
            applyFilter();
        });
    }

    applyAllergenHide();
    applyFilter();      // sets the panel / chip visibility and the count text for the starting tab

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