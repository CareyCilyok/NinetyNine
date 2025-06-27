using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using NinetyNine.Model;
using NinetyNine.Presentation.Services;
using ReactiveUI;

namespace NinetyNine.Presentation.ViewModels
{
    public class GamePageViewModel : ViewModelBase
    {
        private readonly IGameService _gameService;
        private Game? _currentGame;
        private string _playerName = "Player 1";
        private string _venueName = "Home Table";
        private TableSize _selectedTableSize = TableSize.NineFoot;

        public GamePageViewModel() : this(new GameService())
        {
        }

        public GamePageViewModel(IGameService gameService)
        {
            _gameService = gameService;
            FrameViewModels = new ObservableCollection<FrameControlViewModel>();

            // Initialize commands
            NewGameCommand = ReactiveCommand.CreateFromTask(CreateNewGameAsync);
            CompleteFrameCommand = ReactiveCommand.CreateFromTask(CompleteCurrentFrameAsync,
                this.WhenAnyValue(x => x.CanCompleteFrame));
            ResetFrameCommand = ReactiveCommand.CreateFromTask(ResetCurrentFrameAsync,
                this.WhenAnyValue(x => x.CanResetFrame));
            CompleteGameCommand = ReactiveCommand.CreateFromTask(CompleteGameAsync,
                this.WhenAnyValue(x => x.CanCompleteGame));

            // Subscribe to game service events
            _gameService.CurrentGameChanged += OnCurrentGameChanged;
            _gameService.FrameCompleted += OnFrameCompleted;
            _gameService.GameCompleted += OnGameCompleted;

            // Initialize with some demo data
            InitializeDemoGame();
        }

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
            }
        }

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
        /// Whether a game is currently in progress
        /// </summary>
        public bool IsGameInProgress => CurrentGame?.IsInProgress ?? false;

        /// <summary>
        /// Whether the current game is completed
        /// </summary>
        public bool IsGameCompleted => CurrentGame?.IsCompleted ?? false;

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
        public ReactiveCommand<Unit, Unit> CompleteFrameCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetFrameCommand { get; }
        public ReactiveCommand<Unit, Unit> CompleteGameCommand { get; }

        #endregion

        #region Command Handlers

        private async Task CreateNewGameAsync()
        {
            try
            {
                // Create demo player and venue
                var player = new Player { PlayerId = Guid.NewGuid(), Name = PlayerName };
                var venue = new Venue { VenueId = Guid.NewGuid(), Name = VenueName };

                await _gameService.CreateNewGameAsync(player, venue, SelectedTableSize);
            }
            catch (Exception ex)
            {
                // In a real app, show error dialog
                System.Diagnostics.Debug.WriteLine($"Error creating new game: {ex.Message}");
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
            this.RaisePropertyChanged(nameof(GameStatus));
        }

        private void UpdateFrameViewModels()
        {
            FrameViewModels.Clear();

            if (CurrentGame?.Frames != null)
            {
                foreach (var frame in CurrentGame.Frames.OrderBy(f => f.FrameNumber))
                {
                    var viewModel = new FrameControlViewModel(frame);
                    FrameViewModels.Add(viewModel);
                }
            }
        }

        private async void InitializeDemoGame()
        {
            // Create a demo game for testing
            await CreateNewGameAsync();
        }

        #endregion
    }
}