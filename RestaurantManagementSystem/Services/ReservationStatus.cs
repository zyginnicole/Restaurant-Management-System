namespace RestaurantManagementSystem.Services
{
    public static class ReservationStatus
    {
        public const string Pending = "Pending"; //Waiting for staff approval
        public const string Confirmed = "Confirmed";
        public const string Seated = "Seated"; //guests are at the table
        public const string Completed = "Completed"; //bill paid
        public const string Cancelled = "Cancelled"; 
        public const string NoShow = "No-show";
        public const string Declined = "Declined";

        //These still hold their table; every other status frees it
        public static readonly string[] Active = { Pending, Confirmed, Seated };

    }
}
