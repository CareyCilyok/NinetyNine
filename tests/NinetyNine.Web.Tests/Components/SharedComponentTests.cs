using Bunit;
using Microsoft.AspNetCore.Components;
using NinetyNine.Model;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests.Components;

/// <summary>
/// bUnit tests for shared Blazor components in the Wave-4 redesigned markup:
/// PlayerBadge, VisibilityToggle (extra assertions), FrameInputDialog,
/// TableSizePicker, and InitialsAvatar.
/// Tests that would duplicate the exact same assertion already in the root-level
/// VisibilityToggleTests or AvatarImageTests use different assertion angles
/// rather than copy-pasting.
/// </summary>
public class SharedComponentTests : TestContext
{
    // ═══════════════════════════════════════════════════════════════════════════
    // PlayerBadge
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void PlayerBadge_RendersDisplayName()
    {
        var playerId = Guid.NewGuid();

        var cut = RenderComponent<PlayerBadge>(p => p
            .Add(x => x.PlayerId, playerId)
            .Add(x => x.DisplayName, "AliceWonder")
            .Add(x => x.AvatarUrl, (string?)null));

        cut.Find(".player-badge-name").TextContent.Should().Be("AliceWonder");
    }

    [Fact]
    public void PlayerBadge_LinksToPlayerProfile_WithCorrectPlayerId()
    {
        var playerId = Guid.Parse("11111111-0000-0000-0000-000000000000");

        var cut = RenderComponent<PlayerBadge>(p => p
            .Add(x => x.PlayerId, playerId)
            .Add(x => x.DisplayName, "Bob")
            .Add(x => x.AvatarUrl, (string?)null));

        var link = cut.Find("a.player-badge");
        link.GetAttribute("href").Should().Be(
            $"/players/{playerId}",
            "badge must link to the player's profile page");
    }

    [Fact]
    public void PlayerBadge_HasAriaLabel_ContainingDisplayName()
    {
        var cut = RenderComponent<PlayerBadge>(p => p
            .Add(x => x.PlayerId, Guid.NewGuid())
            .Add(x => x.DisplayName, "Carol")
            .Add(x => x.AvatarUrl, (string?)null));

        var link = cut.Find("a[aria-label]");
        link.GetAttribute("aria-label").Should().Contain("Carol",
            "aria-label must identify the player by name");
    }

    [Fact]
    public void PlayerBadge_RendersAvatarImage_WhenUrlProvided()
    {
        var cut = RenderComponent<PlayerBadge>(p => p
            .Add(x => x.PlayerId, Guid.NewGuid())
            .Add(x => x.DisplayName, "Dave")
            .Add(x => x.AvatarUrl, "/api/avatars/dave-id"));

        cut.Find("img").GetAttribute("src").Should().Be("/api/avatars/dave-id",
            "when avatar URL is set, an img element should be rendered");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // VisibilityToggle — additional assertions beyond root-level tests
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void VisibilityToggle_AriaChecked_AttributePresent_WhenValueIsTrue()
    {
        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "Email")
            .Add(x => x.Value, true));

        // Blazor renders a boolean `true` expression as the attribute being present
        // (empty string) rather than the string "True". The attribute must exist.
        cut.Find("input[type='checkbox']")
            .HasAttribute("aria-checked")
            .Should().BeTrue("aria-checked attribute must be present when Value is true");
    }

    [Fact]
    public void VisibilityToggle_AriaChecked_AttributeAbsent_WhenValueIsFalse()
    {
        var cut = RenderComponent<VisibilityToggle>(p => p
            .Add(x => x.Label, "Phone")
            .Add(x => x.Value, false));

        // Blazor omits the attribute entirely when a boolean expression is false,
        // which is the correct ARIA pattern for aria-checked=false (falsy = absent).
        cut.Find("input[type='checkbox']")
            .HasAttribute("aria-checked")
            .Should().BeFalse("aria-checked attribute must be absent when Value is false (Blazor boolean attribute semantics)");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // FrameInputDialog tests deleted in v0.3.4 — the dialog itself was
    // removed when Play.razor moved to the in-cell scoring flow (in-cell
    // Break/Ball + BallPicker + Finish-frame on TurnCalloutCard).

    // ═══════════════════════════════════════════════════════════════════════════
    // TableSizePicker
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TableSizePicker_Renders_FourOptions()
    {
        var cut = RenderComponent<TableSizePicker>(p => p
            .Add(x => x.Value, TableSize.SevenFoot));

        var options = cut.FindAll("[role='radio']");
        options.Should().HaveCount(4,
            "picker must render 4 options: 6ft, 7ft, 9ft, and 10ft");
    }

    [Fact]
    public void TableSizePicker_SelectedOption_HasAriaCheckedTrue()
    {
        var cut = RenderComponent<TableSizePicker>(p => p
            .Add(x => x.Value, TableSize.NineFoot));

        var selected = cut.FindAll("[role='radio']")
            .FirstOrDefault(b => b.GetAttribute("aria-checked") == "true");
        selected.Should().NotBeNull("selected option must have aria-checked='true'");
        selected!.TextContent.Trim().Should().Be("9 ft",
            "the 9 ft option must be marked as selected");
    }

    [Fact]
    public void TableSizePicker_SelectedOption_HasSelectedCssModifier()
    {
        var cut = RenderComponent<TableSizePicker>(p => p
            .Add(x => x.Value, TableSize.TenFoot));

        var selected = cut.FindAll("[role='radio']")
            .FirstOrDefault(b => b.ClassList.Contains("nn-picker__option--selected"));
        selected.Should().NotBeNull("selected option must carry the nn-picker__option--selected class");
        selected!.TextContent.Trim().Should().Be("10 ft");
    }

    [Fact]
    public void TableSizePicker_Clicking_DifferentOption_Fires_ValueChangedCallback()
    {
        TableSize? received = null;

        var cut = RenderComponent<TableSizePicker>(p => p
            .Add(x => x.Value, TableSize.SevenFoot)
            .Add(x => x.ValueChanged, EventCallback.Factory.Create<TableSize>(
                this, v => received = v)));

        // Click the "6 ft" option (which is not currently selected)
        var sixFtButton = cut.FindAll("[role='radio']")
            .First(b => b.TextContent.Trim() == "6 ft");
        sixFtButton.Click();

        received.Should().NotBeNull("ValueChanged callback must fire on option click");
        received!.Value.Should().Be(TableSize.SixFoot,
            "clicking 6 ft must fire with TableSize.SixFoot");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // InitialsAvatar
    // ═══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void InitialsAvatar_SingleWord_RendersFirstChar()
    {
        var cut = RenderComponent<InitialsAvatar>(p => p
            .Add(x => x.DisplayName, "Zara")
            .Add(x => x.SizePx, 40));

        cut.Find("text").TextContent.Should().Be("Z",
            "single-word name should produce the first character as initials");
    }

    [Fact]
    public void InitialsAvatar_TwoWords_RendersFirstCharOfEachWord()
    {
        var cut = RenderComponent<InitialsAvatar>(p => p
            .Add(x => x.DisplayName, "John Doe")
            .Add(x => x.SizePx, 40));

        cut.Find("text").TextContent.Should().Be("JD",
            "two-word name should produce first chars of both words as initials");
    }

    [Fact]
    public void InitialsAvatar_EmptyDisplayName_RendersQuestionMark()
    {
        var cut = RenderComponent<InitialsAvatar>(p => p
            .Add(x => x.DisplayName, "")
            .Add(x => x.SizePx, 32));

        cut.Find("text").TextContent.Should().Be("?",
            "empty display name should render '?' as the fallback initials");
    }

    [Fact]
    public void InitialsAvatar_UppercasesInitials()
    {
        var cut = RenderComponent<InitialsAvatar>(p => p
            .Add(x => x.DisplayName, "alice bob")
            .Add(x => x.SizePx, 40));

        cut.Find("text").TextContent.Should().Be("AB",
            "initials must be uppercased regardless of the source casing");
    }

    [Fact]
    public void InitialsAvatar_SvgDimensionsMatchSizePx()
    {
        var cut = RenderComponent<InitialsAvatar>(p => p
            .Add(x => x.DisplayName, "Test")
            .Add(x => x.SizePx, 56));

        var svg = cut.Find("svg");
        svg.GetAttribute("width").Should().Be("56");
        svg.GetAttribute("height").Should().Be("56");
    }
}
