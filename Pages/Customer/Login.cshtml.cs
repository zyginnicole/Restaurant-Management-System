using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;
using CustomerModel = RestaurantManagementSystem.Model.Customer;

namespace RestaurantManagementSystem.Pages.Customer
{
    [AllowAnonymous] // must stay open: this is where customers sign in
    public class LoginModel : PageModel
    {
        private readonly AuthService _authService;
        private readonly RememberMeService _rememberMeService;

        public LoginModel(AuthService authService, RememberMeService rememberMeService)
        {
            _authService = authService;
            _rememberMeService = rememberMeService;
        }

        // The form fields (Customer.custEmail, Customer.custPassword) bind to this object
        [BindProperty]
        public CustomerModel Customer { get; set; } = new CustomerModel();

        // "Remember me" tick box (name="rememberMe"): stay signed in on this device for 30 days
        [BindProperty]
        public bool RememberMe { get; set; }

        // The page the customer tried to open before signing in (set by RequireRoleFilter), e.g. /Customer/Orders
        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        // Shown in the red alert box on the page when sign-in fails
        public string? ErrorMessage { get; set; }

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

            //the service checks the password hash and the lockout rules
            LoginResult result = await _authService.LoginCustomerAsync(
                Customer.custEmail, Customer.custPassword);

            if (!result.Succeeded)
            {
                ErrorMessage = result.ErrorMessage;
                return Page();
            }

            //clear any old session, then store user ID + role + name
            HttpContext.Session.SignInUser(result.UserId, result.Role, result.UserName);

            //Remember me: a 30-day cookie on this device; otherwise remove any old one
            if (RememberMe)
            {
                await _rememberMeService.IssueAsync(HttpContext, result.UserId);
            }
            else
            {
                _rememberMeService.Clear(HttpContext);
            }

            //back to the page they wanted, but only a page inside the customer area of THIS site
            if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl) &&
                ReturnUrl.StartsWith("/Customer/", StringComparison.OrdinalIgnoreCase))
            {
                return LocalRedirect(ReturnUrl);
            }

            return RedirectToPage("/Customer/Dashboard");
        }
    }
}