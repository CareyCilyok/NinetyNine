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
using System.Reactive.Linq;
using System.Windows.Input;
using Avalonia.Media;
using NinetyNine.Model;
using ReactiveUI;

namespace NinetyNine.Presentation.ViewModels
{
    public class FrameControlViewModel : ViewModelBase
    {
        private Frame? _frame;
        private bool _isEditable = false;

        public FrameControlViewModel()
        {
            // Initialize commands
            IncrementBreakBonusCommand = ReactiveCommand.Create(IncrementBreakBonus, this.WhenAnyValue(x => x.CanEdit));
            DecrementBreakBonusCommand = ReactiveCommand.Create(DecrementBreakBonus, this.WhenAnyValue(x => x.CanEdit));
            IncrementBallCountCommand = ReactiveCommand.Create(IncrementBallCount, this.WhenAnyValue(x => x.CanEdit));
            DecrementBallCountCommand = ReactiveCommand.Create(DecrementBallCount, this.WhenAnyValue(x => x.CanEdit));
            EditFrameCommand = ReactiveCommand.Create(EditFrame, this.WhenAnyValue(x => x.CanEdit));
            ResetFrameCommand = ReactiveCommand.Create(ResetFrame, this.WhenAnyValue(x => x.CanReset));

            // Set up property change subscriptions
            this.WhenAnyValue(x => x.Frame)
                .Where(frame => frame != null)
                .Subscribe(frame => SubscribeToFrameChanges());
        }

        public FrameControlViewModel(Frame frame) : this()
        {
            Frame = frame;
        }

        /// <summary>
        /// The frame this view model represents
        /// </summary>
        public Frame? Frame
        {
            get => _frame;
            set => this.RaiseAndSetIfChanged(ref _frame, value);
        }

        /// <summary>
        /// Whether this frame can be edited
        /// </summary>
        public bool IsEditable
        {
            get => _isEditable;
            set => this.RaiseAndSetIfChanged(ref _isEditable, value);
        }

        /// <summary>
        /// Break bonus score (0 or 1)
        /// </summary>
        public int BreakBonus
        {
            get => Frame?.BreakBonus ?? 0;
            set
            {
                if (Frame != null && Frame.BreakBonus != value)
                {
                    Frame.BreakBonus = Math.Max(0, Math.Min(1, value));
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(FrameScore));
                    this.RaisePropertyChanged(nameof(IsValidScore));
                }
            }
        }

        /// <summary>
        /// Ball count score (0-10)
        /// </summary>
        public int BallCount
        {
            get => Frame?.BallCount ?? 0;
            set
            {
                if (Frame != null && Frame.BallCount != value)
                {
                    Frame.BallCount = Math.Max(0, Math.Min(10, value));
                    this.RaisePropertyChanged();
                    this.RaisePropertyChanged(nameof(FrameScore));
                    this.RaisePropertyChanged(nameof(IsValidScore));
                }
            }
        }

        /// <summary>
        /// Total frame score
        /// </summary>
        public int FrameScore => Frame?.FrameScore ?? 0;

        /// <summary>
        /// Running total through this frame
        /// </summary>
        public int RunningTotal => Frame?.RunningTotal ?? 0;

        /// <summary>
        /// Frame number (1-9)
        /// </summary>
        public int FrameNumber => Frame?.FrameNumber ?? 0;

        /// <summary>
        /// Whether this frame is completed
        /// </summary>
        public bool IsCompleted => Frame?.IsCompleted ?? false;

        /// <summary>
        /// Whether this frame is currently active
        /// </summary>
        public bool IsActive => Frame?.IsActive ?? false;

        /// <summary>
        /// Whether the current score is valid
        /// </summary>
        public bool IsValidScore => Frame?.IsValidScore ?? true;

        /// <summary>
        /// Whether this is a perfect frame (11 points)
        /// </summary>
        public bool IsPerfectFrame => Frame?.IsPerfectFrame ?? false;

        /// <summary>
        /// Background color based on frame state
        /// </summary>
        public IBrush BackgroundBrush
        {
            get
            {
                if (Frame == null) return Brushes.Transparent;
                
                if (IsActive) return Brushes.LightBlue;
                if (IsPerfectFrame) return Brushes.Gold;
                if (IsCompleted) return Brushes.LightGreen;
                if (!IsValidScore) return Brushes.LightCoral;
                
                return Brushes.Transparent;
            }
        }

        /// <summary>
        /// Border color based on frame state
        /// </summary>
        public IBrush BorderBrush
        {
            get
            {
                if (Frame == null) return Brushes.Gray;
                
                if (IsActive) return Brushes.Blue;
                if (!IsValidScore) return Brushes.Red;
                if (IsCompleted) return Brushes.Green;
                
                return Brushes.Gray;
            }
        }

        /// <summary>
        /// Whether this frame can be edited
        /// </summary>
        public bool CanEdit => Frame != null && (IsActive || IsEditable) && !IsCompleted;

        /// <summary>
        /// Whether this frame can be reset
        /// </summary>
        public bool CanReset => Frame != null && !IsCompleted && (BreakBonus > 0 || BallCount > 0);

        /// <summary>
        /// Display text for the frame
        /// </summary>
        public string DisplayText
        {
            get
            {
                if (Frame == null) return "0";
                if (IsCompleted) return RunningTotal.ToString();
                if (IsActive) return $"{BreakBonus}+{BallCount}";
                return "0";
            }
        }

        /// <summary>
        /// Tooltip text with detailed information
        /// </summary>
        public string ToolTipText
        {
            get
            {
                if (Frame == null) return "Empty Frame";
                
                var text = $"Frame {FrameNumber}";
                if (IsCompleted)
                {
                    text += $"\nBreak Bonus: {BreakBonus}\nBall Count: {BallCount}\nFrame Score: {FrameScore}\nRunning Total: {RunningTotal}";
                    if (IsPerfectFrame) text += "\n★ PERFECT FRAME! ★";
                }
                else if (IsActive)
                {
                    text += $"\nCurrent Frame\nBreak Bonus: {BreakBonus}\nBall Count: {BallCount}";
                    if (!IsValidScore) text += "\n⚠ Invalid Score (Max 11)";
                }
                else
                {
                    text += "\nNot Started";
                }
                
                if (!string.IsNullOrEmpty(Frame.Notes))
                {
                    text += $"\nNotes: {Frame.Notes}";
                }
                
                return text;
            }
        }

        // Commands
        public ReactiveCommand<Unit, Unit> IncrementBreakBonusCommand { get; }
        public ReactiveCommand<Unit, Unit> DecrementBreakBonusCommand { get; }
        public ReactiveCommand<Unit, Unit> IncrementBallCountCommand { get; }
        public ReactiveCommand<Unit, Unit> DecrementBallCountCommand { get; }
        public ReactiveCommand<Unit, Unit> EditFrameCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetFrameCommand { get; }

        private void IncrementBreakBonus()
        {
            if (BreakBonus < 1 && FrameScore < 11)
                BreakBonus++;
        }

        private void DecrementBreakBonus()
        {
            if (BreakBonus > 0)
                BreakBonus--;
        }

        private void IncrementBallCount()
        {
            if (BallCount < 10 && FrameScore < 11)
                BallCount++;
        }

        private void DecrementBallCount()
        {
            if (BallCount > 0)
                BallCount--;
        }

        private void EditFrame()
        {
            IsEditable = !IsEditable;
        }

        private void ResetFrame()
        {
            if (Frame != null)
            {
                Frame.ResetFrame();
                RefreshAll();
            }
        }

        private void SubscribeToFrameChanges()
        {
            if (Frame != null)
            {
                Frame.PropertyChanged += (sender, e) => RefreshAll();
            }
        }

        private void RefreshAll()
        {
            this.RaisePropertyChanged(nameof(BreakBonus));
            this.RaisePropertyChanged(nameof(BallCount));
            this.RaisePropertyChanged(nameof(FrameScore));
            this.RaisePropertyChanged(nameof(RunningTotal));
            this.RaisePropertyChanged(nameof(IsCompleted));
            this.RaisePropertyChanged(nameof(IsActive));
            this.RaisePropertyChanged(nameof(IsValidScore));
            this.RaisePropertyChanged(nameof(IsPerfectFrame));
            this.RaisePropertyChanged(nameof(BackgroundBrush));
            this.RaisePropertyChanged(nameof(BorderBrush));
            this.RaisePropertyChanged(nameof(DisplayText));
            this.RaisePropertyChanged(nameof(ToolTipText));
            this.RaisePropertyChanged(nameof(CanEdit));
            this.RaisePropertyChanged(nameof(CanReset));
        }
    }
}