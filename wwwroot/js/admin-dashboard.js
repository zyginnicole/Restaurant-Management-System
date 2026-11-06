// Admin Dashboard: tab switching, segmented buttons and database-backed Chart.js charts.
(function () {
    // ---------- Tabs: Live operations / Sales & trends ----------
    var tabs = document.querySelectorAll('.md-page-tabs [data-tab]');
    var chartsDrawn = false;
    var peakChartInstance = null;

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

    function chartStep(maxValue) {
        if (maxValue <= 0) return 1;

        var roughStep = maxValue / 5;
        var magnitude = Math.pow(10, Math.floor(Math.log10(roughStep)));
        var normalised = roughStep / magnitude;
        var nice = normalised <= 1 ? 1 : normalised <= 2 ? 2 : normalised <= 5 ? 5 : 10;
        return nice * magnitude;
    }

    function formatHourLabel(label, format) {
        var hour = Number(String(label).split(':')[0]);
        if (format === '24') return hour.toString().padStart(2, '0') + ':00';

        var suffix = hour >= 12 ? 'PM' : 'AM';
        var shownHour = hour % 12 || 12;
        return shownHour + ':00 ' + suffix;
    }

    function drawPeakChart() {
        if (typeof Chart === 'undefined') return;

        var peakCanvas = document.getElementById('peakChart');
        if (!peakCanvas) return;

        var hours = JSON.parse(peakCanvas.dataset.labels || '[]');
        var bookings = JSON.parse(peakCanvas.dataset.values || '[]');
        var formatSelect = document.getElementById('peakTimeFormat');
        var format = formatSelect ? formatSelect.value : '24';
        var labels = hours.map(function (hour) { return formatHourLabel(hour, format); });
        var bar = '#5165ad';
        var highlight = '#4b9a8e';
        var grid = '#efeae0';
        var busiest = bookings.length ? Math.max.apply(null, bookings) : 0;

        if (peakChartInstance) peakChartInstance.destroy();

        peakChartInstance = new Chart(peakCanvas, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    data: bookings,
                    backgroundColor: bookings.map(function (value) { return value === busiest ? highlight : bar; }),
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
                scales: {
                    x: {
                        grid: { display: false },
                        border: { color: grid },
                        title: { display: true, text: 'Time' }
                    },
                    y: {
                        grid: { color: grid },
                        border: { display: false },
                        beginAtZero: true,
                        suggestedMax: Math.max(chartStep(busiest) * 5, chartStep(busiest)),
                        ticks: { stepSize: chartStep(busiest), precision: 0 },
                        title: { display: true, text: 'Reservations' }
                    }
                }
            }
        });
    }

    // ---------- Charts ----------
    function drawCharts() {
        if (typeof Chart === 'undefined') return;

        var bar = '#5165ad';        // indigo bars
        var highlight = '#4b9a8e';  // teal: today / busiest hour
        var grid = '#efeae0';

        Chart.defaults.font.family = '"Jost", "Segoe UI", sans-serif';
        Chart.defaults.font.size = 11;
        Chart.defaults.color = '#7a756b';

        var salesCanvas = document.getElementById('salesChart');
        if (!salesCanvas) return;
        var sales = JSON.parse(salesCanvas.dataset.values || '[]');
        var days = JSON.parse(salesCanvas.dataset.labels || '[]');
        var salesMax = sales.length ? Math.max.apply(null, sales) : 0;
        var salesStep = chartStep(salesMax);

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
                    tooltip: { callbacks: { label: function (c) { return 'RM ' + Number(c.raw).toLocaleString('en-MY', { maximumFractionDigits: 0 }); } } }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        border: { color: grid },
                        title: { display: true, text: 'Day of month' }
                    },
                    y: {
                        grid: { color: grid },
                        border: { display: false },
                        beginAtZero: true,
                        suggestedMax: Math.max(salesStep * 5, salesStep),
                        ticks: {
                            stepSize: salesStep,
                            precision: 0,
                            callback: function (value) { return 'RM ' + Number(value).toLocaleString('en-MY', { maximumFractionDigits: 0 }); }
                        },
                        title: { display: true, text: 'Daily sales (RM)' }
                    }
                }
            }
        });

        drawPeakChart();
    }

    var peakFormat = document.getElementById('peakTimeFormat');
    if (peakFormat) peakFormat.addEventListener('change', drawPeakChart);
})();

// ---------- Top menu items: ranked list ----------
// The page supplies menu-item quantities, categories and revenue from ReportService.
(function () {
    var list = document.getElementById('rankList');
    if (!list) return;

    var items = JSON.parse(list.dataset.items || '[]').map(function (item) {
        return {
            name: item.name,
            category: item.category || 'Menu',
            portions: Number(item.sold || 0),
            revenue: Number(item.revenue || 0),
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
