using Bunit;
using NinetyNine.Web.Components.Shared;

namespace NinetyNine.Web.Tests;

/// <summary>
/// bUnit tests for the <see cref="AvatarImage"/> Blazor component.
/// </summary>
public class AvatarImageTests : TestContext
{
    [Fact]
    public void AvatarImage_RendersImg_WhenAvatarUrlSupplied()
    {
        var cut = RenderComponent<AvatarImage>(p => p
            .Add(x => x.AvatarUrl, "/api/avatars/some-id")
            .Add(x => x.DisplayName, "Alice")
            .Add(x => x.SizePx, 48));

        var img = cut.Find("img");
        img.Should().NotBeNull();
        img.GetAttribute("src").Should().Be("/api/avatars/some-id");
        img.GetAttribute("alt").Should().Contain("Alice");
    }

    [Fact]
    public void AvatarImage_RendersInitialsFallback_WhenAvatarUrlIsNull()
    {
        var cut = RenderComponent<AvatarImage>(p => p
            .Add(x => x.AvatarUrl, (string?)null)
            .Add(x => x.DisplayName, "Bob")
            .Add(x => x.SizePx, 40));

        // Should render InitialsAvatar (SVG), not an img tag
        cut.FindAll("img").Should().BeEmpty("no img element when no avatar URL");
        cut.Find("svg").Should().NotBeNull("SVG initials avatar should be rendered");
    }

    [Fact]
    public void AvatarImage_RendersInitialsFallback_WhenAvatarUrlIsEmpty()
    {
        var cut = RenderComponent<AvatarImage>(p => p
            .Add(x => x.AvatarUrl, "")
            .Add(x => x.DisplayName, "Carol")
            .Add(x => x.SizePx, 40));

        cut.FindAll("img").Should().BeEmpty();
        cut.Find("svg").Should().NotBeNull();
    }

    [Fact]
    public void AvatarImage_Initials_FromSingleWordDisplayName()
    {
        var cut = RenderComponent<AvatarImage>(p => p
            .Add(x => x.AvatarUrl, (string?)null)
            .Add(x => x.DisplayName, "Alice"));

        var text = cut.Find("text").TextContent;
        text.Should().Be("A", "single-word name → first char of that word");
    }

    [Fact]
    public void AvatarImage_Initials_FromTwoWordDisplayName()
    {
        var cut = RenderComponent<AvatarImage>(p => p
            .Add(x => x.AvatarUrl, (string?)null)
            .Add(x => x.DisplayName, "John Doe"));

        var text = cut.Find("text").TextContent;
        text.Should().Be("JD", "two-word name → first chars of each word");
    }

    [Fact]
    public void AvatarImage_Initials_QuestionMark_WhenDisplayNameEmpty()
    {
        var cut = RenderComponent<AvatarImage>(p => p
            .Add(x => x.AvatarUrl, (string?)null)
            .Add(x => x.DisplayName, ""));

        var text = cut.Find("text").TextContent;
        text.Should().Be("?", "empty display name → '?' placeholder");
    }

    [Fact]
    public void AvatarImage_ContainerHasCorrectDimensions()
    {
        var cut = RenderComponent<AvatarImage>(p => p
            .Add(x => x.AvatarUrl, "/api/avatars/x")
            .Add(x => x.DisplayName, "Test")
            .Add(x => x.SizePx, 64));

        var container = cut.Find(".avatar-container");
        var style = container.GetAttribute("style");
        style.Should().Contain("64px");
    }

    [Fact]
    public void AvatarImage_ImgHasCorrectWidthAndHeight()
    {
        var cut = RenderComponent<AvatarImage>(p => p
            .Add(x => x.AvatarUrl, "/api/avatars/player-1")
            .Add(x => x.DisplayName, "Dave")
            .Add(x => x.SizePx, 56));

        var img = cut.Find("img");
        img.GetAttribute("width").Should().Be("56");
        img.GetAttribute("height").Should().Be("56");
    }
}
