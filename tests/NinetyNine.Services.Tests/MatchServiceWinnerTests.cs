using NinetyNine.Model;
using NinetyNine.Services;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Unit tests for <see cref="MatchService.SelectConcurrentWinner"/>. The
/// method is the win-condition arbiter for v0.5.x Concurrent matches:
/// highest TotalScore wins; ties broken by most perfect frames; final
/// tie-break by earliest CompletedAt timestamp. Lives outside the
/// Mongo-backed integration suite because it is pure logic.
/// </summary>
public class MatchServiceWinnerTests
{
    private static Game MakeCompletedGame(
        Guid playerId,
        int totalScore,
        int perfectFrames = 0,
        DateTime? completedAt = null)
    {
        // Synthesise nine completed frames whose FrameScore sums to totalScore
        // and whose first `perfectFrames` frames carry FrameScore == 11.
        // The Game's TotalScore property recomputes from frames, so the
        // arithmetic must add up.
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            PlayerId = playerId,
            GameState = GameState.Completed,
            CompletedAt = completedAt ?? DateTime.UtcNow,
        };

        int remaining = totalScore;
        for (int i = 1; i <= 9; i++)
        {
            int score;
            if (i <= perfectFrames)
            {
                score = 11;            // perfect frame
                remaining -= 11;
            }
            else if (i == 9)
            {
                score = remaining;     // pour what's left into the last frame
            }
            else
            {
                // Distribute remainder across the non-perfect frames; cap at 11.
                int framesLeft = 9 - i;
                int average = framesLeft > 0 ? remaining / (framesLeft + 1) : remaining;
                score = Math.Clamp(average, 0, 11);
                remaining -= score;
            }

            // Encode the score as ballCount + breakBonus so frame.IsValidScore
            // would still hold (BreakBonus 0/1, BallCount 0..10, total ≤ 11).
            int breakBonus = score >= 1 && score <= 11 ? 1 : 0;
            int ballCount = score - breakBonus;
            if (ballCount < 0) { ballCount = 0; breakBonus = score; }

            game.Frames.Add(new Frame
            {
                FrameId = Guid.NewGuid(),
                GameId = game.GameId,
                FrameNumber = i,
                BreakBonus = breakBonus,
                BallCount = ballCount,
                IsCompleted = true,
                IsActive = false,
                RunningTotal = 0, // not needed for the winner arithmetic
            });
        }

        return game;
    }

    [Fact]
    public void SelectConcurrentWinner_HighestTotalScore_Wins()
    {
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();
        var p3 = Guid.NewGuid();

        var games = new[]
        {
            MakeCompletedGame(p1, totalScore: 60),
            MakeCompletedGame(p2, totalScore: 88),
            MakeCompletedGame(p3, totalScore: 55),
        };

        MatchService.SelectConcurrentWinner(games)
            .Should().Be(p2, "p2 has the highest total score");
    }

    [Fact]
    public void SelectConcurrentWinner_TiedScore_MorePerfectFrames_Wins()
    {
        var winner = Guid.NewGuid();
        var loser  = Guid.NewGuid();

        var games = new[]
        {
            // Both reach 77 but `winner` got there with more 11-point frames.
            MakeCompletedGame(loser,  totalScore: 77, perfectFrames: 1),
            MakeCompletedGame(winner, totalScore: 77, perfectFrames: 4),
        };

        MatchService.SelectConcurrentWinner(games)
            .Should().Be(winner,
                "ties on TotalScore are broken by Game.PerfectFrames " +
                "(frames scored at the maximum 11)");
    }

    [Fact]
    public void SelectConcurrentWinner_TiedOnScoreAndPerfects_EarliestCompletion_Wins()
    {
        var fastest = Guid.NewGuid();
        var slower  = Guid.NewGuid();
        var slowest = Guid.NewGuid();

        var t0 = new DateTime(2026, 4, 25, 17, 0, 0, DateTimeKind.Utc);

        var games = new[]
        {
            MakeCompletedGame(slowest, 66, perfectFrames: 2, completedAt: t0.AddMinutes(20)),
            MakeCompletedGame(fastest, 66, perfectFrames: 2, completedAt: t0.AddMinutes(5)),
            MakeCompletedGame(slower,  66, perfectFrames: 2, completedAt: t0.AddMinutes(10)),
        };

        MatchService.SelectConcurrentWinner(games)
            .Should().Be(fastest,
                "after TotalScore and PerfectFrames tie, the player whose " +
                "Game completed first wins — running out the rack quickly is " +
                "the final tie-breaker");
    }

    [Fact]
    public void SelectConcurrentWinner_EmptyList_Throws()
    {
        var act = () => MatchService.SelectConcurrentWinner(Array.Empty<Game>());
        act.Should().Throw<InvalidOperationException>();
    }
}
