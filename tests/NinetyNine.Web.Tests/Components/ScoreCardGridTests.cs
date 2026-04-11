using Bunit;
using Microsoft.AspNetCore.Components;
using NinetyNine.Model;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests.Components;

/// <summary>
/// bUnit tests for the post-Wave-4 redesigned <see cref="ScoreCardGrid"/> composite component.
/// The existing root-level ScoreCardGridTests covers FrameCell primitives in isolation;
/// these tests exercise ScoreCardGrid as an assembled whole.
/// Class prefix "Redesigned_" on shared method names avoids xUnit class-collision warnings
/// even though each class is in its own namespace.
/// </summary>
public class Redesigned_ScoreCardGridTests : TestContext
{
    // ─── Test-data helpers ───────────────────────────────────────────────────

    private static Game MakeGame(int completedCount = 0, bool isCompleted = false)
    {
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            GameState = isCompleted ? GameState.Completed : GameState.InProgress
        };

        // Build 9 frames manually so we control IsCompleted / IsActive exactly.
        for (int i = 1; i <= 9; i++)
        {
            int runningTotal = completedCount >= i ? (i * 7) : 0; // arbitrary valid totals
            game.Frames.Add(new Frame
            {
                FrameId = Guid.NewGuid(),
                GameId = game.GameId,
                FrameNumber = i,
                IsCompleted = completedCount >= i,
                IsActive = !isCompleted && (i == completedCount + 1),
                BreakBonus = completedCount >= i ? 1 : 0,
                BallCount = completedCount >= i ? 6 : 0,
                RunningTotal = runningTotal
            });
        }

        return game;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Grid column count
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Redesigned_Grid_Renders_Exactly_Nine_FrameCells()
    {
        var game = MakeGame(completedCount: 0);
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, game)
            .Add(x => x.Mode, ScoreCardMode.View));

        // Each FrameCell renders a [role='gridcell'] element
        var cells = cut.FindAll("[role='gridcell']");
        cells.Should().HaveCount(9, "score card always has exactly 9 frame columns");
    }

    [Fact]
    public void Redesigned_Grid_NullGame_DoesNotRenderGridCells()
    {
        // When Game is null, EnsureFrames() returns empty — no FrameCells emitted
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, (Game?)null)
            .Add(x => x.Mode, ScoreCardMode.View));

        cut.FindAll("[role='gridcell']").Should().BeEmpty(
            "null game should not render any frame cells");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CSS class modifiers per frame state
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Redesigned_Grid_ActiveFrame_HasActiveCssClass()
    {
        // Frame 1 is active (completedCount == 0 -> frame 1 IsActive)
        var game = MakeGame(completedCount: 0);
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, game)
            .Add(x => x.Mode, ScoreCardMode.Edit));

        // The active FrameCell should carry frame-cell-active
        var cells = cut.FindAll("[role='gridcell']");
        cells.Any(c => c.ClassList.Contains("frame-cell-active"))
            .Should().BeTrue("the active frame must carry the frame-cell-active CSS modifier");
    }

    [Fact]
    public void Redesigned_Grid_CompletedFrames_HaveCompletedCssClass()
    {
        var game = MakeGame(completedCount: 3);
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, game)
            .Add(x => x.Mode, ScoreCardMode.View));

        var cells = cut.FindAll("[role='gridcell']");
        var completedCells = cells.Where(c => c.ClassList.Contains("frame-cell-completed")).ToList();
        completedCells.Should().HaveCount(3, "exactly 3 completed frames should have the completed CSS modifier");
    }

    [Fact]
    public void Redesigned_Grid_PendingFrames_DoNotHaveActiveOrCompletedCssClass()
    {
        // With completedCount=2, frames 3-9 are pending (not active, not completed)
        var game = MakeGame(completedCount: 2);
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, game)
            .Add(x => x.Mode, ScoreCardMode.View));

        var cells = cut.FindAll("[role='gridcell']");
        // Frames 4-9 (index 3-8) should have neither class
        for (int i = 3; i <= 8; i++)
        {
            cells[i].ClassList.Should().NotContain("frame-cell-active",
                $"frame {i + 1} is pending and must not be marked active");
            cells[i].ClassList.Should().NotContain("frame-cell-completed",
                $"frame {i + 1} is pending and must not be marked completed");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // View vs Edit mode
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Redesigned_Grid_ViewMode_ActiveFrame_HasNoScoreButton()
    {
        var game = MakeGame(completedCount: 0); // frame 1 is active
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, game)
            .Add(x => x.Mode, ScoreCardMode.View));

        // In the desktop grid area (.sc-grid), the FrameCell for frame 1 must not have a button
        var gridSection = cut.Find(".sc-grid");
        gridSection.QuerySelectorAll("button").Should().BeEmpty(
            "View mode must not expose score buttons inside the desktop grid cells");
    }

    [Fact]
    public void Redesigned_Grid_EditMode_ActiveFrame_HasScoreButton()
    {
        var game = MakeGame(completedCount: 0); // frame 1 is active
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, game)
            .Add(x => x.Mode, ScoreCardMode.Edit));

        // The desktop grid's active FrameCell should have a button
        var gridSection = cut.Find(".sc-grid");
        gridSection.QuerySelectorAll("button").Should().NotBeEmpty(
            "Edit mode active frame must expose a score button inside the desktop grid");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Summary strip
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Redesigned_Grid_SummaryStrip_IsRendered_WhenGameIsNotNull()
    {
        var game = MakeGame(completedCount: 4);
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, game)
            .Add(x => x.Mode, ScoreCardMode.View));

        cut.Find(".sc-summary").Should().NotBeNull("summary strip must render when a game is provided");
    }

    [Fact]
    public void Redesigned_Grid_SummaryStrip_IsNotRendered_WhenGameIsNull()
    {
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, (Game?)null)
            .Add(x => x.Mode, ScoreCardMode.View));

        cut.FindAll(".sc-summary").Should().BeEmpty("summary strip must not render when game is null");
    }

    [Fact]
    public void Redesigned_Grid_SummaryStrip_ShowsCompletedFrameCount()
    {
        var game = MakeGame(completedCount: 5);
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, game)
            .Add(x => x.Mode, ScoreCardMode.View));

        // The summary shows "5 / 9" for completed count
        var summary = cut.Find(".sc-summary");
        summary.TextContent.Should().Contain("5", "completed frame count must appear in summary");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Frame number badges
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Redesigned_Grid_FrameNumberBadges_AreOneThrough9_InOrder()
    {
        var game = MakeGame(completedCount: 0);
        var cut = RenderComponent<ScoreCardGrid>(p => p
            .Add(x => x.Game, game)
            .Add(x => x.Mode, ScoreCardMode.View));

        // .frame-number badges from the desktop FrameCells (inside .sc-grid)
        var frameNumbers = cut.Find(".sc-grid")
            .QuerySelectorAll(".frame-number")
            .Select(el => el.TextContent.Trim())
            .ToList();

        frameNumbers.Should().HaveCount(9, "there must be exactly 9 frame-number badges");
        frameNumbers.Should().BeEquivalentTo(
            Enumerable.Range(1, 9).Select(n => n.ToString()),
            opts => opts.WithStrictOrdering(),
            "frame numbers must appear 1 through 9 in order");
    }
}
