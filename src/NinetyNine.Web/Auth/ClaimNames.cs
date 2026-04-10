namespace NinetyNine.Web.Auth;

/// <summary>
/// Application-specific claim type constants used across auth and Blazor components.
/// </summary>
public static class ClaimNames
{
    /// <summary>The player's internal Guid, stored as a string.</summary>
    public const string PlayerId = "player_id";

    /// <summary>Optional avatar URL for the authenticated player.</summary>
    public const string AvatarUrl = "avatar_url";

    /// <summary>Name of the short-lived temp cookie used during registration.</summary>
    public const string TempRegistrationCookie = "NinetyNine.Reg";

    /// <summary>Temp cookie key for the OAuth provider name.</summary>
    public const string TempProvider = "provider";

    /// <summary>Temp cookie key for the OAuth provider user ID.</summary>
    public const string TempProviderUserId = "provider_user_id";
}
