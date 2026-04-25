using NinetyNine.Model;

namespace NinetyNine.Web.Components.Shared;

/// <summary>
/// Visual classification of a frame within the score card. Drives the
/// per-state styling in <see cref="FrameCell"/> and <see cref="ScoreCardGrid"/>
/// — never communicate state by color alone; this enum is the second signal
/// (border style, badge fill, opacity, etc.).
/// </summary>
/// <remarks>
/// Four values, ordered roughly chronologically:
/// <list type="bullet">
///   <item><description><see cref="Pending"/> — not yet played, but next-up
///     (or status calculation lacks a current-frame reference).</description></item>
///   <item><description><see cref="Active"/> — the frame currently being scored.</description></item>
///   <item><description><see cref="Completed"/> — score recorded.</description></item>
///   <item><description><see cref="Future"/> — not yet played and several frames
///     out from the active one. Visually de-emphasized vs <c>Pending</c>.</description></item>
/// </list>
/// </remarks>
public enum FrameStatus
{
    Pending = 0,
    Active = 1,
    Completed = 2,
    Future = 3
}

/// <summary>
/// Extension helpers for deriving a <see cref="FrameStatus"/> from a
/// <see cref="Frame"/>. Replaces the if/else chain previously embedded in
/// <c>FrameCell.CellCssClass</c>.
/// </summary>
public static class FrameStatusExtensions
{
    /// <summary>
    /// Classify a frame's visual state.
    /// </summary>
    /// <param name="frame">The frame to classify.</param>
    /// <param name="currentFrameNumber">
    /// The game's <c>CurrentFrameNumber</c>. Pass it to differentiate
    /// <see cref="FrameStatus.Pending"/> (next-up) from
    /// <see cref="FrameStatus.Future"/> (further out). Omit it (the FrameCell
    /// case — that component has no access to the parent <c>Game</c>) to get a
    /// three-value classification where everything unplayed collapses to
    /// <c>Pending</c>.
    /// </param>
    /// <remarks>
    /// Resolution order is fixed and defensive:
    /// <list type="number">
    ///   <item><description><c>IsCompleted</c> wins over everything else
    ///     (a completed frame should never re-render as Active).</description></item>
    ///   <item><description><c>IsActive</c> next.</description></item>
    ///   <item><description>Otherwise: <c>FrameNumber &gt; currentFrameNumber + 1</c>
    ///     ⇒ <c>Future</c>; everything else ⇒ <c>Pending</c>.</description></item>
    /// </list>
    /// </remarks>
    public static FrameStatus GetStatus(this Frame frame, int? currentFrameNumber = null)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.IsCompleted) return FrameStatus.Completed;
        if (frame.IsActive) return FrameStatus.Active;

        if (currentFrameNumber is null) return FrameStatus.Pending;

        return frame.FrameNumber > currentFrameNumber.Value + 1
            ? FrameStatus.Future
            : FrameStatus.Pending;
    }
}
