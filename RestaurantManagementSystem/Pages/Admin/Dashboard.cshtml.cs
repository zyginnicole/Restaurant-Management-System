using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages.Admin
{
    // Admin dashboard: live operations and sales & trends. Static UI for now –
    // calculate the figures from Orders, Payments, Reservations, Customers and Feedback later.
    public class DashboardModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
