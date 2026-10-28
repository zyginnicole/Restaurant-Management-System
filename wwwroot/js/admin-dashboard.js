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

        var salesCanvas = document.getElementById('salesChart');
        if (!salesCanvas) return;
        var sales = JSON.parse(salesCanvas.dataset.values || '[]');
        var days = JSON.parse(salesCanvas.dataset.labels || '[]');

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

        // Reservations by start hour for the chosen period (data-labels / data-values from the server; busiest = teal)
        var peakCanvas = document.getElementById('peakChart');
        if (!peakCanvas) return;   // no reservations in this period
        var hours = JSON.parse(peakCanvas.dataset.labels || '[]');
        var bookings = JSON.parse(peakCanvas.dataset.values || '[]');

        new Chart(peakCanvas, {
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

    var items = JSON.parse(list.dataset.items || '[]').map(function (item) {
        return {
            name: item.name,
            category: 'All',
            portions: Number(item.sold || 0),
            revenue: Number(item.sold || 0),
            trend: 0
        };
    });

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
// Reloads the page for the chosen period: ?year=2026&month=10&day=25 · ?year=2026&month=10 · ?year=2026.
// The server works out the figures and the period wording (DashboardModel).
(function () {
    var day = document.getElementById('periodDay');
    var month = document.getElementById('periodMonth');
    var year = document.getElementById('periodYear');
    if (!day || !month || !year) return;

    // A single day only makes sense inside a chosen month
    function lockDay() {
        day.disabled = month.value === '';
        if (day.disabled) day.value = '';
    }

    function reload() {
        lockDay();
        var query = new URLSearchParams({ year: year.value, month: month.value || '0' });
        if (day.value) query.set('day', day.value);
        window.location.search = query.toString();
    }

    [day, month, year].forEach(function (select) { select.addEventListener('change', reload); });
    lockDay();
})();
