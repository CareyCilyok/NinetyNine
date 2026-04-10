using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;

namespace NinetyNine.Web.Auth;

/// <summary>
/// Minimal API endpoints for authentication: login challenge and logout.
/// </summary>
public static class AuthEndpoints
{
    public static void Map(WebApplication app)
    {
        // Challenge the specified OAuth provider.
        // Example: GET /login?provider=Google&returnUrl=/games
        app.MapGet("/login", async (HttpContext context, string? provider, string? returnUrl) =>
        {
            provider ??= "Google";
            var redirectUri = returnUrl ?? "/";

            var properties = new AuthenticationProperties
            {
                RedirectUri = redirectUri
            };

            await context.ChallengeAsync(provider, properties);
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        // Sign the user out and redirect to the home page.
        app.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        })
        .RequireAuthorization();
    }
}
