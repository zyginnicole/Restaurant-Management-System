using System.Globalization;
using RestaurantManagementSystem.Model;
using RestaurantManagementSystem.Services;

namespace RestaurantManagementSystem.Pages.Staff
{
    // Texts for a booking row / the Reservation details pop-up, shared by the Staff Reservations page and the Dashboard.
    public static class StaffReservationText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // List order on the staff pages: what needs action first.
        // Pending (approve) → Confirmed (seat) → Seated (at the table) → finished bookings.
        public static int Priority(string status) => status switch
        {
            ReservationStatus.Pending => 0,
            ReservationStatus.Confirmed => 1,
            ReservationStatus.Seated => 2,
            ReservationStatus.Completed => 3,
            ReservationStatus.NoShow => 4,
            ReservationStatus.Declined => 5,
            _ => 6   // Cancelled
        };

        // CSS class for a status: "No-show" → "is-noshow", "Declined" → "is-declined"
        public static string StatusClass(string status) => "is-" + status.Replace("-", "").ToLowerInvariant();

        // How the booking was made / who handled it (top of the details pop-up)
        public static string Booked(Reservation r)
        {
            var text = "Booked on " + r.reservationCreateAt.ToString("d MMM yyyy, HH:mm", Inv);
            var who = r.Staff?.staffName;
            return r.reservationStatus switch
            {
                ReservationStatus.Pending => text + " · waiting for staff approval, answer by " + ReservationService.PendingDeadline(r).ToString("HH:mm", Inv),
                ReservationStatus.Cancelled => text + (who != null ? " · cancelled by " + who : " · cancelled by the guest"),
                _ when who != null => text + " · " + r.reservationStatus.ToLowerInvariant() + " by " + who,
                _ => text
            };
        }

        // "T-03 is held 19:00–21:00 (120 min). Next booking on this table: R-0052 at 21:30."
        // Only when another active booking follows on the same table that day – otherwise empty (no note in the pop-up).
        public static string NextOnTable(Reservation r, IEnumerable<Reservation> sameDay, int diningMinutes)
        {
            var start = r.reservationDateTime;
            var next = sameDay
                .Where(o => o.tableID == r.tableID && o.reservationID != r.reservationID && o.reservationDateTime > start
                         && ReservationStatus.Active.Contains(o.reservationStatus))
                .OrderBy(o => o.reservationDateTime)
                .FirstOrDefault();
            if (next == null) return "";
            return $"{r.DiningTable?.tableNumber} is held {start.ToString("HH:mm", Inv)}–{start.AddMinutes(diningMinutes).ToString("HH:mm", Inv)} " +
                   $"({diningMinutes} min). Next booking on this table: {ReservationService.Code(next)} at {next.reservationDateTime.ToString("HH:mm", Inv)}.";
        }
    }
}
