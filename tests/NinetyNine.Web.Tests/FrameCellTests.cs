using Bunit;
using Microsoft.AspNetCore.Components;
using NinetyNine.Model;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests;

/// <summary>
/// bUnit tests for the <see cref="FrameCell"/> Blazor component.
/// </summary>
public class FrameCellTests : TestContext
{
    private static Frame MakeFrame(int number = 1) => new Frame
    {
        FrameId = Guid.NewGuid(),
        GameId = Guid.NewGuid(),
        FrameNumber = number
    };

    [Fact]
    public void FrameCell_Renders_FrameNumber()
    {
        var frame = MakeFrame(3);
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, false));

        cut.Find(".frame-number").TextContent.Should().Be("3");
    }

    [Fact]
    public void FrameCell_EmptyState_ShowsPlaceholders_NotZeros()
    {
        // Active, not completed frame should show "-" placeholders
        var frame = MakeFrame();
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.View));

        // Break bonus and ball count should show placeholder, not "0"
        var placeholders = cut.FindAll(".score-placeholder");
        placeholders.Should().HaveCount(3, "all three rows should show placeholder when frame is empty and not completed");
    }

    [Fact]
    public void FrameCell_Completed_ShowsActualScores()
    {
        var frame = new Frame
        {
            FrameId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            FrameNumber = 2,
            BreakBonus = 1,
            BallCount = 7,
            RunningTotal = 22,
            IsCompleted = true
        };

        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, false));

        var values = cut.FindAll(".score-value").Where(e => !e.ClassList.Contains("score-placeholder")).ToList();
        values.Any(v => v.TextContent == "1").Should().BeTrue("break bonus should be shown");
        values.Any(v => v.TextContent == "7").Should().BeTrue("ball count should be shown");
        values.Any(v => v.TextContent == "22").Should().BeTrue("running total should be shown");
    }

    [Fact]
    public void FrameCell_ActiveEditMode_HasActiveCssClass()
    {
        var frame = MakeFrame();
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit));

        cut.Find("[role='gridcell']").ClassList
            .Should().Contain("frame-cell-active");
    }

    [Fact]
    public void FrameCell_CompletedFrame_HasCompletedCssClass()
    {
        var frame = new Frame
        {
            FrameId = Guid.NewGuid(),
            FrameNumber = 1,
            IsCompleted = true,
            BreakBonus = 0,
            BallCount = 5,
            RunningTotal = 5
        };

        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, false));

        cut.Find("[role='gridcell']").ClassList
            .Should().Contain("frame-cell-completed");
    }

    [Fact]
    public void FrameCell_AriaLabel_IncludesFrameNumber_ForPendingFrame()
    {
        var frame = MakeFrame(5);
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, false));

        var ariaLabel = cut.Find("[role='gridcell']").GetAttribute("aria-label");
        ariaLabel.Should().Contain("5", "aria-label should include the frame number");
    }

    [Fact]
    public void FrameCell_AriaLabel_IncludesScores_ForCompletedFrame()
    {
        var frame = new Frame
        {
            FrameId = Guid.NewGuid(),
            FrameNumber = 4,
            BreakBonus = 1,
            BallCount = 9,
            RunningTotal = 35,
            IsCompleted = true
        };

        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, false));

        var ariaLabel = cut.Find("[role='gridcell']").GetAttribute("aria-label");
        ariaLabel.Should().Contain("4");
        ariaLabel.Should().Contain("1");  // break bonus
        ariaLabel.Should().Contain("9");  // ball count
        ariaLabel.Should().Contain("35"); // running total
    }

    [Fact]
    public void FrameCell_EditMode_ActiveFrame_ShowsScoreButton()
    {
        var frame = MakeFrame();
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit));

        cut.Find("button").Should().NotBeNull("active frame in Edit mode should have a Score button");
    }

    [Fact]
    public void FrameCell_ViewMode_NoScoreButton()
    {
        var frame = MakeFrame();
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.View));

        cut.FindAll("button").Should().BeEmpty("View mode should not show a Score button");
    }

    [Fact]
    public void FrameCell_OnFrameActivated_Fires_WhenScoreButtonClicked()
    {
        Frame? activatedFrame = null;
        var frame = MakeFrame();

        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit)
            .Add(x => x.OnFrameActivated, EventCallback.Factory.Create<Frame>(
                this, f => activatedFrame = f)));

        cut.Find("button").Click();
        activatedFrame.Should().NotBeNull();
        activatedFrame!.FrameNumber.Should().Be(frame.FrameNumber);
    }
}
