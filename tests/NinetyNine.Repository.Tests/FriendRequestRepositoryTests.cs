using Microsoft.Extensions.Logging.Abstractions;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Repository.Tests;

[Collection(MongoCollection.Name)]
[Trait("Category", "Integration")]
public class FriendRequestRepositoryTests(MongoFixture fixture)
{
    private IFriendRequestRepository CreateRepo()
    {
        var ctx = fixture.CreateDbContext();
        return new FriendRequestRepository(ctx, NullLogger<FriendRequestRepository>.Instance);
    }

    private static FriendRequest MakeRequest(Guid from, Guid to, FriendRequestStatus status = FriendRequestStatus.Pending)
        => new()
        {
            FromPlayerId = from,
            ToPlayerId = to,
            Status = status,
            CreatedAt = DateTime.UtcNow,
        };

    [Fact]
    public async Task CreateAndGetPending_RoundTrip()
    {
        var repo = CreateRepo();
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        var request = MakeRequest(from, to);

        await repo.CreateAsync(request);

        var retrieved = await repo.GetPendingAsync(from, to);
        retrieved.Should().NotBeNull();
        retrieved!.RequestId.Should().Be(request.RequestId);
    }

    [Fact]
    public async Task DuplicatePending_IsBlockedByPartialUniqueIndex()
    {
        var repo = CreateRepo();
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        await repo.CreateAsync(MakeRequest(from, to));

        var act = async () => await repo.CreateAsync(MakeRequest(from, to));
        await act.Should().ThrowAsync<MongoDB.Driver.MongoWriteException>(
            "the partial unique index on (fromPlayerId, toPlayerId) where status=Pending must prevent dupes");
    }

    [Fact]
    public async Task AfterDecline_NewPendingRequest_IsAllowed()
    {
        var repo = CreateRepo();
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        var first = MakeRequest(from, to);
        await repo.CreateAsync(first);
        await repo.UpdateStatusAsync(first.RequestId, FriendRequestStatus.Declined, DateTime.UtcNow);

        // A new Pending should now be allowed because the partial index
        // only counts Pending rows.
        var second = MakeRequest(from, to);
        var act = async () => await repo.CreateAsync(second);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListIncomingAsync_FiltersByStatus()
    {
        var repo = CreateRepo();
        var to = Guid.NewGuid();

        await repo.CreateAsync(MakeRequest(Guid.NewGuid(), to, FriendRequestStatus.Pending));
        await repo.CreateAsync(MakeRequest(Guid.NewGuid(), to, FriendRequestStatus.Pending));
        await repo.CreateAsync(MakeRequest(Guid.NewGuid(), to, FriendRequestStatus.Declined));

        var pending = await repo.ListIncomingAsync(to, FriendRequestStatus.Pending);
        pending.Should().HaveCount(2);

        var declined = await repo.ListIncomingAsync(to, FriendRequestStatus.Declined);
        declined.Should().HaveCount(1);
    }

    [Fact]
    public async Task SweepExpiredAsync_MarksOldPendingAsExpired()
    {
        var repo = CreateRepo();
        var to = Guid.NewGuid();

        var old = new FriendRequest
        {
            FromPlayerId = Guid.NewGuid(),
            ToPlayerId = to,
            Status = FriendRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-45),
        };
        await repo.CreateAsync(old);

        var fresh = MakeRequest(Guid.NewGuid(), to);
        await repo.CreateAsync(fresh);

        var swept = await repo.SweepExpiredAsync(DateTime.UtcNow.AddDays(-30));
        swept.Should().Be(1);

        var pending = await repo.ListIncomingAsync(to, FriendRequestStatus.Pending);
        pending.Should().ContainSingle(r => r.RequestId == fresh.RequestId);

        var expired = await repo.ListIncomingAsync(to, FriendRequestStatus.Expired);
        expired.Should().ContainSingle(r => r.RequestId == old.RequestId);
    }
}
