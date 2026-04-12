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

        // Friends + Communities (Sprint 0 S0.3 — plan docs/plans/friends-communities-v1.md)
        RegisterFriendshipClassMap();
        RegisterFriendRequestClassMap();
        RegisterCommunityClassMap();
        RegisterCommunityMembershipClassMap();
        RegisterCommunityInvitationClassMap();
        RegisterCommunityJoinRequestClassMap();

        // Sprint 4 S4.3
        RegisterOwnershipTransferClassMap();

        // Sprint 5 S5.2
        RegisterNotificationClassMap();

        // Sprint 5 S5.4
        RegisterPlayerBlockClassMap();
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

            // Venue.CommunityId and CreatedByPlayerId use the global
            // default Guid serializer (binary UUID subtype 4). The Sprint 0
            // venue seeder wrote them as binary before Sprint 3 existed,
            // and rewriting every seeded venue to use string storage
            // just to match Community._id would require a live migration
            // pass first. C# Guid equality works regardless of the Mongo
            // storage format, so `venue.CommunityId == community.CommunityId`
            // still resolves correctly.
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

    // ── Friends + Communities class maps (Sprint 0 S0.3) ────────────────
    // All new entities use string-encoded Guid ids so Mongo tooling
    // (mongo-express, mongosh) can display them cleanly and so joins to
    // the existing Player / Venue collections serialize identically.

    private static void RegisterFriendshipClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Friendship))) return;

        BsonClassMap.RegisterClassMap<Friendship>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(f => f.FriendshipId));
            cm.GetMemberMap(f => f.FriendshipId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(f => f.PlayerAId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(f => f.PlayerBId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(f => f.InitiatedByPlayerId)
              .SetSerializer(new NullableSerializer<Guid>(
                  new GuidSerializer(BsonType.String)));
        });
    }

    private static void RegisterFriendRequestClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(FriendRequest))) return;

        BsonClassMap.RegisterClassMap<FriendRequest>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(r => r.RequestId));
            cm.GetMemberMap(r => r.RequestId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(r => r.FromPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(r => r.ToPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
        });
    }

    private static void RegisterCommunityClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Community))) return;

        BsonClassMap.RegisterClassMap<Community>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(c => c.CommunityId));
            cm.GetMemberMap(c => c.CommunityId)
              .SetSerializer(new GuidSerializer(BsonType.String));

            // OwnerPlayerId is non-nullable now (2026-04-11 principle
            // update: venues can never own a community).
            cm.GetMemberMap(c => c.OwnerPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(c => c.CreatedByPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));

            // Legacy schema-v1 docs still have `ownerType: "Player"` and
            // possibly `ownerVenueId: null` — properties the C# class
            // no longer exposes. Tell the class map to ignore them so
            // old docs still deserialize cleanly. Every community doc
            // written after this release stops emitting the legacy
            // fields automatically.
            cm.SetIgnoreExtraElements(true);
        });
    }

    private static void RegisterCommunityMembershipClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(CommunityMembership))) return;

        BsonClassMap.RegisterClassMap<CommunityMembership>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(m => m.MembershipId));
            cm.GetMemberMap(m => m.MembershipId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(m => m.CommunityId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(m => m.PlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(m => m.InvitedByPlayerId)
              .SetSerializer(new NullableSerializer<Guid>(
                  new GuidSerializer(BsonType.String)));
        });
    }

    private static void RegisterCommunityInvitationClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(CommunityInvitation))) return;

        BsonClassMap.RegisterClassMap<CommunityInvitation>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(i => i.InvitationId));
            cm.GetMemberMap(i => i.InvitationId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(i => i.CommunityId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(i => i.InvitedPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(i => i.InvitedByPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
        });
    }

    private static void RegisterCommunityJoinRequestClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(CommunityJoinRequest))) return;

        BsonClassMap.RegisterClassMap<CommunityJoinRequest>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(r => r.RequestId));
            cm.GetMemberMap(r => r.RequestId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(r => r.CommunityId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(r => r.PlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(r => r.DecidedByPlayerId)
              .SetSerializer(new NullableSerializer<Guid>(
                  new GuidSerializer(BsonType.String)));
        });
    }

    private static void RegisterOwnershipTransferClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(OwnershipTransfer))) return;

        BsonClassMap.RegisterClassMap<OwnershipTransfer>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(t => t.TransferId));
            cm.GetMemberMap(t => t.TransferId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(t => t.CommunityId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(t => t.FromPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(t => t.ToPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
        });
    }

    private static void RegisterNotificationClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Notification))) return;

        BsonClassMap.RegisterClassMap<Notification>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(n => n.NotificationId));
            cm.GetMemberMap(n => n.NotificationId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(n => n.PlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
        });
    }

    private static void RegisterPlayerBlockClassMap()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(PlayerBlock))) return;

        BsonClassMap.RegisterClassMap<PlayerBlock>(cm =>
        {
            cm.AutoMap();
            cm.SetIdMember(cm.GetMemberMap(b => b.BlockId));
            cm.GetMemberMap(b => b.BlockId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(b => b.BlockerPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
            cm.GetMemberMap(b => b.BlockedPlayerId)
              .SetSerializer(new GuidSerializer(BsonType.String));
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
