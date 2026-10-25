using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RestaurantManagementSystem.Helpers;

namespace RestaurantManagementSystem.Filters
{
    // Runs before every page in a protected folder (set up in Program.cs).
    // If the signed-in user's role is not the one this folder needs, they are sent to the login page.
    // Pages marked [AllowAnonymous] (login, register) are skipped.
    public class RequireRoleFilter : IPageFilter
    {
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

            string? role = context.HttpContext.Session.GetUserRole();

            if (role != _requiredRole)
            {
                // Not signed in, or signed in with the wrong role -> back to the login page.
                // Setting context.Result stops the page from running at all.
                context.Result = new RedirectToPageResult(_loginPage);
                return;
            }

            // Allowed. Tell the browser not to keep a copy of this private page,
            // so pressing Back after signing out cannot show it again.
            context.HttpContext.Response.Headers.CacheControl = "no-store, no-cache";
        }

        public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
        }
    }
}