namespace NinetyNine.Model;

/// <summary>
/// Represents a registered player in the NinetyNine application.
/// </summary>
public class Player
{
    public Guid PlayerId { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";

    // Optional PII — visibility controlled per field
    public string? EmailAddress { get; set; }
    public string? PhoneNumber { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }

    public ProfileVisibility Visibility { get; set; } = new();
    public AvatarRef? Avatar { get; set; }

    public List<LinkedIdentity> LinkedIdentities { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-field visibility flags controlling what other users can see on a player's profile.
/// </summary>
public class ProfileVisibility
{
    /// <summary>Whether the email address is publicly visible. Defaults to hidden.</summary>
    public bool EmailAddress { get; set; } = false;

    /// <summary>Whether the phone number is publicly visible. Defaults to hidden.</summary>
    public bool PhoneNumber { get; set; } = false;

    /// <summary>Controls First/Middle/Last name together. Defaults to hidden.</summary>
    public bool RealName { get; set; } = false;

    /// <summary>Whether the avatar is publicly visible. Defaults to visible.</summary>
    public bool Avatar { get; set; } = true;
}

/// <summary>
/// A third-party OAuth/OIDC identity linked to a player account.
/// </summary>
public class LinkedIdentity
{
    /// <summary>Provider name, e.g. "Google".</summary>
    public string Provider { get; set; } = "";

    /// <summary>The OIDC <c>sub</c> claim value from the provider.</summary>
    public string ProviderUserId { get; set; } = "";

    public DateTime LinkedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Metadata for an uploaded avatar image stored in GridFS.
/// </summary>
public class AvatarRef
{
    /// <summary>GridFS ObjectId serialized as a string.</summary>
    public string StorageKey { get; set; } = "";

    public string ContentType { get; set; } = "";
    public int WidthPx { get; set; }
    public int HeightPx { get; set; }
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
