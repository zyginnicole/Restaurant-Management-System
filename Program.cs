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

// Account services (business logic layer)
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<DemoAccountSeeder>();

var app = builder.Build();

// ---------- Database setup: create/update DB, then seed ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // 1. Create the database if missing and apply all pending migrations
    db.Database.Migrate();

    // 2. Development only: demo admin and staff accounts (must come BEFORE Seed,
    //    because promotions need an existing Admin)
    if (app.Environment.IsDevelopment())
    {
        var demoSeeder = scope.ServiceProvider.GetRequiredService<DemoAccountSeeder>();
        await demoSeeder.SeedAsync();
    }

    // 3. Menu, tables, restaurant, promotions (safe to run on every startup)
    DbSeeder.Seed(db);
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