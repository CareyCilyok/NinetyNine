using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using NinetyNine.Model;
using NinetyNine.Presentation.Services;
using ReactiveUI;

namespace NinetyNine.Presentation.ViewModels
{
    public class VenuePageViewModel : ViewModelBase
    {
        private readonly IVenueService _venueService;
        private readonly IStatisticsService _statisticsService;
        private Venue? _selectedVenue;
        private VenueStatistics? _selectedVenueStatistics;
        private bool _isLoading;
        private bool _isEditing;
        private bool _isCreatingNew;
        private string _searchText = string.Empty;

        public VenuePageViewModel() : this(new VenueService(), new StatisticsService())
        {
        }

        public VenuePageViewModel(IVenueService venueService, IStatisticsService statisticsService)
        {
            _venueService = venueService;
            _statisticsService = statisticsService;
            
            Venues = new ObservableCollection<Venue>();
            PopularVenues = new ObservableCollection<PopularVenue>();
            
            // Initialize with empty venue for creation
            NewVenue = new Venue();

            // Commands
            SearchCommand = ReactiveCommand.CreateFromTask(SearchVenuesAsync);
            SelectVenueCommand = ReactiveCommand.CreateFromTask<Venue>(SelectVenueAsync);
            CreateNewVenueCommand = ReactiveCommand.Create(StartCreateNewVenue);
            SaveVenueCommand = ReactiveCommand.CreateFromTask(SaveVenueAsync,
                this.WhenAnyValue(x => x.IsEditing, x => x.IsCreatingNew, (editing, creating) => editing || creating));
            CancelEditCommand = ReactiveCommand.Create(CancelEdit,
                this.WhenAnyValue(x => x.IsEditing, x => x.IsCreatingNew, (editing, creating) => editing || creating));
            EditVenueCommand = ReactiveCommand.Create(StartEditVenue,
                this.WhenAnyValue(x => x.SelectedVenue, x => x.IsEditing, x => x.IsCreatingNew, 
                    (venue, editing, creating) => venue != null && !editing && !creating));
            DeleteVenueCommand = ReactiveCommand.CreateFromTask(DeleteSelectedVenueAsync,
                this.WhenAnyValue(x => x.SelectedVenue, x => x.IsEditing, x => x.IsCreatingNew,
                    (venue, editing, creating) => venue != null && !editing && !creating));
            RefreshDataCommand = ReactiveCommand.CreateFromTask(RefreshDataAsync);

            // Load initial data
            _ = RefreshDataAsync();
        }

        #region Properties

        /// <summary>
        /// Collection of all venues
        /// </summary>
        public ObservableCollection<Venue> Venues { get; }

        /// <summary>
        /// Collection of popular venues
        /// </summary>
        public ObservableCollection<PopularVenue> PopularVenues { get; }

        /// <summary>
        /// Currently selected venue
        /// </summary>
        public Venue? SelectedVenue
        {
            get => _selectedVenue;
            set
            {
                this.RaiseAndSetIfChanged(ref _selectedVenue, value);
                if (value != null && !IsEditing && !IsCreatingNew)
                {
                    _ = LoadVenueStatisticsAsync(value.VenueId);
                }
            }
        }

        /// <summary>
        /// Statistics for the selected venue
        /// </summary>
        public VenueStatistics? SelectedVenueStatistics
        {
            get => _selectedVenueStatistics;
            private set => this.RaiseAndSetIfChanged(ref _selectedVenueStatistics, value);
        }

        /// <summary>
        /// New venue being created
        /// </summary>
        public Venue NewVenue { get; private set; }

        /// <summary>
        /// Search text for filtering venues
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => this.RaiseAndSetIfChanged(ref _searchText, value);
        }

        /// <summary>
        /// Whether data is currently loading
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        /// <summary>
        /// Whether currently editing a venue
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            private set => this.RaiseAndSetIfChanged(ref _isEditing, value);
        }

        /// <summary>
        /// Whether currently creating a new venue
        /// </summary>
        public bool IsCreatingNew
        {
            get => _isCreatingNew;
            private set => this.RaiseAndSetIfChanged(ref _isCreatingNew, value);
        }

        /// <summary>
        /// Whether venue details should be visible
        /// </summary>
        public bool IsVenueDetailsVisible => SelectedVenue != null && !IsCreatingNew;

        /// <summary>
        /// Whether venue form should be visible
        /// </summary>
        public bool IsVenueFormVisible => IsEditing || IsCreatingNew;

        /// <summary>
        /// Title for the venue form
        /// </summary>
        public string VenueFormTitle => IsCreatingNew ? "Create New Venue" : "Edit Venue";

        /// <summary>
        /// Venue being edited (either selected venue or new venue)
        /// </summary>
        public Venue EditingVenue => IsCreatingNew ? NewVenue : (SelectedVenue ?? new Venue());

        /// <summary>
        /// Formatted venue statistics text
        /// </summary>
        public string VenueStatsText
        {
            get
            {
                if (SelectedVenueStatistics == null) return "No statistics available";
                return $"{SelectedVenueStatistics.TotalGamesPlayed} games • " +
                       $"{SelectedVenueStatistics.UniquePlayersCount} players • " +
                       $"{SelectedVenueStatistics.AverageScore:F1} avg score";
            }
        }

        #endregion

        #region Commands

        public ReactiveCommand<Unit, Unit> SearchCommand { get; }
        public ReactiveCommand<Venue, Unit> SelectVenueCommand { get; }
        public ReactiveCommand<Unit, Unit> CreateNewVenueCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveVenueCommand { get; }
        public ReactiveCommand<Unit, Unit> CancelEditCommand { get; }
        public ReactiveCommand<Unit, Unit> EditVenueCommand { get; }
        public ReactiveCommand<Unit, Unit> DeleteVenueCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshDataCommand { get; }

        #endregion

        #region Private Methods

        private async Task RefreshDataAsync()
        {
            try
            {
                IsLoading = true;

                // Load all venues
                var venues = await _venueService.GetAllVenuesAsync();
                Venues.Clear();
                foreach (var venue in venues)
                {
                    Venues.Add(venue);
                }

                // Load popular venues
                var popularVenues = await _venueService.GetPopularVenuesAsync(10);
                PopularVenues.Clear();
                foreach (var venue in popularVenues)
                {
                    PopularVenues.Add(venue);
                }

                // Update property notifications
                this.RaisePropertyChanged(nameof(IsVenueDetailsVisible));
                this.RaisePropertyChanged(nameof(IsVenueFormVisible));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error refreshing venue data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SearchVenuesAsync()
        {
            try
            {
                IsLoading = true;

                var venues = await _venueService.SearchVenuesAsync(SearchText);
                Venues.Clear();
                foreach (var venue in venues)
                {
                    Venues.Add(venue);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error searching venues: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SelectVenueAsync(Venue venue)
        {
            SelectedVenue = venue;
            await LoadVenueStatisticsAsync(venue.VenueId);
        }

        private async Task LoadVenueStatisticsAsync(Guid venueId)
        {
            try
            {
                SelectedVenueStatistics = await _statisticsService.GetVenueStatisticsAsync(venueId);
                this.RaisePropertyChanged(nameof(VenueStatsText));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading venue statistics: {ex.Message}");
            }
        }

        private void StartCreateNewVenue()
        {
            NewVenue = new Venue();
            IsCreatingNew = true;
            this.RaisePropertyChanged(nameof(IsVenueDetailsVisible));
            this.RaisePropertyChanged(nameof(IsVenueFormVisible));
            this.RaisePropertyChanged(nameof(VenueFormTitle));
            this.RaisePropertyChanged(nameof(EditingVenue));
        }

        private void StartEditVenue()
        {
            if (SelectedVenue != null)
            {
                IsEditing = true;
                this.RaisePropertyChanged(nameof(IsVenueDetailsVisible));
                this.RaisePropertyChanged(nameof(IsVenueFormVisible));
                this.RaisePropertyChanged(nameof(VenueFormTitle));
                this.RaisePropertyChanged(nameof(EditingVenue));
            }
        }

        private async Task SaveVenueAsync()
        {
            try
            {
                IsLoading = true;

                if (IsCreatingNew)
                {
                    // Create new venue
                    var createdVenue = await _venueService.CreateVenueAsync(NewVenue);
                    Venues.Add(createdVenue);
                    SelectedVenue = createdVenue;
                    IsCreatingNew = false;
                }
                else if (IsEditing && SelectedVenue != null)
                {
                    // Update existing venue
                    await _venueService.UpdateVenueAsync(SelectedVenue);
                    IsEditing = false;
                }

                this.RaisePropertyChanged(nameof(IsVenueDetailsVisible));
                this.RaisePropertyChanged(nameof(IsVenueFormVisible));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving venue: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CancelEdit()
        {
            if (IsCreatingNew)
            {
                IsCreatingNew = false;
                NewVenue = new Venue();
            }
            else if (IsEditing)
            {
                IsEditing = false;
                // TODO: Revert changes to selected venue
            }

            this.RaisePropertyChanged(nameof(IsVenueDetailsVisible));
            this.RaisePropertyChanged(nameof(IsVenueFormVisible));
        }

        private async Task DeleteSelectedVenueAsync()
        {
            if (SelectedVenue == null) return;

            try
            {
                IsLoading = true;

                var success = await _venueService.DeleteVenueAsync(SelectedVenue.VenueId);
                if (success)
                {
                    Venues.Remove(SelectedVenue);
                    SelectedVenue = null;
                    SelectedVenueStatistics = null;
                }

                this.RaisePropertyChanged(nameof(IsVenueDetailsVisible));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting venue: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion
    }
}