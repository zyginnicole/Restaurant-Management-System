// Admin Dashboard: tab switching, segmented buttons and the Chart.js charts.
// Chart data is example data from the design – load real figures later.
(function () {
    // ---------- Tabs: Live operations / Sales & trends ----------
    var tabs = document.querySelectorAll('.md-page-tabs [data-tab]');
    var chartsDrawn = false;

    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            tabs.forEach(function (t) { t.classList.toggle('active', t === tab); });
            document.querySelectorAll('[data-pane]').forEach(function (pane) {
                pane.classList.toggle('d-none', pane.dataset.pane !== tab.dataset.tab);
            });

            // Charts are drawn the first time the tab is opened (a hidden canvas has no size)
            if (tab.dataset.tab === 'trends' && !chartsDrawn) {
                drawCharts();
                chartsDrawn = true;
            }
        });
    });

    // ---------- Segmented buttons (period, daily/weekly/monthly) ----------
    document.querySelectorAll('.md-segment').forEach(function (group) {
        group.querySelectorAll('button').forEach(function (button) {
            button.addEventListener('click', function () {
                group.querySelectorAll('button').forEach(function (b) { b.classList.remove('active'); });
                button.classList.add('active');
            });
        });
    });

    // ---------- Charts ----------
    function drawCharts() {
        if (typeof Chart === 'undefined') return;

        var bar = '#5165ad';        // indigo bars
        var highlight = '#4b9a8e';  // teal: today / busiest hour
        var grid = '#efeae0';

        Chart.defaults.font.family = '"Jost", "Segoe UI", sans-serif';
        Chart.defaults.font.size = 11;
        Chart.defaults.color = '#7a756b';

        var axes = {
            x: { grid: { display: false }, border: { color: grid } },
            y: { grid: { color: grid }, border: { display: false }, beginAtZero: true }
        };

        // Daily sales, RM thousands (16 Sep = today, partial)
        var sales = [7.18, 8.09, 6.39, 9.76, 11.18, 12.62, 10.07, 7.92, 8.41, 6.88, 10.48, 12.93, 13.39, 9.60, 8.79, 4.60];
        var days = sales.map(function (_, i) { return String(i + 1); });

        new Chart(document.getElementById('salesChart'), {
            type: 'bar',
            data: {
                labels: days,
                datasets: [{
                    data: sales,
                    backgroundColor: sales.map(function (_, i) { return i === sales.length - 1 ? highlight : bar; }),
                    borderRadius: 2,
                    maxBarThickness: 28
                }]
            },
            options: {
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: function (c) { return 'RM ' + (c.raw * 1000).toLocaleString('en-MY'); } } }
                },
                scales: axes
            }
        });

        // Reservations by start hour (20:00 = busiest)
        var hours = ['12:00', '13:00', '14:00', '18:00', '19:00', '20:00', '21:00', '22:00'];
        var bookings = [30, 34, 12, 22, 58, 72, 36, 9];

        new Chart(document.getElementById('peakChart'), {
            type: 'bar',
            data: {
                labels: hours,
                datasets: [{
                    data: bookings,
                    backgroundColor: bookings.map(function (v) { return v === Math.max.apply(null, bookings) ? highlight : bar; }),
                    borderRadius: 2,
                    maxBarThickness: 24
                }]
            },
            options: {
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: { callbacks: { label: function (c) { return c.raw + ' reservations'; } } }
                },
                scales: axes
            }
        });
    }
})();

// ---------- Top menu items: ranked list ----------
// Example data – replace with this month's figures per menu item:
//   portions = sum of OrderItem quantity, revenue = quantity × Menu.menuPrice, trend = % change vs last month
(function () {
    var list = document.getElementById('rankList');
    if (!list) return;

    var items = [
        { name: 'Wagyu Beef Tenderloin', category: 'Mains', portions: 212, revenue: 35616, trend: 12 },
        { name: 'Pan-Seared Sea Bass', category: 'Mains', portions: 187, revenue: 18326, trend: 4 },
        { name: 'Lobster Thermidor', category: 'Mains', portions: 149, revenue: 19072, trend: -6 },
        { name: 'Chocolate Fondant', category: 'Desserts', portions: 138, revenue: 4968, trend: 9 },
        { name: 'Caviar & Soft Egg', category: 'Starters', portions: 96, revenue: 8160, trend: 0 },
        { name: 'Duck à l\'Orange', category: 'Mains', portions: 90, revenue: 8280, trend: 3 },
        { name: 'Oyster Selection', category: 'Starters', portions: 84, revenue: 5712, trend: -2 },
        { name: 'Château Margaux 2018 · glass', category: 'Beverages', portions: 80, revenue: 9600, trend: 7 },
        { name: 'Mille-Feuille', category: 'Desserts', portions: 72, revenue: 2304, trend: 5 },
        { name: 'Sparkling Water', category: 'Beverages', portions: 70, revenue: 1260, trend: 1 }
    ];

    var metric = 'portions';
    var category = 'All';
    var totals = {
        portions: items.reduce(function (s, i) { return s + i.portions; }, 0),
        revenue: items.reduce(function (s, i) { return s + i.revenue; }, 0)
    };

    function escapeHtml(text) {
        return text.replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
        });
    }

    function draw() {
        var rows = items
            .filter(function (i) { return category === 'All' || i.category === category; })
            .sort(function (a, b) { return b[metric] - a[metric]; })
            .slice(0, 5);
        var max = rows.length ? rows[0][metric] : 1;

        document.getElementById('rankValueHead').textContent = metric === 'portions' ? 'Sold' : 'RM';

        list.innerHTML = rows.map(function (item, index) {
            var value = item[metric];
            var percent = Math.round(value / totals[metric] * 100);
            var shown = metric === 'portions' ? value : 'RM ' + value.toLocaleString('en-MY');
            var trend = item.trend > 0 ? '<span class="md-trend is-up">▲ ' + item.trend + '%</span>'
                      : item.trend < 0 ? '<span class="md-trend is-down">▼ ' + Math.abs(item.trend) + '%</span>'
                      : '<span class="md-trend">—</span>';

            return '<li class="md-rank-row' + (index === 0 ? ' is-top' : '') + '">' +
                '<span class="md-rank-no">' + (index + 1) + '</span>' +
                '<span class="md-rank-dish"><span class="md-rank-name">' + escapeHtml(item.name) + '</span>' +
                '<span class="md-rank-meta">' + item.category + ' · ' + percent + '%</span></span>' +
                '<span class="md-rank-bar"><i style="width:' + Math.round(value / max * 100) + '%"></i></span>' +
                '<span class="md-rank-value">' + shown + '</span>' +
                trend +
                '</li>';
        }).join('');
    }

    document.querySelectorAll('#rankMetric button').forEach(function (b) {
        b.addEventListener('click', function () { metric = b.dataset.metric; draw(); });
    });
    document.querySelectorAll('#rankCategory button').forEach(function (b) {
        b.addEventListener('click', function () {
            document.querySelectorAll('#rankCategory button').forEach(function (x) { x.classList.toggle('active', x === b); });
            category = b.dataset.category;
            draw();
        });
    });

    draw();
})();

// ---------- Period selector (day / month / year) ----------
// Updates the period wording on the page. Loading figures for the chosen period is for the team to add.
(function () {
    var day = document.getElementById('periodDay');
    var month = document.getElementById('periodMonth');
    var year = document.getElementById('periodYear');
    if (!day || !month || !year) return;

    function update() {
        // A single day only makes sense inside a chosen month
        day.disabled = month.value === '';
        if (day.disabled) day.value = '';

        var label = month.value === '' ? year.value                       // whole year
                  : day.value === '' ? month.value                        // whole month
                  : day.value + ' ' + month.value;                        // single date

        document.querySelectorAll('[data-period-label]').forEach(function (el) {
            el.textContent = el.hasAttribute('data-with-year') && month.value !== '' ? label + ' ' + year.value : label;
        });
    }

    [day, month, year].forEach(function (select) { select.addEventListener('change', update); });
    update();
})();
