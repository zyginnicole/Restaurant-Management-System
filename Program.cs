using Microsoft.EntityFrameworkCore;
using RestaurantManagementSystem.Data;
using RestaurantManagementSystem.Filters;
using RestaurantManagementSystem.Helpers;
using RestaurantManagementSystem.Services;

// QuestPDF requires the application to declare the license used by this project.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages + role protection per folder.
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
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptionsAction: sqlOptions => sqlOptions.EnableRetryOnFailure()));

// Session stores the signed-in user's ID, role and name between pages.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddMemoryCache();

builder.Services.AddSingleton<PasswordService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddHostedService<ReservationCleanupService>();
builder.Services.AddScoped<RecommendationService>();
builder.Services.AddScoped<PromotionService>();
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<DemoAccountSeeder>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ExportService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<ChatbotService>();

builder.Services.AddHttpClient<OllamaClient>((services, client) =>
{
    var config = services.GetRequiredService<IConfiguration>();
    client.BaseAddress = new Uri(config["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromSeconds(config.GetValue("Ollama:TimeoutSeconds", 60));
});

var app = builder.Build();

// Apply migrations before seeding so the database matches the current model.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();

    if (app.Environment.IsDevelopment())
    {
        var demoSeeder = scope.ServiceProvider.GetRequiredService<DemoAccountSeeder>();
        await demoSeeder.SeedAsync();
    }

    DbSeeder.Seed(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/Error");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.Use(async (context, next) =>
{
    var session = context.Session;
    if (session.Keys.Any())
    {
        var settings = await context.RequestServices.GetRequiredService<SettingsService>().GetAsync();
        var last = session.GetString("LastActive");

        if (last != null && DateTimeOffset.TryParse(last, out var time) &&
            DateTimeOffset.Now - time > TimeSpan.FromMinutes(settings.idleTimeoutMinutes))
        {
            session.Clear();
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

// Chatbot endpoint used by the customer menu and dashboard widgets.
app.MapPost("/api/chat", async (ChatRequest request, ChatbotService bot) =>
{
    var navigation = bot.GetNavigation(request.Message);
    return Results.Ok(new
    {
        reply = await bot.GetReplyAsync(request.Message),
        linkUrl = navigation?.Url,
        linkText = navigation?.Text
    });
});

app.Run();
