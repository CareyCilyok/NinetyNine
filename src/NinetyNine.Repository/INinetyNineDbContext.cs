using MongoDB.Driver;
using NinetyNine.Model;

namespace NinetyNine.Repository;

/// <summary>
/// Provides access to the MongoDB collections used by the NinetyNine application.
/// </summary>
public interface INinetyNineDbContext
{
    IMongoCollection<Player> Players { get; }
    IMongoCollection<Venue> Venues { get; }
    IMongoCollection<Game> Games { get; }

    // Friends + Communities (Sprint 0 S0.3 — docs/plans/friends-communities-v1.md)
    IMongoCollection<Friendship> Friendships { get; }
    IMongoCollection<FriendRequest> FriendRequests { get; }
    IMongoCollection<Community> Communities { get; }
    IMongoCollection<CommunityMembership> CommunityMembers { get; }
    IMongoCollection<CommunityInvitation> CommunityInvitations { get; }
    IMongoCollection<CommunityJoinRequest> CommunityJoinRequests { get; }

    // Sprint 4 S4.3
    IMongoCollection<OwnershipTransfer> OwnershipTransfers { get; }

    // Sprint 5 S5.2
    IMongoCollection<Notification> Notifications { get; }

    // Sprint 5 S5.4
    IMongoCollection<PlayerBlock> PlayerBlocks { get; }

    // Sprint 9 S9.1
    IMongoCollection<Poll> Polls { get; }
    IMongoCollection<Vote> Votes { get; }

    // Sprint 10 S10.1
    IMongoCollection<Match> Matches { get; }

    IMongoDatabase Database { get; }
}
