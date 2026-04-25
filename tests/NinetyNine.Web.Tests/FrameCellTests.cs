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

        // Trim() because the .frame-number wrapper now contains an SVG
        // PoolBall whose internal whitespace bleeds into TextContent.
        cut.Find(".frame-number").TextContent.Trim().Should().Be("3");
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
    public void FrameCell_EditMode_ActiveFrame_HasInteractiveBreakAndBallControls()
    {
        // v0.3.3: in-cell Break and Ball buttons replace the wrap-cell Score button.
        var frame = MakeFrame();
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit));

        var breakBtn = cut.Find(".frame-cell-break");
        var ballBtn = cut.Find(".frame-cell-ball");

        breakBtn.HasAttribute("disabled").Should().BeFalse(
            "active frame in Edit mode exposes the Break button as interactive");
        ballBtn.HasAttribute("disabled").Should().BeFalse(
            "active frame in Edit mode exposes the Ball button as interactive");
    }

    [Fact]
    public void FrameCell_ViewMode_BreakAndBallControls_AreDisabled()
    {
        // View mode renders the Break/Ball buttons but they must not be interactive.
        var frame = MakeFrame();
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.View));

        cut.Find(".frame-cell-break").HasAttribute("disabled")
            .Should().BeTrue("View mode disables the Break button");
        cut.Find(".frame-cell-ball").HasAttribute("disabled")
            .Should().BeTrue("View mode disables the Ball button");
    }

    [Fact]
    public void FrameCell_OnBreakToggle_Fires_WhenBreakButtonClicked()
    {
        bool toggled = false;
        var frame = MakeFrame();

        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit)
            .Add(x => x.OnBreakToggle, EventCallback.Factory.Create(this, () => toggled = true)));

        cut.Find(".frame-cell-break").Click();

        toggled.Should().BeTrue("OnBreakToggle must fire when the Break button is tapped");
    }

    [Fact]
    public void FrameCell_OnPickerOpen_Fires_WhenBallButtonClicked_AndPickerClosed()
    {
        bool opened = false;
        var frame = MakeFrame();

        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit)
            .Add(x => x.IsPickerOpen, false)
            .Add(x => x.OnPickerOpen, EventCallback.Factory.Create(this, () => opened = true)));

        cut.Find(".frame-cell-ball").Click();

        opened.Should().BeTrue("clicking Ball when picker is closed must fire OnPickerOpen");
    }

    [Fact]
    public void FrameCell_OnPickerClose_Fires_WhenBallButtonClicked_AndPickerOpen()
    {
        // Toggle behavior: clicking Ball while the picker is open closes it.
        bool closed = false;
        var frame = MakeFrame();

        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit)
            .Add(x => x.IsPickerOpen, true)
            .Add(x => x.OnPickerClose, EventCallback.Factory.Create(this, () => closed = true)));

        cut.Find(".frame-cell-ball").Click();

        closed.Should().BeTrue("clicking Ball when picker is open must fire OnPickerClose");
    }

    [Fact]
    public void FrameCell_PickerOpen_RendersBallPicker()
    {
        var frame = MakeFrame();
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit)
            .Add(x => x.IsPickerOpen, true));

        cut.FindAll(".nn-ball-picker").Should().HaveCount(1,
            "active+edit cell with IsPickerOpen=true must render the BallPicker child");
    }

    [Fact]
    public void FrameCell_PickerClosed_DoesNot_RenderBallPicker()
    {
        var frame = MakeFrame();
        var cut = RenderComponent<FrameCell>(p => p
            .Add(x => x.Frame, frame)
            .Add(x => x.IsActive, true)
            .Add(x => x.Mode, ScoreCardMode.Edit)
            .Add(x => x.IsPickerOpen, false));

        cut.FindAll(".nn-ball-picker").Should().BeEmpty();
    }
}
