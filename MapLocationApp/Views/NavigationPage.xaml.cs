using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using MapLocationApp.Services;
using MapLocationApp.Models;
using System.Collections.ObjectModel;

namespace MapLocationApp.Views
{
    /// <summary>
    /// Advanced turn-by-turn navigation page demonstrating the new navigation services
    /// </summary>
    public partial class NavigationPage : ContentPage, INotifyPropertyChanged
    {
        private readonly INavigationService _navigationService;
        private readonly INavigationPreferencesService _preferencesService;
        private readonly ILaneGuidanceService _laneGuidanceService;
        private readonly IAdvancedLocationService _locationService;
        private readonly IRouteService _routeService;

        private NavigationViewModel _viewModel;
        private NavigationPreferences _preferences;
        private Route? _testRoute;

        public ObservableCollection<LaneInfo> LaneInfo { get; set; } = new();

        public bool HasLaneGuidance => LaneInfo.Count > 0;
        public bool IsNavigating => _viewModel?.IsNavigating ?? false;
        public bool IsVoiceEnabled => _preferences?.IsVoiceEnabled ?? true;

        public NavigationPage()
        {
            InitializeComponent();
            
            // Get services from DI container
            _navigationService = ServiceHelper.GetService<INavigationService>();
            _preferencesService = ServiceHelper.GetService<INavigationPreferencesService>();
            _laneGuidanceService = ServiceHelper.GetService<ILaneGuidanceService>();
            _locationService = ServiceHelper.GetService<IAdvancedLocationService>();
            _routeService = ServiceHelper.GetService<IRouteService>();

            _viewModel = new NavigationViewModel();
            BindingContext = _viewModel;

            // Subscribe to events
            _navigationService.InstructionUpdated += OnInstructionUpdated;
            _navigationService.StatusChanged += OnNavigationStatusChanged;
            _laneGuidanceService.LaneGuidanceAvailable += OnLaneGuidanceAvailable;

            // Initialize
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Load user preferences
                _preferences = await _preferencesService.GetPreferencesAsync();
                
                // Create a test route for demonstration
                await CreateTestRoute();
                
                // Start location updates
                await _locationService.StartLocationUpdatesAsync();

                OnPropertyChanged(nameof(IsVoiceEnabled));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Initialization Error", $"Failed to initialize navigation: {ex.Message}", "OK");
            }
        }

        private async Task CreateTestRoute()
        {
            try
            {
                // Create a sample route for testing
                var routeResult = await _routeService.CalculateRouteAsync(
                    25.0330, 121.5654,  // Taipei 101 area
                    25.0478, 121.5173,  // Taipei Main Station area
                    RouteType.Driving
                );

                if (routeResult?.Success == true && routeResult.Route != null)
                {
                    _testRoute = routeResult.Route;
                    _testRoute.Name = "Demo Route: Taipei 101 → Main Station";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create test route: {ex.Message}");
            }
        }

        private async void OnStartNavigationClicked(object sender, EventArgs e)
        {
            try
            {
                if (_testRoute == null)
                {
                    await DisplayAlert("Error", "No route available for navigation", "OK");
                    return;
                }

                StartNavigationButton.IsEnabled = false;
                StartNavigationButton.Text = "🔄 Starting...";

                var success = await _navigationService.StartNavigationAsync(_testRoute);
                
                if (success)
                {
                    await DisplayAlert("Navigation Started", "Turn-by-turn navigation is now active", "OK");
                }
                else
                {
                    await DisplayAlert("Error", "Failed to start navigation", "OK");
                    StartNavigationButton.IsEnabled = true;
                    StartNavigationButton.Text = "🧭 Start Navigation";
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to start navigation: {ex.Message}", "OK");
                StartNavigationButton.IsEnabled = true;
                StartNavigationButton.Text = "🧭 Start Navigation";
            }
        }

        private async void OnStopNavigationClicked(object sender, EventArgs e)
        {
            try
            {
                await _navigationService.StopNavigationAsync();
                await DisplayAlert("Navigation Stopped", "Navigation has been stopped", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to stop navigation: {ex.Message}", "OK");
            }
        }

        private async void OnVoiceToggleClicked(object sender, EventArgs e)
        {
            try
            {
                var newVoiceState = !IsVoiceEnabled;
                await _navigationService.SetVoiceGuidanceAsync(newVoiceState);
                
                // Update preferences
                _preferences.VoiceLevel = newVoiceState ? VoiceGuidanceLevel.Normal : VoiceGuidanceLevel.Off;
                await _preferencesService.SavePreferencesAsync(_preferences);

                OnPropertyChanged(nameof(IsVoiceEnabled));
                
                var message = newVoiceState ? "Voice guidance enabled" : "Voice guidance disabled";
                await DisplayAlert("Voice Settings", message, "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Failed to toggle voice guidance: {ex.Message}", "OK");
            }
        }

        private async void OnSettingsClicked(object sender, EventArgs e)
        {
            // In a real app, this would navigate to a settings page
            var options = new[]
            {
                "Avoid Tolls: " + (_preferences.AvoidTolls ? "Yes" : "No"),
                "Avoid Highways: " + (_preferences.AvoidHighways ? "Yes" : "No"),
                "Voice Level: " + _preferences.VoiceLevel,
                "Language: " + _preferences.PreferredLanguage,
                "Route Optimization: " + _preferences.DefaultOptimization
            };

            var result = await DisplayActionSheet("Navigation Settings", "Cancel", null, options);
            
            if (result != null && result != "Cancel")
            {
                await DisplayAlert("Settings", $"Selected: {result}", "OK");
            }
        }

        private async void OnInstructionUpdated(object? sender, NavigationInstruction instruction)
        {
            try
            {
                // Update the UI on the main thread
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _viewModel.CurrentInstruction = instruction;
                    OnPropertyChanged(nameof(IsNavigating));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating instruction: {ex.Message}");
            }
        }

        private async void OnNavigationStatusChanged(object? sender, NavigationStatus status)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    _viewModel.IsNavigating = status.IsActive;
                    _viewModel.IsVoiceEnabled = status.IsVoiceEnabled;
                    _viewModel.SignalQuality = status.SignalQuality;
                    _viewModel.IsOfflineMode = status.IsOfflineMode;

                    if (status.Progress != null)
                    {
                        _viewModel.ProgressPercentage = status.Progress.ProgressPercentage;
                        _viewModel.RemainingDistance = FormatDistance(status.Progress.RemainingDistance);
                        _viewModel.RemainingTime = FormatTimeSpan(status.Progress.EstimatedTimeRemaining);
                    }

                    StartNavigationButton.IsEnabled = !status.IsActive;
                    StartNavigationButton.Text = "🧭 Start Navigation";
                    
                    OnPropertyChanged(nameof(IsNavigating));
                    OnPropertyChanged(nameof(IsVoiceEnabled));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating navigation status: {ex.Message}");
            }
        }

        private async void OnLaneGuidanceAvailable(object? sender, LaneGuidanceResult laneGuidance)
        {
            try
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    LaneInfo.Clear();
                    foreach (var lane in laneGuidance.Lanes)
                    {
                        LaneInfo.Add(lane);
                    }
                    OnPropertyChanged(nameof(HasLaneGuidance));
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating lane guidance: {ex.Message}");
            }
        }

        private string FormatDistance(double distanceMeters)
        {
            if (distanceMeters < 1000)
            {
                return $"{(int)distanceMeters} 公尺";
            }
            else
            {
                var km = distanceMeters / 1000.0;
                return $"{km:F1} 公里";
            }
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
            {
                return $"{(int)timeSpan.TotalHours} 小時 {timeSpan.Minutes} 分鐘";
            }
            else
            {
                return $"{timeSpan.Minutes} 分鐘";
            }
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            
            // Unsubscribe from events
            _navigationService.InstructionUpdated -= OnInstructionUpdated;
            _navigationService.StatusChanged -= OnNavigationStatusChanged;
            _laneGuidanceService.LaneGuidanceAvailable -= OnLaneGuidanceAvailable;
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}