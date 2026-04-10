using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using NinetyNine.Services;

namespace NinetyNine.Web.Auth;

/// <summary>
/// Post-OAuth ticket processing — hooked into Google's <see cref="OAuthEvents.OnTicketReceived"/>.
/// If the player exists: augments the principal with PlayerId and DisplayName claims.
/// If the player does not exist: stashes provider info in a short-lived temp cookie and
/// redirects to /register.
/// </summary>
public static class ExternalLoginHandler
{
    /// <summary>
    /// Creates an <see cref="OAuthEvents"/> instance wired up for post-login player resolution.
    /// Assign this to <see cref="Microsoft.AspNetCore.Authentication.Google.GoogleOptions.Events"/>.
    /// </summary>
    public static OAuthEvents CreateOAuthEvents() => new()
    {
        OnTicketReceived = OnTicketReceived
    };

    private static async Task OnTicketReceived(TicketReceivedContext context)
    {
        var playerService = context.HttpContext.RequestServices
            .GetRequiredService<IPlayerService>();
        var logger = context.HttpContext.RequestServices
            .GetRequiredService<ILogger<OAuthEvents>>();

        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("No principal received from OAuth provider.");
            return;
        }

        // The raw sub claim from Google is in NameIdentifier on the Google identity
        var sub = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var provider = context.Scheme.Name; // "Google"

        if (string.IsNullOrEmpty(sub))
        {
            context.Fail("OAuth provider did not return a subject claim.");
            return;
        }

        try
        {
            var player = await playerService.LoginAsync(provider, sub, context.HttpContext.RequestAborted);

            if (player is not null)
            {
                // Augment principal with application-specific claims
                var appIdentity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimNames.PlayerId, player.PlayerId.ToString()),
                    new Claim(ClaimTypes.Name, player.DisplayName),
                    new Claim(ClaimTypes.NameIdentifier, player.PlayerId.ToString())
                });

                if (player.Avatar is not null)
                {
                    appIdentity.AddClaim(new Claim(
                        ClaimNames.AvatarUrl,
                        $"/api/avatars/{player.PlayerId}"));
                }

                // Replace the principal with one that includes app claims
                context.Principal = new ClaimsPrincipal(
                    principal.Identities.Append(appIdentity));

                context.Success();

                logger.LogInformation(
                    "Player {PlayerId} ({DisplayName}) authenticated via {Provider}.",
                    player.PlayerId, player.DisplayName, provider);
            }
            else
            {
                // Unknown player — stash info in temp cookie and redirect to /register
                logger.LogInformation(
                    "No player found for provider={Provider} sub={Sub}; redirecting to /register.",
                    provider, sub);

                context.HttpContext.Response.Cookies.Append(
                    ClaimNames.TempRegistrationCookie,
                    $"{provider}|{sub}",
                    new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Lax,
                        Secure = context.HttpContext.Request.IsHttps,
                        MaxAge = TimeSpan.FromMinutes(15),
                        IsEssential = true
                    });

                context.ReturnUri = "/register";
                context.HandleResponse();
                context.HttpContext.Response.Redirect("/register");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during external login for provider={Provider} sub={Sub}.", provider, sub);
            context.Fail(ex);
        }
    }

    /// <summary>
    /// Reads the temp registration cookie and returns (Provider, ProviderUserId), or null if absent/invalid.
    /// </summary>
    public static (string Provider, string ProviderUserId)? ReadTempRegistrationCookie(
        IRequestCookieCollection cookies)
    {
        if (!cookies.TryGetValue(ClaimNames.TempRegistrationCookie, out var value)
            || string.IsNullOrEmpty(value))
            return null;

        var parts = value.Split('|', 2);
        return parts.Length == 2 ? (parts[0], parts[1]) : null;
    }

    /// <summary>Clears the temp registration cookie from the response.</summary>
    public static void ClearTempRegistrationCookie(HttpResponse response)
    {
        response.Cookies.Delete(ClaimNames.TempRegistrationCookie);
    }
}
