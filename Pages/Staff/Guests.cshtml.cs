using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Staff Guests: customer profiles to prepare each visit (read-only for staff).
    //  - Per guest: visits = Completed reservations, no-shows = "No-show" reservations,
    //    average spend = average Payment.paymentAmount of their Paid orders, last visit = latest Completed reservation.
    //    Tabs: All · Tonight (a Pending / Confirmed / Seated reservation today) · Regulars (3 or more visits).
    //    ?guest=12 shows that customer on the right.
    //  - Walk-in guests booked by staff appear too (guest profile, marked "Not registered yet") – see AuthService.
    //  - Dining preferences = Customer.DietaryPref, plus Customer.kitchenNotes when the customer wrote one.
    public class GuestsModel : PageModel
    {
        private readonly ApplicationDbContext _db;

        public GuestsModel(ApplicationDbContext db)
        {
            _db = db;
        }

        public List<GuestView> Guests { get; set; } = new();

        public class GuestView
        {
            public Model.Customer Customer { get; set; } = null!;
            public bool NotRegistered { get; set; }                  // walk-in guest profile (no password yet)
            public int Visits { get; set; }
            public int NoShows { get; set; }
            public decimal AvgSpend { get; set; }
            public Reservation? Tonight { get; set; }                // today's Pending / Confirmed / Seated booking
            public DateTime? LastVisit { get; set; }
            public List<Reservation> History { get; set; } = new();  // every reservation, newest first
        }

        public async Task OnGetAsync()
        {
            var customers = await _db.Customers.AsNoTracking()
                .Include(c => c.Reservations).ThenInclude(r => r.DiningTable)
                .OrderBy(c => c.custName)
                .ToListAsync();

            // Paid order amounts per customer (for the average spend)
            var paid = await _db.Payments.AsNoTracking()
                .Where(p => p.paymentStatus == "Paid" && p.Order != null)
                .Select(p => new { p.Order!.custID, p.paymentAmount })
                .ToListAsync();

            var today = DateTime.Today;
            Guests = customers.Select(c =>
            {
                var history = c.Reservations.OrderByDescending(r => r.reservationDateTime).ToList();
                var completed = history.Where(r => r.reservationStatus == ReservationStatus.Completed).ToList();
                var amounts = paid.Where(p => p.custID == c.custID).Select(p => p.paymentAmount).ToList();
                return new GuestView
                {
                    Customer = c,
                    NotRegistered = AuthService.IsGuestProfile(c),
                    Visits = completed.Count,
                    NoShows = history.Count(r => r.reservationStatus == ReservationStatus.NoShow),
                    AvgSpend = amounts.Count > 0 ? amounts.Average() : 0m,
                    LastVisit = completed.FirstOrDefault()?.reservationDateTime,
                    Tonight = history
                        .Where(r => r.reservationDateTime.Date == today && ReservationStatus.Active.Contains(r.reservationStatus))
                        .OrderBy(r => r.reservationDateTime)
                        .FirstOrDefault(),
                    History = history
                };
            }).ToList();
        }
    }
}
