using NinetyNine.Model;

namespace NinetyNine.Services;

/// <summary>
/// Manages poll lifecycle: creation, voting, closing, and results.
/// Enforces all invariants from the v2 roadmap Sprint 9 S9.3:
/// quorum, supermajority, authz, one-vote-per-player, bandwagon
/// prevention, and binding auto-execution for member removal.
/// </summary>
public interface IPollService
{
    Task<ServiceResult<Poll>> CreatePollAsync(
        Guid? communityId,
        Guid createdByPlayerId,
        string title,
        string? description,
        PollType pollType,
        List<PollOption> options,
        int durationDays,
        bool? anonymousVoting = null,
        CancellationToken ct = default);

    Task<ServiceResult> CastVoteAsync(
        Guid pollId,
        Guid playerId,
        int optionIndex,
        CancellationToken ct = default);

    /// <summary>
    /// Closes a poll, computing results. Called by the expiration sweep
    /// or manually by Owner/Admin.
    /// </summary>
    Task<ServiceResult<PollResult>> ClosePollAsync(
        Guid pollId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the poll with results. Results are null if the viewer
    /// hasn't voted yet (bandwagon prevention).
    /// </summary>
    Task<Poll?> GetPollForViewerAsync(
        Guid pollId,
        Guid? viewerId,
        CancellationToken ct = default);

    Task<IReadOnlyList<Poll>> ListCommunityPollsAsync(
        Guid communityId,
        PollStatus? status = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<Poll>> ListSiteWidePollsAsync(
        PollStatus? status = null,
        CancellationToken ct = default);
}
