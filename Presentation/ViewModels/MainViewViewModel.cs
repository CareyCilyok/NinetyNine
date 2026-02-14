using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using ReactiveUI;

namespace NinetyNine.Presentation.ViewModels
{
    /// <summary>
    /// Main view model for the primary application view
    /// </summary>
    public class MainViewViewModel : ViewModelBase
    {
        private NavigationViewItemViewModel? _selectedNavItem;
        private ViewModelBase? _currentPageContent;

        public MainViewViewModel()
        {
            Descriptions = new Descriptions();
            Titles = new Titles();

            // Initialize navigation items
            AddNavItems();
        }

        /// <summary>
        /// Populates the navigation items
        /// </summary>
        private void AddNavItems()
        {
            var navItems = new List<NavigationViewItemViewModel>
            {
                new NavigationViewItemViewModel
                {
                    Header = Titles.Game,
                    Title = Titles.Game,
                    Content = new GameControlViewModel()
                },
                new NavigationViewItemViewModel
                {
                    Header = Titles.Statistics,
                    Title = Titles.Statistics,
                    Content = new StatisticsPageViewModel()
                },
                new NavigationViewItemViewModel
                {
                    Header = Titles.Profile,
                    Title = Titles.Profile,
                    Content = new ProfilePageViewModel()
                },
                new NavigationViewItemViewModel
                {
                    Header = Titles.Venues,
                    Title = Titles.Venues,
                    Content = new VenuePageViewModel()
                }
            };

            NavItems = navItems;

            // Set default selection
            if (NavItems.Count > 0)
            {
                SelectedNavItem = NavItems[0];
            }
        }

        /// <summary>
        /// Titles for navigation items
        /// </summary>
        public Titles Titles { get; }

        /// <summary>
        /// Descriptions for navigation items
        /// </summary>
        public Descriptions Descriptions { get; }

        /// <summary>
        /// Navigation items
        /// </summary>
        public IReadOnlyList<NavigationViewItemViewModel> NavItems { get; private set; } = new List<NavigationViewItemViewModel>();

        /// <summary>
        /// Currently selected navigation item
        /// </summary>
        public NavigationViewItemViewModel? SelectedNavItem
        {
            get => _selectedNavItem;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedNavItem, value);
                CurrentPageContent = value?.Content as ViewModelBase;
            }
        }

        /// <summary>
        /// Current page content view model
        /// </summary>
        public ViewModelBase? CurrentPageContent
        {
            get => _currentPageContent;
            private set => this.RaiseAndSetIfChanged(ref _currentPageContent, value);
        }

        /// <summary>
        /// Gets the description for a navigation item
        /// </summary>
        public string GetDescription(string title)
        {
            return title switch
            {
                "Game" => Descriptions.Game,
                "Statistics" => Descriptions.Statistics,
                "Profile" => Descriptions.Profile,
                "Venues" => Descriptions.Venues,
                _ => string.Empty
            };
        }
    }

    /// <summary>
    /// Description strings for navigation items
    /// </summary>
    public class Descriptions
    {
        public string Game => "Create a new game single or multi player of 99.";
        public string Statistics => "View player statistics. See your win/loss history, average score, handicap and more.";
        public string Profile => "Edit your profile";
        public string Venues => "Add or edit venues where you play or hangout";
    }

    /// <summary>
    /// Title strings for navigation items
    /// </summary>
    public class Titles
    {
        public string Game => "Game";
        public string Statistics => "Statistics";
        public string Profile => "Profile";
        public string Venues => "Venues";
    }

    /// <summary>
    /// View model for a navigation item
    /// </summary>
    public class NavigationViewItemViewModel
    {
        public IImage? Icon { get; set; }
        public string Header { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public object? Content { get; set; }
        public IReadOnlyList<NavigationViewItemViewModel> NavItems { get; set; } = new List<NavigationViewItemViewModel>();
    }
}
