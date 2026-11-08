namespace RestaurantManagementSystem.Services
{
    // Read-only rows and numbers for the Admin → Accounts page (filled by AccountService).

    // One row of the Staff tab: a Staff row ("S-12") or an Admin row ("A-1")
    public class StaffAccountRow
    {
        public string AccountId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;       // admins have no phone
        public string JobTitle { get; set; } = string.Empty;    // admins have no job title
        public string Role { get; set; } = string.Empty;        // "Staff" or "Administrator"
        public bool Active { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }        // set after too many failed sign-ins
    }

    // One row of the Customers tab
    public class CustomerAccountRow
    {
        public int CustId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public DateTimeOffset MemberSince { get; set; }
        public int Visits { get; set; }                         // completed reservations
        public bool PromoOptIn { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }

    // The four stat cards at the top of the page
    public class AccountStats
    {
        public int CustomerTotal { get; set; }
        public int CustomersNewThisWeek { get; set; }
        public int StaffActive { get; set; }
        public int StaffDeactivated { get; set; }
        public int AdminTotal { get; set; }
        public int LockedCustomers { get; set; }
        public int LockedStaff { get; set; }                    // staff + administrators
    }
}