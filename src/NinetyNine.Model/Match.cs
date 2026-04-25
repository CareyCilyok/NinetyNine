namespace NinetyNine.Model;

/// <summary>
/// A head-to-head or group pool match. References <see cref="Game"/>
/// documents by ID rather than embedding them — each constituent game
/// is a complete aggregate with its own frames and scoring state.
/// <para>
/// Two scoring rotations are supported:
/// </para>
/// <list type="bullet">
///   <item><b>Sequential</b> (default; head-to-head): one player plays
///     a complete 9-frame Game, then the next; matches end when a
///     player reaches the win count required by <see cref="Format"/>
///     and <see cref="Target"/>.</item>
///   <item><b>Concurrent</b> (group, 2–4 players): players alternate
///     innings frame-by-frame — seat 0 frame 1 → seat 1 frame 1 → …
///     → seat 0 frame 2 → …, until every Game is complete. The
///     winner is the player with the highest <c>TotalScore</c>
///     across their nine frames (with tie-breakers).</item>
/// </list>
/// <para>
/// Formats applicable to Sequential: single (one game), race to N
/// (first to win N), best of N (first to win ⌈N/2⌉). The schema stores N
/// even for single-game matches (target = 1) so best-of retrofit is a
/// non-migration change. Concurrent matches always run for exactly nine
/// frames per player and ignore <see cref="MatchFormat"/>/<see cref="Target"/>.
/// </para>
/// <para>See <c>docs/plans/v2-roadmap.md</c> Sprint 10 S10.1 (Sequential)
/// and the v0.5.x multi-player plan (Concurrent).</para>
/// </summary>
public class Match
{
    public Guid MatchId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Scoring rotation: Sequential (head-to-head, one Game at a time)
    /// or Concurrent (alternating innings across N Games at once).
    /// Existing rows persisted before v0.5.0 default to Sequential —
    /// the field is positioned so a default-valued enum parse on legacy
    /// documents reads as Sequential without migration.
    /// </summary>
    public MatchType Type { get; set; } = MatchType.Sequential;

    /// <summary>
    /// Structural format: Single (N=1), RaceTo (first to N wins),
    /// BestOf (first to ⌈N/2⌉ wins). Ignored for
    /// <see cref="MatchType.Concurrent"/> — concurrent matches always
    /// run for nine frames per player.
    /// </summary>
    public MatchFormat Format { get; set; } = MatchFormat.Single;

    /// <summary>
    /// For <see cref="MatchFormat.RaceTo"/>, the number of wins
    /// required. For <see cref="MatchFormat.BestOf"/>, the total
    /// possible game count (N). Always 1 for <see cref="MatchFormat.Single"/>.
    /// Ignored for <see cref="MatchType.Concurrent"/>.
    /// </summary>
    public int Target { get; set; } = 1;

    /// <summary>
    /// Participating players, in seating/lag order. Index 0 breaks
    /// the first game (subject to <see cref="BreakMethod"/> override).
    /// Sequential matches require exactly 2 players; Concurrent matches
    /// allow 2–4 (validated by <c>IMatchService</c> at create time).
    /// </summary>
    public List<Guid> PlayerIds { get; set; } = [];

    /// <summary>
    /// For <see cref="MatchType.Concurrent"/>, the seat index (into
    /// <see cref="PlayerIds"/>) that is currently up to shoot. Rotates
    /// 0 → 1 → … → N-1 → 0 on each <c>FinishInning</c>; when it wraps
    /// back to 0 the match advances to the next frame number on each
    /// player's Game. Always 0 for Sequential matches (the active player
    /// is implicitly whoever owns the current Game).
    /// </summary>
    public int CurrentPlayerSeat { get; set; }

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

public enum MatchType
{
    /// <summary>One Game at a time, head-to-head only (2 players).
    /// Pre-v0.5.0 behaviour and the default for new matches.</summary>
    Sequential = 0,

    /// <summary>Alternating-innings concurrent play (2–4 players,
    /// each with their own complete 9-frame Game). Win condition is
    /// highest TotalScore across all players' nine frames.</summary>
    Concurrent = 1,
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
