namespace NinetyNine.Model;

/// <summary>
/// A single vote cast by a player on a <see cref="Poll"/>. Stored in a
/// separate collection with a unique compound index on
/// <c>(PollId, PlayerId)</c> to enforce one-vote-per-player at the
/// database level without transactions.
/// <para>See <c>docs/plans/v2-roadmap.md</c> Sprint 9 S9.1.</para>
/// </summary>
public class Vote
{
    public Guid VoteId { get; set; } = Guid.NewGuid();
    public Guid PollId { get; set; }
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Index into <see cref="Poll.Options"/> that the player chose.
    /// </summary>
    public int OptionIndex { get; set; }

    public DateTime CastAt { get; set; } = DateTime.UtcNow;
}
