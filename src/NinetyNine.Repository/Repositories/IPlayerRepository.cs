using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for <see cref="Player"/> documents.
/// </summary>
public interface IPlayerRepository
{
    /// <summary>Look up a player by email (case-insensitive). Returns null if not found.</summary>
    Task<Player?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>Look up a player by their current email verification token. Returns null if not found.</summary>
    /// <remarks>Caller is responsible for expiry checks.</remarks>
    Task<Player?> GetByEmailVerificationTokenAsync(string token, CancellationToken ct = default);

    /// <summary>Look up a player by their current password reset token. Returns null if not found.</summary>
    /// <remarks>Caller is responsible for expiry checks.</remarks>
    Task<Player?> GetByPasswordResetTokenAsync(string token, CancellationToken ct = default);

    /// <summary>True if a player with the given email already exists (case-insensitive).</summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    Task<Player?> GetByIdAsync(Guid playerId, CancellationToken ct = default);
    Task<Player?> GetByDisplayNameAsync(string displayName, CancellationToken ct = default);
    Task<bool> DisplayNameExistsAsync(string displayName, CancellationToken ct = default);
    Task<IReadOnlyList<Player>> SearchAsync(string query, int limit, CancellationToken ct = default);
    Task CreateAsync(Player player, CancellationToken ct = default);
    Task UpdateAsync(Player player, CancellationToken ct = default);
    Task DeleteAsync(Guid playerId, CancellationToken ct = default);
}
