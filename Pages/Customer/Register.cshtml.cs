using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using CustomerModel = RestaurantManagementSystem.Model.Customer;

namespace RestaurantManagementSystem.Pages.Customer
{
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public RegisterModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // The form fields (Customer.custName, custEmail, custPhone, DietryPref, custPassword) bind to this object
        [BindProperty]
        public CustomerModel Customer { get; set; } = new CustomerModel();

        // These three are NOT saved in the Customer table, they only help the form
        [BindProperty]
        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;   // validation only

        [BindProperty]
        public bool AgreeTerms { get; set; }                          // must be ticked

        [BindProperty]
        public bool PromoOptIn { get; set; }                          // optional

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
            //check phone number: digits only, 10 or 11 digits
            if (!string.IsNullOrEmpty(Customer.custPhone) &&
                (!Customer.custPhone.All(char.IsDigit) || Customer.custPhone.Length < 10))
            {
                ModelState.AddModelError(
                    "Customer.custPhone",
                    "Phone number must be 10 to 11 digits (numbers only).");
            }

            //check both passwords match
            if (!string.IsNullOrEmpty(ConfirmPassword) && Customer.custPassword != ConfirmPassword)
            {
                ModelState.AddModelError(
                    nameof(ConfirmPassword),
                    "Passwords do not match.");
            }

            //check terms and conditions
            if (!AgreeTerms)
            {
                ModelState.AddModelError(
                    nameof(AgreeTerms),
                    "You must agree to the Terms and Privacy Policy.");
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            //check duplicate email
            string email = Customer.custEmail.Trim();

            bool emailExists = await _context.Customers
                .AnyAsync(c => c.custEmail == email);

            if (emailExists)
            {
                ModelState.AddModelError(
                    "Customer.custEmail",
                    "This email is already registered. Please sign in or use another email.");
                return Page();
            }

            //save new customer
            Customer.custName = Customer.custName.Trim();
            Customer.custEmail = email;
            Customer.custCreateAt = DateTimeOffset.Now;

            _context.Customers.Add(Customer);
            await _context.SaveChangesAsync();

            //show the success pop-up on this page (redirect to itself so refresh won't re-submit the form)
            TempData["SuccessMessage"] = "Your account has been created. You can now sign in.";
            return RedirectToPage();
        }
    }
}