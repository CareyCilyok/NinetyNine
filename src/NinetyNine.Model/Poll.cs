namespace NinetyNine.Model;

/// <summary>
/// A time-limited poll for community-level or site-wide decisions.
/// Options are embedded; votes are in a separate collection with a
/// unique compound index enforcing one-vote-per-player.
/// <para>See <c>docs/plans/v2-roadmap.md</c> Sprint 9 S9.1.</para>
/// </summary>
public class Poll
{
    public Guid PollId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Null for site-wide polls (e.g., feature proposals). Set for
    /// community-scoped polls.
    /// </summary>
    public Guid? CommunityId { get; set; }

    public Guid CreatedByPlayerId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public PollType PollType { get; set; } = PollType.Advisory;

    /// <summary>Embedded options. Index aligns with <see cref="Vote.OptionIndex"/>.</summary>
    public List<PollOption> Options { get; set; } = [];

    /// <summary>
    /// Number of eligible voters captured at poll creation time.
    /// For community polls: community member count. For site-wide: all
    /// active (non-retired) players. The quorum denominator does not
    /// shift after creation.
    /// </summary>
    public int EligibleVoterCount { get; set; }

    /// <summary>Fraction of eligible voters required for a binding result. Default 0.5.</summary>
    public double QuorumThreshold { get; set; } = 0.5;

    /// <summary>
    /// Fraction of votes the winning option needs for the result to be
    /// binding. Null = simple majority. 0.667 for member-removal polls
    /// (2/3 supermajority, rounded up).
    /// </summary>
    public double? SupermajorityThreshold { get; set; }

    /// <summary>
    /// When true, individual votes are not attributable to players in
    /// the results. Mandatory for member-targeting polls; creator
    /// choice otherwise (default true).
    /// </summary>
    public bool AnonymousVoting { get; set; } = true;

    public PollStatus Status { get; set; } = PollStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Denormalized result computed on close. Null while the poll is Open.
    /// </summary>
    public PollResult? Result { get; set; }
}

/// <summary>A single option within a <see cref="Poll"/>.</summary>
public class PollOption
{
    public int Index { get; set; }
    public string Label { get; set; } = "";

    /// <summary>
    /// For <see cref="PollType.MemberRemoval"/> polls, the player
    /// being voted on. Null for other poll types.
    /// </summary>
    public Guid? TargetPlayerId { get; set; }
}

/// <summary>
/// Denormalized result embedded in a <see cref="Poll"/> on close.
/// </summary>
public class PollResult
{
    /// <summary>Vote count per option index.</summary>
    public int[] VoteCounts { get; set; } = [];
    public int TotalVotes { get; set; }
    public bool QuorumMet { get; set; }
    public bool ThresholdMet { get; set; }

    /// <summary>Index of the winning option, or null if no winner.</summary>
    public int? WinningOptionIndex { get; set; }
}

public enum PollType
{
    Advisory = 0,
    MemberRemoval = 1,
    FeatureProposal = 2,
}

public enum PollStatus
{
    Open = 0,
    Closed = 1,
    Expired = 2,
}
