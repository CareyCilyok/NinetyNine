using Bunit;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests.Components;

/// <summary>
/// bUnit tests for the <see cref="PoolBall"/> Blazor component — the inline
/// SVG renderer for a numbered pool/billiard ball used throughout the v2
/// score-card UI.
/// </summary>
public class PoolBallTests : TestContext
{
    // ─── Basic rendering ──────────────────────────────────────────────────────

    [Fact]
    public void PoolBall_Renders_SvgRoot()
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, 1));

        cut.Find("svg").Should().NotBeNull(
            "PoolBall must render an inline <svg> element");
    }

    [Fact]
    public void PoolBall_DefaultSize_IsThirtySix_Pixels()
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, 1));

        var svg = cut.Find("svg");
        svg.GetAttribute("width").Should().Be("36",
            "default Size parameter must produce width=36");
        svg.GetAttribute("height").Should().Be("36",
            "default Size parameter must produce height=36");
    }

    [Fact]
    public void PoolBall_CustomSize_IsHonored()
    {
        var cut = RenderComponent<PoolBall>(p => p
            .Add(x => x.Number, 3)
            .Add(x => x.Size, 64));

        var svg = cut.Find("svg");
        svg.GetAttribute("width").Should().Be("64");
        svg.GetAttribute("height").Should().Be("64");
    }

    [Fact]
    public void PoolBall_ViewBox_IsAlways_FortyByForty()
    {
        // The internal geometry is sized in viewBox coordinates so the ball
        // scales uniformly regardless of the Size prop.
        var cut = RenderComponent<PoolBall>(p => p
            .Add(x => x.Number, 5)
            .Add(x => x.Size, 128));

        cut.Find("svg").GetAttribute("viewBox").Should().Be("0 0 40 40");
    }

    // ─── Accessibility ────────────────────────────────────────────────────────

    [Fact]
    public void PoolBall_HasAriaLabel_WithBallNumber()
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, 7));

        cut.Find("svg").GetAttribute("aria-label").Should().Be("7 ball",
            "aria-label must announce the ball number");
    }

    [Fact]
    public void PoolBall_RoleIs_Img()
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, 4));

        cut.Find("svg").GetAttribute("role").Should().Be("img",
            "the ball is meaningful imagery and should expose role=img");
    }

    [Fact]
    public void PoolBall_RendersBallNumber_AsTextContent()
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, 6));

        var text = cut.Find("text");
        text.TextContent.Trim().Should().Be("6",
            "the ball number must appear as visible text");
    }

    // ─── Solid vs striped pattern ─────────────────────────────────────────────

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void PoolBall_NumbersOneThroughEight_AreSolid_ByDefault(int number)
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, number));

        // Solid balls use a single shaded circle and no stripe rect.
        cut.FindAll("rect").Should().BeEmpty(
            $"ball {number} is solid and must not render a stripe rect");
    }

    [Fact]
    public void PoolBall_NumberNine_IsStriped_ByDefault()
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, 9));

        // Striped balls render a horizontal band as a <rect> over a white sphere.
        cut.FindAll("rect").Should().HaveCount(1,
            "ball 9 must render a stripe rect band");
    }

    [Fact]
    public void PoolBall_StripedTrueOverride_ForcesStripePattern()
    {
        var cut = RenderComponent<PoolBall>(p => p
            .Add(x => x.Number, 3)
            .Add(x => x.Striped, true));

        cut.FindAll("rect").Should().HaveCount(1,
            "Striped=true must force the stripe band even on a solid ball number");
    }

    [Fact]
    public void PoolBall_StripedFalseOverride_ForcesSolidPattern()
    {
        var cut = RenderComponent<PoolBall>(p => p
            .Add(x => x.Number, 9)
            .Add(x => x.Striped, false));

        cut.FindAll("rect").Should().BeEmpty(
            "Striped=false must force a solid render even on the 9-ball");
    }

    // ─── Color sequence (P&B standard) ────────────────────────────────────────

    [Theory]
    [InlineData(1, "#e5c107")] // yellow
    [InlineData(2, "#0a3a8c")] // blue
    [InlineData(3, "#b8211b")] // red
    [InlineData(4, "#4a1d72")] // purple
    [InlineData(5, "#d66616")] // orange
    [InlineData(6, "#0b6b3a")] // green
    [InlineData(7, "#6b1f18")] // maroon
    [InlineData(8, "#0f0f0f")] // black
    [InlineData(9, "#e5c107")] // yellow (striped)
    public void PoolBall_RendersStandardPbColor_PerBallNumber(int number, string expectedHex)
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, number));

        // The pigment color appears as a stop on the base radial gradient and,
        // for the striped 9-ball, as the stripe rect's fill. We match either
        // appearance via the rendered markup.
        cut.Markup.Should().Contain(expectedHex,
            $"ball {number} must render the standard P&B pigment {expectedHex}");
    }

    // ─── Dim mode ─────────────────────────────────────────────────────────────

    [Fact]
    public void PoolBall_Default_IsNotDim()
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, 1));

        cut.Find("svg").ClassList.Should().NotContain("nn-pool-ball--dim",
            "default render must not carry the dim modifier class");
    }

    [Fact]
    public void PoolBall_DimTrue_AppliesDimModifierClass()
    {
        var cut = RenderComponent<PoolBall>(p => p
            .Add(x => x.Number, 1)
            .Add(x => x.Dim, true));

        cut.Find("svg").ClassList.Should().Contain("nn-pool-ball--dim",
            "Dim=true must add nn-pool-ball--dim so the picker grayscale filter applies");
    }

    [Fact]
    public void PoolBall_AlwaysCarries_BaseClass()
    {
        var cut = RenderComponent<PoolBall>(p => p
            .Add(x => x.Number, 5)
            .Add(x => x.Dim, true));

        cut.Find("svg").ClassList.Should().Contain("nn-pool-ball",
            "the base class must be present whether or not dim is set");
    }

    // ─── Geometry primitives ─────────────────────────────────────────────────

    [Fact]
    public void PoolBall_RendersNumberDisc_AndSpecularHighlight()
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, 2));

        // A solid ball produces three circles: shaded base, white number disc,
        // and the upper-left specular highlight.
        cut.FindAll("circle").Should().HaveCount(3,
            "solid ball must render base + number disc + highlight (3 circles)");
    }

    [Fact]
    public void PoolBall_StripedBall_RendersFourCircles()
    {
        var cut = RenderComponent<PoolBall>(p => p.Add(x => x.Number, 9));

        // Striped: white sphere + shaded overlay circle + number disc + highlight.
        cut.FindAll("circle").Should().HaveCount(4,
            "striped ball renders an extra white sphere beneath the shaded overlay");
    }
}
