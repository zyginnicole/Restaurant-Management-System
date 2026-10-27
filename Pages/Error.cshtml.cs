using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RestaurantManagementSystem.Pages
{
    // One shared error page for the whole site. It is reached in two ways (set up in Program.cs):
    //
    //   1. Crash / unexpected failure (status 500)
    //      app.UseExceptionHandler("/Error")   <- already in Program.cs
    //      Triggered by: an exception that isn't caught, e.g. database down or a bug.
    //      Only works outside Development (in Visual Studio you see the developer error screen instead).
    //      Shows: "Something went wrong" + reference ID.
    //
    //   2. Page or record not found (status 404)
    //      app.UseStatusCodePagesWithReExecute("/Error")   <- TO ADD in Program.cs, before app.UseRouting();
    //      Triggered by: a wrong URL (e.g. /Staff/menu), or a PageModel that does `return NotFound();`
    //      because the record doesn't exist or belongs to another user.
    //      Shows: "Page not found" (no reference ID).
    //      Until that line is added, 404s show the browser's own grey "page can't be found" screen.
    //
    // The URL in the address bar does not change, and the original status code is kept,
    // which is how this page knows which message to show.
    // Don't use RedirectToPage("/Error"): it changes the URL and sends status 200, so the page can't tell what went wrong.
    //
    // Which one to write in a PageModel:
    //   Record missing / not the user's own     -> return NotFound();
    //   Not signed in / session expired / wrong role -> return RedirectToPage("/Customer/Login"); (or "/Staff/Login")
    //   Wrong form input                        -> ModelState.AddModelError("", "message"); return Page();
    //   Action no longer allowed (status moved) -> return RedirectToPage();
    //   Unexpected failure                      -> nothing, let the exception go
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    [IgnoreAntiforgeryToken]
    public class ErrorModel : PageModel
    {
        // The request ID helps match what the user saw with the server log.
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

        // The original status code (404, 500...) is kept on the response when this page runs.
        // Read as a property (not in OnGet) so it also works when the error came from a form POST.
        public int ErrorStatusCode => HttpContext.Response.StatusCode;

        public bool IsNotFound => ErrorStatusCode == 404;

        public void OnGet()
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        }
    }
}
