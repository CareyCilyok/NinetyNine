using NinetyNine.Model;

namespace NinetyNine.Repository.Repositories;

/// <summary>
/// Data access contract for <see cref="Community"/> documents.
/// <para>See <c>docs/plans/friends-communities-v1.md</c> Sprint 0 S0.4.</para>
/// </summary>
public interface ICommunityRepository
{
    Task<Community?> GetByIdAsync(Guid communityId, CancellationToken ct = default);

    /// <summary>Slug is unique and case-sensitive; used for URL routing.</summary>
    Task<Community?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Case-insensitive name lookup. Returns null if no community has the
    /// given name. Used by the seeder reconcile pass to avoid dupes.
    /// </summary>
    Task<Community?> GetByNameAsync(string name, CancellationToken ct = default);

    /// <summary>Lists communities owned by a pool player. Sorted by name.</summary>
    Task<IReadOnlyList<Community>> ListByOwnerPlayerAsync(Guid ownerPlayerId, CancellationToken ct = default);

    /// <summary>
    /// Case-insensitive prefix search over public communities only.
    /// Private communities are never returned from this method — they are
    /// fully hidden from non-members per the locked fork selection.
    /// </summary>
    Task<IReadOnlyList<Community>> SearchPublicByNameAsync(
        string namePrefix,
        int limit = 20,
        CancellationToken ct = default);

    Task CreateAsync(Community community, CancellationToken ct = default);
    Task UpdateAsync(Community community, CancellationToken ct = default);
    Task DeleteAsync(Guid communityId, CancellationToken ct = default);
}
