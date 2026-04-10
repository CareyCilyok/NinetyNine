using NinetyNine.Model;

namespace NinetyNine.Services.Models;

/// <summary>
/// Partial update payload for a player's profile. Null fields are left unchanged.
/// </summary>
public record PlayerProfileUpdate(
    string? DisplayName,
    string? EmailAddress,
    string? PhoneNumber,
    string? FirstName,
    string? MiddleName,
    string? LastName,
    ProfileVisibility? Visibility);
