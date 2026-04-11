using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using NinetyNine.Repository;
using NinetyNine.Services;
using NinetyNine.Web.Auth;
using NinetyNine.Web.Components;

// Mock auth toggle: enabled in Development by default; never honored in
// Production regardless of config. See docs/architecture.md for the OAuth
// flow this bypasses during UX prototyping.

// Register BSON class maps before any DI wiring touches MongoDB types.
BsonConfiguration.Register();

var builder = WebApplication.CreateBuilder(args);

// ── Razor / Blazor ────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── HTTP context accessor (for reading claims in Blazor server pages) ─────────
builder.Services.AddHttpContextAccessor();

// ── Cascading auth state ─────────────────────────────────────────────────────
builder.Services.AddCascadingAuthenticationState();

// ── Data + Domain ─────────────────────────────────────────────────────────────
builder.Services.AddNinetyNineRepository(builder.Configuration);
builder.Services.AddNinetyNineServices();

// ── Health checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongo");

// ── Authentication ────────────────────────────────────────────────────────────
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "NinetyNine.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
    // TODO(WP-05): wire password auth

builder.Services.AddAuthorization();

// ── Anti-forgery ──────────────────────────────────────────────────────────────
builder.Services.AddAntiforgery();

// ── Rate limiting ─────────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── HTTP logging (Development only) ──────────────────────────────────────────
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpLogging(_ => { });
}

var app = builder.Build();

// ── Mock-mode startup: seed test data ────────────────────────────────────────
var mockAuthEnabled = app.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Auth:Mock:Enabled");
if (mockAuthEnabled)
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    try
    {
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex,
            "Data seeder failed (continuing). Is MongoDB reachable at the dev connection string?");
    }
}

// ── Security headers ──────────────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data:; style-src 'self' 'unsafe-inline';";
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpLogging();
}

app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// ── Health check ──────────────────────────────────────────────────────────────
app.MapHealthChecks("/healthz");

// ── Auth endpoints ────────────────────────────────────────────────────────────
AuthEndpoints.Map(app);
if (mockAuthEnabled)
{
    MockAuthEndpoints.Map(app);
    app.Logger.LogWarning(
        "Mock auth is ENABLED. Do not run with this configuration in production.");
}

// ── Avatar endpoint ───────────────────────────────────────────────────────────
AvatarEndpoint.Map(app);

// ── Blazor ────────────────────────────────────────────────────────────────────
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
