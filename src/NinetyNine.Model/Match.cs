namespace NinetyNine.Model;

/// <summary>
/// A head-to-head or group pool match. References <see cref="Game"/>
/// documents by ID rather than embedding them — each constituent game
/// is a complete aggregate with its own frames and scoring state.
/// <para>
/// Formats: single (one game), race to N (first to win N), best of N
/// (first to win ⌈N/2⌉). The schema stores N even for single-game
/// matches (target = 1) so best-of retrofit is a non-migration change.
/// </para>
/// <para>See <c>docs/plans/v2-roadmap.md</c> Sprint 10 S10.1.</para>
/// </summary>
public class Match
{
    public Guid MatchId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Structural format: Single (N=1), RaceTo (first to N wins),
    /// BestOf (first to ⌈N/2⌉ wins).
    /// </summary>
    public MatchFormat Format { get; set; } = MatchFormat.Single;

    /// <summary>
    /// For <see cref="MatchFormat.RaceTo"/>, the number of wins
    /// required. For <see cref="MatchFormat.BestOf"/>, the total
    /// possible game count (N). Always 1 for <see cref="MatchFormat.Single"/>.
    /// </summary>
    public int Target { get; set; } = 1;

    /// <summary>
    /// Participating players, in seating/lag order. Index 0 breaks
    /// the first game (subject to <see cref="BreakMethod"/> override).
    /// </summary>
    public List<Guid> PlayerIds { get; set; } = [];

    /// <summary>
    /// Game IDs in play order. Updated as new games are added when
    /// the match continues past game 1.
    /// </summary>
    public List<Guid> GameIds { get; set; } = [];

    /// <summary>
    /// How the first break was decided. Influences nothing beyond
    /// the historical record, but players care about the distinction.
    /// </summary>
    public BreakMethod BreakMethod { get; set; } = BreakMethod.Lagged;

    /// <summary>
    /// Optional table number (league/tournament context).
    /// </summary>
    public int? TableNumber { get; set; }

    /// <summary>
    /// Optional free-text wager or stakes label — for the players'
    /// own tracking. NEVER surfaced on leaderboards or public
    /// profiles. Pool players track this between themselves; the
    /// app respects that without making it part of the public record.
    /// </summary>
    public string? Stakes { get; set; }

    /// <summary>
    /// Venue where the match was played. Required — every match has a table.
    /// </summary>
    public Guid VenueId { get; set; }

    public MatchStatus Status { get; set; } = MatchStatus.Created;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Winning player ID (null while InProgress or Abandoned).
    /// </summary>
    public Guid? WinnerPlayerId { get; set; }
}

public enum MatchFormat
{
    Single = 0,
    RaceTo = 1,
    BestOf = 2,
}

public enum BreakMethod
{
    Lagged = 0,
    CoinFlip = 1,
    MutualAgreement = 2,
    PreviousLoserBreaks = 3,
}

public enum MatchStatus
{
    Created = 0,
    InProgress = 1,
    Completed = 2,
    Abandoned = 3,
}
