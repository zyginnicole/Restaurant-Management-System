using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ---- Session: remembers who is signed in (Dev 1) ----
builder.Services.AddDistributedMemoryCache();          // session data is kept in server memory
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);    // signed out after 30 min of no activity
    options.Cookie.HttpOnly = true;                    // page scripts can't read the session cookie
    options.Cookie.IsEssential = true;                 // the site needs this cookie to work
});

// ---- Account services (Dev 1) ----
builder.Services.AddSingleton<PasswordService>();      // one shared PasswordService for the whole app
builder.Services.AddScoped<AuthService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error");   // shows the Error page for 404 / return NotFound();

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapRazorPages();

app.Run();