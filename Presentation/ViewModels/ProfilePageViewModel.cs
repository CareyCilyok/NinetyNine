using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using NinetyNine.Model;
using NinetyNine.Presentation.Services;
using ReactiveUI;

namespace NinetyNine.Presentation.ViewModels
{
    public class ProfilePageViewModel : ViewModelBase
    {
        private readonly IStatisticsService _statisticsService;
        private readonly IPlayerService _playerService;
        private Player _currentPlayer = null!;
        private Player? _playerBackup;
        private PlayerStatistics? _playerStatistics;
        private bool _isEditing;
        private bool _isLoading;
        private bool _isInitialized;

        public ProfilePageViewModel() : this(new StatisticsService(), new PlayerService())
        {
        }

        public ProfilePageViewModel(IStatisticsService statisticsService, IPlayerService playerService)
        {
            _statisticsService = statisticsService;
            _playerService = playerService;

            Achievements = new ObservableCollection<Achievement>();
            RecentActivity = new ObservableCollection<ActivityItem>();
            FavoriteVenues = new ObservableCollection<VenueItem>();

            EditProfileCommand = ReactiveCommand.Create(ToggleEditMode);
            SaveProfileCommand = ReactiveCommand.CreateFromTask(SaveProfileAsync,
                this.WhenAnyValue(x => x.IsEditing));
            CancelEditCommand = ReactiveCommand.Create(CancelEdit,
                this.WhenAnyValue(x => x.IsEditing));
            RefreshDataCommand = ReactiveCommand.CreateFromTask(RefreshDataAsync);

            // Load initial data
            _ = InitializeAndRefreshAsync();
        }

        #region Properties

        /// <summary>
        /// Current player profile
        /// </summary>
        public Player CurrentPlayer
        {
            get => _currentPlayer;
            set => this.RaiseAndSetIfChanged(ref _currentPlayer, value);
        }

        /// <summary>
        /// Player statistics
        /// </summary>
        public PlayerStatistics? PlayerStatistics
        {
            get => _playerStatistics;
            private set => this.RaiseAndSetIfChanged(ref _playerStatistics, value);
        }

        /// <summary>
        /// Whether profile is in edit mode
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            private set => this.RaiseAndSetIfChanged(ref _isEditing, value);
        }

        /// <summary>
        /// Whether data is loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        /// <summary>
        /// Player achievements
        /// </summary>
        public ObservableCollection<Achievement> Achievements { get; }

        /// <summary>
        /// Recent activity items
        /// </summary>
        public ObservableCollection<ActivityItem> RecentActivity { get; }

        /// <summary>
        /// Favorite venues
        /// </summary>
        public ObservableCollection<VenueItem> FavoriteVenues { get; }

        /// <summary>
        /// Formatted player name
        /// </summary>
        public string PlayerName => CurrentPlayer?.Name ?? "Unknown Player";

        /// <summary>
        /// Formatted member since date
        /// </summary>
        public string MemberSinceText
        {
            get
            {
                if (PlayerStatistics?.FirstGameDate != null && PlayerStatistics.FirstGameDate != default)
                {
                    return $"Member since {PlayerStatistics.FirstGameDate:MMM yyyy}";
                }
                return "No games yet";
            }
        }

        /// <summary>
        /// Formatted total games text
        /// </summary>
        public string TotalGamesText => $"{PlayerStatistics?.TotalGames ?? 0} Games Played";

        /// <summary>
        /// Formatted average score text
        /// </summary>
        public string AverageScoreText => $"{PlayerStatistics?.AverageScore:F1} Average Score";

        /// <summary>
        /// Formatted highest score text
        /// </summary>
        public string HighestScoreText => $"{PlayerStatistics?.HighestScore ?? 0} Highest Score";

        /// <summary>
        /// Formatted playing time text
        /// </summary>
        public string PlayingTimeText => $"{PlayerStatistics?.DaysPlaying ?? 0} Days Playing";

        /// <summary>
        /// Player rank/level calculation
        /// </summary>
        public string PlayerRank
        {
            get
            {
                var avgScore = PlayerStatistics?.AverageScore ?? 0;
                return avgScore switch
                {
                    >= 85 => "Master",
                    >= 75 => "Expert",
                    >= 65 => "Advanced",
                    >= 55 => "Intermediate",
                    >= 45 => "Improving",
                    _ => "Beginner"
                };
            }
        }

        /// <summary>
        /// Player level progress (0-100)
        /// </summary>
        public double LevelProgress
        {
            get
            {
                var avgScore = PlayerStatistics?.AverageScore ?? 0;
                var baseLevel = (int)(avgScore / 10) * 10;
                var progress = ((avgScore - baseLevel) / 10) * 100;
                return Math.Max(0, Math.Min(100, progress));
            }
        }

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> EditProfileCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveProfileCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelEditCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshDataCommand { get; }

        #endregion

        #region Private Methods

        private async Task InitializeAndRefreshAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Initialize player service to get the real player
                await _playerService.InitializeAsync();
                CurrentPlayer = _playerService.CurrentPlayer;
                _isInitialized = true;

                // Now refresh with real player data
                await RefreshDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing ProfilePageViewModel: {ex.Message}");
            }
        }

        private void ToggleEditMode()
        {
            if (!IsEditing)
            {
                // Entering edit mode - create backup of current player
                _playerBackup = new Player
                {
                    PlayerId = CurrentPlayer.PlayerId,
                    FirstName = CurrentPlayer.FirstName,
                    LastName = CurrentPlayer.LastName,
                    Username = CurrentPlayer.Username,
                    EmailAddress = CurrentPlayer.EmailAddress,
                    PhoneNumber = CurrentPlayer.PhoneNumber,
                    MiddleName = CurrentPlayer.MiddleName
                };
            }
            IsEditing = !IsEditing;
        }

        private async Task SaveProfileAsync()
        {
            try
            {
                IsLoading = true;

                // Save via PlayerService
                await _playerService.UpdatePlayerAsync(CurrentPlayer);

                // Clear backup after successful save
                _playerBackup = null;
                IsEditing = false;

                // Notify property changes
                this.RaisePropertyChanged(nameof(PlayerName));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving profile: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CancelEdit()
        {
            // Revert to backup if available
            if (_playerBackup != null)
            {
                CurrentPlayer.FirstName = _playerBackup.FirstName;
                CurrentPlayer.LastName = _playerBackup.LastName;
                CurrentPlayer.Username = _playerBackup.Username;
                CurrentPlayer.EmailAddress = _playerBackup.EmailAddress;
                CurrentPlayer.PhoneNumber = _playerBackup.PhoneNumber;
                CurrentPlayer.MiddleName = _playerBackup.MiddleName;

                _playerBackup = null;

                // Notify property changes
                this.RaisePropertyChanged(nameof(CurrentPlayer));
                this.RaisePropertyChanged(nameof(PlayerName));
            }

            IsEditing = false;
        }

        private async Task RefreshDataAsync()
        {
            if (!_isInitialized) return;

            try
            {
                IsLoading = true;

                // Invalidate stats cache to get fresh data
                _statisticsService.InvalidateCache();

                // Load player statistics using real player ID
                PlayerStatistics = await _statisticsService.GetPlayerStatisticsAsync(CurrentPlayer.PlayerId);

                // Load achievements (based on real stats)
                await LoadAchievementsAsync();

                // Load recent activity
                await LoadRecentActivityAsync();

                // Load favorite venues
                await LoadFavoriteVenuesAsync();

                // Notify property changes
                this.RaisePropertyChanged(nameof(PlayerName));
                this.RaisePropertyChanged(nameof(MemberSinceText));
                this.RaisePropertyChanged(nameof(TotalGamesText));
                this.RaisePropertyChanged(nameof(AverageScoreText));
                this.RaisePropertyChanged(nameof(HighestScoreText));
                this.RaisePropertyChanged(nameof(PlayingTimeText));
                this.RaisePropertyChanged(nameof(PlayerRank));
                this.RaisePropertyChanged(nameof(LevelProgress));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing profile data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAchievementsAsync()
        {
            Achievements.Clear();

            var achievements = new[]
            {
                new Achievement { Title = "First Game", Description = "Completed your first game", Icon = "Game", IsUnlocked = (PlayerStatistics?.CompletedGames ?? 0) > 0 },
                new Achievement { Title = "Perfect Frame", Description = "Scored 11 points in a single frame", Icon = "Star", IsUnlocked = (PlayerStatistics?.PerfectFrames ?? 0) > 0 },
                new Achievement { Title = "High Scorer", Description = "Achieved a score of 80+", Icon = "Target", IsUnlocked = (PlayerStatistics?.HighestScore ?? 0) >= 80 },
                new Achievement { Title = "Consistent Player", Description = "Played 10 games", Icon = "Medal", IsUnlocked = (PlayerStatistics?.TotalGames ?? 0) >= 10 },
                new Achievement { Title = "Break Master", Description = "70% break success rate", Icon = "Zap", IsUnlocked = (PlayerStatistics?.BreakSuccessRate ?? 0) >= 70 }
            };

            foreach (var achievement in achievements)
            {
                Achievements.Add(achievement);
            }

            await Task.CompletedTask;
        }

        private async Task LoadRecentActivityAsync()
        {
            RecentActivity.Clear();

            // Get recent games from statistics service
            var recentGames = await _statisticsService.GetRecentGamesAsync(CurrentPlayer.PlayerId, 5);

            foreach (var game in recentGames)
            {
                var description = game.IsCompleted
                    ? $"Scored {game.TotalScore} points at {game.LocationPlayed?.Name ?? "Unknown"}"
                    : $"Game in progress at {game.LocationPlayed?.Name ?? "Unknown"}";

                RecentActivity.Add(new ActivityItem
                {
                    Description = description,
                    Date = game.WhenPlayed,
                    Icon = "Game"
                });
            }
        }

        private async Task LoadFavoriteVenuesAsync()
        {
            FavoriteVenues.Clear();

            // For now, just show venues from recent games
            var recentGames = await _statisticsService.GetRecentGamesAsync(CurrentPlayer.PlayerId, 20);

            var venueStats = new System.Collections.Generic.Dictionary<string, (int count, double totalScore, DateTime lastPlayed)>();

            foreach (var game in recentGames.Where(g => g.IsCompleted))
            {
                var venueName = game.LocationPlayed?.Name ?? "Unknown";
                if (venueStats.ContainsKey(venueName))
                {
                    var (count, totalScore, lastPlayed) = venueStats[venueName];
                    venueStats[venueName] = (count + 1, totalScore + game.TotalScore, game.WhenPlayed > lastPlayed ? game.WhenPlayed : lastPlayed);
                }
                else
                {
                    venueStats[venueName] = (1, game.TotalScore, game.WhenPlayed);
                }
            }

            foreach (var (venueName, stats) in venueStats.OrderByDescending(kvp => kvp.Value.count).Take(3))
            {
                FavoriteVenues.Add(new VenueItem
                {
                    Name = venueName,
                    GamesPlayed = stats.count,
                    AverageScore = stats.totalScore / stats.count,
                    LastPlayed = stats.lastPlayed
                });
            }
        }

        #endregion
    }

    /// <summary>
    /// Achievement model for display
    /// </summary>
    public class Achievement
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool IsUnlocked { get; set; }
        public string UnlockedText => IsUnlocked ? "Unlocked" : "Locked";
        public string BackgroundColor => IsUnlocked ? "Gold" : "LightGray";
    }

    /// <summary>
    /// Activity item for recent activity
    /// </summary>
    public class ActivityItem
    {
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Icon { get; set; } = string.Empty;
        public string TimeAgo => GetTimeAgo(Date);

        private string GetTimeAgo(DateTime date)
        {
            var timeSpan = DateTime.Now - date;
            return timeSpan.TotalMinutes switch
            {
                < 1 => "Just now",
                < 60 => $"{(int)timeSpan.TotalMinutes}m ago",
                < 1440 => $"{(int)timeSpan.TotalHours}h ago",
                _ => $"{(int)timeSpan.TotalDays}d ago"
            };
        }
    }

    /// <summary>
    /// Venue item for favorites
    /// </summary>
    public class VenueItem
    {
        public string Name { get; set; } = string.Empty;
        public int GamesPlayed { get; set; }
        public double AverageScore { get; set; }
        public DateTime LastPlayed { get; set; }
        public string GamesPlayedText => $"{GamesPlayed} games";
        public string AverageScoreText => $"{AverageScore:F1} avg";
        public string LastPlayedText => $"Last: {LastPlayed:MMM dd}";
    }
}
