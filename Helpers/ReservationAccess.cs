using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Model;

namespace RestaurantManagementSystem.Helpers
{
    // What a customer may do on Menu / Checkout today:
    //  - HasReservation: a reservation today (Pending, Confirmed or Seated) → the menu is open and they can order takeaway
    //  - Seated:         that reservation has status "Seated" (staff seated the guests) → dine-in is allowed, at that table
    public record OrderingAccess(bool HasReservation, Reservation? Seated)
    {
        public bool CanDineIn => Seated != null;
    }

    public static class ReservationAccess
    {
        // Cancelled / Declined / No-show / Completed (bill already paid) do not count
        private static readonly string[] OrderingStatuses = { "Pending", "Confirmed", "Seated" };

        private static IQueryable<Reservation> TodayQuery(ApplicationDbContext db, int custId)
        {
            var today = DateTime.Today;
            var statuses = OrderingStatuses;

            return db.Reservations.AsNoTracking()
                .Where(r => r.custID == custId
                         && r.reservationDateTime >= today && r.reservationDateTime < today.AddDays(1)
                         && statuses.Contains(r.reservationStatus))
                .OrderBy(r => r.reservationDateTime);
        }

        // One query: reservation today? and is the guest Seated? (Checkout)
        public static async Task<OrderingAccess> CheckAsync(ApplicationDbContext db, int custId)
        {
            var list = await TodayQuery(db, custId).ToListAsync();
            return new OrderingAccess(list.Count > 0, list.FirstOrDefault(r => r.reservationStatus == "Seated"));
        }

        // The customer's earliest reservation for today, or null = the menu stays locked (Menu)
        public static Task<Reservation?> FindTodayAsync(ApplicationDbContext db, int custId)
        {
            return TodayQuery(db, custId).FirstOrDefaultAsync();
        }
    }
}