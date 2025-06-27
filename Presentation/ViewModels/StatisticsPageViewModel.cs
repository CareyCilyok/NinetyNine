using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using NinetyNine.Presentation.Services;
using ReactiveUI;

namespace NinetyNine.Presentation.ViewModels
{
    public class StatisticsPageViewModel : ViewModelBase
    {
        private readonly IStatisticsService _statisticsService;
        private PlayerStatistics? _playerStatistics;
        private FrameAnalysis? _frameAnalysis;
        private bool _isLoading;
        private string _selectedTimeRange = "Last 30 Days";

        public StatisticsPageViewModel() : this(new StatisticsService())
        {
        }

        public StatisticsPageViewModel(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
            
            RecentGames = new ObservableCollection<GameSummary>();
            LeaderboardEntries = new ObservableCollection<LeaderboardEntry>();
            ProgressData = new ObservableCollection<ProgressDataPoint>();
            ImprovementSuggestions = new ObservableCollection<string>();
            
            RefreshDataCommand = ReactiveCommand.CreateFromTask(RefreshDataAsync);
            
            // Load initial data
            _ = RefreshDataAsync();
        }

        #region Properties

        /// <summary>
        /// Player statistics summary
        /// </summary>
        public PlayerStatistics? PlayerStatistics
        {
            get => _playerStatistics;
            private set => this.RaiseAndSetIfChanged(ref _playerStatistics, value);
        }

        /// <summary>
        /// Frame analysis for improvement suggestions
        /// </summary>
        public FrameAnalysis? FrameAnalysis
        {
            get => _frameAnalysis;
            private set => this.RaiseAndSetIfChanged(ref _frameAnalysis, value);
        }

        /// <summary>
        /// Recent games played
        /// </summary>
        public ObservableCollection<GameSummary> RecentGames { get; }

        /// <summary>
        /// Leaderboard entries
        /// </summary>
        public ObservableCollection<LeaderboardEntry> LeaderboardEntries { get; }

        /// <summary>
        /// Progress tracking data
        /// </summary>
        public ObservableCollection<ProgressDataPoint> ProgressData { get; }

        /// <summary>
        /// Improvement suggestions based on analysis
        /// </summary>
        public ObservableCollection<string> ImprovementSuggestions { get; }

        /// <summary>
        /// Whether data is currently loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        /// <summary>
        /// Selected time range for statistics
        /// </summary>
        public string SelectedTimeRange
        {
            get => _selectedTimeRange;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedTimeRange, value);
                _ = RefreshDataAsync();
            }
        }

        /// <summary>
        /// Available time ranges
        /// </summary>
        public string[] TimeRanges { get; } = { "Last 7 Days", "Last 30 Days", "Last 90 Days", "All Time" };

        /// <summary>
        /// Formatted total games text
        /// </summary>
        public string TotalGamesText => $"{PlayerStatistics?.TotalGames ?? 0} Total Games";

        /// <summary>
        /// Formatted completion rate text
        /// </summary>
        public string CompletionRateText
        {
            get
            {
                if (PlayerStatistics?.TotalGames > 0)
                {
                    var rate = (double)PlayerStatistics.CompletedGames / PlayerStatistics.TotalGames * 100;
                    return $"{rate:F1}% Completion Rate";
                }
                return "0% Completion Rate";
            }
        }

        /// <summary>
        /// Formatted average score text
        /// </summary>
        public string AverageScoreText => $"{PlayerStatistics?.AverageScore:F1} Avg Score";

        /// <summary>
        /// Formatted highest score text
        /// </summary>
        public string HighestScoreText => $"{PlayerStatistics?.HighestScore ?? 0} High Score";

        /// <summary>
        /// Formatted perfect frames text
        /// </summary>
        public string PerfectFramesText => $"{PlayerStatistics?.PerfectFrames ?? 0} Perfect Frames";

        /// <summary>
        /// Formatted break success rate text
        /// </summary>
        public string BreakSuccessRateText => $"{PlayerStatistics?.BreakSuccessRate:F1}% Break Success";

        /// <summary>
        /// Formatted improvement trend text
        /// </summary>
        public string ImprovementTrendText
        {
            get
            {
                var trend = PlayerStatistics?.ImprovementTrend ?? 0;
                var direction = trend >= 0 ? "↗" : "↘";
                var color = trend >= 0 ? "Green" : "Red";
                return $"{direction} {Math.Abs(trend):F1} points";
            }
        }

        /// <summary>
        /// Formatted consistency score text
        /// </summary>
        public string ConsistencyScoreText => $"{FrameAnalysis?.ConsistencyScore:F1}% Consistency";

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> RefreshDataCommand { get; }

        #endregion

        #region Private Methods

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;

                // Use a demo player ID for now
                var demoPlayerId = Guid.NewGuid();

                // Load all statistics data
                var playerStatsTask = _statisticsService.GetPlayerStatisticsAsync(demoPlayerId);
                var frameAnalysisTask = _statisticsService.GetFrameAnalysisAsync(demoPlayerId);
                var recentGamesTask = _statisticsService.GetRecentGamesAsync(demoPlayerId, 10);
                var leaderboardTask = _statisticsService.GetLeaderboardAsync(10);
                var progressTask = _statisticsService.GetProgressDataAsync(demoPlayerId, GetDaysFromTimeRange());

                await Task.WhenAll(playerStatsTask, frameAnalysisTask, recentGamesTask, leaderboardTask, progressTask);

                PlayerStatistics = await playerStatsTask;
                FrameAnalysis = await frameAnalysisTask;

                // Update recent games
                RecentGames.Clear();
                var recentGames = await recentGamesTask;
                foreach (var game in recentGames)
                {
                    RecentGames.Add(new GameSummary
                    {
                        Date = game.WhenPlayed,
                        Score = game.TotalScore,
                        Venue = game.LocationPlayed?.Name ?? "Unknown",
                        TableSize = game.TableSize.ToString(),
                        Duration = "~45 min", // Mock duration
                        Status = game.IsCompleted ? "Completed" : "In Progress"
                    });
                }

                // Update leaderboard
                LeaderboardEntries.Clear();
                var leaderboard = await leaderboardTask;
                foreach (var entry in leaderboard)
                {
                    LeaderboardEntries.Add(entry);
                }

                // Update progress data
                ProgressData.Clear();
                var progressData = await progressTask;
                foreach (var point in progressData)
                {
                    ProgressData.Add(point);
                }

                // Update improvement suggestions
                ImprovementSuggestions.Clear();
                foreach (var suggestion in FrameAnalysis?.ImprovementSuggestions ?? new())
                {
                    ImprovementSuggestions.Add(suggestion);
                }

                // Notify UI of property changes
                this.RaisePropertyChanged(nameof(TotalGamesText));
                this.RaisePropertyChanged(nameof(CompletionRateText));
                this.RaisePropertyChanged(nameof(AverageScoreText));
                this.RaisePropertyChanged(nameof(HighestScoreText));
                this.RaisePropertyChanged(nameof(PerfectFramesText));
                this.RaisePropertyChanged(nameof(BreakSuccessRateText));
                this.RaisePropertyChanged(nameof(ImprovementTrendText));
                this.RaisePropertyChanged(nameof(ConsistencyScoreText));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing statistics: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private int GetDaysFromTimeRange()
        {
            return SelectedTimeRange switch
            {
                "Last 7 Days" => 7,
                "Last 30 Days" => 30,
                "Last 90 Days" => 90,
                "All Time" => 365,
                _ => 30
            };
        }

        #endregion
    }

    /// <summary>
    /// Game summary for display in recent games list
    /// </summary>
    public class GameSummary
    {
        public DateTime Date { get; set; }
        public int Score { get; set; }
        public string Venue { get; set; } = string.Empty;
        public string TableSize { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FormattedDate => Date.ToString("MMM dd, yyyy");
        public string ScoreText => $"{Score}/99";
    }
}