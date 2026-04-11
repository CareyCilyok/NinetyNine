using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository;

/// <summary>
/// Registers all BSON serialization conventions and class maps for the NinetyNine domain.
/// Call <see cref="Register"/> exactly once at application startup (idempotent on repeated calls).
/// </summary>
public static class BsonConfiguration
{
    private static bool _registered;
    private static readonly object _lock = new();

    /// <summary>
    /// Idempotent — safe to call multiple times. Registers:
    /// <list type="bullet">
    ///   <item>Standard GUID representation</item>
    ///   <item>CamelCase field names</item>
    ///   <item>Enums serialized as strings</item>
    ///   <item>Class maps for Player, Venue, Game, and Frame with computed properties ignored</item>
    /// </list>
    /// </summary>
    public static void Register()
    {
        lock (_lock)
        {
            if (_registered) return;
            _registered = true;
        }

#pragma warning disable CS0618 // GuidRepresentation is obsolete in MongoDB Driver 3.x but required for serialization config
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
#pragma warning restore CS0618

        // Global convention pack: camelCase element names + enums as strings
        var conventionPack = new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new EnumRepresentationConvention(BsonType.String)
        };
        ConventionRegistry.Register("NinetyNineConventions", conventionPack, _ => true);

        RegisterPlayerClassMap();
        RegisterVenueClassMap();
        RegisterGameClassMap();
        RegisterFrameClassMap();
    }

    private static void RegisterPlayerClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Player))) return;

        BsonClassMap.RegisterClassMap<Player>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(p => p.PlayerId));
            cm.GetMemberMap(p => p.PlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
        });

        if (!BsonClassMap.IsClassMapRegistered(typeof(ProfileVisibility)))
        {
            BsonClassMap.RegisterClassMap<ProfileVisibility>(cm => cm.AutoMap());
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(AvatarRef)))
        {
            BsonClassMap.RegisterClassMap<AvatarRef>(cm => cm.AutoMap());
        }
    }

    private static void RegisterVenueClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Venue))) return;

        BsonClassMap.RegisterClassMap<Venue>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(v => v.VenueId));
            cm.GetMemberMap(v => v.VenueId)
              .SetSerializer(new GuidSerializer(BsonType.String));
        });
    }

    private static void RegisterGameClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Game))) return;

        BsonClassMap.RegisterClassMap<Game>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(g => g.GameId));
            cm.GetMemberMap(g => g.GameId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(g => g.PlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(g => g.VenueId)
              .SetSerializer(new GuidSerializer(BsonType.String));

            // Ignore all computed properties
            cm.UnmapProperty(g => g.TotalScore);
            cm.UnmapProperty(g => g.RunningTotal);
            cm.UnmapProperty(g => g.IsInProgress);
            cm.UnmapProperty(g => g.IsCompleted);
            cm.UnmapProperty(g => g.CompletedFrames);
            cm.UnmapProperty(g => g.CurrentFrame);
            cm.UnmapProperty(g => g.AverageScore);
            cm.UnmapProperty(g => g.BestFrame);
            cm.UnmapProperty(g => g.PerfectFrames);
            cm.UnmapProperty(g => g.IsPerfectGame);
        });
    }

    private static void RegisterFrameClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Frame))) return;

        BsonClassMap.RegisterClassMap<Frame>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(f => f.FrameId));
            cm.GetMemberMap(f => f.FrameId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(f => f.GameId)
              .SetSerializer(new GuidSerializer(BsonType.String));

            // Ignore computed properties
            cm.UnmapProperty(f => f.FrameScore);
            cm.UnmapProperty(f => f.IsValidScore);
            cm.UnmapProperty(f => f.IsPerfectFrame);
        });
    }

    /// <summary>
    /// Creates the three auth-related indexes on the <c>players</c> collection:
    /// <list type="bullet">
    ///   <item>Unique ascending index on <c>emailAddress</c> (enforces one account per address).</item>
    ///   <item>Sparse ascending index on <c>emailVerificationToken</c> (fast lookup during /verify-email).</item>
    ///   <item>Sparse ascending index on <c>passwordResetToken</c> (fast lookup during /reset-password).</item>
    /// </list>
    /// Idempotent — safe to call on every startup. If an index already exists with the same name
    /// and identical key spec, MongoDB silently succeeds. If a conflicting definition is detected
    /// (error codes 85 or 86), the conflict is logged and startup continues so existing data is
    /// never lost due to an index mismatch (manual intervention required in that case).
    /// </summary>
    /// <param name="players">The <c>players</c> MongoDB collection.</param>
    /// <param name="logger">Optional logger; pass <c>null</c> to suppress log output.</param>
    /// <param name="ct">Optional cancellation token.</param>
    public static async Task EnsureAuthIndexesAsync(
        IMongoCollection<Player> players,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(players);

        // 1. Unique index on emailAddress — lowercasing is handled at write time by the repository.
        var emailIndex = new CreateIndexModel<Player>(
            Builders<Player>.IndexKeys.Ascending(p => p.EmailAddress),
            new CreateIndexOptions { Name = "ux_email", Unique = true });

        // 2. Sparse index on emailVerificationToken — only non-null values are indexed.
        var emailVerifyIndex = new CreateIndexModel<Player>(
            Builders<Player>.IndexKeys.Ascending(p => p.EmailVerificationToken),
            new CreateIndexOptions { Name = "ix_email_verification_token", Sparse = true });

        // 3. Sparse index on passwordResetToken — only non-null values are indexed.
        var passwordResetIndex = new CreateIndexModel<Player>(
            Builders<Player>.IndexKeys.Ascending(p => p.PasswordResetToken),
            new CreateIndexOptions { Name = "ix_password_reset_token", Sparse = true });

        var models = new[] { emailIndex, emailVerifyIndex, passwordResetIndex };

        try
        {
            await players.Indexes.CreateManyAsync(models, cancellationToken: ct);
            logger?.LogInformation(
                "Auth indexes ensured on players collection (ux_email, ix_email_verification_token, ix_password_reset_token).");
        }
        catch (MongoCommandException ex) when (ex.Code is 85 or 86)
        {
            // 85 = IndexOptionsConflict, 86 = IndexKeySpecsConflict.
            // An index with the same name but a different definition already exists.
            // Log and continue — do not crash startup. Manual resolution is required.
            logger?.LogWarning(
                ex,
                "Auth index creation encountered a conflicting definition (code {Code}). " +
                "Existing indexes are preserved. Manual intervention may be required.",
                ex.Code);
        }
    }
}
