using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository;

/// <summary>
/// Concrete MongoDB database context. Creates collections and ensures
/// all required indexes exist on construction (idempotent).
/// </summary>
public sealed class NinetyNineDbContext : INinetyNineDbContext
{
    private readonly IMongoDatabase _database;

    public NinetyNineDbContext(IMongoClient client, IOptions<MongoDbSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(settings);

        _database = client.GetDatabase(settings.Value.DatabaseName);
        EnsureIndexes();
    }

    public IMongoCollection<Player> Players =>
        _database.GetCollection<Player>("players");

    public IMongoCollection<Venue> Venues =>
        _database.GetCollection<Venue>("venues");

    public IMongoCollection<Game> Games =>
        _database.GetCollection<Game>("games");

    // Friends + Communities (Sprint 0 S0.3)
    public IMongoCollection<Friendship> Friendships =>
        _database.GetCollection<Friendship>("friendships");

    public IMongoCollection<FriendRequest> FriendRequests =>
        _database.GetCollection<FriendRequest>("friend_requests");

    public IMongoCollection<Community> Communities =>
        _database.GetCollection<Community>("communities");

    public IMongoCollection<CommunityMembership> CommunityMembers =>
        _database.GetCollection<CommunityMembership>("community_members");

    public IMongoCollection<CommunityInvitation> CommunityInvitations =>
        _database.GetCollection<CommunityInvitation>("community_invitations");

    public IMongoCollection<CommunityJoinRequest> CommunityJoinRequests =>
        _database.GetCollection<CommunityJoinRequest>("community_join_requests");

    // Sprint 4 S4.3
    public IMongoCollection<OwnershipTransfer> OwnershipTransfers =>
        _database.GetCollection<OwnershipTransfer>("ownership_transfers");

    public IMongoDatabase Database => _database;

    /// <summary>
    /// Creates all required indexes idempotently. MongoDB ignores duplicate
    /// index-creation requests for identical definitions.
    /// </summary>
    private void EnsureIndexes()
    {
        EnsurePlayerIndexes();
        EnsureVenueIndexes();
        EnsureGameIndexes();

        // Friends + Communities — plan Sprint 0 S0.3 (15 indexes) + Sprint 2 S2.1 (5 more)
        EnsureFriendshipIndexes();
        EnsureFriendRequestIndexes();
        EnsureCommunityIndexes();
        EnsureCommunityMemberIndexes();
        EnsureCommunityInvitationIndexes();
        EnsureCommunityJoinRequestIndexes();
    }

    private void EnsurePlayerIndexes()
    {
        var players = Players;
        var indexModels = new List<CreateIndexModel<Player>>
        {
            // Unique index on displayName (case-sensitive)
            new(Builders<Player>.IndexKeys.Ascending(p => p.DisplayName),
                new CreateIndexOptions { Unique = true, Name = "idx_players_displayName_unique" }),

            // Unique compound index on linkedIdentities array fields
            new(Builders<Player>.IndexKeys
                    .Ascending("linkedIdentities.provider")
                    .Ascending("linkedIdentities.providerUserId"),
                new CreateIndexOptions
                {
                    Unique = true,
                    Sparse = true,
                    Name = "idx_players_linkedIdentities_unique"
                })
        };

        players.Indexes.CreateMany(indexModels);
    }

    private void EnsureVenueIndexes()
    {
        var venues = Venues;
        var indexes = new List<CreateIndexModel<Venue>>
        {
            new(Builders<Venue>.IndexKeys.Ascending(v => v.Name),
                new CreateIndexOptions { Name = "idx_venues_name" }),

            // Plan index #15 — "venues in community" lookup. Sparse because
            // most venues are unaffiliated.
            new(Builders<Venue>.IndexKeys.Ascending(v => v.CommunityId),
                new CreateIndexOptions { Name = "idx_venues_communityId", Sparse = true }),
        };

        venues.Indexes.CreateMany(indexes);
    }

    // ── Friends + Communities indexes (plan S0.3 — 15 indexes total) ────
    // Index numbering below matches the table in
    // docs/plans/friends-communities-v1.md Sprint 0 S0.3.

    private void EnsureFriendshipIndexes()
    {
        var friendships = Friendships;
        var indexes = new List<CreateIndexModel<Friendship>>
        {
            // 1. Edge existence check — unique on the canonical pair key.
            new(Builders<Friendship>.IndexKeys.Ascending(f => f.PlayerIdsKey),
                new CreateIndexOptions { Name = "ux_friendships_playerIdsKey", Unique = true }),

            // 2. All friendships where player is the "A" side.
            new(Builders<Friendship>.IndexKeys.Ascending(f => f.PlayerAId),
                new CreateIndexOptions { Name = "idx_friendships_playerA" }),

            // 3. All friendships where player is the "B" side.
            new(Builders<Friendship>.IndexKeys.Ascending(f => f.PlayerBId),
                new CreateIndexOptions { Name = "idx_friendships_playerB" }),
        };
        friendships.Indexes.CreateMany(indexes);
    }

    private void EnsureFriendRequestIndexes()
    {
        var requests = FriendRequests;
        var indexes = new List<CreateIndexModel<FriendRequest>>
        {
            // 4. Inbox filtered by status (typical query: to + Pending).
            new(Builders<FriendRequest>.IndexKeys
                    .Ascending(r => r.ToPlayerId)
                    .Ascending(r => r.Status),
                new CreateIndexOptions { Name = "idx_friend_requests_to_status" }),

            // 5. Outbox filtered by status.
            new(Builders<FriendRequest>.IndexKeys
                    .Ascending(r => r.FromPlayerId)
                    .Ascending(r => r.Status),
                new CreateIndexOptions { Name = "idx_friend_requests_from_status" }),

            // 6. Prevent duplicate Pending request between the same pair.
            // Partial filter on Status = Pending so terminal states can
            // coexist with a new Pending request after a cooldown expires.
            // PartialFilterExpression lives on CreateIndexOptions<T>, not
            // the non-generic base.
            new(Builders<FriendRequest>.IndexKeys
                    .Ascending(r => r.FromPlayerId)
                    .Ascending(r => r.ToPlayerId),
                new CreateIndexOptions<FriendRequest>
                {
                    Name = "ux_friend_requests_pending_pair",
                    Unique = true,
                    PartialFilterExpression = Builders<FriendRequest>.Filter.Eq(
                        r => r.Status, FriendRequestStatus.Pending),
                }),
        };
        requests.Indexes.CreateMany(indexes);
    }

    private void EnsureCommunityIndexes()
    {
        var communities = Communities;

        // 7. Case-insensitive uniqueness on Name via collation strength 2.
        var nameIndex = new CreateIndexModel<Community>(
            Builders<Community>.IndexKeys.Ascending(c => c.Name),
            new CreateIndexOptions
            {
                Name = "ux_communities_name_ci",
                Unique = true,
                Collation = new Collation("en", strength: CollationStrength.Secondary),
            });

        // 8. Slug used for URL lookup — unique and case-sensitive.
        var slugIndex = new CreateIndexModel<Community>(
            Builders<Community>.IndexKeys.Ascending(c => c.Slug),
            new CreateIndexOptions { Name = "ux_communities_slug", Unique = true });

        // 9. Browse public communities.
        var visibilityIndex = new CreateIndexModel<Community>(
            Builders<Community>.IndexKeys.Ascending(c => c.Visibility),
            new CreateIndexOptions { Name = "idx_communities_visibility" });

        // 10. "Communities I own". Kept sparse for compatibility with
        // pre-principle-update dev DBs where the index was originally
        // created sparse. Functionally identical now that OwnerPlayerId
        // is non-nullable — sparse on an always-non-null field indexes
        // every document. Removing `Sparse = true` would trigger
        // MongoDB's "same name, different options" guard.
        var ownerPlayerIndex = new CreateIndexModel<Community>(
            Builders<Community>.IndexKeys.Ascending(c => c.OwnerPlayerId),
            new CreateIndexOptions { Name = "idx_communities_ownerPlayerId", Sparse = true });

        // ── idx_communities_ownerVenueId is intentionally absent.
        // Venues cannot own communities; see the plan's 2026-04-11 fork
        // B reversal. Legacy indexes on existing dev databases are
        // harmless (empty) and can be dropped manually if desired.

        communities.Indexes.CreateMany(new[]
        {
            nameIndex,
            slugIndex,
            visibilityIndex,
            ownerPlayerIndex,
        });
    }

    private void EnsureCommunityMemberIndexes()
    {
        var members = CommunityMembers;
        var indexes = new List<CreateIndexModel<CommunityMembership>>
        {
            // 12. Members ordered by join time (for community detail page).
            new(Builders<CommunityMembership>.IndexKeys
                    .Ascending(m => m.CommunityId)
                    .Ascending(m => m.JoinedAt),
                new CreateIndexOptions { Name = "idx_community_members_community_joinedAt" }),

            // 13. "My communities" lookup and dedupe of (player, community).
            new(Builders<CommunityMembership>.IndexKeys
                    .Ascending(m => m.PlayerId)
                    .Ascending(m => m.CommunityId),
                new CreateIndexOptions { Name = "ux_community_members_player_community", Unique = true }),

            // 14. List owners / admins of a community.
            new(Builders<CommunityMembership>.IndexKeys
                    .Ascending(m => m.CommunityId)
                    .Ascending(m => m.Role),
                new CreateIndexOptions { Name = "idx_community_members_community_role" }),
        };
        members.Indexes.CreateMany(indexes);
    }

    // ── Sprint 2 S2.1 indexes (invitations + join requests) ──────────

    private void EnsureCommunityInvitationIndexes()
    {
        var invitations = CommunityInvitations;
        var indexes = new List<CreateIndexModel<CommunityInvitation>>
        {
            // 16. Inbox: "invitations for me" filtered by status.
            new(Builders<CommunityInvitation>.IndexKeys
                    .Ascending(i => i.InvitedPlayerId)
                    .Ascending(i => i.Status),
                new CreateIndexOptions { Name = "idx_community_invitations_invitee_status" }),

            // 17. Community settings: "pending invites for this community".
            new(Builders<CommunityInvitation>.IndexKeys
                    .Ascending(i => i.CommunityId)
                    .Ascending(i => i.Status),
                new CreateIndexOptions { Name = "idx_community_invitations_community_status" }),

            // 18. Partial unique: prevent duplicate Pending invite per
            // (community, invitee). Terminal states can coexist with a
            // new Pending row after cancellation / expiry.
            new(Builders<CommunityInvitation>.IndexKeys
                    .Ascending(i => i.CommunityId)
                    .Ascending(i => i.InvitedPlayerId),
                new CreateIndexOptions<CommunityInvitation>
                {
                    Name = "ux_community_invitations_pending_pair",
                    Unique = true,
                    PartialFilterExpression = Builders<CommunityInvitation>.Filter.Eq(
                        i => i.Status, CommunityInvitationStatus.Pending),
                }),
        };
        invitations.Indexes.CreateMany(indexes);
    }

    private void EnsureCommunityJoinRequestIndexes()
    {
        var requests = CommunityJoinRequests;
        var indexes = new List<CreateIndexModel<CommunityJoinRequest>>
        {
            // 19. Community owner queue: "pending join requests at this community".
            new(Builders<CommunityJoinRequest>.IndexKeys
                    .Ascending(r => r.CommunityId)
                    .Ascending(r => r.Status),
                new CreateIndexOptions { Name = "idx_community_join_requests_community_status" }),

            // 20. Partial unique: prevent duplicate Pending join request
            // from the same (player, community) pair.
            new(Builders<CommunityJoinRequest>.IndexKeys
                    .Ascending(r => r.PlayerId)
                    .Ascending(r => r.CommunityId),
                new CreateIndexOptions<CommunityJoinRequest>
                {
                    Name = "ux_community_join_requests_pending_pair",
                    Unique = true,
                    PartialFilterExpression = Builders<CommunityJoinRequest>.Filter.Eq(
                        r => r.Status, CommunityJoinRequestStatus.Pending),
                }),
        };
        requests.Indexes.CreateMany(indexes);
    }

    private void EnsureGameIndexes()
    {
        var games = Games;
        var indexModels = new List<CreateIndexModel<Game>>
        {
            new(Builders<Game>.IndexKeys.Ascending(g => g.PlayerId),
                new CreateIndexOptions { Name = "idx_games_playerId" }),

            new(Builders<Game>.IndexKeys.Descending(g => g.WhenPlayed),
                new CreateIndexOptions { Name = "idx_games_whenPlayed_desc" }),

            new(Builders<Game>.IndexKeys.Ascending(g => g.GameState),
                new CreateIndexOptions { Name = "idx_games_gameState" })
        };

        games.Indexes.CreateMany(indexModels);
    }
}
