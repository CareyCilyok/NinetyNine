namespace NinetyNine.Repository.Storage;

/// <summary>
/// Contract for binary avatar image storage backed by MongoDB GridFS.
/// </summary>
public interface IAvatarStore
{
    /// <summary>
    /// Uploads avatar image content to the store.
    /// </summary>
    /// <param name="content">The image stream to upload.</param>
    /// <param name="contentType">MIME type, e.g. "image/png".</param>
    /// <param name="filename">Suggested filename for the GridFS entry.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The storage key (GridFS ObjectId as string) for later retrieval.</returns>
    Task<string> UploadAsync(Stream content, string contentType, string filename, CancellationToken ct = default);

    /// <summary>
    /// Downloads an avatar image from the store.
    /// </summary>
    /// <param name="storageKey">The GridFS ObjectId string returned by <see cref="UploadAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple of (content stream, content-type), or null if not found.</returns>
    Task<(Stream content, string contentType)?> DownloadAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// Permanently removes an avatar image from the store.
    /// </summary>
    /// <param name="storageKey">The GridFS ObjectId string.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
