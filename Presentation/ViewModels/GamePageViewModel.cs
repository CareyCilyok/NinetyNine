using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using NinetyNine.Model;
using NinetyNine.Presentation.Services;
using ReactiveUI;

namespace NinetyNine.Presentation.ViewModels
{
    public class GamePageViewModel : ViewModelBase
    {
        private readonly IGameService _gameService;
        private readonly IPlayerService _playerService;
        private readonly ICelebrationService _celebrationService;
        private Game? _currentGame;
        private Game? _inProgressGame;
        private string _playerName = "Player 1";
        private string _venueName = "Home Table";
        private TableSize _selectedTableSize = TableSize.NineFoot;
        private bool _showConfetti;
        private bool _isInitialized;

        public GamePageViewModel() : this(new GameService(), new PlayerService(), CelebrationService.Instance)
        {
        }

        public GamePageViewModel(IGameService gameService) : this(gameService, new PlayerService(), CelebrationService.Instance)
        {
        }

        public GamePageViewModel(IGameService gameService, ICelebrationService celebrationService)
            : this(gameService, new PlayerService(), celebrationService)
        {
        }

        public GamePageViewModel(IGameService gameService, IPlayerService playerService, ICelebrationService celebrationService)
        {
            _gameService = gameService;
            _playerService = playerService;
            _celebrationService = celebrationService;
            FrameViewModels = new ObservableCollection<FrameControlViewModel>();

            // Initialize commands
            NewGameCommand = ReactiveCommand.CreateFromTask(CreateNewGameAsync);
            ResumeGameCommand = ReactiveCommand.CreateFromTask(ResumeInProgressGameAsync,
                this.WhenAnyValue(x => x.HasInProgressGame));
            CompleteFrameCommand = ReactiveCommand.CreateFromTask(CompleteCurrentFrameAsync,
                this.WhenAnyValue(x => x.CanCompleteFrame));
            ResetFrameCommand = ReactiveCommand.CreateFromTask(ResetCurrentFrameAsync,
                this.WhenAnyValue(x => x.CanResetFrame));
            CompleteGameCommand = ReactiveCommand.CreateFromTask(CompleteGameAsync,
                this.WhenAnyValue(x => x.CanCompleteGame));
            UndoLastFrameCommand = ReactiveCommand.CreateFromTask(UndoLastFrameAsync,
                this.WhenAnyValue(x => x.CanUndoLastFrame));

            // Subscribe to game service events
            _gameService.CurrentGameChanged += OnCurrentGameChanged;
            _gameService.FrameCompleted += OnFrameCompleted;
            _gameService.GameCompleted += OnGameCompleted;

            // Initialize asynchronously
            _ = InitializeAsync();
        }

        #region Initialization

        private async Task InitializeAsync()
        {
            if (_isInitialized) return;

            try
            {
                // Initialize player service (loads or creates default player)
                await _playerService.InitializeAsync();

                // Set player name from persisted profile
                PlayerName = _playerService.CurrentPlayer.Name;

                // Check for in-progress games
                var inProgressGame = await _gameService.GetMostRecentInProgressGameAsync();
                if (inProgressGame != null)
                {
                    InProgressGame = inProgressGame;
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing GamePageViewModel: {ex.Message}");
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// The current game being played
        /// </summary>
        public Game? CurrentGame
        {
            get => _currentGame;
            private set
            {
                this.RaiseAndSetIfChanged(ref _currentGame, value);
                UpdateFrameViewModels();
                this.RaisePropertyChanged(nameof(IsGameInProgress));
                this.RaisePropertyChanged(nameof(IsGameCompleted));
                this.RaisePropertyChanged(nameof(CurrentFrameNumber));
                this.RaisePropertyChanged(nameof(TotalScore));
                this.RaisePropertyChanged(nameof(GameStatus));
                this.RaisePropertyChanged(nameof(ShowStartPanel));
            }
        }

        /// <summary>
        /// An in-progress game that can be resumed
        /// </summary>
        public Game? InProgressGame
        {
            get => _inProgressGame;
            private set
            {
                this.RaiseAndSetIfChanged(ref _inProgressGame, value);
                this.RaisePropertyChanged(nameof(HasInProgressGame));
                this.RaisePropertyChanged(nameof(ResumeGameText));
            }
        }

        /// <summary>
        /// Whether there is an in-progress game to resume
        /// </summary>
        public bool HasInProgressGame => InProgressGame != null && CurrentGame == null;

        /// <summary>
        /// Text for the resume game button
        /// </summary>
        public string ResumeGameText
        {
            get
            {
                if (InProgressGame == null) return "Resume Game";
                return $"Resume Game (Frame {InProgressGame.CurrentFrameNumber}, Score: {InProgressGame.TotalScore})";
            }
        }

        /// <summary>
        /// Whether to show the start panel (no game active)
        /// </summary>
        public bool ShowStartPanel => !IsGameInProgress && !IsGameCompleted;

        /// <summary>
        /// ViewModels for the 9 frame controls
        /// </summary>
        public ObservableCollection<FrameControlViewModel> FrameViewModels { get; }

        /// <summary>
        /// Player name for new games
        /// </summary>
        public string PlayerName
        {
            get => _playerName;
            set => this.RaiseAndSetIfChanged(ref _playerName, value);
        }

        /// <summary>
        /// Venue name for new games
        /// </summary>
        public string VenueName
        {
            get => _venueName;
            set => this.RaiseAndSetIfChanged(ref _venueName, value);
        }

        /// <summary>
        /// Selected table size for new games
        /// </summary>
        public TableSize SelectedTableSize
        {
            get => _selectedTableSize;
            set => this.RaiseAndSetIfChanged(ref _selectedTableSize, value);
        }

        /// <summary>
        /// Available table sizes for selection
        /// </summary>
        public TableSizeItem[] TableSizes { get; } = new[]
        {
            new TableSizeItem(TableSize.SevenFoot, "7-Foot"),
            new TableSizeItem(TableSize.NineFoot, "9-Foot"),
            new TableSizeItem(TableSize.TenFoot, "10-Foot")
        };

        /// <summary>
        /// Whether the last frame can be undone
        /// </summary>
        public bool CanUndoLastFrame =>
            CurrentGame?.IsInProgress == true &&
            CurrentGame.CurrentFrameNumber > 1 &&
            CurrentGame.Frames.Any(f => f.FrameNumber == CurrentGame.CurrentFrameNumber - 1 && f.IsCompleted);

        /// <summary>
        /// Whether a game is currently in progress
        /// </summary>
        public bool IsGameInProgress => CurrentGame?.IsInProgress ?? false;

        /// <summary>
        /// Whether the current game is completed
        /// </summary>
        public bool IsGameCompleted => CurrentGame?.IsCompleted ?? false;

        /// <summary>
        /// Whether the current game is a perfect game (99 points)
        /// </summary>
        public bool IsPerfectGame => CurrentGame?.IsPerfectGame ?? false;

        /// <summary>
        /// Whether to show confetti celebration
        /// </summary>
        public bool ShowConfetti
        {
            get => _showConfetti;
            private set => this.RaiseAndSetIfChanged(ref _showConfetti, value);
        }

        /// <summary>
        /// Current frame number being played
        /// </summary>
        public int CurrentFrameNumber => CurrentGame?.CurrentFrameNumber ?? 0;

        /// <summary>
        /// Total score for the current game
        /// </summary>
        public int TotalScore => CurrentGame?.TotalScore ?? 0;

        /// <summary>
        /// Game status text
        /// </summary>
        public string GameStatus
        {
            get
            {
                if (CurrentGame == null)
                    return "No game in progress";

                return CurrentGame.GameState switch
                {
                    GameState.NotStarted => "Game not started",
                    GameState.InProgress => $"Frame {CurrentFrameNumber} of 9 - Score: {TotalScore}",
                    GameState.Completed when IsPerfectGame => "LEGENDARY! PERFECT 99!",
                    GameState.Completed => $"Game Complete! Final Score: {TotalScore}/99",
                    GameState.Paused => "Game Paused",
                    _ => "Unknown status"
                };
            }
        }

        /// <summary>
        /// Whether the current frame can be completed
        /// </summary>
        public bool CanCompleteFrame =>
            CurrentGame?.IsInProgress == true &&
            CurrentGame.CurrentFrame?.IsActive == true &&
            CurrentGame.CurrentFrame.IsValidScore;

        /// <summary>
        /// Whether the current frame can be reset
        /// </summary>
        public bool CanResetFrame =>
            CurrentGame?.IsInProgress == true &&
            CurrentGame.CurrentFrame?.IsActive == true;

        /// <summary>
        /// Whether the game can be completed
        /// </summary>
        public bool CanCompleteGame => CurrentGame?.IsInProgress == true;

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> NewGameCommand { get; }
        public ReactiveCommand<Unit, Unit> ResumeGameCommand { get; }
        public ReactiveCommand<Unit, Unit> CompleteFrameCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetFrameCommand { get; }
        public ReactiveCommand<Unit, Unit> CompleteGameCommand { get; }
        public ReactiveCommand<Unit, Unit> UndoLastFrameCommand { get; }

        #endregion

        #region Command Handlers

        private async Task CreateNewGameAsync()
        {
            try
            {
                // Use persisted player
                var player = _playerService.CurrentPlayer;

                // Update player name if changed in UI
                if (player.Name != PlayerName)
                {
                    player.Name = PlayerName;
                    await _playerService.UpdatePlayerAsync(player);
                }

                var venue = new Venue { VenueId = Guid.NewGuid(), Name = VenueName };

                await _gameService.CreateNewGameAsync(player, venue, SelectedTableSize);

                // Clear the in-progress game reference since we're starting fresh
                InProgressGame = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating new game: {ex.Message}");
            }
        }

        private async Task ResumeInProgressGameAsync()
        {
            if (InProgressGame == null) return;

            try
            {
                // Load the in-progress game
                await _gameService.LoadGameAsync(InProgressGame.GameId);
                InProgressGame = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resuming game: {ex.Message}");
            }
        }

        private async Task CompleteCurrentFrameAsync()
        {
            if (CurrentGame?.CurrentFrame == null) return;

            try
            {
                var currentFrame = CurrentGame.CurrentFrame;
                await _gameService.CompleteCurrentFrameAsync(
                    currentFrame.BreakBonus,
                    currentFrame.BallCount,
                    currentFrame.Notes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error completing frame: {ex.Message}");
            }
        }

        private async Task ResetCurrentFrameAsync()
        {
            try
            {
                await _gameService.ResetCurrentFrameAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting frame: {ex.Message}");
            }
        }

        private async Task CompleteGameAsync()
        {
            try
            {
                await _gameService.CompleteGameAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error completing game: {ex.Message}");
            }
        }

        private async Task UndoLastFrameAsync()
        {
            if (CurrentGame == null || CurrentGame.CurrentFrameNumber <= 1) return;

            try
            {
                await _gameService.UndoLastFrameAsync();

                // Refresh UI
                UpdateFrameViewModels();
                this.RaisePropertyChanged(nameof(TotalScore));
                this.RaisePropertyChanged(nameof(CurrentFrameNumber));
                this.RaisePropertyChanged(nameof(GameStatus));
                this.RaisePropertyChanged(nameof(CanCompleteFrame));
                this.RaisePropertyChanged(nameof(CanUndoLastFrame));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error undoing last frame: {ex.Message}");
            }
        }

        #endregion

        #region Private Methods

        private void OnCurrentGameChanged(object? sender, Game? game)
        {
            CurrentGame = game;
        }

        private void OnFrameCompleted(object? sender, Frame frame)
        {
            // Update UI properties
            this.RaisePropertyChanged(nameof(TotalScore));
            this.RaisePropertyChanged(nameof(CurrentFrameNumber));
            this.RaisePropertyChanged(nameof(GameStatus));
            this.RaisePropertyChanged(nameof(CanCompleteFrame));
        }

        private void OnGameCompleted(object? sender, Game game)
        {
            // Update UI properties
            this.RaisePropertyChanged(nameof(IsGameCompleted));
            this.RaisePropertyChanged(nameof(IsPerfectGame));
            this.RaisePropertyChanged(nameof(GameStatus));
            this.RaisePropertyChanged(nameof(ShowStartPanel));

            // Trigger celebrations
            if (IsPerfectGame)
            {
                ShowConfetti = true;
                _celebrationService.TriggerPerfectGame(TotalScore);
            }
            else
            {
                _celebrationService.TriggerGameCompleted(TotalScore);
            }
        }

        private void UpdateFrameViewModels()
        {
            FrameViewModels.Clear();

            if (CurrentGame?.Frames != null)
            {
                foreach (var frame in CurrentGame.Frames.OrderBy(f => f.FrameNumber))
                {
                    var viewModel = new FrameControlViewModel(frame, _celebrationService);
                    FrameViewModels.Add(viewModel);
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// TableSize item for ComboBox binding with display text
    /// </summary>
    public class TableSizeItem
    {
        public TableSize Value { get; }
        public string DisplayName { get; }

        public TableSizeItem(TableSize value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public override string ToString() => DisplayName;
    }
}
