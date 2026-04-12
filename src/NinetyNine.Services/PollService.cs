using Microsoft.Extensions.Logging;
using NinetyNine.Model;
using NinetyNine.Repository.Repositories;

namespace NinetyNine.Services;

public sealed class PollService(
    IPollRepository polls,
    IVoteRepository votes,
    ICommunityMemberRepository members,
    ICommunityRepository communities,
    IPlayerRepository players,
    ICommunityService communityService,
    INotificationService notificationService,
    ILogger<PollService> logger) : IPollService
{
    private const int MaxActivePollsPerCommunity = 10;

    public async Task<ServiceResult<Poll>> CreatePollAsync(
        Guid? communityId,
        Guid createdByPlayerId,
        string title,
        string? description,
        PollType pollType,
        List<PollOption> options,
        int durationDays,
        bool? anonymousVoting = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            return ServiceResult<Poll>.Fail("InvalidInput", "Title is required.");

        if (options.Count < 2)
            return ServiceResult<Poll>.Fail("InvalidInput", "At least two options are required.");

        // Duration bounds: 1–14 days; 3-day floor for member removal.
        if (durationDays < 1 || durationDays > 14)
            return ServiceResult<Poll>.Fail("InvalidInput", "Duration must be 1–14 days.");
        if (pollType == PollType.MemberRemoval && durationDays < 3)
            return ServiceResult<Poll>.Fail("InvalidInput",
                "Member removal polls must be open for at least 3 days.");

        int eligibleVoters;
        if (communityId is not null)
        {
            var community = await communities.GetByIdAsync(communityId.Value, ct);
            if (community is null)
                return ServiceResult<Poll>.Fail("CommunityNotFound", "Community not found.");

            // Authz: Owner/Admin for any type; Member for Advisory only.
            var membership = await members.GetMembershipAsync(communityId.Value, createdByPlayerId, ct);
            if (membership is null)
                return ServiceResult<Poll>.Fail("NotAMember", "You are not a member of this community.");

            if (pollType != PollType.Advisory
                && membership.Role != CommunityRole.Owner
                && membership.Role != CommunityRole.Admin)
                return ServiceResult<Poll>.Fail("NotAuthorized",
                    "Only owners and admins can create non-advisory polls.");

            // Active poll cap.
            var active = await polls.ListByCommunityAsync(communityId.Value, PollStatus.Open, ct);
            if (active.Count >= MaxActivePollsPerCommunity)
                return ServiceResult<Poll>.Fail("PollCapExceeded",
                    $"A community can have at most {MaxActivePollsPerCommunity} active polls.");

            eligibleVoters = (int)await members.CountMembersAsync(communityId.Value, ct);
        }
        else
        {
            // Site-wide poll: only configurable site admins can create.
            // For now, any authenticated player can create FeatureProposal.
            if (pollType != PollType.FeatureProposal)
                return ServiceResult<Poll>.Fail("NotAuthorized",
                    "Only feature proposal polls can be site-wide.");

            var allPlayers = await players.ListAllAsync(ct);
            eligibleVoters = allPlayers.Count(p => p.RetiredAt is null);
        }

        // Anonymous voting: mandatory for member-targeting polls.
        var isAnonymous = anonymousVoting ?? true;
        if (pollType == PollType.MemberRemoval)
            isAnonymous = true;

        var poll = new Poll
        {
            CommunityId = communityId,
            CreatedByPlayerId = createdByPlayerId,
            Title = title.Trim(),
            Description = description?.Trim(),
            PollType = pollType,
            Options = options,
            EligibleVoterCount = eligibleVoters,
            QuorumThreshold = 0.5,
            SupermajorityThreshold = pollType == PollType.MemberRemoval ? 2.0 / 3.0 : null,
            AnonymousVoting = isAnonymous,
            ExpiresAt = DateTime.UtcNow.AddDays(durationDays),
        };

        await polls.CreateAsync(poll, ct);
        return ServiceResult<Poll>.Ok(poll);
    }

    public async Task<ServiceResult> CastVoteAsync(
        Guid pollId, Guid playerId, int optionIndex, CancellationToken ct = default)
    {
        var poll = await polls.GetByIdAsync(pollId, ct);
        if (poll is null)
            return ServiceResult.Fail("PollNotFound", "Poll not found.");

        if (poll.Status != PollStatus.Open)
            return ServiceResult.Fail("PollNotOpen", "This poll is no longer open.");

        if (DateTime.UtcNow > poll.ExpiresAt)
            return ServiceResult.Fail("PollExpired", "This poll has expired.");

        if (optionIndex < 0 || optionIndex >= poll.Options.Count)
            return ServiceResult.Fail("InvalidOption", "Invalid option.");

        // Eligibility: community member or any authenticated player for site-wide.
        if (poll.CommunityId is not null)
        {
            var membership = await members.GetMembershipAsync(poll.CommunityId.Value, playerId, ct);
            if (membership is null)
                return ServiceResult.Fail("NotEligible", "You are not a member of this community.");
        }

        // One-vote-per-player: service guard + unique index.
        var existing = await votes.GetByPollAndPlayerAsync(pollId, playerId, ct);
        if (existing is not null)
            return ServiceResult.Fail("AlreadyVoted", "You have already voted on this poll.");

        var vote = new Vote
        {
            PollId = pollId,
            PlayerId = playerId,
            OptionIndex = optionIndex,
        };

        try
        {
            await votes.CreateAsync(vote, ct);
        }
        catch (MongoDB.Driver.MongoWriteException ex)
            when (ex.WriteError?.Category == MongoDB.Driver.ServerErrorCategory.DuplicateKey)
        {
            return ServiceResult.Fail("AlreadyVoted", "You have already voted on this poll.");
        }

        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<PollResult>> ClosePollAsync(
        Guid pollId, CancellationToken ct = default)
    {
        var poll = await polls.GetByIdAsync(pollId, ct);
        if (poll is null)
            return ServiceResult<PollResult>.Fail("PollNotFound", "Poll not found.");

        if (poll.Status != PollStatus.Open)
            return ServiceResult<PollResult>.Fail("PollNotOpen", "This poll is not open.");

        var allVotes = await votes.ListByPollAsync(pollId, ct);

        var voteCounts = new int[poll.Options.Count];
        foreach (var v in allVotes)
        {
            if (v.OptionIndex >= 0 && v.OptionIndex < voteCounts.Length)
                voteCounts[v.OptionIndex]++;
        }

        var totalVotes = allVotes.Count;
        var quorumNeeded = (int)Math.Ceiling(poll.EligibleVoterCount * poll.QuorumThreshold);
        var quorumMet = totalVotes >= quorumNeeded;

        // Find the winning option.
        int? winnerIndex = null;
        var maxVotes = voteCounts.Max();
        if (maxVotes > 0)
        {
            var winners = Enumerable.Range(0, voteCounts.Length)
                .Where(i => voteCounts[i] == maxVotes)
                .ToList();
            if (winners.Count == 1)
                winnerIndex = winners[0];
            // Tie: no winner.
        }

        // Supermajority check.
        var thresholdMet = true;
        if (poll.SupermajorityThreshold is not null && winnerIndex is not null)
        {
            var needed = (int)Math.Ceiling(totalVotes * poll.SupermajorityThreshold.Value);
            thresholdMet = voteCounts[winnerIndex.Value] >= needed;
        }

        var result = new PollResult
        {
            VoteCounts = voteCounts,
            TotalVotes = totalVotes,
            QuorumMet = quorumMet,
            ThresholdMet = thresholdMet,
            WinningOptionIndex = winnerIndex,
        };

        poll.Result = result;
        poll.Status = DateTime.UtcNow > poll.ExpiresAt ? PollStatus.Expired : PollStatus.Closed;
        poll.ClosedAt = DateTime.UtcNow;
        await polls.UpdateAsync(poll, ct);

        // Binding member-removal: auto-execute if quorum + supermajority met.
        if (poll.PollType == PollType.MemberRemoval
            && quorumMet && thresholdMet
            && winnerIndex is not null
            && poll.CommunityId is not null)
        {
            var targetOption = poll.Options[winnerIndex.Value];
            if (targetOption.TargetPlayerId is not null
                && targetOption.Label.StartsWith("Remove", StringComparison.OrdinalIgnoreCase))
            {
                var removeResult = await communityService.RemoveMemberAsync(
                    poll.CommunityId.Value,
                    targetOption.TargetPlayerId.Value,
                    poll.CreatedByPlayerId,
                    ct);

                if (removeResult.Success)
                {
                    await notificationService.NotifyAsync(
                        targetOption.TargetPlayerId.Value,
                        "MemberRemovedByVote",
                        $"You were removed from a community by a member vote on \"{poll.Title}\".",
                        ct: ct);

                    logger.LogInformation(
                        "Binding member-removal poll {PollId} auto-executed: removed {PlayerId}",
                        pollId, targetOption.TargetPlayerId.Value);
                }
            }
        }

        return ServiceResult<PollResult>.Ok(result);
    }

    public async Task<Poll?> GetPollForViewerAsync(
        Guid pollId, Guid? viewerId, CancellationToken ct = default)
    {
        var poll = await polls.GetByIdAsync(pollId, ct);
        if (poll is null) return null;

        // Bandwagon prevention: hide results if the viewer hasn't voted.
        if (poll.Status == PollStatus.Open && viewerId is not null)
        {
            var viewerVote = await votes.GetByPollAndPlayerAsync(pollId, viewerId.Value, ct);
            if (viewerVote is null)
                poll.Result = null; // Don't show results until they've voted.
        }

        return poll;
    }

    public Task<IReadOnlyList<Poll>> ListCommunityPollsAsync(
        Guid communityId, PollStatus? status = null, CancellationToken ct = default)
        => polls.ListByCommunityAsync(communityId, status, ct);

    public Task<IReadOnlyList<Poll>> ListSiteWidePollsAsync(
        PollStatus? status = null, CancellationToken ct = default)
        => polls.ListSiteWideAsync(status, ct);
}
