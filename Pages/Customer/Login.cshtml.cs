using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using CustomerModel = RestaurantManagementSystem.Model.Customer;

namespace RestaurantManagementSystem.Pages.Customer
{
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public LoginModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // The form fields (Customer.custEmail, Customer.custPassword) bind to this object
        [BindProperty]
        public CustomerModel Customer { get; set; } = new CustomerModel();

        // Shown in the red alert box on the page when sign-in fails
        public string? ErrorMessage { get; set; }

        public IActionResult OnGet()
        {
            // already signed in, go straight to the dashboard
            if (HttpContext.Session.GetString("CustomerLoggedIn") == "true")
            {
                return RedirectToPage("/Customer/Dashboard");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // login only needs email + password, so skip the validation of the other Customer fields
            ModelState.Remove("Customer.custName");
            ModelState.Remove("Customer.custPhone");
            ModelState.Remove("Customer.custPassword");   // register password rules must not block login

            //check password is not empty
            if (string.IsNullOrEmpty(Customer.custPassword))
            {
                ModelState.AddModelError(
                    "Customer.custPassword",
                    "Password is required!");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            //find customer by email
            string email = Customer.custEmail.Trim();

            CustomerModel? customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.custEmail == email);

            //check email and password (same message for both, so we don't reveal which one is wrong)
            if (customer == null || customer.custPassword != Customer.custPassword)
            {
                ErrorMessage = "Incorrect email or password.";
                return Page();
            }

            //save login info in session
            HttpContext.Session.SetString("CustomerLoggedIn", "true");
            HttpContext.Session.SetInt32("CustID", customer.custID);
            HttpContext.Session.SetString("CustName", customer.custName);

            return RedirectToPage("/Customer/Dashboard");
        }
    }
}