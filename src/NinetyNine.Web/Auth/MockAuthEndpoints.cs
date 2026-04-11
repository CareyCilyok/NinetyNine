using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;
using NinetyNine.Services;

namespace NinetyNine.Web.Auth;

/// <summary>
/// DEV-ONLY mock auth endpoints for prototyping the UX without wiring real OAuth.
/// Wire via <see cref="Map"/> only when <c>Auth:Mock:Enabled</c> is <c>true</c> AND
/// the environment is Development. Never map in production.
/// </summary>
public static class MockAuthEndpoints
{
    public static void Map(WebApplication app)
    {
        // Sign in as an existing seeded player by displayName or playerId.
        // Example: GET /mock/signin-as?displayName=carey&returnUrl=/games
        // Example: GET /mock/signin-as?playerId={guid}&returnUrl=/games  (backward compat)
        app.MapGet("/mock/signin-as", async (
            HttpContext context,
            string? displayName,
            Guid? playerId,
            string? returnUrl,
            IPlayerRepository playerRepository,
            ILogger<Program> logger) =>
        {
            Player? player;

            if (displayName is not null)
            {
                if (playerId is not null)
                    logger.LogWarning(
                        "Mock signin-as: both displayName and playerId provided; using displayName '{DisplayName}'.",
                        displayName);

                player = await playerRepository.GetByDisplayNameAsync(displayName, context.RequestAborted);
                if (player is null)
                {
                    logger.LogWarning("Mock signin-as: player with displayName '{DisplayName}' not found.", displayName);
                    return Results.NotFound($"Player '{displayName}' not found.");
                }
            }
            else if (playerId is not null)
            {
                player = await playerRepository.GetByIdAsync(playerId.Value, context.RequestAborted);
                if (player is null)
                {
                    logger.LogWarning("Mock signin-as: player {PlayerId} not found.", playerId);
                    return Results.NotFound($"Player {playerId} not found.");
                }
            }
            else
            {
                return Results.BadRequest("Must provide ?displayName=... or ?playerId=...");
            }

            await SignInAsAsync(context, player);
            logger.LogWarning(
                "Mock auth is ENABLED — signed in as {DisplayName} ({PlayerId}).",
                player.DisplayName, player.PlayerId);

            return Results.Redirect(SanitizeReturnUrl(returnUrl) ?? "/");
        })
        .AllowAnonymous();

        // Start the new-user flow: stash a mock provider/sub in the temp cookie and
        // redirect to /register. After successful registration the Register page
        // will redirect back to /mock/signin-as to complete login.
        app.MapGet("/mock/signin-new", (HttpContext context) =>
        {
            var mockSub = $"mock-new-{Guid.NewGuid():N}";

            context.Response.Cookies.Append(
                ClaimNames.TempRegistrationCookie,
                $"{IDataSeeder.MockProvider}|{mockSub}",
                new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = context.Request.IsHttps,
                    MaxAge = TimeSpan.FromMinutes(15),
                    IsEssential = true
                });

            return Results.Redirect("/register");
        })
        .AllowAnonymous();
    }

    /// <summary>
    /// Issues the standard application auth cookie for the given player.
    /// Shared by <c>/mock/signin-as</c> and by <c>Register.razor</c> after
    /// a mock-mode registration completes.
    /// </summary>
    public static async Task SignInAsAsync(HttpContext context, Player player)
    {
        var claims = new List<Claim>
        {
            new(ClaimNames.PlayerId, player.PlayerId.ToString()),
            new(ClaimTypes.Name, player.DisplayName),
            new(ClaimTypes.NameIdentifier, player.PlayerId.ToString())
        };

        if (player.Avatar is not null)
            claims.Add(new Claim(ClaimNames.AvatarUrl, $"/api/avatars/{player.PlayerId}"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
            });
    }

    /// <summary>Sanitizes the return URL to prevent open-redirect attacks.</summary>
    private static string? SanitizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return null;
        // Only allow relative paths that stay on this site
        return Uri.IsWellFormedUriString(returnUrl, UriKind.Relative) && returnUrl.StartsWith('/')
            ? returnUrl
            : null;
    }
}
