using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Controls;
using NinetyNine.Presentation.Pages;

namespace NinetyNine.Presentation.Views
{
    public partial class MainView : UserControl
    {
        private GamePage? _gamePage;
        private StatisticsPage? _statisticsPage;
        private ProfilePage? _profilePage;
        private VenuePage? _venuePage;

        public MainView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            _gamePage = this.FindControl<GamePage>("GamePage");
            _statisticsPage = this.FindControl<StatisticsPage>("StatisticsPage");
            _profilePage = this.FindControl<ProfilePage>("ProfilePage");
            _venuePage = this.FindControl<VenuePage>("VenuePage");
        }

        private void NavView_SelectionChanged(object? sender, NavigationViewSelectionChangedEventArgs e)
        {
            if (e.SelectedItem is NavigationViewItem item)
            {
                var tag = item.Tag?.ToString();
                ShowPage(tag);
            }
        }

        private void ShowPage(string? tag)
        {
            // Hide all pages
            if (_gamePage != null) _gamePage.IsVisible = false;
            if (_statisticsPage != null) _statisticsPage.IsVisible = false;
            if (_profilePage != null) _profilePage.IsVisible = false;
            if (_venuePage != null) _venuePage.IsVisible = false;

            // Show selected page
            switch (tag)
            {
                case "Game":
                    if (_gamePage != null) _gamePage.IsVisible = true;
                    break;
                case "Statistics":
                    if (_statisticsPage != null) _statisticsPage.IsVisible = true;
                    break;
                case "Profile":
                    if (_profilePage != null) _profilePage.IsVisible = true;
                    break;
                case "Venues":
                    if (_venuePage != null) _venuePage.IsVisible = true;
                    break;
                default:
                    if (_gamePage != null) _gamePage.IsVisible = true;
                    break;
            }
        }
    }
}
