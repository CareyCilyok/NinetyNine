using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using NinetyNine.Model;
using NinetyNine.Repository;
using NinetyNine.Services;
using NinetyNine.Web.Auth;
using NinetyNine.Web.Auth.EmailSender;
using NinetyNine.Web.Components;
using ServicesIEmailSender = NinetyNine.Services.Auth.IEmailSender;

// Mock auth toggle: enabled in Development by default; never honored in
// Production regardless of config. See docs/architecture.md for the OAuth
// flow this bypasses during UX prototyping.

// Register BSON class maps before any DI wiring touches MongoDB types.
BsonConfiguration.Register();

var builder = WebApplication.CreateBuilder(args);

// ── Data Protection ───────────────────────────────────────────────────────────
// Persist the Data Protection key ring to a stable directory so auth and
// antiforgery cookies remain decryptable across container rebuilds. Without
// this, every rebuild generates a fresh key ring and all existing browser
// cookies fail to decrypt (manifests as "antiforgery token could not be
// decrypted" errors on form submission). The directory is mounted from a
// named Docker volume in docker-compose.dev.yml. SetApplicationName pins the
// keys to this app even if multiple apps share the same volume.
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? (OperatingSystem.IsWindows() ? @"C:\ninetynine\keys" : "/var/ninetynine/keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("NinetyNine");

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

// ── Email sender factory ──────────────────────────────────────────────────────
// Selects the Services-layer IEmailSender implementation based on Email:Provider config.
// "MailKit" = production SMTP via MailKit adapter;
// "Mock"    = in-memory test accumulator adapter;
// anything else (including "Console" or unset) = structured-log fallback adapter.
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<ServicesIEmailSender>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<EmailSettings>>().Value;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    return settings.Provider?.ToLowerInvariant() switch
    {
        "mailkit" => (ServicesIEmailSender)new MailKitEmailSenderAdapter(
            new MailKitEmailSender(
                sp.GetRequiredService<IOptions<EmailSettings>>(),
                loggerFactory.CreateLogger<MailKitEmailSender>())),
        "mock" => new MockEmailSenderAdapter(new MockEmailSender()),
        _ => new ConsoleEmailSenderAdapter(
            new ConsoleEmailSender(loggerFactory.CreateLogger<ConsoleEmailSender>())),
    };
});

// ── Password hasher (PBKDF2 via ASP.NET Core Identity) ───────────────────────
builder.Services.AddScoped<IPasswordHasher<Player>, PasswordHasher<Player>>();

builder.Services.AddAuthorization();

// ── SignalR (Sprint 8 S8.1) ──────────────────────────────────────────────────
builder.Services.AddSignalR();
builder.Services.AddSingleton<NinetyNine.Web.Hubs.IHubConnectionTracker,
    NinetyNine.Web.Hubs.HubConnectionTracker>();
builder.Services.AddHostedService<NinetyNine.Web.Services.NotificationPollerService>();

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

// ── Startup seeding ─────────────────────────────────────────────────────────
// Seed:Mode controls what gets written:
//   "Production"  — real venues only (default for prod deployments).
//   "Development" — full mock dataset (default in appsettings.Development.json).
// The Auth:Mock:Enabled flag is independent — it gates the mock auth endpoints,
// not the seeder. They were coupled before v0.7.0; now they're separate
// concerns. Setting Seed:Mode=None (or any unknown value) is treated as None
// and the seeder is skipped entirely.
var seedModeRaw = builder.Configuration["Seed:Mode"] ?? "Production";
if (Enum.TryParse<SeedMode>(seedModeRaw, ignoreCase: true, out var seedMode))
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    try
    {
        await seeder.SeedAsync(seedMode);
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex,
            "Data seeder failed in {Mode} mode (continuing). Is MongoDB reachable at the configured connection string?",
            seedMode);
    }
}
else
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(
        "Seed:Mode = '{Mode}' is not Production/Development — seeder skipped.",
        seedModeRaw);
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
// Mock auth is independent of seed mode — gating it requires both
// IsDevelopment() AND the explicit Auth:Mock:Enabled flag in
// appsettings.Development.json. Production deployments never expose these.
var mockAuthEnabled = app.Environment.IsDevelopment()
    && builder.Configuration.GetValue<bool>("Auth:Mock:Enabled");
if (mockAuthEnabled)
{
    MockAuthEndpoints.Map(app);
    app.Logger.LogWarning(
        "Mock auth is ENABLED. Do not run with this configuration in production.");
}

// ── Avatar endpoint ───────────────────────────────────────────────────────────
AvatarEndpoint.Map(app);

// ── SignalR (Sprint 8 S8.1) ───────────────────────────────────────────────────
app.MapHub<NinetyNine.Web.Hubs.NotificationHub>("/hubs/notifications");

// ── Blazor ────────────────────────────────────────────────────────────────────
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
