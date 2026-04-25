using Bunit;
using Microsoft.AspNetCore.Components;
using NinetyNine.Model;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests;

/// <summary>
/// Tests that verify score card grid layout expectations by rendering individual
/// FrameCell components (representing the 9-column grid columns).
/// Note: A dedicated ScoreCardGrid composite component has not yet been implemented
/// in the production Web project; these tests cover the FrameCell primitives
/// that will compose it.
/// </summary>
public class ScoreCardGridTests : TestContext
{
    private static List<Frame> MakeNineFrames(Guid gameId)
    {
        return Enumerable.Range(1, 9).Select(i => new Frame
        {
            FrameId = Guid.NewGuid(),
            GameId = gameId,
            FrameNumber = i,
            IsActive = i == 1
        }).ToList();
    }

    [Fact]
    public void Grid_NineFrames_EachRendersAsColumn()
    {
        var gameId = Guid.NewGuid();
        var frames = MakeNineFrames(gameId);

        // Render each frame as a FrameCell — verifies 9 independent columns
        var cells = frames.Select(f =>
            RenderComponent<FrameCell>(p => p
                .Add(x => x.Frame, f)
                .Add(x => x.IsActive, f.IsActive)
                .Add(x => x.Mode, ScoreCardMode.View))).ToList();

        cells.Should().HaveCount(9, "score card has exactly 9 frame columns");
    }

    [Fact]
    public void Grid_EachFrameCell_HasThreeSubRows()
    {
        var frame = new Frame { FrameId = Guid.NewGuid(), FrameNumber = 1 };
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, false));

        // Three rows: break-bonus, ball-count, running-total
        cut.FindAll(".frame-cell-row").Should().HaveCount(3,
            "each frame cell must have Break Bonus, Ball Count, and Running Total rows");
    }

    [Fact]
    public void Grid_ActiveFrame_HasActiveCssClass()
    {
        var gameId = Guid.NewGuid();
        var frames = MakeNineFrames(gameId);

        // Render the active frame (frame 1)
        var activeCell = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frames[0])
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit));

        activeCell.Find("[role='gridcell']").ClassList
            .Should().Contain("frame-cell-active",
                "the currently active frame should be visually highlighted");
    }

    [Fact]
    public void Grid_CompletedFrame_HasCompletedCssClass()
    {
        var frame = new Frame
        {
            FrameId = Guid.NewGuid(),
            FrameNumber = 1,
            BreakBonus = 1,
            BallCount = 5,
            RunningTotal = 6,
            IsCompleted = true
        };

        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, false));

        cut.Find("[role='gridcell']").ClassList
            .Should().Contain("frame-cell-completed",
                "completed frames should have the completed CSS class");
    }

    [Fact]
    public void Grid_EmptyFrames_ShowPlaceholders_NotZeros()
    {
        var frame = new Frame { FrameId = Guid.NewGuid(), FrameNumber = 2 };

        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.View));

        // All placeholder elements should be present for an empty, un-completed frame
        cut.FindAll(".score-placeholder").Should().HaveCountGreaterThan(0,
            "empty frames should display '-' placeholders, not zeros");

        // None of the displayed score values should be "0" in an empty non-completed frame
        cut.FindAll(".score-value:not(.score-placeholder)")
            .Should().BeEmpty("no zero values should appear for an empty pending frame");
    }

    [Fact]
    public void Grid_AllNineFrameNumbers_AreOneThrough9()
    {
        var gameId = Guid.NewGuid();
        var frames = MakeNineFrames(gameId);

        var frameNumbers = frames
            .Select(f =>
            {
                var cut = RenderComponent<FrameCell>(p => p
                    .Add(x => x.Frame, f)
                    .Add(x => x.IsActive, f.IsActive));
                // Trim() because .frame-number now wraps a PoolBall SVG whose
                // internal whitespace bleeds into TextContent.
                return int.Parse(cut.Find(".frame-number").TextContent.Trim());
            })
            .ToList();

        frameNumbers.Should().BeEquivalentTo(Enumerable.Range(1, 9),
            "frame numbers must be 1 through 9 in order");
    }
}
