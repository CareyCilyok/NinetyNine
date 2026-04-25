namespace NinetyNine.Model;

/// <summary>
/// Represents a single frame (rack) within a NinetyNine game.
/// A game consists of exactly 9 frames. Under the v2 ruleset (effective
/// v0.3.0), maximum frame score is 10 points (1 break bonus + 9 balls
/// pocketed at 1 point each). Legacy frames recorded under the v1 rule
/// (max 11) remain in storage unchanged; this validator does not retro-
/// actively mark them invalid for read paths, but no new frame can be
/// recorded with BallCount &gt; 9.
/// </summary>
public class Frame
{
    public Guid FrameId { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }

    /// <summary>Frame number within the game, 1–9.</summary>
    public int FrameNumber { get; set; }

    /// <summary>
    /// Break bonus awarded when at least one ball is legally pocketed on the break.
    /// Value is 0 or 1. Not awarded on a scratch break even if balls are pocketed.
    /// </summary>
    public int BreakBonus { get; set; }

    /// <summary>
    /// Object balls legally pocketed in this frame, scoring 1 point each.
    /// Range: 0–9 under the v2 rule (max-10-per-frame ceiling).
    /// </summary>
    public int BallCount { get; set; }

    /// <summary>Cumulative game score through and including this frame.</summary>
    public int RunningTotal { get; set; }

    public bool IsCompleted { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }

    // ── Computed properties (BsonIgnored in BSON class map) ──────────────────

    /// <summary>BreakBonus + BallCount. Maximum 10 under the v2 rule.</summary>
    public int FrameScore => BreakBonus + BallCount;

    /// <summary>True when FrameScore does not exceed 10.</summary>
    public bool IsValidScore => FrameScore <= 10;

    /// <summary>True when the player achieved the maximum score of 10 for this frame.</summary>
    public bool IsPerfectFrame => FrameScore == 10;

    // ── Behavior ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates all invariants for this frame under the v2 ruleset.
    /// </summary>
    /// <returns><c>true</c> if all invariants hold; otherwise <c>false</c>.</returns>
    public bool ValidateFrame()
    {
        if (BreakBonus is not (0 or 1))
            return false;
        if (BallCount < 0 || BallCount > 9)
            return false;
        if (FrameScore > 10)
            return false;
        if (FrameNumber < 1 || FrameNumber > 9)
            return false;
        return true;
    }

    /// <summary>
    /// Marks this frame as completed and calculates the running total.
    /// </summary>
    /// <param name="previousRunningTotal">The running total at the end of the preceding frame.</param>
    /// <exception cref="InvalidOperationException">Thrown when the frame contains invalid scores.</exception>
    public void CompleteFrame(int previousRunningTotal = 0)
    {
        if (!ValidateFrame())
            throw new InvalidOperationException(
                $"Frame {FrameNumber} has invalid scores: BreakBonus={BreakBonus}, BallCount={BallCount}.");

        RunningTotal = previousRunningTotal + FrameScore;
        IsCompleted = true;
        IsActive = false;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Resets this frame to its initial state, clearing all score data.
    /// </summary>
    public void ResetFrame()
    {
        BreakBonus = 0;
        BallCount = 0;
        RunningTotal = 0;
        IsCompleted = false;
        IsActive = false;
        CompletedAt = null;
        Notes = null;
    }
}
