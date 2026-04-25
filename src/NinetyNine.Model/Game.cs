namespace NinetyNine.Model;

/// <summary>Lifecycle state of a NinetyNine game.</summary>
public enum GameState
{
    NotStarted,
    InProgress,
    Completed,
    Paused
}

/// <summary>Billiard table size in feet.</summary>
public enum TableSize
{
    Unknown = 0,
    SixFoot = 6,
    SevenFoot = 7,
    NineFoot = 9,
    TenFoot = 10
}

/// <summary>
/// Aggregate root for a single-player NinetyNine game.
/// A game consists of exactly 9 frames; maximum total score is 99.
/// All game behavior methods enforce invariants and throw on violations.
/// </summary>
public class Game
{
    public Guid GameId { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
    public Guid VenueId { get; set; }
    public DateTime WhenPlayed { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public TableSize TableSize { get; set; } = TableSize.Unknown;
    public GameState GameState { get; set; } = GameState.NotStarted;
    public int CurrentFrameNumber { get; set; } = 1;
    public List<Frame> Frames { get; set; } = new(9);
    public string? Notes { get; set; }

    /// <summary>
    /// True when this game is being scored under "Efren" variant rules:
    /// the player runs out from where the cue ball stops after the break,
    /// with no ball-in-hand placement. Named for Efren Reyes. Score math
    /// is unchanged from the standard rule (max 11 per frame, max 99 per
    /// game); this flag drives the visual indicator on frame tiles and
    /// later filters in stats. The narrow interpretation applies — only
    /// post-break placement is removed; mid-frame foul recovery follows
    /// the normal rule.
    /// </summary>
    public bool IsEfrenVariant { get; set; }

    // ── Computed properties (BsonIgnored in BSON class map) ──────────────────

    /// <summary>Sum of FrameScore for all completed frames.</summary>
    public int TotalScore => Frames.Where(f => f.IsCompleted).Sum(f => f.FrameScore);

    /// <summary>Running total from the last completed frame, or 0 if none.</summary>
    public int RunningTotal => Frames.LastOrDefault(f => f.IsCompleted)?.RunningTotal ?? 0;

    /// <summary>True when the game is currently in progress.</summary>
    public bool IsInProgress => GameState == GameState.InProgress;

    /// <summary>True when the game has been completed.</summary>
    public bool IsCompleted => GameState == GameState.Completed;

    /// <summary>Count of frames that have been completed.</summary>
    public int CompletedFrames => Frames.Count(f => f.IsCompleted);

    /// <summary>The currently active frame, or null if none is active.</summary>
    public Frame? CurrentFrame => Frames.FirstOrDefault(f => f.IsActive);

    /// <summary>Average score per completed frame, or 0 if no frames completed.</summary>
    public double AverageScore => CompletedFrames > 0 ? (double)TotalScore / CompletedFrames : 0;

    /// <summary>The highest-scoring completed frame, or null if none.</summary>
    public Frame? BestFrame => Frames
        .Where(f => f.IsCompleted)
        .OrderByDescending(f => f.FrameScore)
        .FirstOrDefault();

    /// <summary>Count of completed frames with the maximum score of 11.</summary>
    public int PerfectFrames => Frames.Count(f => f.IsPerfectFrame);

    /// <summary>True when the game is completed with a total score of 99.</summary>
    public bool IsPerfectGame => IsCompleted && TotalScore == 99;

    // ── Behavior ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates and attaches exactly 9 <see cref="Frame"/> objects, activating the first one.
    /// Transitions the game state to <see cref="GameState.InProgress"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the game is not in <see cref="GameState.NotStarted"/> state.</exception>
    public void InitializeFrames()
    {
        if (GameState != GameState.NotStarted)
            throw new InvalidOperationException(
                $"Cannot initialize frames: game is in state {GameState}.");

        Frames.Clear();
        for (int i = 1; i <= 9; i++)
        {
            Frames.Add(new Frame
            {
                FrameId = Guid.NewGuid(),
                GameId = GameId,
                FrameNumber = i,
                IsActive = i == 1
            });
        }

        CurrentFrameNumber = 1;
        GameState = GameState.InProgress;
    }

    /// <summary>
    /// Activates the frame immediately following the most recently completed frame.
    /// Finalizes the game when all 9 frames are complete.
    /// </summary>
    /// <returns>
    /// <c>true</c> if there is a next frame and it was activated;
    /// <c>false</c> if all 9 frames are complete (game is finalized as a side effect).
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the game is not in progress or no frame has been completed yet.</exception>
    public bool AdvanceToNextFrame()
    {
        if (GameState != GameState.InProgress)
            throw new InvalidOperationException(
                $"Cannot advance frame: game is in state {GameState}.");

        var lastCompleted = Frames
            .Where(f => f.IsCompleted)
            .OrderByDescending(f => f.FrameNumber)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No completed frame to advance from.");

        int nextFrameNumber = lastCompleted.FrameNumber + 1;
        if (nextFrameNumber > 9)
        {
            GameState = GameState.Completed;
            CompletedAt ??= DateTime.UtcNow;
            return false;
        }

        // Defensive: deactivate any stray active frames (no-op if contract honored)
        foreach (var f in Frames.Where(f => f.IsActive))
            f.IsActive = false;

        var nextFrame = Frames.FirstOrDefault(f => f.FrameNumber == nextFrameNumber)
            ?? throw new InvalidOperationException($"Frame {nextFrameNumber} not found.");

        nextFrame.IsActive = true;
        CurrentFrameNumber = nextFrameNumber;
        return true;
    }

    /// <summary>
    /// Records the score for the current active frame, marks it complete, and
    /// auto-advances to the next frame. When the completed frame is frame 9,
    /// transitions the game to <see cref="GameState.Completed"/>.
    /// </summary>
    /// <param name="breakBonus">0 or 1.</param>
    /// <param name="ballCount">0–10.</param>
    /// <param name="notes">Optional per-frame notes.</param>
    /// <exception cref="InvalidOperationException">Thrown when the game is not in progress or scores are invalid.</exception>
    public void CompleteCurrentFrame(int breakBonus, int ballCount, string? notes = null)
    {
        if (GameState != GameState.InProgress)
            throw new InvalidOperationException(
                $"Cannot record frame: game is in state {GameState}.");

        var frame = Frames.FirstOrDefault(f => f.IsActive)
            ?? throw new InvalidOperationException("No active frame found.");

        frame.BreakBonus = breakBonus;
        frame.BallCount = ballCount;
        frame.Notes = notes;

        int previousRunningTotal = Frames
            .Where(f => f.IsCompleted)
            .OrderByDescending(f => f.FrameNumber)
            .FirstOrDefault()?.RunningTotal ?? 0;

        // CompleteFrame validates, sets IsCompleted=true, IsActive=false, RunningTotal
        frame.CompleteFrame(previousRunningTotal);

        // Auto-advance or finalize the game
        if (frame.FrameNumber >= 9)
        {
            GameState = GameState.Completed;
            CompletedAt ??= DateTime.UtcNow;
        }
        else
        {
            var nextFrame = Frames.First(f => f.FrameNumber == frame.FrameNumber + 1);
            nextFrame.IsActive = true;
            CurrentFrameNumber = nextFrame.FrameNumber;
        }
    }

    /// <summary>
    /// Validates the overall game state and all frames for consistency.
    /// </summary>
    /// <returns><c>true</c> if the game is valid; otherwise <c>false</c>.</returns>
    public bool ValidateGame()
    {
        if (Frames.Count != 9)
            return false;

        int activeCount = Frames.Count(f => f.IsActive);
        if (activeCount > 1)
            return false;

        foreach (var frame in Frames)
        {
            if (frame.IsCompleted && !frame.ValidateFrame())
                return false;
        }

        // Running totals must be monotonically non-decreasing
        int previous = 0;
        foreach (var frame in Frames.Where(f => f.IsCompleted).OrderBy(f => f.FrameNumber))
        {
            if (frame.RunningTotal < previous)
                return false;
            previous = frame.RunningTotal;
        }

        return true;
    }
}
