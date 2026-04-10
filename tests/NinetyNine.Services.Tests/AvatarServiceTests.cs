using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository;
using NinetyNine.Repository.Storage;
using NinetyNine.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace NinetyNine.Services.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class AvatarServiceTests(MongoFixture fixture)
{
    private AvatarService CreateService()
    {
        var ctx = fixture.CreateDbContext();
        var store = new GridFsAvatarStore(ctx, NullLogger<GridFsAvatarStore>.Instance);
        return new AvatarService(store, NullLogger<AvatarService>.Instance);
    }

    private static Player MakePlayer() => new() { PlayerId = Guid.NewGuid(), DisplayName = "AvatarTest" };

    /// <summary>Creates a small valid PNG image stream.</summary>
    private static MemoryStream MakePngStream(int width = 10, int height = 10)
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        ms.Position = 0;
        return ms;
    }

    /// <summary>Creates a small valid JPEG image stream.</summary>
    private static MemoryStream MakeJpegStream()
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(10, 10);
        var ms = new MemoryStream();
        image.Save(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder());
        ms.Position = 0;
        return ms;
    }

    /// <summary>Creates a small valid WebP image stream.</summary>
    private static MemoryStream MakeWebpStream()
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(10, 10);
        var ms = new MemoryStream();
        image.Save(ms, new SixLabors.ImageSharp.Formats.Webp.WebpEncoder());
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task ProcessAndStoreAsync_Accepts_ValidPng()
    {
        var svc = CreateService();
        var player = MakePlayer();
        using var stream = MakePngStream();

        var avatarRef = await svc.ProcessAndStoreAsync(player, stream, "image/png");
        avatarRef.Should().NotBeNull();
        avatarRef.StorageKey.Should().NotBeNullOrEmpty();
        avatarRef.ContentType.Should().Be("image/png");
        avatarRef.WidthPx.Should().BeGreaterThan(0);
        avatarRef.HeightPx.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProcessAndStoreAsync_Accepts_Jpeg()
    {
        var svc = CreateService();
        var player = MakePlayer();
        using var stream = MakeJpegStream();

        var avatarRef = await svc.ProcessAndStoreAsync(player, stream, "image/jpeg");
        avatarRef.Should().NotBeNull();
        avatarRef.ContentType.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task ProcessAndStoreAsync_Accepts_Webp()
    {
        var svc = CreateService();
        var player = MakePlayer();
        using var stream = MakeWebpStream();

        var avatarRef = await svc.ProcessAndStoreAsync(player, stream, "image/webp");
        avatarRef.Should().NotBeNull();
        avatarRef.ContentType.Should().Be("image/webp");
    }

    [Fact]
    public async Task ProcessAndStoreAsync_Rejects_InvalidContentType()
    {
        var svc = CreateService();
        var player = MakePlayer();
        using var stream = new MemoryStream(new byte[] { 0xFF, 0xFE });

        var act = async () => await svc.ProcessAndStoreAsync(player, stream, "application/pdf");
        await act.Should().ThrowAsync<ArgumentException>(
            "unsupported content type must be rejected");
    }

    [Fact]
    public async Task ProcessAndStoreAsync_ResizesOversizedImage()
    {
        var svc = CreateService();
        var player = MakePlayer();
        // Create a 1024×1024 PNG (larger than max 512×512)
        using var stream = MakePngStream(width: 1024, height: 1024);

        var avatarRef = await svc.ProcessAndStoreAsync(player, stream, "image/png");
        avatarRef.WidthPx.Should().BeLessOrEqualTo(512, "image should be resized to max 512px");
        avatarRef.HeightPx.Should().BeLessOrEqualTo(512, "image should be resized to max 512px");
    }

    [Fact]
    public async Task ProcessAndStoreAsync_Rejects_OversizedInput()
    {
        var svc = CreateService();
        var player = MakePlayer();

        // Create a seekable stream larger than 2MB
        var tooLarge = new byte[3 * 1024 * 1024]; // 3MB
        using var stream = new MemoryStream(tooLarge);

        var act = async () => await svc.ProcessAndStoreAsync(player, stream, "image/png");
        await act.Should().ThrowAsync<ArgumentException>(
            "images exceeding 2MB must be rejected before processing");
    }
}
