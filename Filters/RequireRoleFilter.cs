using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using RestaurantManagementSystem.Helpers;

namespace RestaurantManagementSystem.Filters
{
    // Runs before every page in a protected folder (set up in Program.cs).
    // Pages marked [AllowAnonymous] (login, register, ...) are skipped.
    // - Right role             → the page runs (and the browser is told not to keep a copy).
    // - Not signed in          → the login page, with a message ("Please sign in" or "signed out after X minutes")
    //                            and returnUrl, so after signing in they come back to the page they wanted.
    // - Signed in, wrong role  → the Access denied page (e.g. a customer opening /Admin/Accounts).
    public class RequireRoleFilter : IPageFilter
    {
        // Set by the idle-timeout code in Program.cs when it signs someone out
        public const string SessionExpiredKey = "SessionExpiredMinutes";

        private readonly string _requiredRole;
        private readonly string _loginPage;

        public RequireRoleFilter(string requiredRole, string loginPage)
        {
            _requiredRole = requiredRole;
            _loginPage = loginPage;
        }

        public void OnPageHandlerSelected(PageHandlerSelectedContext context)
        {
        }

        public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            // Login and register pages must stay open
            bool isPublicPage = context.HandlerInstance.GetType()
                .IsDefined(typeof(AllowAnonymousAttribute), inherit: true);

            if (isPublicPage)
            {
                return;
            }

            HttpContext http = context.HttpContext;
            string? role = http.Session.GetUserRole();

            if (role == _requiredRole)
            {
                // Allowed. Tell the browser not to keep a copy of this private page,
                // so pressing Back after signing out cannot show it again.
                http.Response.Headers.CacheControl = "no-store, no-cache";
                return;
            }

            if (role == null)
            {
                // Not signed in → login page with a message (TempData survives the redirect)
                string notice = http.Items.TryGetValue(SessionExpiredKey, out object? minutes)
                    ? $"You were signed out after {minutes} minutes without activity. Please sign in again."
                    : "Please sign in to continue.";

                ITempDataDictionary tempData = http.RequestServices
                    .GetRequiredService<ITempDataDictionaryFactory>()
                    .GetTempData(http);
                tempData["LoginNotice"] = notice;

                // Come back here after signing in (only for normal page visits, not form posts)
                string? returnUrl = HttpMethods.IsGet(http.Request.Method)
                    ? http.Request.Path + http.Request.QueryString
                    : null;

                // Setting context.Result stops the page from running at all
                context.Result = new RedirectToPageResult(_loginPage, new { returnUrl });
                return;
            }

            // Signed in, but this area belongs to another role
            context.Result = new RedirectToPageResult("/AccessDenied");
        }

        public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
        }
    }
}