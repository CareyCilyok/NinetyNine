using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
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
}
