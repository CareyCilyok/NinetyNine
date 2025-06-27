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
using System.Linq;
using System.Threading.Tasks;
using NinetyNine.Model;

namespace NinetyNine.Presentation.Services
{
    /// <summary>
    /// Service for calculating and managing game statistics
    /// </summary>
    public class StatisticsService : IStatisticsService
    {
        private readonly List<Game> _mockGames;

        public StatisticsService()
        {
            _mockGames = GenerateMockGameData();
        }

        /// <summary>
        /// Gets overall player statistics
        /// </summary>
        public async Task<PlayerStatistics> GetPlayerStatisticsAsync(Guid playerId)
        {
            var playerGames = _mockGames.Where(g => g.PlayerId == playerId).ToList();
            
            if (!playerGames.Any())
            {
                return new PlayerStatistics();
            }

            var completedGames = playerGames.Where(g => g.IsCompleted).ToList();
            var allFrames = completedGames.SelectMany(g => g.Frames).Where(f => f.IsCompleted).ToList();

            var stats = new PlayerStatistics
            {
                TotalGames = playerGames.Count,
                CompletedGames = completedGames.Count,
                AverageScore = completedGames.Any() ? completedGames.Average(g => g.TotalScore) : 0,
                HighestScore = completedGames.Any() ? completedGames.Max(g => g.TotalScore) : 0,
                PerfectFrames = allFrames.Count(f => f.FrameScore == 11),
                AverageFrameScore = allFrames.Any() ? allFrames.Average(f => f.FrameScore) : 0,
                TotalFramesPlayed = allFrames.Count,
                BreakSuccessRate = allFrames.Any() ? allFrames.Average(f => f.BreakBonus) * 100 : 0,
                FirstGameDate = playerGames.Min(g => g.WhenPlayed),
                LastGameDate = playerGames.Max(g => g.WhenPlayed),
                DaysPlaying = (int)(playerGames.Max(g => g.WhenPlayed) - playerGames.Min(g => g.WhenPlayed)).TotalDays + 1
            };

            // Calculate table size preferences
            foreach (var game in completedGames)
            {
                if (stats.TableSizePreferences.ContainsKey(game.TableSize))
                    stats.TableSizePreferences[game.TableSize]++;
                else
                    stats.TableSizePreferences[game.TableSize] = 1;
            }

            // Calculate score distribution
            foreach (var game in completedGames)
            {
                var scoreRange = (game.TotalScore / 10) * 10; // Group by 10s
                if (stats.ScoreDistribution.ContainsKey(scoreRange))
                    stats.ScoreDistribution[scoreRange]++;
                else
                    stats.ScoreDistribution[scoreRange] = 1;
            }

            // Calculate improvement trend (simplified)
            if (completedGames.Count >= 2)
            {
                var recentGames = completedGames.OrderByDescending(g => g.WhenPlayed).Take(5);
                var earlierGames = completedGames.OrderBy(g => g.WhenPlayed).Take(5);
                stats.ImprovementTrend = recentGames.Average(g => g.TotalScore) - earlierGames.Average(g => g.TotalScore);
            }

            return await Task.FromResult(stats);
        }

        /// <summary>
        /// Gets game history for a player
        /// </summary>
        public async Task<List<Game>> GetRecentGamesAsync(Guid playerId, int limit = 10)
        {
            var recentGames = _mockGames
                .Where(g => g.PlayerId == playerId)
                .OrderByDescending(g => g.WhenPlayed)
                .Take(limit)
                .ToList();

            return await Task.FromResult(recentGames);
        }

        /// <summary>
        /// Gets venue statistics
        /// </summary>
        public async Task<VenueStatistics> GetVenueStatisticsAsync(Guid venueId)
        {
            var venueGames = _mockGames.Where(g => g.VenueId == venueId).ToList();
            
            if (!venueGames.Any())
            {
                return new VenueStatistics();
            }

            var stats = new VenueStatistics
            {
                TotalGamesPlayed = venueGames.Count,
                AverageScore = venueGames.Where(g => g.IsCompleted).Average(g => g.TotalScore),
                UniquePlayersCount = venueGames.Select(g => g.PlayerId).Distinct().Count(),
                FirstGameDate = venueGames.Min(g => g.WhenPlayed),
                LastGameDate = venueGames.Max(g => g.WhenPlayed)
            };

            // Calculate table usage
            foreach (var game in venueGames)
            {
                if (stats.TableUsage.ContainsKey(game.TableSize))
                    stats.TableUsage[game.TableSize]++;
                else
                    stats.TableUsage[game.TableSize] = 1;
            }

            return await Task.FromResult(stats);
        }

        /// <summary>
        /// Gets all-time leaderboard
        /// </summary>
        public async Task<List<LeaderboardEntry>> GetLeaderboardAsync(int limit = 10)
        {
            var playerStats = _mockGames
                .Where(g => g.IsCompleted)
                .GroupBy(g => g.PlayerId)
                .Select(group => new LeaderboardEntry
                {
                    PlayerName = group.First().Player?.Name ?? "Unknown Player",
                    AverageScore = group.Average(g => g.TotalScore),
                    HighestScore = group.Max(g => g.TotalScore),
                    TotalGames = group.Count(),
                    PerfectFrames = group.SelectMany(g => g.Frames).Count(f => f.FrameScore == 11),
                    BreakSuccessRate = group.SelectMany(g => g.Frames).Average(f => f.BreakBonus) * 100
                })
                .OrderByDescending(p => p.AverageScore)
                .Take(limit)
                .ToList();

            // Assign ranks
            for (int i = 0; i < playerStats.Count; i++)
            {
                playerStats[i].Rank = i + 1;
            }

            return await Task.FromResult(playerStats);
        }

        /// <summary>
        /// Gets frame analysis for improvement suggestions
        /// </summary>
        public async Task<FrameAnalysis> GetFrameAnalysisAsync(Guid playerId)
        {
            var playerGames = _mockGames.Where(g => g.PlayerId == playerId && g.IsCompleted).ToList();
            var allFrames = playerGames.SelectMany(g => g.Frames).Where(f => f.IsCompleted).ToList();

            if (!allFrames.Any())
            {
                return new FrameAnalysis();
            }

            var analysis = new FrameAnalysis
            {
                AverageBreakBonus = allFrames.Average(f => f.BreakBonus),
                AverageBallCount = allFrames.Average(f => f.BallCount),
                ConsistencyScore = CalculateConsistencyScore(allFrames)
            };

            // Calculate frame performance by frame number
            for (int i = 1; i <= 9; i++)
            {
                var frameData = allFrames.Where(f => f.FrameNumber == i).ToList();
                if (frameData.Any())
                {
                    analysis.FramePerformance[i] = frameData.Average(f => f.FrameScore);
                }
            }

            // Find strongest and weakest frames
            if (analysis.FramePerformance.Any())
            {
                analysis.StrongestFrameNumber = analysis.FramePerformance.OrderByDescending(kvp => kvp.Value).First().Key;
                analysis.WeakestFrameNumber = analysis.FramePerformance.OrderBy(kvp => kvp.Value).First().Key;
            }

            // Generate improvement suggestions
            analysis.ImprovementSuggestions = GenerateImprovementSuggestions(analysis, allFrames);

            return await Task.FromResult(analysis);
        }

        /// <summary>
        /// Gets progress tracking over time
        /// </summary>
        public async Task<List<ProgressDataPoint>> GetProgressDataAsync(Guid playerId, int days = 30)
        {
            var cutoffDate = DateTime.Now.AddDays(-days);
            var recentGames = _mockGames
                .Where(g => g.PlayerId == playerId && g.WhenPlayed >= cutoffDate && g.IsCompleted)
                .OrderBy(g => g.WhenPlayed)
                .ToList();

            var progressData = new List<ProgressDataPoint>();
            var currentDate = cutoffDate.Date;
            var endDate = DateTime.Now.Date;

            while (currentDate <= endDate)
            {
                var dayGames = recentGames.Where(g => g.WhenPlayed.Date == currentDate).ToList();
                
                if (dayGames.Any())
                {
                    progressData.Add(new ProgressDataPoint
                    {
                        Date = currentDate,
                        AverageScore = dayGames.Average(g => g.TotalScore),
                        GamesPlayed = dayGames.Count,
                        TrendLine = CalculateTrendLine(progressData, dayGames.Average(g => g.TotalScore))
                    });
                }

                currentDate = currentDate.AddDays(1);
            }

            return await Task.FromResult(progressData);
        }

        #region Private Methods

        private List<Game> GenerateMockGameData()
        {
            var games = new List<Game>();
            var random = new Random();
            var playerId = Guid.NewGuid();
            var venueId = Guid.NewGuid();

            var player = new Player { PlayerId = playerId, Name = "John Doe", FirstName = "John", LastName = "Doe" };
            var venue = new Venue { VenueId = venueId, Name = "Home Table" };

            // Generate 15 mock games over the past month
            for (int i = 0; i < 15; i++)
            {
                var game = new Game
                {
                    GameId = Guid.NewGuid(),
                    PlayerId = playerId,
                    Player = player,
                    VenueId = venueId,
                    LocationPlayed = venue,
                    TableSize = (TableSize)(random.Next(2, 4) + 5), // 7, 8, or 9 foot
                    WhenPlayed = DateTime.Now.AddDays(-random.Next(0, 30)),
                    GameState = GameState.Completed
                };

                game.InitializeFrames();

                // Simulate playing the game with realistic scores
                for (int frameNum = 1; frameNum <= 9; frameNum++)
                {
                    var frame = game.Frames.First(f => f.FrameNumber == frameNum);
                    
                    // Simulate realistic break bonus (30% success rate)
                    var breakBonus = random.NextDouble() < 0.3 ? 1 : 0;
                    
                    // Simulate realistic ball count (average 6-8 balls)
                    var ballCount = Math.Min(10, Math.Max(0, (int)(random.NextGaussian(7, 2))));
                    
                    // Ensure total doesn't exceed 11
                    if (breakBonus + ballCount > 11)
                        ballCount = 11 - breakBonus;

                    frame.BreakBonus = breakBonus;
                    frame.BallCount = ballCount;
                    frame.CompleteFrame();
                }

                games.Add(game);
            }

            return games;
        }

        private double CalculateConsistencyScore(List<Frame> frames)
        {
            if (!frames.Any()) return 0;
            
            var scores = frames.Select(f => f.FrameScore).ToList();
            var average = scores.Average();
            var variance = scores.Sum(score => Math.Pow(score - average, 2)) / scores.Count;
            var standardDeviation = Math.Sqrt(variance);
            
            // Return a consistency score from 0-100 (lower standard deviation = higher consistency)
            return Math.Max(0, 100 - (standardDeviation * 10));
        }

        private List<string> GenerateImprovementSuggestions(FrameAnalysis analysis, List<Frame> frames)
        {
            var suggestions = new List<string>();

            if (analysis.AverageBreakBonus < 0.3)
            {
                suggestions.Add("Focus on improving your break technique - aim for more break bonuses");
            }

            if (analysis.AverageBallCount < 6)
            {
                suggestions.Add("Work on ball control and positioning to increase your ball count per frame");
            }

            if (analysis.ConsistencyScore < 50)
            {
                suggestions.Add("Practice maintaining consistent performance across all frames");
            }

            if (analysis.FramePerformance.Any() && analysis.FramePerformance.Values.Max() - analysis.FramePerformance.Values.Min() > 3)
            {
                suggestions.Add($"Focus extra practice on Frame {analysis.WeakestFrameNumber} - it's your weakest area");
            }

            if (analysis.FramePerformance.ContainsKey(9) && analysis.FramePerformance[9] < analysis.FramePerformance.Values.Average())
            {
                suggestions.Add("Practice pressure situations - your 9th frame performance could be stronger");
            }

            if (!suggestions.Any())
            {
                suggestions.Add("Great consistency! Keep practicing to maintain your high performance level");
            }

            return suggestions;
        }

        private double CalculateTrendLine(List<ProgressDataPoint> existingData, double currentScore)
        {
            // Simple moving average for trend calculation
            if (existingData.Count < 3) return currentScore;
            
            var recentScores = existingData.TakeLast(5).Select(d => d.AverageScore).ToList();
            recentScores.Add(currentScore);
            
            return recentScores.Average();
        }

        #endregion
    }

    /// <summary>
    /// Extension method for Gaussian random number generation
    /// </summary>
    public static class RandomExtensions
    {
        public static double NextGaussian(this Random random, double mean = 0, double stdDev = 1)
        {
            var u1 = random.NextDouble();
            var u2 = random.NextDouble();
            var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + stdDev * randStdNormal;
        }
    }
}