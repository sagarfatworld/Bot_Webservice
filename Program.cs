using Botatwork_in_Livechat.Middleware;
using Botatwork_in_Livechat.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Configure HttpClient with default headers
builder.Services.AddHttpClient("BotApi", client =>
{
    client.BaseAddress = new Uri("https://api.botatwork.com/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Add SQL Connection
builder.Services.AddTransient<SqlConnection>(_ =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Services
builder.Services.AddScoped<ITokenService, TokenService>();


builder.Services.AddScoped<IChatStorageService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<ChatStorageService>>();
    var configuration = provider.GetRequiredService<IConfiguration>();

    try
    {
        return new ChatStorageService(configuration, logger);
    }
    catch (Exception ex)
    {
        logger.LogError($"Failed to create ChatStorageService: {ex.Message}");
        throw;
    }
});

builder.Services.AddHostedService<ChatCleanupService>();

// Add Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromDays(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// Add Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Index";
        options.LogoutPath = "/Home/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Add the token refresh middleware
app.UseTokenRefresh();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Token refresh timer
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStarted.Register(() =>
{
    var timer = new Timer(async _ =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
            await tokenService.RefreshTokenIfNeeded();
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Error in token refresh timer");
        }
    }, null, TimeSpan.Zero, TimeSpan.FromMinutes(5));
});

app.Run();