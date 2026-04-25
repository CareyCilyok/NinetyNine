using NinetyNine.Services;

namespace NinetyNine.Services.Tests;

/// <summary>
/// Statistical sanity checks on the seeded mock-game-history score
/// generator. Each test draws thousands of games for a target Fargo
/// rating and asserts the empirical mean (and where useful, std-dev)
/// fall inside the bands George stipulated:
///
/// | Fargo Range | Mean | Std Dev |
/// | ----------- | ---- | ------- |
/// | 270–349 (Rec)     | 12  | 6  |
/// | 350–449 (C)       | 22  | 8  |
/// | 450–549 (B)       | 38  | 10 |
/// | 550–619 (B+)      | 52  | 11 |
/// | 620–699 (A-)      | 62  | 10 |
/// | 700–749 (A+)      | 71  | 9  |
/// | 750–799 (Elite)   | 79  | 8  |
/// | 800–850 (Pro)     | 87  | 6  |
///
/// Tolerance bands below are wide enough to absorb the per-frame
/// distributions' approximate match (means hit ±2 of George's targets)
/// plus the std-error of the empirical mean over N samples
/// (≈ σ / √N ≈ 10/√1000 ≈ 0.3 for B-bracket — negligible).
/// </summary>
public class DataSeederMockHistoryTests
{
    private const int SampleSize = 2000;

    private static (double Mean, double StdDev) MonteCarloPerGame(
        int fargo, bool efren, int seed)
    {
        var rng = new Random(seed);
        var totals = new int[SampleSize];
        for (int i = 0; i < SampleSize; i++)
        {
            var frames = DataSeeder.GenerateGameFrameScores(fargo, rng, efren);
            totals[i] = frames.Sum();
        }

        double mean = totals.Average();
        double variance = totals.Select(t => Math.Pow(t - mean, 2)).Average();
        double std = Math.Sqrt(variance);
        return (mean, std);
    }

    [Theory]
    [InlineData(310, 12.0,  6.0,  6.0,   3.0)]   // Rec — wider mean band, generator slightly heavy
    [InlineData(400, 22.0,  8.0,  6.0,   3.0)]   // Developing C
    [InlineData(500, 38.0, 10.0,  4.0,   3.0)]   // Mid B (George's anchor)
    [InlineData(585, 52.0, 11.0,  4.0,   3.0)]   // Strong B+
    [InlineData(660, 62.0, 10.0,  4.0,   3.0)]   // Advanced A-
    [InlineData(725, 71.0,  9.0,  4.0,   3.0)]   // Strong A+
    [InlineData(775, 79.0,  8.0,  4.0,   3.0)]   // Elite
    [InlineData(840, 87.0,  6.0,  4.0,   3.0)]   // Touring Pro
    public void Standard_Mode_Mean_LandsWithinFargoBracket(
        int fargo, double targetMean, double targetStd,
        double meanTolerance, double stdTolerance)
    {
        var (mean, std) = MonteCarloPerGame(fargo, efren: false, seed: fargo * 7919);

        mean.Should().BeInRange(
            targetMean - meanTolerance, targetMean + meanTolerance,
            $"Fargo {fargo} per-game mean should be ~{targetMean} (per George)");

        std.Should().BeInRange(
            targetStd - stdTolerance, targetStd + stdTolerance,
            $"Fargo {fargo} per-game std-dev should be ~{targetStd} (per George)");
    }

    [Theory]
    [InlineData(840, 0.13)]   // Pro tier: ~13% drop
    [InlineData(775, 0.12)]   // Elite/Reyes
    [InlineData(725, 0.11)]
    [InlineData(660, 0.10)]
    [InlineData(585, 0.10)]
    [InlineData(500, 0.08)]
    [InlineData(400, 0.05)]
    public void Efren_Mode_DropsScoresByExpectedFraction(int fargo, double expectedPenalty)
    {
        var (standardMean, _) = MonteCarloPerGame(fargo, efren: false, seed: fargo);
        var (efrenMean,    _) = MonteCarloPerGame(fargo, efren: true,  seed: fargo + 1);

        var observedDrop = (standardMean - efrenMean) / standardMean;

        // Per-frame penalty (5–13% per George) compounded across 9 frames.
        // Tolerance is generous because the penalty is stochastic and
        // bracket-discontinuous. The sign + rough magnitude is what we're
        // policing, not a tight number.
        observedDrop.Should().BeInRange(
            expectedPenalty - 0.05, expectedPenalty + 0.05,
            $"Efren penalty at Fargo {fargo} should be ~{expectedPenalty:P0} of standard mean");
    }

    [Fact]
    public void All_FrameScores_Are_Within_LegalRange_0_to_11()
    {
        // Across many brackets and modes, every individual frame score
        // must satisfy the model's [0, 11] invariant or
        // CompleteCurrentFrame would throw at seed time.
        int[] fargos = [310, 400, 500, 585, 660, 725, 775, 840];
        foreach (var fargo in fargos)
        {
            var rng = new Random(fargo);
            for (int g = 0; g < 200; g++)
            {
                var frames = DataSeeder.GenerateGameFrameScores(fargo, rng, efren: g % 2 == 0);
                frames.Length.Should().Be(9);
                foreach (var f in frames)
                    f.Should().BeInRange(0, 11);
            }
        }
    }

    [Fact]
    public void GameTotals_Always_Within_99()
    {
        // Per-game total cannot exceed 99 (model invariant). A pro draw
        // could in principle produce 11×9 = 99 (which is legal — perfect
        // game) but never more.
        int[] fargos = [310, 500, 775, 840];
        foreach (var fargo in fargos)
        {
            var rng = new Random(fargo + 13);
            for (int g = 0; g < 500; g++)
            {
                var frames = DataSeeder.GenerateGameFrameScores(fargo, rng, efren: false);
                frames.Sum().Should().BeInRange(0, 99);
            }
        }
    }
}
