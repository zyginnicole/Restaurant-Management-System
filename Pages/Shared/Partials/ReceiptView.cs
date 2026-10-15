namespace RestaurantManagementSystem.Pages.Shared.Partials
{
    // Data for the shared receipt (_Receipt.cshtml) – used for both dine-in and takeaway.
    // Not a database table: the backend fills it from Payment + Order (+ OrderItems, Menu, Customer,
    // Reservation, DiningTable) and passes it to <partial name="~/Pages/Shared/Partials/_Receipt.cshtml" model="..." />.
    //
    //   ReceiptNo      "RCP-" + Payment.paymentID.ToString("0000")
    //   OrderCode      "ORD-" + Order.orderID.ToString("0000")
    //   IsDineIn       Order.reservationID != null  (takeaway orders have no reservation)
    //   Items          one line per OrderItem: quantity, Menu.menuName, unitPrice
    //   PaymentMethod  Payment.paymentMethod,  PaymentStatus  Payment.paymentStatus
    //   PaidAt         Payment.paymentDate
    //
    // Service charge, SST and total are worked out here, so every page shows the same numbers.
    // The total should match Order.totalAmount / Payment.paymentAmount.
    public class ReceiptView
    {
        public const decimal ServiceChargeRate = 0.10m;   // dine-in only
        public const decimal SstRate = 0.06m;             // on subtotal + service charge

        public string ReceiptNo { get; set; } = "";
        public string OrderCode { get; set; } = "";
        public bool IsDineIn { get; set; }
        public string? ReservationCode { get; set; }      // dine-in, e.g. "R-0038"
        public string? TableCode { get; set; }            // dine-in, e.g. "T-12"
        public string? PickupTime { get; set; }           // takeaway – no field in Order yet, leave null to hide
        public string CustomerName { get; set; } = "";
        public List<ReceiptLine> Items { get; set; } = new();
        public string PaymentMethod { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public DateTimeOffset PaidAt { get; set; }

        public decimal Subtotal => Items.Sum(i => i.LineTotal);
        public decimal ServiceCharge => IsDineIn ? Round(Subtotal * ServiceChargeRate) : 0m;
        public decimal Sst => Round((Subtotal + ServiceCharge) * SstRate);
        public decimal Total => Subtotal + ServiceCharge + Sst;

        private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public class ReceiptLine
    {
        public int Quantity { get; set; }
        public string Name { get; set; } = "";
        public decimal UnitPrice { get; set; }            // OrderItem.unitPrice (price when ordered)
        public string? Note { get; set; }                 // e.g. "Medium rare" – no field in OrderItem yet, leave null to hide
        public decimal LineTotal => Quantity * UnitPrice;
    }
}
