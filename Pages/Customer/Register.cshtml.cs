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
        private readonly SignUpVerificationService _verificationService;

        public RegisterModel(AuthService authService, SettingsService settingsService,
                             SignUpVerificationService verificationService)
        {
            _authService = authService;
            _settingsService = settingsService;
            _verificationService = verificationService;
        }

        // The form fields (Customer.custName, custEmail, custPhone, custBirthday, custPassword) bind to this object
        [BindProperty]
        public CustomerModel Customer { get; set; } = new CustomerModel();

        // These are NOT columns in the Customer table, they only help the form
        [BindProperty]
        [Required(ErrorMessage = "Please confirm your password.")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;   // validation only

        [BindProperty]
        public bool AgreeTerms { get; set; }                          // must be ticked

        [BindProperty]
        public bool PromoOptIn { get; set; }                          // optional, copied to Customer.promoOptIn

        // Ticked boxes on the form. They are joined into Customer.DietaryPref / Customer.Allergies before saving.
        [BindProperty]
        public List<string> SelectedPrefs { get; set; } = new();

        [BindProperty]
        public List<string> SelectedAllergies { get; set; } = new();

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

            //check birthday (optional): not in the future, not before 1900
            if (Customer.custBirthday.HasValue &&
                (Customer.custBirthday.Value.Date > DateTime.Today || Customer.custBirthday.Value.Year < 1900))
            {
                ModelState.AddModelError(
                    "Customer.custBirthday",
                    "Please enter a valid birthday.");
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

            //ticked boxes → saved fields
            Customer.promoOptIn = PromoOptIn;
            Customer.DietaryPref = MenuVocabulary.Join(SelectedPrefs.Where(x => MenuVocabulary.Tags.Contains(x)));
            Customer.Allergies = MenuVocabulary.Join(SelectedAllergies.Where(x => MenuVocabulary.Allergens.Contains(x)));

            //email must not belong to ANY account (customer, staff or admin).
            //Exception: a guest profile (staff booked a walk-in with this email) is allowed,
            //it becomes this customer's account once the email is verified (bookings already in it).
            bool isGuestProfile = await _authService.FindGuestProfileAsync(Customer.custEmail) != null;
            if (!isGuestProfile && await _authService.IsEmailTakenAsync(Customer.custEmail))
            {
                ModelState.AddModelError(
                    "Customer.custEmail",
                    "This email is already registered. Please sign in or use another email.");
                return Page();
            }

            //NOT saved yet: the details wait in server memory (password hashed) and a 6-digit code is emailed.
            //The account is only created on the Verify email page, so fake emails never become accounts.
            var (error, devCode) = await _verificationService.StartAsync(Customer);
            if (error != null)
            {
                ModelState.AddModelError(string.Empty, error);
                return Page();
            }

            //the email is passed in TempData (not the URL)
            string email = AuthService.NormaliseEmail(Customer.custEmail);
            TempData["VerifyEmail"] = email;
            TempData["VerifyInfo"] = devCode == null
                ? $"We've sent a 6-digit code to {email}. It expires in {SignUpVerificationService.CodeMinutes} minutes."
                : $"Email isn't set up on this computer, so here is your code for testing: {devCode}";

            return RedirectToPage("/Customer/VerifyEmail");
        }
    }
}