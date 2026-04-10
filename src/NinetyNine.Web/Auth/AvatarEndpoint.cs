using NinetyNine.Repository.Storage;

namespace NinetyNine.Web.Auth;

/// <summary>
/// Minimal API endpoint that streams avatar images from GridFS.
/// GET /api/avatars/{playerId}
/// </summary>
public static class AvatarEndpoint
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/avatars/{playerId:guid}", async (
            Guid playerId,
            HttpContext context,
            IAvatarStore avatarStore,
            NinetyNine.Repository.Repositories.IPlayerRepository playerRepo,
            CancellationToken ct) =>
        {
            var player = await playerRepo.GetByIdAsync(playerId, ct);
            if (player?.Avatar is null)
                return Results.NotFound();

            var result = await avatarStore.DownloadAsync(player.Avatar.StorageKey, ct);
            if (result is null)
                return Results.NotFound();

            var (stream, contentType) = result.Value;

            context.Response.Headers["Cache-Control"] = "private, max-age=3600";
            return Results.Stream(stream, contentType);
        })
        .AllowAnonymous();
    }
}
