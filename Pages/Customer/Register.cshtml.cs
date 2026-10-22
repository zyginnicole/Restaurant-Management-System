using Microsoft.AspNetCore.Mvc.Filters;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;
using CustomerModel = RestaurantManagementSystem.Model.Customer;

namespace RestaurantManagementSystem.Pages.Customer
{
    [AllowAnonymous] // must stay open: new customers are not signed in yet
    public class RegisterModel : PageModel
    {
        private readonly AuthService _authService;
        private readonly SettingsService _settingsService;

        public RegisterModel(AuthService authService, SettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
        }

        // The form fields (Customer.custName, custEmail, custPhone, DietaryPref, custPassword) bind to this object
        [BindProperty]
        public CustomerModel Customer { get; set; } = new CustomerModel();

        // These three are NOT columns in the Customer table, they only help the form
        [BindProperty]
        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;   // validation only

        [BindProperty]
        public bool AgreeTerms { get; set; }                          // must be ticked

        [BindProperty]
        public bool PromoOptIn { get; set; }                          // optional, copied to Customer.promoOptIn

        // Password rule sentence shown under the password box (built from Admin > Settings > Security)
        public string PasswordHint { get; set; } = string.Empty;

        // Runs before OnGet / OnPost, so the hint is also filled when the form is shown again after an error
        public override async Task OnPageHandlerExecutionAsync(
            PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            PasswordHint = PasswordPolicy.Describe(await _settingsService.GetAsync());
            await next();
        }

        public IActionResult OnGet()
        {
            // already signed in as a customer, go straight to the dashboard
            if (HttpContext.Session.IsSignedInAs(UserRoles.Customer))
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

            var settings = await _settingsService.GetAsync();
            string? passwordError = PasswordPolicy.Check(Customer.custPassword, settings);
            if (passwordError != null)
            {
                ModelState.AddModelError("Customer.custPassword", passwordError);
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            //email must not belong to ANY account (customer, staff or admin)
            if (await _authService.IsEmailTakenAsync(Customer.custEmail))
            {
                ModelState.AddModelError(
                    "Customer.custEmail",
                    "This email is already registered. Please sign in or use another email.");
                return Page();
            }

            //save: the service trims the fields and hashes the password
            Customer.promoOptIn = PromoOptIn;
            await _authService.RegisterCustomerAsync(Customer);

            //show the success pop-up (redirect to itself so refresh won't re-submit the form)
            TempData["SuccessMessage"] = "Your account has been created. You can now sign in.";
            return RedirectToPage();
        }
    }
}