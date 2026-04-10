namespace NinetyNine.Services.Models;

/// <summary>
/// Aggregated statistics for a single player.
/// </summary>
public record PlayerStats(
    Guid PlayerId,
    int GamesPlayed,
    int GamesCompleted,
    double AverageScore,
    int BestScore,
    int PerfectGames,
    int PerfectFrames,
    DateTime? LastPlayed);
