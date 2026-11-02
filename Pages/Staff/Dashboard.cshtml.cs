using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff dashboard: today's reservations and the open orders (both from the database).
    // Clicking a reservation opens the shared details pop-up; its actions post to /Staff/Reservations and come back here.
    // Orders: every order that is "Order placed", "Preparing" or "Served" and not paid yet (any day, so nothing is missed).
    // Awaiting payment = Served orders without a Paid payment, grouped into bills like Billing does
    // (dine-in: one bill per table, takeaway: one bill per order); amounts are Order.totalAmount, as on the Orders page.
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly ReservationService _reservations;
        private readonly SettingsService _settings;

        public DashboardModel(ApplicationDbContext db, ReservationService reservations, SettingsService settings)
        {
            _db = db;
            _reservations = reservations;
            _settings = settings;
        }

        // Statuses shown on the dashboard (Cancelled, Declined and No-show are only on the Reservations page)
        public static readonly string[] Shown =
            { ReservationStatus.Pending, ReservationStatus.Confirmed, ReservationStatus.Seated, ReservationStatus.Completed };

        public SystemSetting Settings { get; set; } = new();
        public List<Reservation> Today { get; set; } = new();     // every booking today (for the "next on this table" text)
        public List<Reservation> Guests { get; set; } = new();    // the ones shown, earliest first
        public string StaffName { get; set; } = "";
        public int TablesOccupied { get; set; }
        public int TablesTotal { get; set; }

        // Pending requests from today on (all days) and the day of the earliest one – "X need approval" link
        public int PendingUpcoming { get; set; }
        public DateTime? PendingFirstDate { get; set; }
        public int PendingDays { get; set; }   // how many different days those requests are on

        // ---------- Orders (stat cards 3–4 + Active orders) ----------
        public static readonly string[] OpenOrderStatuses = { "Order placed", "Preparing", "Served" };
        public List<OrderRow> OpenOrders { get; set; } = new();
        public decimal AwaitingTotal { get; set; }
        public int AwaitingBills { get; set; }
        public string? AwaitingFirstCode { get; set; }              // first bill to open on Billing (?order=)
        public List<string> AwaitingLabels { get; set; } = new();   // "I-01", "Takeaway"

        public class OrderRow
        {
            public string Code { get; set; } = string.Empty;        // "ORD-0085"
            public string? Table { get; set; }                      // "I-03" (null = takeaway)
            public string Name { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int Items { get; set; }
            public decimal Total { get; set; }
        }

        public async Task OnGetAsync()
        {
            // orders left open from a day that has passed are cancelled (refunded when paid)
            await StaleOrders.CancelPreviousDaysAsync(_db);

            Settings = await _settings.GetAsync();
            StaffName = HttpContext.Session.GetUserName() ?? "";

            Today = await _reservations.GetDayAsync(DateTime.Today);
            Guests = Today.Where(r => Shown.Contains(r.reservationStatus))
                          .OrderBy(r => r.reservationDateTime)
                          .ToList();

            var pending = await _reservations.GetPendingUpcomingAsync();
            PendingUpcoming = pending.Count;
            PendingFirstDate = pending.FirstOrDefault()?.reservationDateTime.Date;
            PendingDays = pending.Select(r => r.reservationDateTime.Date).Distinct().Count();

            // Tables in use right now: a Seated booking, or tableStatus Occupied (e.g. a walk-in order). Out-of-service tables don't count.
            var tables = await _db.DiningTables.AsNoTracking().Where(t => t.tableStatus != "Maintenance").ToListAsync();
            var seatedTableIds = Today.Where(r => r.reservationStatus == ReservationStatus.Seated).Select(r => r.tableID).ToHashSet();
            TablesTotal = tables.Count;
            TablesOccupied = tables.Count(t => t.tableStatus == "Occupied" || seatedTableIds.Contains(t.tableID));

            await LoadOrdersAsync();
        }

        private async Task LoadOrdersAsync()
        {
            var orders = await _db.Orders.AsNoTracking()
                .Include(o => o.Customer)
                .Include(o => o.DiningTable)
                .Include(o => o.Payment)
                .Include(o => o.OrderItems)
                .Where(o => OpenOrderStatuses.Contains(o.orderStatus))
                .ToListAsync();
            // a paid takeaway that is Served is only waiting to be collected, not for payment
            bool Paid(Order o) => o.Payment?.paymentStatus == "Paid";

            // Order placed first (to accept), then Preparing, then Served; newest first within each
            OpenOrders = orders
                .OrderBy(o => Array.IndexOf(OpenOrderStatuses, o.orderStatus))
                .ThenByDescending(o => o.orderDateTime)
                .Select(o => new OrderRow
                {
                    Code = "ORD-" + o.orderID.ToString("0000"),
                    Table = o.DiningTable?.tableNumber,
                    Name = o.Customer?.custName ?? "Guest",
                    Status = o.orderStatus,
                    Items = o.OrderItems.Sum(oi => oi.quantity),
                    Total = Paid(o) ? o.Payment!.paymentAmount : o.totalAmount
                })
                .ToList();

            var awaiting = orders.Where(o => o.orderStatus == "Served" && !Paid(o)).ToList();
            var bills = awaiting.Where(o => o.tableID != null).GroupBy(o => o.tableID!.Value).Select(g => g.ToList())
                .Concat(awaiting.Where(o => o.tableID == null).Select(o => new List<Order> { o }))
                .Select(g => g.OrderBy(o => o.orderID).ToList())
                .OrderBy(g => g[0].orderID)
                .ToList();

            AwaitingTotal = awaiting.Sum(o => o.totalAmount);
            AwaitingBills = bills.Count;
            AwaitingFirstCode = bills.Count > 0 ? "ORD-" + bills[0][0].orderID.ToString("0000") : null;
            AwaitingLabels = bills.Select(g => g[0].DiningTable?.tableNumber ?? "Takeaway").ToList();
        }
    }
}