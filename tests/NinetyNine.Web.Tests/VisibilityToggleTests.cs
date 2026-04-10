using Bunit;
using Microsoft.AspNetCore.Components;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests;

/// <summary>
/// bUnit tests for the <see cref="VisibilityToggle"/> Blazor component.
/// </summary>
public class VisibilityToggleTests : TestContext
{
    [Fact]
    public void VisibilityToggle_InitialState_MatchesBoundValue_True()
    {
        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "Email")
            .Add(x => x.Value, true));

        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.HasAttribute("checked").Should().BeTrue("initial Value=true should render as checked");
    }

    [Fact]
    public void VisibilityToggle_InitialState_MatchesBoundValue_False()
    {
        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "Phone")
            .Add(x => x.Value, false));

        var checkbox = cut.Find("input[type='checkbox']");
        checkbox.HasAttribute("checked").Should().BeFalse("initial Value=false should render as unchecked");
    }

    [Fact]
    public void VisibilityToggle_ShowsPublicBadge_WhenValueIsTrue()
    {
        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "Avatar")
            .Add(x => x.Value, true));

        cut.Find(".badge").TextContent.Trim().Should().Be("Public");
    }

    [Fact]
    public void VisibilityToggle_ShowsPrivateBadge_WhenValueIsFalse()
    {
        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "Avatar")
            .Add(x => x.Value, false));

        cut.Find(".badge").TextContent.Trim().Should().Be("Private");
    }

    [Fact]
    public void VisibilityToggle_Change_FiresValueChangedCallback()
    {
        bool? received = null;

        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "RealName")
            .Add(x => x.Value, false)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<bool>(this, v => received = v)));

        cut.Find("input[type='checkbox']").Change(true);

        received.Should().NotBeNull("callback should have fired");
        received!.Value.Should().BeTrue("toggling from false to true should fire with true");
    }

    [Fact]
    public void VisibilityToggle_Change_FiresFalse_WhenUnchecked()
    {
        bool? received = null;

        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "Email")
            .Add(x => x.Value, true)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<bool>(this, v => received = v)));

        cut.Find("input[type='checkbox']").Change(false);

        received.Should().NotBeNull();
        received!.Value.Should().BeFalse("unchecking should fire with false");
    }

    [Fact]
    public void VisibilityToggle_LabelIsRendered()
    {
        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "My Custom Label")
            .Add(x => x.Value, false));

        cut.Find("label").TextContent.Should().Contain("My Custom Label");
    }

    [Fact]
    public void VisibilityToggle_InputAndLabelAreLinked()
    {
        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "Email")
            .Add(x => x.Value, false));

        var inputId = cut.Find("input[type='checkbox']").GetAttribute("id");
        var labelFor = cut.Find("label").GetAttribute("for");
        inputId.Should().NotBeNullOrEmpty("input must have an id");
        labelFor.Should().Be(inputId, "label's for attribute must match input id");
    }

    [Fact]
    public void VisibilityToggle_HasSwitchRole()
    {
        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "Email")
            .Add(x => x.Value, false));

        cut.Find("input[role='switch']").Should().NotBeNull("input should have role='switch'");
    }
}
