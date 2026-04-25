using NinetyNine.Model;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests;

/// <summary>
/// Unit tests for <see cref="FrameStatus"/> and its
/// <see cref="FrameStatusExtensions.GetStatus(Frame, int?)"/> classifier.
/// </summary>
public class FrameStatusTests
{
    private static Frame MakeFrame(
        int number = 1,
        bool isActive = false,
        bool isCompleted = false) => new Frame
        {
            FrameId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            FrameNumber = number,
            IsActive = isActive,
            IsCompleted = isCompleted
        };

    // ─── Completed wins ───────────────────────────────────────────────────────

    [Fact]
    public void GetStatus_CompletedFrame_ReturnsCompleted()
    {
        var frame = MakeFrame(isCompleted: true);

        frame.GetStatus(currentFrameNumber: 5).Should().Be(FrameStatus.Completed);
    }

    [Fact]
    public void GetStatus_CompletedAndActive_ReturnsCompleted()
    {
        // Defensive: if the model ever ends up in this contradictory state,
        // Completed wins so a "done" frame never re-renders as the live one.
        var frame = MakeFrame(isActive: true, isCompleted: true);

        frame.GetStatus(currentFrameNumber: 1).Should().Be(FrameStatus.Completed,
            "a completed frame must never present as Active");
    }

    [Fact]
    public void GetStatus_CompletedFrame_NoCurrentFrameNumber_ReturnsCompleted()
    {
        var frame = MakeFrame(isCompleted: true);

        frame.GetStatus().Should().Be(FrameStatus.Completed,
            "Completed classification does not depend on currentFrameNumber");
    }

    // ─── Active beats Pending/Future ──────────────────────────────────────────

    [Fact]
    public void GetStatus_ActiveFrame_ReturnsActive()
    {
        var frame = MakeFrame(number: 3, isActive: true);

        frame.GetStatus(currentFrameNumber: 3).Should().Be(FrameStatus.Active);
    }

    [Fact]
    public void GetStatus_ActiveFrame_NoCurrentFrameNumber_ReturnsActive()
    {
        var frame = MakeFrame(isActive: true);

        frame.GetStatus().Should().Be(FrameStatus.Active);
    }

    // ─── Pending vs Future split (with currentFrameNumber) ────────────────────

    [Fact]
    public void GetStatus_NextUpFrame_ReturnsPending()
    {
        // Frame N+1 when current is N → next up → Pending.
        var frame = MakeFrame(number: 4);

        frame.GetStatus(currentFrameNumber: 3).Should().Be(FrameStatus.Pending,
            "frame at currentFrameNumber+1 is the next-up Pending slot");
    }

    [Fact]
    public void GetStatus_FrameAtCurrent_NotActive_ReturnsPending()
    {
        // Frame whose number == current but IsActive=false (transient state)
        // should still classify as Pending, not Future.
        var frame = MakeFrame(number: 5);

        frame.GetStatus(currentFrameNumber: 5).Should().Be(FrameStatus.Pending);
    }

    [Fact]
    public void GetStatus_FrameTwoOut_ReturnsFuture()
    {
        // Frame N+2 when current is N → Future.
        var frame = MakeFrame(number: 6);

        frame.GetStatus(currentFrameNumber: 4).Should().Be(FrameStatus.Future,
            "frame at currentFrameNumber+2 is in the future");
    }

    [Fact]
    public void GetStatus_LastFrame_AtGameStart_ReturnsFuture()
    {
        // Game at frame 1; frame 9 is not active, not completed, way out → Future.
        var frame = MakeFrame(number: 9);

        frame.GetStatus(currentFrameNumber: 1).Should().Be(FrameStatus.Future);
    }

    [Fact]
    public void GetStatus_Frame1AtCurrent1_NotActive_ReturnsPending()
    {
        // Pre-initialized state: game indexed at frame 1 but no IsActive set yet.
        var frame = MakeFrame(number: 1);

        frame.GetStatus(currentFrameNumber: 1).Should().Be(FrameStatus.Pending);
    }

    [Fact]
    public void GetStatus_LowerNumberedFrame_NotCompleted_ReturnsPending()
    {
        // Defensive: a "skipped" frame (lower than current, not completed) is
        // still classified as Pending — not Future. Keeps the rule monotonic.
        var frame = MakeFrame(number: 2);

        frame.GetStatus(currentFrameNumber: 5).Should().Be(FrameStatus.Pending,
            "lower-numbered uncompleted frames stay Pending, not Future");
    }

    // ─── Pending without currentFrameNumber (FrameCell call site) ─────────────

    [Fact]
    public void GetStatus_NoCurrentFrameNumber_NotPlayedFrame_ReturnsPending()
    {
        var frame = MakeFrame(number: 7);

        frame.GetStatus().Should().Be(FrameStatus.Pending,
            "without currentFrameNumber, every unplayed frame collapses to Pending");
    }

    [Fact]
    public void GetStatus_NoCurrentFrameNumber_NeverReturnsFuture()
    {
        // The 3-value FrameCell-side classification must never produce Future,
        // since it has no reference point against which to decide that.
        for (int n = 1; n <= 9; n++)
        {
            var frame = MakeFrame(number: n);
            frame.GetStatus().Should().NotBe(FrameStatus.Future,
                $"frame {n} without currentFrameNumber must not classify as Future");
        }
    }

    // ─── Argument validation ──────────────────────────────────────────────────

    [Fact]
    public void GetStatus_NullFrame_Throws()
    {
        Frame? frame = null;

        var act = () => frame!.GetStatus(currentFrameNumber: 1);

        act.Should().Throw<ArgumentNullException>();
    }

    // ─── Enum value invariants ────────────────────────────────────────────────

    [Fact]
    public void FrameStatus_HasExactlyFourValues()
    {
        Enum.GetValues<FrameStatus>().Should().HaveCount(4,
            "headroom for differentiating Pending (next-up) from Future (further out)");
    }

    [Fact]
    public void FrameStatus_ContainsAllExpectedNames()
    {
        var names = Enum.GetNames<FrameStatus>();

        names.Should().BeEquivalentTo(new[] { "Pending", "Active", "Completed", "Future" });
    }
}
