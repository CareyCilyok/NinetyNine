using Bunit;
using Microsoft.AspNetCore.Components;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests.Components;

/// <summary>
/// bUnit tests for the <see cref="BallPicker"/> Blazor component — the 3×3
/// popover used by the v2 FrameCell to record which balls were pocketed.
/// The picker is a controlled view: it never owns state, only renders the
/// supplied <c>Selected</c> set and fires events back to the parent.
/// </summary>
public class BallPickerTests : TestContext
{
    // ─── Render shape ─────────────────────────────────────────────────────────

    [Fact]
    public void BallPicker_Renders_NineBallButtons()
    {
        var cut = RenderComponent<BallPicker>();

        // Each ball is a button with class .nn-ball-picker__ball.
        cut.FindAll(".nn-ball-picker__ball")
            .Should().HaveCount(9, "the 3x3 grid renders one button per ball 1-9");
    }

    [Fact]
    public void BallPicker_Renders_ClearAndDoneButtons()
    {
        var cut = RenderComponent<BallPicker>();

        cut.Find(".nn-ball-picker__clear").Should().NotBeNull();
        cut.Find(".nn-ball-picker__done").Should().NotBeNull();
    }

    [Fact]
    public void BallPicker_HasDialogRole_AndAccessibleName()
    {
        var cut = RenderComponent<BallPicker>();

        var root = cut.Find(".nn-ball-picker");
        root.GetAttribute("role").Should().Be("dialog");
        root.GetAttribute("aria-label").Should().Be("Balls pocketed picker");
    }

    [Fact]
    public void BallPicker_BallButton_HasAriaPressed_ReflectingSelection()
    {
        var selected = new HashSet<int> { 3, 7 };

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.Selected, (IReadOnlySet<int>)selected));

        var balls = cut.FindAll(".nn-ball-picker__ball");
        balls[2].GetAttribute("aria-pressed").Should().Be("true",
            "ball 3 is selected");
        balls[6].GetAttribute("aria-pressed").Should().Be("true",
            "ball 7 is selected");
        balls[0].GetAttribute("aria-pressed").Should().Be("false",
            "ball 1 is not selected");
    }

    // ─── Selection styling ────────────────────────────────────────────────────

    [Fact]
    public void BallPicker_SelectedBall_HasSelectedModifier()
    {
        var selected = new HashSet<int> { 5 };

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.Selected, (IReadOnlySet<int>)selected));

        cut.FindAll(".nn-ball-picker__ball--selected")
            .Should().HaveCount(1, "exactly one ball selected → exactly one modifier class applied");
    }

    [Fact]
    public void BallPicker_SelectedBall_RendersCheckBadge()
    {
        var selected = new HashSet<int> { 9 };

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.Selected, (IReadOnlySet<int>)selected));

        cut.FindAll(".nn-ball-picker__check")
            .Should().HaveCount(1, "selected balls get a teal check badge");
    }

    [Fact]
    public void BallPicker_NoSelections_RendersNoCheckBadges()
    {
        var cut = RenderComponent<BallPicker>();

        cut.FindAll(".nn-ball-picker__check").Should().BeEmpty();
    }

    [Fact]
    public void BallPicker_NullSelected_TreatedAs_Empty()
    {
        // Defensive: parent may pass null; the picker must render without throwing.
        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.Selected, (IReadOnlySet<int>?)null));

        cut.FindAll(".nn-ball-picker__ball").Should().HaveCount(9);
        cut.FindAll(".nn-ball-picker__ball--selected").Should().BeEmpty();
    }

    // ─── Event wiring ─────────────────────────────────────────────────────────

    [Fact]
    public void BallPicker_TogglingBall_FiresOnToggle_WithBallNumber()
    {
        int? toggled = null;

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.OnToggle, EventCallback.Factory.Create<int>(this, n => toggled = n)));

        cut.FindAll(".nn-ball-picker__ball")[2].Click();    // ball 3

        toggled.Should().Be(3, "OnToggle must report the tapped ball number");
    }

    [Fact]
    public void BallPicker_ClickingClear_FiresOnClear()
    {
        bool cleared = false;

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.OnClear, EventCallback.Factory.Create(this, () => cleared = true)));

        cut.Find(".nn-ball-picker__clear").Click();

        cleared.Should().BeTrue();
    }

    [Fact]
    public void BallPicker_ClickingDone_FiresOnDone()
    {
        bool done = false;

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.OnDone, EventCallback.Factory.Create(this, () => done = true)));

        cut.Find(".nn-ball-picker__done").Click();

        done.Should().BeTrue();
    }

    [Fact]
    public void BallPicker_TogglingBall_DoesNot_FireOnDone()
    {
        bool done = false;
        int? toggled = null;

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.OnToggle, EventCallback.Factory.Create<int>(this, n => toggled = n))
            .Add(x => x.OnDone,   EventCallback.Factory.Create(this, () => done = true)));

        cut.FindAll(".nn-ball-picker__ball")[4].Click();    // ball 5

        toggled.Should().Be(5);
        done.Should().BeFalse("toggling a ball must not auto-close the picker");
    }

    // ─── Done-button label ────────────────────────────────────────────────────

    [Fact]
    public void BallPicker_DoneLabel_NoBalls_ReadsNoBalls()
    {
        var cut = RenderComponent<BallPicker>();

        cut.Find(".nn-ball-picker__done").TextContent
            .Should().Contain("no balls",
                "Done button reads 'no balls' when nothing is selected");
    }

    [Fact]
    public void BallPicker_DoneLabel_OneBall_IsSingular()
    {
        var selected = new HashSet<int> { 1 };

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.Selected, (IReadOnlySet<int>)selected));

        cut.Find(".nn-ball-picker__done").TextContent
            .Should().Contain("1 ball",
                "Done button uses singular 'ball' when count is 1");
    }

    [Fact]
    public void BallPicker_DoneLabel_MultipleBalls_IsPlural()
    {
        var selected = new HashSet<int> { 2, 5, 7 };

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.Selected, (IReadOnlySet<int>)selected));

        cut.Find(".nn-ball-picker__done").TextContent
            .Should().Contain("3 balls",
                "Done button uses plural 'balls' when count > 1");
    }

    [Fact]
    public void BallPicker_DoneAriaLabel_ReflectsCount()
    {
        var selected = new HashSet<int> { 1, 2 };

        var cut = RenderComponent<BallPicker>(p => p
            .Add(x => x.Selected, (IReadOnlySet<int>)selected));

        cut.Find(".nn-ball-picker__done").GetAttribute("aria-label")
            .Should().Be("Done, 2 balls selected");
    }
}
