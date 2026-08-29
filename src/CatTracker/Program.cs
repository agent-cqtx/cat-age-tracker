using System.Text.Json.Serialization;
using CatTracker.Endpoints;
using CatTracker.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Request/response bodies should accept and emit enums (e.g. AppointmentCategory)
// as their string names ("Vet") rather than raw numbers.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// --- Data storage -----------------------------------------------------
// Everything lives under this directory as small JSON files. In Docker this
// is a mounted volume so data survives container rebuilds; see docker-compose.yml.
var dataDir = builder.Configuration["DATA_DIR"] ?? Path.Combine(builder.Environment.ContentRootPath, "data");
builder.Services.AddSingleton(new JsonFileStore(dataDir));
builder.Services.AddSingleton<ProfileRepository>();
builder.Services.AddSingleton<AppointmentRepository>();
builder.Services.AddSingleton<DetailRepository>();

// --- Auth ---------------------------------------------------------------
builder.Services.AddSingleton<PasswordProvider>();
builder.Services.AddSingleton<LoginThrottle>();

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "cat_tracker_auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromHours(12);
        options.SlidingExpiration = true;

        // This app is an API + static frontend, not a page-redirect app:
        // return plain status codes instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

var passwordProvider = app.Services.GetRequiredService<PasswordProvider>();
if (passwordProvider.WasGenerated)
{
    app.Logger.LogWarning("=================================================================");
    app.Logger.LogWarning("No APP_PASSWORD was set. A temporary password was generated for this run:");
    app.Logger.LogWarning("    {Password}", passwordProvider.Password);
    app.Logger.LogWarning("It will change on every restart. Set APP_PASSWORD in your .env file to keep a fixed password.");
    app.Logger.LogWarning("=================================================================");
}

app.UseDefaultFiles();

// This is a small self-hosted app that gets rebuilt/redeployed often during
// development -- browsers caching index.html/app.js/styles.css from before
// a rebuild (and then silently rendering against stale markup) is a much
// more likely problem than the bandwidth cost of not caching a few small
// files. Deleting cookies doesn't clear this cache, which is why a stale
// tab can look broken even after clearing site data -- a hard refresh
// (Ctrl+Shift+R) or "Disable cache" in devtools' Network tab fixes it.
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "no-store";
    }
});

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapCatEndpoints();
app.MapAppointmentEndpoints();
app.MapDetailEndpoints();

app.Run();
