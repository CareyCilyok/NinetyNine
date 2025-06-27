/// Copyright (c) 2020-2022
///
/// Permission is hereby granted, free of charge, to any person obtaining a copy
/// of this software and associated documentation files (the "Software"), to deal
/// in the Software without restriction, including without limitation the rights
/// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
/// copies of the Software, and to permit persons to whom the Software is
/// furnished to do so, subject to the following conditions:
/// 
/// The above copyright notice and this permission notice shall be included in all
/// copies or substantial portions of the Software.
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
/// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
/// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
/// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
/// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
/// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
/// SOFTWARE.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NinetyNine.Model;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Service for calculating and managing game statistics
    /// </summary>
    public interface IStatisticsService
    {
        /// <summary>
        /// Gets overall player statistics
        /// </summary>
        /// <param name="playerId">Player ID</param>
        /// <returns>Player statistics</returns>
        Task<PlayerStatistics> GetPlayerStatisticsAsync(Guid playerId);

        /// <summary>
        /// Gets game history for a player
        /// </summary>
        /// <param name="playerId">Player ID</param>
        /// <param name="limit">Maximum number of games to return</param>
        /// <returns>List of recent games</returns>
        Task<List<Game>> GetRecentGamesAsync(Guid playerId, int limit = 10);

        /// <summary>
        /// Gets venue statistics
        /// </summary>
        /// <param name="venueId">Venue ID</param>
        /// <returns>Venue statistics</returns>
        Task<VenueStatistics> GetVenueStatisticsAsync(Guid venueId);

        /// <summary>
        /// Gets all-time leaderboard
        /// </summary>
        /// <param name="limit">Maximum number of players to return</param>
        /// <returns>Leaderboard entries</returns>
        Task<List<LeaderboardEntry>> GetLeaderboardAsync(int limit = 10);

        /// <summary>
        /// Gets frame analysis for improvement suggestions
        /// </summary>
        /// <param name="playerId">Player ID</param>
        /// <returns>Frame analysis data</returns>
        Task<FrameAnalysis> GetFrameAnalysisAsync(Guid playerId);

        /// <summary>
        /// Gets progress tracking over time
        /// </summary>
        /// <param name="playerId">Player ID</param>
        /// <param name="days">Number of days to look back</param>
        /// <returns>Progress data points</returns>
        Task<List<ProgressDataPoint>> GetProgressDataAsync(Guid playerId, int days = 30);
    }

    /// <summary>
    /// Player statistics summary
    /// </summary>
    public class PlayerStatistics
    {
        public int TotalGames { get; set; }
        public int CompletedGames { get; set; }
        public double AverageScore { get; set; }
        public int HighestScore { get; set; }
        public int PerfectFrames { get; set; }
        public double AverageFrameScore { get; set; }
        public int TotalFramesPlayed { get; set; }
        public double BreakSuccessRate { get; set; }
        public Dictionary<TableSize, int> TableSizePreferences { get; set; } = new();
        public Dictionary<int, int> ScoreDistribution { get; set; } = new();
        public DateTime FirstGameDate { get; set; }
        public DateTime LastGameDate { get; set; }
        public int DaysPlaying { get; set; }
        public double ImprovementTrend { get; set; }
    }

    /// <summary>
    /// Venue statistics summary
    /// </summary>
    public class VenueStatistics
    {
        public int TotalGamesPlayed { get; set; }
        public double AverageScore { get; set; }
        public int UniquePlayersCount { get; set; }
        public Dictionary<TableSize, int> TableUsage { get; set; } = new();
        public DateTime FirstGameDate { get; set; }
        public DateTime LastGameDate { get; set; }
    }

    /// <summary>
    /// Leaderboard entry
    /// </summary>
    public class LeaderboardEntry
    {
        public string PlayerName { get; set; } = string.Empty;
        public double AverageScore { get; set; }
        public int HighestScore { get; set; }
        public int TotalGames { get; set; }
        public int PerfectFrames { get; set; }
        public double BreakSuccessRate { get; set; }
        public int Rank { get; set; }
    }

    /// <summary>
    /// Frame analysis for improvement
    /// </summary>
    public class FrameAnalysis
    {
        public double AverageBreakBonus { get; set; }
        public double AverageBallCount { get; set; }
        public Dictionary<int, double> FramePerformance { get; set; } = new();
        public List<string> ImprovementSuggestions { get; set; } = new();
        public int WeakestFrameNumber { get; set; }
        public int StrongestFrameNumber { get; set; }
        public double ConsistencyScore { get; set; }
    }

    /// <summary>
    /// Progress tracking data point
    /// </summary>
    public class ProgressDataPoint
    {
        public DateTime Date { get; set; }
        public double AverageScore { get; set; }
        public int GamesPlayed { get; set; }
        public double TrendLine { get; set; }
    }
}