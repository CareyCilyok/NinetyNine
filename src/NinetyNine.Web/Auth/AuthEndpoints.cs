using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NinetyNine.Model;
using NinetyNine.Services.Auth;

namespace NinetyNine.Web.Auth;

/// <summary>
/// Minimal API endpoints for email/password authentication:
/// register, login, logout, email verification, password reset, and resend verification.
/// All POST endpoints enforce CSRF protection via anti-forgery tokens.
/// All endpoints are rate-limited via the "auth" policy.
/// </summary>
public static class AuthEndpoints
{
    // ── Request records ───────────────────────────────────────────────────────────

    /// <summary>Request body for the register endpoint.</summary>
    private sealed record RegisterRequest(
        string Email,
        string DisplayName,
        string Password,
        string ConfirmPassword);

    /// <summary>Request body for the login endpoint.</summary>
    private sealed record LoginRequest(string Email, string Password);

    /// <summary>Request body for the resend-verification endpoint.</summary>
    private sealed record ResendVerificationRequest(string Email);

    /// <summary>Request body for the forgot-password endpoint.</summary>
    private sealed record ForgotPasswordRequest(string Email);

    /// <summary>Request body for the reset-password endpoint.</summary>
    private sealed record ResetPasswordRequest(
        string Token,
        string NewPassword,
        string ConfirmPassword);

    // ── Endpoint mapping ──────────────────────────────────────────────────────────

    public static void Map(WebApplication app)
    {
        // POST /api/auth/register — create a new account and send verification email.
        app.MapPost("/api/auth/register", async (
            HttpContext context,
            [FromBody] RegisterRequest request,
            IAuthService authService,
            IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(context);

            var verifyUrlBase = BuildUrlBase(context);
            var result = await authService.RegisterAsync(
                request.Email,
                request.DisplayName,
                request.Password,
                request.ConfirmPassword,
                verifyUrlBase,
                context.RequestAborted);

            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            return Results.Ok(new
            {
                message = "Registration successful. Please check your email to verify your account."
            });
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        // POST /api/auth/login — authenticate with email/password and issue a session cookie.
        app.MapPost("/api/auth/login", async (
            HttpContext context,
            [FromBody] LoginRequest request,
            IAuthService authService,
            IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(context);

            var result = await authService.LoginAsync(
                request.Email,
                request.Password,
                context.RequestAborted);

            if (!result.Success || result.Value is null)
                return Results.Unauthorized();

            await SignInPlayerAsync(context, result.Value);

            return Results.Ok(new { message = "Login successful." });
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        // POST /logout — sign out and clear the session cookie.
        app.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/");
        })
        .RequireAuthorization();

        // GET /verify-email?token=... — verify email address via one-time token.
        app.MapGet("/verify-email", async (
            HttpContext context,
            string? token,
            IAuthService authService) =>
        {
            if (string.IsNullOrWhiteSpace(token))
                return Results.Content(BuildVerifyErrorHtml(
                    "Missing verification token. Please use the link from your email."),
                    "text/html");

            var result = await authService.VerifyEmailAsync(token, context.RequestAborted);

            if (!result.Success)
                return Results.Content(BuildVerifyErrorHtml(result.ErrorMessage ?? "Verification failed."),
                    "text/html");

            return Results.Redirect("/login?verified=1");
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        // POST /api/auth/resend-verification — re-send the email verification link.
        app.MapPost("/api/auth/resend-verification", async (
            HttpContext context,
            [FromBody] ResendVerificationRequest request,
            IAuthService authService,
            IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(context);

            var verifyUrlBase = BuildUrlBase(context);
            await authService.ResendVerificationAsync(
                request.Email,
                verifyUrlBase,
                context.RequestAborted);

            // Always return the same response — no enumeration.
            return Results.Ok(new
            {
                message = "If that email exists and is unverified, a new link has been sent."
            });
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        // POST /api/auth/forgot-password — request a password reset link.
        app.MapPost("/api/auth/forgot-password", async (
            HttpContext context,
            [FromBody] ForgotPasswordRequest request,
            IAuthService authService,
            IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(context);

            var resetUrlBase = BuildUrlBase(context);
            await authService.ForgotPasswordAsync(
                request.Email,
                resetUrlBase,
                context.RequestAborted);

            // Always return the same response — no enumeration.
            return Results.Ok(new
            {
                message = "If that email is registered, a reset link has been sent."
            });
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous();

        // POST /api/auth/reset-password — complete the password reset.
        app.MapPost("/api/auth/reset-password", async (
            HttpContext context,
            [FromBody] ResetPasswordRequest request,
            IAuthService authService,
            IAntiforgery antiforgery) =>
        {
            await antiforgery.ValidateRequestAsync(context);

            var result = await authService.ResetPasswordAsync(
                request.Token,
                request.NewPassword,
                request.ConfirmPassword,
                context.RequestAborted);

            if (!result.Success)
                return Results.BadRequest(new { error = result.ErrorMessage });

            return Results.Ok(new { message = "Password reset successful. Please sign in." });
        })
        .RequireRateLimiting("auth")
        .AllowAnonymous();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Issues the standard NinetyNine session cookie for the given player.
    /// Mirrors <see cref="MockAuthEndpoints.SignInAsAsync"/> to keep claim schemas consistent.
    /// </summary>
    internal static async Task SignInPlayerAsync(HttpContext context, Player player)
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

    /// <summary>
    /// Builds the absolute URL base (scheme + host + path-base) for the current request.
    /// Passed to <see cref="IAuthService"/> methods that need to construct absolute links.
    /// </summary>
    private static string BuildUrlBase(HttpContext context)
    {
        var request = context.Request;
        var pathBase = request.PathBase.HasValue ? request.PathBase.Value : "";
        return $"{request.Scheme}://{request.Host}{pathBase}";
    }

    /// <summary>
    /// Builds a minimal HTML error page shown when email verification fails.
    /// Includes a link to request a new verification email.
    /// </summary>
    private static string BuildVerifyErrorHtml(string message)
    {
        var safeMessage = System.Net.WebUtility.HtmlEncode(message);
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width,initial-scale=1">
              <title>Email Verification — NinetyNine</title>
              <style>
                body { font-family: system-ui, sans-serif; background: #1a1a2e; color: #e0e0e0;
                        display: flex; align-items: center; justify-content: center; min-height: 100vh; margin: 0; }
                .card { background: #16213e; border-radius: 8px; padding: 40px; max-width: 480px; text-align: center; }
                h1 { font-size: 1.4rem; margin-bottom: 1rem; }
                p { color: #c0c0c0; line-height: 1.6; }
                a { color: #7eb8f7; }
              </style>
            </head>
            <body>
              <div class="card">
                <h1>Verification failed</h1>
                <p>{{safeMessage}}</p>
                <p><a href="/resend-verification">Request a new verification link</a></p>
              </div>
            </body>
            </html>
            """;
    }
}
