using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Services;

// Adds repeatable development data for testing the admin dashboard, reports,
// ratings, announcements and customer notifications.
public class DevelopmentDataSeeder
{
    private const string MarkerEmail = "analytics.customer01@maisondoree.my";

    private readonly ApplicationDbContext _db;
    private readonly PasswordService _passwords;

    public DevelopmentDataSeeder(ApplicationDbContext db, PasswordService passwords)
    {
        _db = db;
        _passwords = passwords;
    }

    // Seeds three months of data once. It does nothing when the marker customer exists.
    public async Task SeedAsync()
    {
        if (await _db.Customers.AnyAsync(c => c.custEmail == MarkerEmail))
            return;

        var admin = await _db.Admins.OrderBy(a => a.adminID).FirstOrDefaultAsync();
        var staff = await _db.StaffMembers.OrderBy(s => s.staffID).FirstOrDefaultAsync();
        var tables = await _db.DiningTables.OrderBy(t => t.tableID).ToListAsync();
        var menus = await _db.Menus.Where(m => m.isAvailable).OrderBy(m => m.menuID).ToListAsync();

        if (admin == null || staff == null || tables.Count == 0 || menus.Count < 3)
            return;

        var customers = CreateCustomers();
        _db.Customers.AddRange(customers);

        var reservations = CreateReservations(customers, staff, tables);
        _db.Reservations.AddRange(reservations);

        var completedReservations = reservations
            .Where(r => r.reservationStatus == "Completed")
            .ToList();

        var orders = CreateOrders(completedReservations, staff, menus);
        _db.Orders.AddRange(orders);

        _db.Payments.AddRange(orders.Select(o => new Payment
        {
            Order = o,
            paymentAmount = o.totalAmount,
            paymentMethod = "Card",
            paymentStatus = "Paid",
            paymentDate = o.orderDateTime.AddMinutes(20)
        }));

        _db.Feedbacks.AddRange(CreateFeedback(orders));
        _db.Announcements.AddRange(CreateAnnouncements(admin));

        await _db.SaveChangesAsync();

        _db.NotificationLogs.AddRange(CreateNotifications(customers));
        await _db.SaveChangesAsync();
    }

    // Creates customers across the current three-month testing window.
    private List<Customer> CreateCustomers()
    {
        var firstMonth = new DateTimeOffset(
            new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-2));

        var names = new[]
        {
            "Amelia Tan", "Daniel Wong", "Hannah Lim", "Ibrahim Rahman",
            "Sofia Lee", "Marcus Chen"
        };

        return names.Select((name, index) => new Customer
        {
            custName = name,
            custEmail = index == 0
                ? MarkerEmail
                : $"analytics.customer{index + 1:00}@maisondoree.my",
            custPassword = _passwords.HashPassword(DemoAccountSeeder.DemoPassword),
            custPhone = $"01{(index + 1):00000000}",
            custCreateAt = firstMonth.AddDays(index * 12 + 2),
            DietaryPref = index % 2 == 0 ? "None" : "Vegetarian",
            Allergies = "None",
            promoOptIn = index % 3 != 0
        }).ToList();
    }

    // Creates completed, cancelled, confirmed and pending reservations in each month.
    private static List<Reservation> CreateReservations(
        List<Customer> customers,
        Staff staff,
        List<DiningTable> tables)
    {
        var firstMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-2);
        var reservations = new List<Reservation>();

        for (var month = 0; month < 3; month++)
        {
            var monthStart = firstMonth.AddMonths(month);
            var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);

            for (var i = 0; i < customers.Count; i++)
            {
                var day = Math.Min(5 + i, daysInMonth);
                var date = monthStart.AddDays(day - 1).AddHours(18 + (i % 4));
                var status = month == 2 && i >= 4
                    ? (i == 4 ? "Pending" : "Confirmed")
                    : i % 5 == 0 ? "Cancelled"
                    : i % 4 == 0 ? "Confirmed"
                    : "Completed";

                reservations.Add(new Reservation
                {
                    Customer = customers[i],
                    DiningTable = tables[i % tables.Count],
                    Staff = staff,
                    reservationDateTime = date,
                    partySize = 2 + (i % 5),
                    specialRequest = i % 3 == 0 ? "Window table if available" : null,
                    reservationStatus = status,
                    reservationCreateAt = new DateTimeOffset(date.AddDays(-7))
                });
            }
        }

        return reservations;
    }

    // Creates paid completed orders with line items so sales and top-item reports have data.
    private static List<Order> CreateOrders(
        List<Reservation> reservations,
        Staff staff,
        List<Menu> menus)
    {
        var orders = new List<Order>();

        foreach (var reservation in reservations)
        {
            var first = menus[reservation.reservationID % menus.Count];
            var second = menus[(reservation.reservationID + 2) % menus.Count];
            var items = new[]
            {
                new OrderItem { Menu = first, quantity = 1 + reservation.partySize % 2, unitPrice = first.menuPrice },
                new OrderItem { Menu = second, quantity = 1, unitPrice = second.menuPrice }
            };

            var subtotal = items.Sum(i => i.quantity * i.unitPrice);
            var serviceCharge = Math.Round(subtotal * .10m, 2);
            var sst = Math.Round((subtotal + serviceCharge) * .06m, 2);

            orders.Add(new Order
            {
                Customer = reservation.Customer,
                Reservation = reservation,
                Staff = staff,
                DiningTable = reservation.DiningTable,
                orderDateTime = new DateTimeOffset(reservation.reservationDateTime),
                orderStatus = "Completed",
                totalAmount = subtotal + serviceCharge + sst,
                OrderItems = items.ToList()
            });
        }

        return orders;
    }

    // Adds ratings to some completed orders so the dashboard and feedback page can be tested.
    private static List<Feedback> CreateFeedback(List<Order> orders)
    {
        return orders
            .Where((_, index) => index % 2 == 0)
            .Select((order, index) => new Feedback
            {
                Customer = order.Customer,
                Order = order,
                rating = 3 + index % 3,
                comment = index % 3 == 0
                    ? "Excellent food and attentive service."
                    : "A pleasant dining experience.",
                feedbackDate = order.orderDateTime.AddHours(2)
            })
            .ToList();
    }

    // Adds customer-facing announcements for the notifications inbox to merge.
    private static List<Announcement> CreateAnnouncements(Admin admin)
    {
        var now = DateTimeOffset.Now;
        return new List<Announcement>
        {
            new()
            {
                Admin = admin,
                announcementTitle = "Weekend tasting menu",
                announcementMessage = "Enjoy our seasonal eight-course tasting menu this weekend.",
                targetAudience = "Customers",
                isPinned = true,
                announcementDate = now.AddDays(-2)
            },
            new()
            {
                Admin = admin,
                announcementTitle = "New seasonal dishes",
                announcementMessage = "Our spring menu is now available in the customer menu.",
                targetAudience = "All",
                isPinned = false,
                announcementDate = now.AddDays(-8)
            }
        };
    }

    // Adds unread and read examples for each seeded customer.
    private static List<Notification> CreateNotifications(List<Customer> customers)
    {
        return customers.Select((customer, index) => new Notification
        {
            Customer = customer,
            notificationCategory = index % 2 == 0 ? "Order" : "Reservation",
            notificationTitle = index % 2 == 0 ? "Payment received" : "Reservation confirmed",
            notificationMessage = index % 2 == 0
                ? "Your payment was received successfully."
                : "Your reservation has been confirmed.",
            linkUrl = index % 2 == 0 ? "/Customer/History" : "/Customer/Reservations",
            isRead = index % 3 == 0,
            notificationCreateAt = DateTimeOffset.Now.AddDays(-index)
        }).ToList();
    }
}
