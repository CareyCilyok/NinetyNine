/// Copyright (c) 2020-2025
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
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace NinetyNine.Presentation.Controls
{
    /// <summary>
    /// A control that displays a confetti celebration effect
    /// </summary>
    public partial class ConfettiControl : UserControl
    {
        private readonly List<ConfettiParticle> _particles = new();
        private readonly List<Rectangle> _confettiShapes = new();
        private readonly Random _random = new();
        private DispatcherTimer? _animationTimer;
        private Canvas? _canvas;
        private Border? _messageOverlay;
        private bool _isAnimating;
        private DateTime _startTime;
        private int _durationMs = 3000;

        // Confetti colors - gold and multi-color
        private readonly Color[] _colors = new Color[]
        {
            Color.Parse("#FFD700"),     // Gold
            Color.Parse("#FFE566"),     // Light Gold
            Color.Parse("#FF4466"),     // Red
            Color.Parse("#00D4FF"),     // Neon Blue
            Color.Parse("#00FF88"),     // Neon Green
            Color.Parse("#B388FF"),     // Purple
            Color.Parse("#FFFFFF"),     // White
        };

        public ConfettiControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _canvas = this.FindControl<Canvas>("ConfettiCanvas");
            _messageOverlay = this.FindControl<Border>("MessageOverlay");
        }

        /// <summary>
        /// Triggers the confetti celebration animation
        /// </summary>
        /// <param name="message">The celebration message to display</param>
        /// <param name="durationMs">Duration of the celebration in milliseconds</param>
        public async Task TriggerCelebration(string message = "LEGENDARY! PERFECT 99!", int durationMs = 3000)
        {
            if (_isAnimating) return;

            _durationMs = durationMs;
            _isAnimating = true;
            _startTime = DateTime.Now;

            // Update message
            var titleBlock = this.FindControl<TextBlock>("CelebrationTitle");
            var subtitleBlock = this.FindControl<TextBlock>("CelebrationSubtitle");

            if (message.Contains("LEGENDARY"))
            {
                if (titleBlock != null) titleBlock.Text = "LEGENDARY!";
                if (subtitleBlock != null) subtitleBlock.Text = "PERFECT 99!";
            }
            else
            {
                if (titleBlock != null) titleBlock.Text = "GAME COMPLETE!";
                if (subtitleBlock != null) subtitleBlock.Text = message;
            }

            // Show message overlay
            if (_messageOverlay != null)
            {
                _messageOverlay.IsVisible = true;
            }

            // Generate particles
            GenerateParticles();

            // Start animation timer
            _animationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
            };
            _animationTimer.Tick += OnAnimationTick;
            _animationTimer.Start();

            // Wait for animation to complete
            await Task.Delay(durationMs);

            // Stop animation
            StopAnimation();
        }

        private void GenerateParticles()
        {
            _particles.Clear();
            _confettiShapes.Clear();

            if (_canvas == null) return;

            _canvas.Children.Clear();

            var bounds = this.Bounds;
            var centerX = bounds.Width / 2;

            // Generate 80 confetti particles
            for (int i = 0; i < 80; i++)
            {
                var particle = new ConfettiParticle
                {
                    X = centerX + (_random.NextDouble() - 0.5) * 200,
                    Y = -_random.NextDouble() * 100,
                    VelocityX = (_random.NextDouble() - 0.5) * 8,
                    VelocityY = _random.NextDouble() * 6 + 3,
                    Rotation = _random.NextDouble() * 360,
                    RotationSpeed = (_random.NextDouble() - 0.5) * 15,
                    Width = _random.NextDouble() * 8 + 4,
                    Height = _random.NextDouble() * 12 + 6,
                    Color = _colors[_random.Next(_colors.Length)],
                    Wobble = _random.NextDouble() * 2 * Math.PI,
                    WobbleSpeed = _random.NextDouble() * 0.15 + 0.05
                };

                var rect = new Rectangle
                {
                    Width = particle.Width,
                    Height = particle.Height,
                    Fill = new SolidColorBrush(particle.Color),
                    RadiusX = 2,
                    RadiusY = 2,
                    RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative)
                };

                Canvas.SetLeft(rect, particle.X);
                Canvas.SetTop(rect, particle.Y);

                _canvas.Children.Add(rect);
                _confettiShapes.Add(rect);
                _particles.Add(particle);
            }
        }

        private void OnAnimationTick(object? sender, EventArgs e)
        {
            var elapsed = (DateTime.Now - _startTime).TotalMilliseconds;

            if (elapsed > _durationMs)
            {
                StopAnimation();
                return;
            }

            // Update particles
            for (int i = 0; i < _particles.Count; i++)
            {
                var particle = _particles[i];
                var shape = _confettiShapes[i];

                particle.X += particle.VelocityX;
                particle.Y += particle.VelocityY;
                particle.VelocityY += 0.12; // Gravity
                particle.Rotation += particle.RotationSpeed;
                particle.Wobble += particle.WobbleSpeed;

                // Add wobble effect
                particle.X += Math.Sin(particle.Wobble) * 0.4;

                // Update shape position
                Canvas.SetLeft(shape, particle.X);
                Canvas.SetTop(shape, particle.Y);

                // Update rotation
                shape.RenderTransform = new RotateTransform(particle.Rotation);

                // Fade out near end
                if (elapsed > _durationMs * 0.7)
                {
                    var fadeProgress = (elapsed - _durationMs * 0.7) / (_durationMs * 0.3);
                    shape.Opacity = 1.0 - fadeProgress;
                }
            }
        }

        private void StopAnimation()
        {
            _animationTimer?.Stop();
            _animationTimer = null;
            _isAnimating = false;
            _particles.Clear();
            _confettiShapes.Clear();

            if (_messageOverlay != null)
            {
                _messageOverlay.IsVisible = false;
            }

            if (_canvas != null)
            {
                _canvas.Children.Clear();
            }
        }

        /// <summary>
        /// Represents a single confetti particle
        /// </summary>
        private class ConfettiParticle
        {
            public double X { get; set; }
            public double Y { get; set; }
            public double VelocityX { get; set; }
            public double VelocityY { get; set; }
            public double Rotation { get; set; }
            public double RotationSpeed { get; set; }
            public double Width { get; set; }
            public double Height { get; set; }
            public Color Color { get; set; }
            public double Alpha { get; set; } = 1.0;
            public double Wobble { get; set; }
            public double WobbleSpeed { get; set; }
        }
    }
}
