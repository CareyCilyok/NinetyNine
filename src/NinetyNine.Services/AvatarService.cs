using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Storage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace NinetyNine.Services;

/// <summary>
/// Validates, resizes, and stores player avatar images.
/// Accepts PNG, JPEG, and WebP; resizes to at most 512×512 pixels preserving aspect ratio.
/// </summary>
public sealed class AvatarService(
    IAvatarStore avatarStore,
    ILogger<AvatarService> logger)
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/webp"
    };

    private const long MaxUploadBytes = 2 * 1024 * 1024; // 2 MB
    private const int MaxDimensionPx = 512;

    /// <summary>
    /// Processes and stores an avatar for the given player.
    /// </summary>
    /// <param name="player">The player whose avatar is being updated.</param>
    /// <param name="imageContent">Raw image stream.</param>
    /// <param name="contentType">MIME type of the incoming image.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="AvatarRef"/> describing the stored image.</returns>
    /// <exception cref="ArgumentException">Thrown for invalid content type or oversized input.</exception>
    public async Task<AvatarRef> ProcessAndStoreAsync(
        Player player, Stream imageContent, string contentType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(imageContent);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        if (!AllowedContentTypes.Contains(contentType))
            throw new ArgumentException(
                $"Content type '{contentType}' is not allowed. Accepted types: {string.Join(", ", AllowedContentTypes)}",
                nameof(contentType));

        if (imageContent.CanSeek && imageContent.Length > MaxUploadBytes)
            throw new ArgumentException(
                $"Image exceeds maximum allowed size of {MaxUploadBytes / 1024 / 1024} MB.",
                nameof(imageContent));

        logger.LogDebug(
            "Processing avatar upload for player {PlayerId}, content type {ContentType}",
            player.PlayerId, contentType);

        // Load and resize with ImageSharp
        using var image = await Image.LoadAsync(imageContent, ct);

        if (image.Width > MaxDimensionPx || image.Height > MaxDimensionPx)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(MaxDimensionPx, MaxDimensionPx),
                Mode = ResizeMode.Max
            }));
        }

        await using var outputStream = new MemoryStream();
        await image.SaveAsync(outputStream, GetEncoder(contentType), ct);
        outputStream.Position = 0;

        string filename = $"avatar_{player.PlayerId:N}{GetExtension(contentType)}";
        string storageKey = await avatarStore.UploadAsync(outputStream, contentType, filename, ct);

        var avatarRef = new AvatarRef
        {
            StorageKey = storageKey,
            ContentType = contentType,
            WidthPx = image.Width,
            HeightPx = image.Height,
            SizeBytes = outputStream.Length,
            UploadedAt = DateTime.UtcNow
        };

        logger.LogInformation(
            "Avatar stored for player {PlayerId}: {Width}x{Height} {ContentType}, key={StorageKey}",
            player.PlayerId, image.Width, image.Height, contentType, storageKey);

        return avatarRef;
    }

    private static SixLabors.ImageSharp.Formats.IImageEncoder GetEncoder(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/png" => new SixLabors.ImageSharp.Formats.Png.PngEncoder(),
            "image/jpeg" => new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder(),
            "image/webp" => new SixLabors.ImageSharp.Formats.Webp.WebpEncoder(),
            _ => throw new ArgumentException($"No encoder for content type '{contentType}'.")
        };

    private static string GetExtension(string contentType) =>
        contentType.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".bin"
        };
}
