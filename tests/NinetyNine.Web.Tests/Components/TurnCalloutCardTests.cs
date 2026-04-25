using Bunit;
using Microsoft.AspNetCore.Components;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests.Components;

/// <summary>
/// bUnit tests for the <see cref="TurnCalloutCard"/> Blazor component — the
/// gold-tinted "your turn" header that sits atop the v2 Play screen.
/// </summary>
public class TurnCalloutCardTests : TestContext
{
    // ─── Render shape ─────────────────────────────────────────────────────────

    [Fact]
    public void TurnCallout_RendersEyebrow_HeadlineAndButton()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1));

        cut.Find(".nn-turn-callout__eyebrow").Should().NotBeNull();
        cut.Find(".nn-turn-callout__headline").Should().NotBeNull();
        cut.Find(".nn-turn-callout__finish").Should().NotBeNull();
    }

    [Fact]
    public void TurnCallout_HeadlineReadsFrameNOf9()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 4));

        cut.Find(".nn-turn-callout__headline").TextContent.Trim()
            .Should().Be("Frame 4 of 9");
    }

    [Fact]
    public void TurnCallout_FinishButton_IncludesFrameNumber()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 7));

        cut.Find(".nn-turn-callout__finish").TextContent
            .Should().Contain("Finish frame 7");
    }

    [Fact]
    public void TurnCallout_FinishButton_HasAriaLabelWithFrameNumber()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 5));

        cut.Find(".nn-turn-callout__finish").GetAttribute("aria-label")
            .Should().Be("Finish frame 5");
    }

    [Fact]
    public void TurnCallout_DefaultEyebrowReadsYourTurn()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1));

        cut.Find(".nn-turn-callout__eyebrow").TextContent.Trim()
            .Should().Be("Your turn");
    }

    [Fact]
    public void TurnCallout_EyebrowOverride_IsHonored()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1)
            .Add(x => x.Eyebrow, "Carey's inning"));

        cut.Find(".nn-turn-callout__eyebrow").TextContent.Trim()
            .Should().Be("Carey's inning");
    }

    // ─── Ball pair ────────────────────────────────────────────────────────────

    [Fact]
    public void TurnCallout_RendersBothInningBallAndNineBall_ForFrames1Through8()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 3));

        var balls = cut.Find(".nn-turn-callout__balls");
        balls.QuerySelectorAll("svg").Should().HaveCount(2,
            "frames 1-8 render the inning ball + the 9-ball");
    }

    [Fact]
    public void TurnCallout_OnFrame9_RendersOnlyOneBall()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 9));

        var balls = cut.Find(".nn-turn-callout__balls");
        balls.QuerySelectorAll("svg").Should().HaveCount(1,
            "frame 9 is itself the 9-ball — no duplicate");
    }

    // ─── Helper text ──────────────────────────────────────────────────────────

    [Fact]
    public void TurnCallout_DefaultHelperText_ExplainsBreakAndBall()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1));

        var helper = cut.Find(".nn-turn-callout__helper").TextContent;
        helper.Should().Contain("Break");
        helper.Should().Contain("Ball");
    }

    [Fact]
    public void TurnCallout_HelperText_CanBeSuppressed()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1)
            .Add(x => x.HelperText, (string?)null));

        cut.FindAll(".nn-turn-callout__helper").Should().BeEmpty(
            "null helper text suppresses the paragraph");
    }

    // ─── CanFinish gating ─────────────────────────────────────────────────────

    [Fact]
    public void TurnCallout_DefaultCanFinish_IsEnabled()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1));

        cut.Find(".nn-turn-callout__finish").HasAttribute("disabled")
            .Should().BeFalse("default (null) CanFinish leaves the button enabled");
    }

    [Fact]
    public void TurnCallout_CanFinishFalse_DisablesButton()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1)
            .Add(x => x.CanFinish, false));

        cut.Find(".nn-turn-callout__finish").HasAttribute("disabled")
            .Should().BeTrue();
    }

    [Fact]
    public void TurnCallout_CanFinishTrue_EnablesButton()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1)
            .Add(x => x.CanFinish, true));

        cut.Find(".nn-turn-callout__finish").HasAttribute("disabled")
            .Should().BeFalse();
    }

    // ─── Event wiring ─────────────────────────────────────────────────────────

    [Fact]
    public void TurnCallout_ClickingFinish_FiresOnFinishFrame()
    {
        bool fired = false;

        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1)
            .Add(x => x.OnFinishFrame, EventCallback.Factory.Create(this, () => fired = true)));

        cut.Find(".nn-turn-callout__finish").Click();

        fired.Should().BeTrue();
    }

    [Fact]
    public void TurnCallout_ClickingFinish_WhenDisabled_DoesNotFire()
    {
        bool fired = false;

        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1)
            .Add(x => x.CanFinish, false)
            .Add(x => x.OnFinishFrame, EventCallback.Factory.Create(this, () => fired = true)));

        // Even if a stray click somehow lands on a disabled button, the handler
        // must reject it. (bUnit's Click() bypasses native disabled gating.)
        cut.Find(".nn-turn-callout__finish").Click();

        fired.Should().BeFalse(
            "disabled button must not propagate the finish event");
    }

    // ─── Accessibility ────────────────────────────────────────────────────────

    [Fact]
    public void TurnCallout_HasRegionRole_AndAccessibleName()
    {
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 2));

        var section = cut.Find(".nn-turn-callout");
        section.GetAttribute("role").Should().Be("region");
        section.GetAttribute("aria-label").Should().Be("Frame 2 turn callout");
    }

    [Fact]
    public void TurnCallout_BallPair_IsAriaHidden()
    {
        // The headline + button already announce the frame; the ball pair is
        // decorative and must not double-announce to screen readers.
        var cut = RenderComponent<TurnCalloutCard>(p => p
            .Add(x => x.FrameNumber, 1));

        cut.Find(".nn-turn-callout__balls").GetAttribute("aria-hidden")
            .Should().Be("true");
    }
}
