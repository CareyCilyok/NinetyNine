namespace NinetyNine.Services.Models;

/// <summary>
/// A single entry in the global leaderboard.
/// </summary>
public record LeaderboardEntry(
    Guid PlayerId,
    string DisplayName,
    string? AvatarUrl,
    int GamesPlayed,
    double AverageScore,
    int BestScore);
