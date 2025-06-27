using System;
using System.Collections.ObjectModel;
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
        private Player _currentPlayer;
        private PlayerStatistics? _playerStatistics;
        private bool _isEditing;
        private bool _isLoading;

        public ProfilePageViewModel() : this(new StatisticsService())
        {
        }

        public ProfilePageViewModel(IStatisticsService statisticsService)
        {
            _statisticsService = statisticsService;
            
            // Initialize with demo player
            _currentPlayer = new Player
            {
                PlayerId = Guid.NewGuid(),
                FirstName = "John",
                LastName = "Doe",
                Username = "johndoe",
                EmailAddress = "john.doe@example.com",
                PhoneNumber = "+1 (555) 123-4567"
            };

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
            _ = RefreshDataAsync();
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
        public string MemberSinceText => $"Member since {PlayerStatistics?.FirstGameDate.ToString("MMM yyyy") ?? "Unknown"}";

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
                    >= 85 => "🏆 Master",
                    >= 75 => "🥇 Expert", 
                    >= 65 => "🥈 Advanced",
                    >= 55 => "🥉 Intermediate",
                    >= 45 => "📈 Improving",
                    _ => "🎱 Beginner"
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

        private void ToggleEditMode()
        {
            IsEditing = !IsEditing;
        }

        private async Task SaveProfileAsync()
        {
            try
            {
                IsLoading = true;
                
                // TODO: Implement actual save to repository
                await Task.Delay(500); // Simulate API call
                
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
            // TODO: Revert any changes
            IsEditing = false;
        }

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;

                // Load player statistics
                PlayerStatistics = await _statisticsService.GetPlayerStatisticsAsync(CurrentPlayer.PlayerId);

                // Load achievements
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
                new Achievement { Title = "First Game", Description = "Completed your first game", Icon = "🎮", IsUnlocked = true },
                new Achievement { Title = "Perfect Frame", Description = "Scored 11 points in a single frame", Icon = "⭐", IsUnlocked = (PlayerStatistics?.PerfectFrames ?? 0) > 0 },
                new Achievement { Title = "High Scorer", Description = "Achieved a score of 80+", Icon = "🎯", IsUnlocked = (PlayerStatistics?.HighestScore ?? 0) >= 80 },
                new Achievement { Title = "Consistent Player", Description = "Played 10 games", Icon = "🏅", IsUnlocked = (PlayerStatistics?.TotalGames ?? 0) >= 10 },
                new Achievement { Title = "Break Master", Description = "70% break success rate", Icon = "💥", IsUnlocked = (PlayerStatistics?.BreakSuccessRate ?? 0) >= 70 },
                new Achievement { Title = "Weekly Warrior", Description = "Play every day for a week", Icon = "📅", IsUnlocked = false }
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
            
            var activities = new[]
            {
                new ActivityItem { Description = "Scored 82 points at Home Table", Date = DateTime.Now.AddHours(-2), Icon = "🎯" },
                new ActivityItem { Description = "Achieved a perfect frame", Date = DateTime.Now.AddDays(-1), Icon = "⭐" },
                new ActivityItem { Description = "Played 3 games today", Date = DateTime.Now.AddDays(-1), Icon = "🎮" },
                new ActivityItem { Description = "Unlocked 'High Scorer' achievement", Date = DateTime.Now.AddDays(-3), Icon = "🏆" },
                new ActivityItem { Description = "Updated profile information", Date = DateTime.Now.AddDays(-5), Icon = "👤" }
            };

            foreach (var activity in activities)
            {
                RecentActivity.Add(activity);
            }

            await Task.CompletedTask;
        }

        private async Task LoadFavoriteVenuesAsync()
        {
            FavoriteVenues.Clear();
            
            var venues = new[]
            {
                new VenueItem { Name = "Home Table", GamesPlayed = 12, AverageScore = 78.5, LastPlayed = DateTime.Now.AddDays(-1) },
                new VenueItem { Name = "Downtown Pool Hall", GamesPlayed = 8, AverageScore = 72.3, LastPlayed = DateTime.Now.AddDays(-7) },
                new VenueItem { Name = "Sports Bar & Grill", GamesPlayed = 5, AverageScore = 69.8, LastPlayed = DateTime.Now.AddDays(-14) }
            };

            foreach (var venue in venues)
            {
                FavoriteVenues.Add(venue);
            }

            await Task.CompletedTask;
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