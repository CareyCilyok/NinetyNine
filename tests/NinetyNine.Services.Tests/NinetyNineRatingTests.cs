using NinetyNine.Model;
using NinetyNine.Services;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Pure-logic tests for <see cref="StatisticsService.ComputeRating"/> —
/// the golf-handicap-style rating formula introduced in v0.9.0.
///
/// <para>Formula recap (per the user spec):</para>
/// <list type="bullet">
///   <item>Take the player's last 20 completed games of one discipline.</item>
///   <item>Within those, take the 5 highest game totals; average them.</item>
///   <item>For players with &lt; 5 games of the discipline, use the simple
///     average across all their games (best-of-5 is undefined).</item>
///   <item>Confidence = <c>min(GameCount, 20) × 5%</c>; "certified" at 20.</item>
/// </list>
/// </summary>
public class NinetyNineRatingTests
{
    private static Game G(int totalScore, DateTime whenPlayed, bool efren = false)
    {
        // Build a Completed game whose Frames sum to totalScore. We don't
        // care about per-frame distribution here — the rating uses
        // Game.TotalScore which is a computed sum of completed frames'
        // FrameScore (BreakBonus + BallCount, with the 9-ball worth 2).
        // Easiest: one frame with BallCount = totalScore (capped at 10
        // per frame), pad with zeros if needed.
        var game = new Game
        {
            GameId = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            VenueId = Guid.NewGuid(),
            GameState = GameState.Completed,
            WhenPlayed = whenPlayed,
            CompletedAt = whenPlayed.AddMinutes(45),
            IsEfrenVariant = efren,
        };

        // Distribute totalScore across 9 frames, ≤ 11 per frame.
        int remaining = totalScore;
        for (int i = 1; i <= 9; i++)
        {
            int share = Math.Min(11, remaining);
            // Split into BreakBonus (0/1) + BallCount (0..10) so the
            // FrameScore math holds and Frame.IsValidScore stays true.
            int breakBonus = share >= 1 && share <= 11 ? Math.Min(share, 1) : 0;
            int ballCount = share - breakBonus;
            game.Frames.Add(new Frame
            {
                FrameId = Guid.NewGuid(),
                GameId = game.GameId,
                FrameNumber = i,
                BreakBonus = breakBonus,
                BallCount = ballCount,
                IsCompleted = true,
            });
            remaining -= share;
        }
        return game;
    }

    [Fact]
    public void ZeroGames_ReturnsZeroRatingZeroConfidence()
    {
        var r = StatisticsService.ComputeRating(GameDiscipline.Standard99, []);

        r.Discipline.Should().Be(GameDiscipline.Standard99);
        r.Rating.Should().Be(0);
        r.GameCount.Should().Be(0);
        r.ConfidencePercent.Should().Be(0);
        r.IsCertified.Should().BeFalse();
        r.UsedHandicapFormula.Should().BeFalse();
        r.HasRating.Should().BeFalse();
    }

    [Fact]
    public void OneGame_SimpleAverage_5PercentConfidence()
    {
        var games = new[] { G(50, DateTime.UtcNow.AddDays(-1)) };

        var r = StatisticsService.ComputeRating(GameDiscipline.Standard99, games);

        r.Rating.Should().Be(50);
        r.GameCount.Should().Be(1);
        r.ConfidencePercent.Should().Be(5);
        r.IsCertified.Should().BeFalse();
        r.UsedHandicapFormula.Should().BeFalse();
    }

    [Fact]
    public void FourGames_StillSimpleAverage()
    {
        var games = new[]
        {
            G(40, DateTime.UtcNow.AddDays(-4)),
            G(50, DateTime.UtcNow.AddDays(-3)),
            G(60, DateTime.UtcNow.AddDays(-2)),
            G(70, DateTime.UtcNow.AddDays(-1)),
        };

        var r = StatisticsService.ComputeRating(GameDiscipline.Standard99, games);

        // (40+50+60+70)/4 = 55
        r.Rating.Should().Be(55);
        r.GameCount.Should().Be(4);
        r.ConfidencePercent.Should().Be(20);
        r.UsedHandicapFormula.Should().BeFalse(
            "fewer than 5 games — simple average, no best-of-5");
    }

    [Fact]
    public void FiveGames_SwitchesToBestFive_NotSimpleAverage()
    {
        var games = new[]
        {
            G(20, DateTime.UtcNow.AddDays(-5)),
            G(30, DateTime.UtcNow.AddDays(-4)),
            G(40, DateTime.UtcNow.AddDays(-3)),
            G(50, DateTime.UtcNow.AddDays(-2)),
            G(60, DateTime.UtcNow.AddDays(-1)),
        };

        var r = StatisticsService.ComputeRating(GameDiscipline.Standard99, games);

        // With exactly 5 games, "best 5 of last 20" = all 5 games. Average = 40.
        r.Rating.Should().Be(40);
        r.GameCount.Should().Be(5);
        r.ConfidencePercent.Should().Be(25);
        r.UsedHandicapFormula.Should().BeTrue();
        r.IsCertified.Should().BeFalse();
    }

    [Fact]
    public void TenGames_AveragesBestFiveOnly_IgnoringWeakest()
    {
        // 10 games with totals 10, 20, 30, ..., 100. Best 5 = 60..100. Avg = 80.
        var games = Enumerable.Range(1, 10)
            .Select(i => G(i * 10, DateTime.UtcNow.AddDays(-i)))
            .ToList();

        var r = StatisticsService.ComputeRating(GameDiscipline.Standard99, games);

        // Best five totals: 100, 90, 80, 70, 60. Wait — game cap is 99, so
        // 100 clamps to 99 by Frame.IsValidScore. Make sure I'm cap-aware.
        // G(100) → frames cap each at 11; total = 9×11 = 99.
        // So best 5 = 99, 90, 80, 70, 60 → mean = 79.8
        r.Rating.Should().Be(79.8);
        r.GameCount.Should().Be(10);
        r.ConfidencePercent.Should().Be(50);
        r.UsedHandicapFormula.Should().BeTrue();
    }

    [Fact]
    public void TwentyOneGames_OnlyLast20Considered_OldestExcluded()
    {
        // Build 21 games. Make the oldest one a fake-perfect (would dominate
        // best-of-5 if included) and the last 20 mediocre, so we can prove
        // the oldest isn't being averaged in.
        var games = new List<Game>();
        // Oldest game: very high (would be best-of-5 if included).
        games.Add(G(99, DateTime.UtcNow.AddDays(-100)));
        // Last 20: each scoring 50.
        for (int i = 0; i < 20; i++)
            games.Add(G(50, DateTime.UtcNow.AddDays(-(i + 1))));

        var r = StatisticsService.ComputeRating(GameDiscipline.Standard99, games);

        // The oldest 99 must NOT be in the best-of-5. Best 5 of last 20 are
        // all 50s → average 50. Total game count is 21 → confidence still
        // 100% (capped at 20 games), certified.
        r.Rating.Should().Be(50);
        r.GameCount.Should().Be(21);
        r.ConfidencePercent.Should().Be(100);
        r.IsCertified.Should().BeTrue();
        r.UsedHandicapFormula.Should().BeTrue();
    }

    [Fact]
    public void TwentyGames_MarkedCertified()
    {
        var games = Enumerable.Range(1, 20)
            .Select(i => G(60, DateTime.UtcNow.AddDays(-i)))
            .ToList();

        var r = StatisticsService.ComputeRating(GameDiscipline.Standard99, games);

        r.GameCount.Should().Be(20);
        r.ConfidencePercent.Should().Be(100);
        r.IsCertified.Should().BeTrue(
            "exactly 20 games is the certification threshold");
        r.UsedHandicapFormula.Should().BeTrue();
    }

    [Fact]
    public void NineteenGames_NotYetCertified_95PercentConfidence()
    {
        var games = Enumerable.Range(1, 19)
            .Select(i => G(60, DateTime.UtcNow.AddDays(-i)))
            .ToList();

        var r = StatisticsService.ComputeRating(GameDiscipline.Standard99, games);

        r.GameCount.Should().Be(19);
        r.ConfidencePercent.Should().Be(95);
        r.IsCertified.Should().BeFalse(
            "certification requires exactly 20 (or more) games of the discipline");
    }

    [Fact]
    public void DisplayRating_RoundsToOneDecimal()
    {
        // Best 5 averages: 11+22+33+44+55 = 165 / 5 = 33.0 (clean).
        // Use mixed values that produce a non-round mean: 11+22+33+44+58 = 168 / 5 = 33.6
        var games = new[]
        {
            G(11, DateTime.UtcNow.AddDays(-5)),
            G(22, DateTime.UtcNow.AddDays(-4)),
            G(33, DateTime.UtcNow.AddDays(-3)),
            G(44, DateTime.UtcNow.AddDays(-2)),
            G(58, DateTime.UtcNow.AddDays(-1)),
        };
        var r = StatisticsService.ComputeRating(GameDiscipline.Standard99, games);
        r.DisplayRating.Should().Be(33.6);
    }

    [Fact]
    public void Discipline_IsCarriedThroughOnTheRecord()
    {
        var standard = StatisticsService.ComputeRating(GameDiscipline.Standard99, []);
        var efren    = StatisticsService.ComputeRating(GameDiscipline.Efren99, []);

        standard.Discipline.Should().Be(GameDiscipline.Standard99);
        efren.Discipline.Should().Be(GameDiscipline.Efren99);
    }
}
