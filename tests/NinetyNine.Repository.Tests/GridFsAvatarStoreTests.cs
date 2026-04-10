using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Repository;
using NinetyNine.Repository.Storage;

namespace NinetyNine.Repository.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class GridFsAvatarStoreTests(MongoFixture fixture)
{
    private IAvatarStore CreateStore()
    {
        var ctx = fixture.CreateDbContext();
        return new GridFsAvatarStore(ctx, NullLogger<GridFsAvatarStore>.Instance);
    }

    private static byte[] MakePngBytes()
    {
        // Minimal valid PNG: 1×1 transparent pixel
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    }

    [Fact]
    public async Task Upload_ReturnsNonEmptyStorageKey()
    {
        var store = CreateStore();
        using var stream = new MemoryStream(MakePngBytes());
        var key = await store.UploadAsync(stream, "image/png", "test-avatar.png");
        key.Should().NotBeNullOrEmpty("upload should return a GridFS ObjectId string");
    }

    [Fact]
    public async Task Upload_ThenDownload_ReturnsOriginalBytesAndContentType()
    {
        var store = CreateStore();
        var original = MakePngBytes();
        using var uploadStream = new MemoryStream(original);
        var key = await store.UploadAsync(uploadStream, "image/png", "round-trip.png");

        var result = await store.DownloadAsync(key);
        result.Should().NotBeNull("file was just uploaded");

        var (contentStream, contentType) = result!.Value;
        contentType.Should().Be("image/png");

        using var ms = new MemoryStream();
        await contentStream.CopyToAsync(ms);
        ms.ToArray().Should().Equal(original);
    }

    [Fact]
    public async Task Delete_RemovesFile_SubsequentDownloadReturnsNull()
    {
        var store = CreateStore();
        using var stream = new MemoryStream(MakePngBytes());
        var key = await store.UploadAsync(stream, "image/png", "to-delete.png");

        await store.DeleteAsync(key);

        var result = await store.DownloadAsync(key);
        result.Should().BeNull("file should be gone after delete");
    }

    [Fact]
    public async Task Download_ReturnsNull_WhenKeyNotFound()
    {
        var store = CreateStore();
        // A well-formed ObjectId that doesn't exist in the bucket
        var result = await store.DownloadAsync("507f1f77bcf86cd799439011");
        result.Should().BeNull();
    }

    [Fact]
    public async Task Download_ReturnsNull_WhenKeyIsInvalidFormat()
    {
        var store = CreateStore();
        var result = await store.DownloadAsync("not-a-valid-object-id");
        result.Should().BeNull("invalid ObjectId format should return null gracefully");
    }

    [Fact]
    public async Task Delete_IsIdempotent_WhenFileAlreadyGone()
    {
        var store = CreateStore();
        // Delete a non-existent key — should not throw
        var act = async () => await store.DeleteAsync("507f1f77bcf86cd799439011");
        await act.Should().NotThrowAsync("deleting a non-existent file should be a no-op");
    }
}
