using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace NinetyNine.Repository.Storage;

/// <summary>
/// GridFS-backed implementation of <see cref="IAvatarStore"/>.
/// Uses the default bucket (<c>fs.files</c> / <c>fs.chunks</c>).
/// </summary>
public sealed class GridFsAvatarStore(INinetyNineDbContext context, ILogger<GridFsAvatarStore> logger)
    : IAvatarStore
{
    private readonly GridFSBucket _bucket = new(context.Database);

    /// <inheritdoc/>
    public async Task<string> UploadAsync(
        Stream content, string contentType, string filename, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(filename);

        var options = new GridFSUploadOptions
        {
            Metadata = new BsonDocument("contentType", contentType)
        };

        logger.LogDebug("Uploading avatar '{Filename}' ({ContentType}) to GridFS", filename, contentType);
        var objectId = await _bucket.UploadFromStreamAsync(filename, content, options, ct);
        return objectId.ToString();
    }

    /// <inheritdoc/>
    public async Task<(Stream content, string contentType)?> DownloadAsync(
        string storageKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        if (!ObjectId.TryParse(storageKey, out var objectId))
        {
            logger.LogWarning("Invalid storage key format: {StorageKey}", storageKey);
            return null;
        }

        try
        {
            var fileInfo = await _bucket.Find(
                Builders<GridFSFileInfo>.Filter.Eq(f => f.Id, objectId))
                .FirstOrDefaultAsync(ct);

            if (fileInfo is null)
            {
                logger.LogDebug("Avatar not found for storage key {StorageKey}", storageKey);
                return null;
            }

            string contentType = fileInfo.Metadata.TryGetValue("contentType", out var ct_value)
                ? ct_value.AsString
                : "application/octet-stream";

            var stream = await _bucket.OpenDownloadStreamAsync(objectId, cancellationToken: ct);
            return (stream, contentType);
        }
        catch (GridFSFileNotFoundException)
        {
            logger.LogDebug("Avatar GridFS file not found for key {StorageKey}", storageKey);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        if (!ObjectId.TryParse(storageKey, out var objectId))
        {
            logger.LogWarning("Cannot delete avatar: invalid storage key format '{StorageKey}'", storageKey);
            return;
        }

        logger.LogInformation("Deleting avatar with storage key {StorageKey}", storageKey);

        try
        {
            await _bucket.DeleteAsync(objectId, ct);
        }
        catch (GridFSFileNotFoundException)
        {
            logger.LogDebug("Avatar {StorageKey} already deleted or not found", storageKey);
        }
    }
}
