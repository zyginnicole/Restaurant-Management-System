using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Filters;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages + role protection per folder.
// Every page in these folders needs the matching role; login/register pages are marked [AllowAnonymous].
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddFolderApplicationModelConvention("/Customer",
        model => model.Filters.Add(new RequireRoleFilter(UserRoles.Customer, "/Customer/Login")));

    options.Conventions.AddFolderApplicationModelConvention("/Staff",
        model => model.Filters.Add(new RequireRoleFilter(UserRoles.Staff, "/Staff/Login")));

    options.Conventions.AddFolderApplicationModelConvention("/Admin",
        model => model.Filters.Add(new RequireRoleFilter(UserRoles.Admin, "/Staff/Login")));
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Session: keeps the signed-in user's ID, role and name between pages
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);   // signed out after 8 hours of no activity
    options.Cookie.HttpOnly = true;                   // JavaScript cannot read the session cookie
    options.Cookie.IsEssential = true;                // session works without a cookie-consent banner
});

// Email (Gmail SMTP): the Gmail address and App Password come from User Secrets, never from GitHub
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// Server memory: holds password reset codes for 10 minutes
builder.Services.AddMemoryCache();

// Account services (business logic layer)
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ReservationService>();
// Closes unanswered Pending requests (15 min before the booking) and earlier days' open bookings (on start, then every minute)
builder.Services.AddHostedService<ReservationCleanupService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<DemoAccountSeeder>();

var app = builder.Build();

// Seed menu/restaurant data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbSeeder.Seed(db);
}

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

app.Use(async (context, next) =>
{
    var session = context.Session;
    if (session.Keys.Any())   // someone is signed in
    {
        var s = await context.RequestServices.GetRequiredService<SettingsService>().GetAsync();
        var last = session.GetString("LastActive");

        if (last != null && DateTimeOffset.TryParse(last, out var t) &&
            DateTimeOffset.Now - t > TimeSpan.FromMinutes(s.idleTimeoutMinutes))
        {
            session.Clear();   // idle too long: signed out
        }
        else
        {
            session.SetString("LastActive", DateTimeOffset.Now.ToString("o"));
        }
    }
    await next();
});

app.UseAuthorization();

app.MapRazorPages();

app.Run();