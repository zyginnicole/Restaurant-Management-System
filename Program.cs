using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Session: keeps the signed-in user's ID, role and name between pages
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);   // signed out after 30 minutes of no activity
    options.Cookie.HttpOnly = true;                   // JavaScript cannot read the session cookie
    options.Cookie.IsEssential = true;                // session works without a cookie-consent banner
});

// Account services (business logic layer)
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<DemoAccountSeeder>();

var app = builder.Build();

// Development only: create the demo admin and staff accounts if they are missing
if (app.Environment.IsDevelopment())
{
    using (IServiceScope scope = app.Services.CreateScope())
    {
        DemoAccountSeeder seeder = scope.ServiceProvider.GetRequiredService<DemoAccountSeeder>();
        await seeder.SeedAsync();
    }
}

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