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
using ReactiveUI;

namespace NinetyNine.Presentation.ViewModels
{
    /// <summary>
    /// Main window view model containing application-level state
    /// </summary>
    public class MainWindowViewModel : ViewModelBase
    {
        private string _title = "Ninety-Nine";
        private ViewModelBase? _currentContent;
        private bool _isInitialized;

        public MainWindowViewModel()
        {
            // Initialize with the main view
            MainView = new MainViewViewModel();
            CurrentContent = MainView;
        }

        /// <summary>
        /// Application title
        /// </summary>
        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        /// <summary>
        /// The main view model
        /// </summary>
        public MainViewViewModel MainView { get; }

        /// <summary>
        /// Current content view model
        /// </summary>
        public ViewModelBase? CurrentContent
        {
            get => _currentContent;
            set => this.RaiseAndSetIfChanged(ref _currentContent, value);
        }

        /// <summary>
        /// Whether the application has been initialized
        /// </summary>
        public bool IsInitialized
        {
            get => _isInitialized;
            private set => this.RaiseAndSetIfChanged(ref _isInitialized, value);
        }

        /// <summary>
        /// Initializes the view model
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;

            // Any initialization logic here
            IsInitialized = true;
        }

        /// <summary>
        /// Navigates to a specific view
        /// </summary>
        /// <param name="viewModel">The view model to navigate to</param>
        public void NavigateTo(ViewModelBase viewModel)
        {
            CurrentContent = viewModel;
        }
    }
}
