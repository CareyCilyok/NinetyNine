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
using System.Reactive;
using System.Threading.Tasks;
using NinetyNine.Model;
using NinetyNine.Presentation.Services;
using ReactiveUI;

namespace NinetyNine.Presentation.ViewModels
{
    /// <summary>
    /// ViewModel for controlling game play
    /// </summary>
    public class GameControlViewModel : ViewModelBase
    {
        private readonly IGameService _gameService;
        private Game? _currentGame;
        private Frame? _currentFrame;
        private int _breakBonus;
        private int _ballCount;
        private string _frameNotes = string.Empty;
        private bool _isGameActive;

        public GameControlViewModel() : this(new GameService())
        {
        }

        public GameControlViewModel(IGameService gameService)
        {
            _gameService = gameService;

            // Subscribe to game service events
            _gameService.CurrentGameChanged += OnCurrentGameChanged;
            _gameService.FrameCompleted += OnFrameCompleted;
            _gameService.GameCompleted += OnGameCompleted;

            // Initialize commands
            StartNewGameCommand = ReactiveCommand.CreateFromTask(StartNewGameAsync);
            CompleteFrameCommand = ReactiveCommand.CreateFromTask(CompleteFrameAsync,
                this.WhenAnyValue(x => x.IsGameActive, x => x.CurrentFrame,
                    (active, frame) => active && frame != null));
            ResetFrameCommand = ReactiveCommand.CreateFromTask(ResetFrameAsync,
                this.WhenAnyValue(x => x.IsGameActive));
            PauseGameCommand = ReactiveCommand.CreateFromTask(PauseGameAsync,
                this.WhenAnyValue(x => x.IsGameActive));
            ResumeGameCommand = ReactiveCommand.CreateFromTask(ResumeGameAsync,
                this.WhenAnyValue(x => x.CurrentGame, game => game?.GameState == GameState.Paused));
        }

        #region Properties

        /// <summary>
        /// Current game being played
        /// </summary>
        public Game? CurrentGame
        {
            get => _currentGame;
            private set
            {
                this.RaiseAndSetIfChanged(ref _currentGame, value);
                this.RaisePropertyChanged(nameof(GameStatusText));
                this.RaisePropertyChanged(nameof(TotalScoreText));
                this.RaisePropertyChanged(nameof(CurrentFrameNumber));
            }
        }

        /// <summary>
        /// Current frame being played
        /// </summary>
        public Frame? CurrentFrame
        {
            get => _currentFrame;
            private set
            {
                this.RaiseAndSetIfChanged(ref _currentFrame, value);
                this.RaisePropertyChanged(nameof(FrameScoreText));
            }
        }

        /// <summary>
        /// Break bonus for current frame (0 or 1)
        /// </summary>
        public int BreakBonus
        {
            get => _breakBonus;
            set
            {
                var newValue = Math.Max(0, Math.Min(1, value));
                this.RaiseAndSetIfChanged(ref _breakBonus, newValue);
                this.RaisePropertyChanged(nameof(FrameScoreText));
            }
        }

        /// <summary>
        /// Ball count for current frame (0-10)
        /// </summary>
        public int BallCount
        {
            get => _ballCount;
            set
            {
                var newValue = Math.Max(0, Math.Min(10, value));
                // Ensure total score doesn't exceed 11
                if (BreakBonus + newValue > 11)
                {
                    newValue = 11 - BreakBonus;
                }
                this.RaiseAndSetIfChanged(ref _ballCount, newValue);
                this.RaisePropertyChanged(nameof(FrameScoreText));
            }
        }

        /// <summary>
        /// Notes for the current frame
        /// </summary>
        public string FrameNotes
        {
            get => _frameNotes;
            set => this.RaiseAndSetIfChanged(ref _frameNotes, value);
        }

        /// <summary>
        /// Whether a game is currently active
        /// </summary>
        public bool IsGameActive
        {
            get => _isGameActive;
            private set => this.RaiseAndSetIfChanged(ref _isGameActive, value);
        }

        /// <summary>
        /// Game status display text
        /// </summary>
        public string GameStatusText => CurrentGame?.GameState switch
        {
            GameState.NotStarted => "Not Started",
            GameState.InProgress => "In Progress",
            GameState.Paused => "Paused",
            GameState.Completed => "Completed",
            _ => "No Game"
        };

        /// <summary>
        /// Total score display text
        /// </summary>
        public string TotalScoreText => $"Total: {CurrentGame?.TotalScore ?? 0}";

        /// <summary>
        /// Current frame number display
        /// </summary>
        public int CurrentFrameNumber => CurrentGame?.CurrentFrameNumber ?? 0;

        /// <summary>
        /// Frame score preview text
        /// </summary>
        public string FrameScoreText => $"Frame Score: {BreakBonus + BallCount}";

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> StartNewGameCommand { get; }
        public ReactiveCommand<Unit, Unit> CompleteFrameCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetFrameCommand { get; }
        public ReactiveCommand<Unit, Unit> PauseGameCommand { get; }
        public ReactiveCommand<Unit, Unit> ResumeGameCommand { get; }

        #endregion

        #region Private Methods

        private async Task StartNewGameAsync()
        {
            // Create default player and venue for now
            var player = new Player
            {
                PlayerId = Guid.NewGuid(),
                FirstName = "Guest",
                LastName = "Player"
            };

            var venue = new Venue
            {
                VenueId = Guid.NewGuid(),
                Name = "Home Table"
            };

            await _gameService.CreateNewGameAsync(player, venue, TableSize.NineFoot);
            ResetFrameInput();
        }

        private async Task CompleteFrameAsync()
        {
            if (CurrentGame == null) return;

            var success = await _gameService.CompleteCurrentFrameAsync(BreakBonus, BallCount,
                string.IsNullOrWhiteSpace(FrameNotes) ? null : FrameNotes);

            if (success)
            {
                ResetFrameInput();
            }
        }

        private async Task ResetFrameAsync()
        {
            await _gameService.ResetCurrentFrameAsync();
            ResetFrameInput();
        }

        private async Task PauseGameAsync()
        {
            await _gameService.PauseGameAsync();
        }

        private async Task ResumeGameAsync()
        {
            await _gameService.ResumeGameAsync();
        }

        private void ResetFrameInput()
        {
            BreakBonus = 0;
            BallCount = 0;
            FrameNotes = string.Empty;
        }

        private void OnCurrentGameChanged(object? sender, Game? game)
        {
            CurrentGame = game;
            CurrentFrame = game?.CurrentFrame;
            IsGameActive = game?.IsInProgress ?? false;
        }

        private void OnFrameCompleted(object? sender, Frame frame)
        {
            CurrentFrame = CurrentGame?.CurrentFrame;
            this.RaisePropertyChanged(nameof(TotalScoreText));
            this.RaisePropertyChanged(nameof(CurrentFrameNumber));
        }

        private void OnGameCompleted(object? sender, Game game)
        {
            IsGameActive = false;
            this.RaisePropertyChanged(nameof(GameStatusText));
        }

        #endregion
    }

    // Keep the old name as an alias for backwards compatibility
    [Obsolete("Use GameControlViewModel instead")]
    public class GameControliewModel : GameControlViewModel
    {
        public GameControliewModel() : base() { }
    }
}
