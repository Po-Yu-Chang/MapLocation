using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

namespace MapLocationApp.Models
{
    /// <summary>
    /// Voice guidance level options
    /// </summary>
    public enum VoiceGuidanceLevel
    {
        Off,        // No voice guidance
        Essential,  // Only critical instructions (turns, arrival)
        Normal,     // Standard instructions
        Detailed    // Detailed instructions with additional info
    }

    /// <summary>
    /// Route optimization preferences
    /// </summary>
    public enum RouteOptimization
    {
        Time,           // Fastest route
        Distance,       // Shortest distance
        Fuel,           // Most fuel efficient
        Eco,            // Eco-friendly (avoid highways, prefer efficient routes)
        Scenic          // Scenic route (when available)
    }

    /// <summary>
    /// User preferences for navigation behavior
    /// </summary>
    public class NavigationPreferences : INotifyPropertyChanged
    {
        private bool _avoidTolls = false;
        private bool _avoidHighways = false;
        private bool _avoidFerries = false;
        private bool _avoidUnpavedRoads = true;
        private VoiceGuidanceLevel _voiceLevel = VoiceGuidanceLevel.Normal;
        private string _preferredLanguage = "zh-TW";
        private RouteOptimization _defaultOptimization = RouteOptimization.Time;
        private float _voiceVolume = 0.8f;
        private float _voiceSpeechRate = 1.0f;
        private bool _announceTrafficAlerts = true;
        private bool _announceSpeedLimits = true;
        private bool _vibrateOnArrival = true;
        private bool _showSpeedWarnings = true;
        private bool _autoRecalculateOnDeviation = true;
        private int _recalculationSensitivity = 3; // 1=sensitive, 5=tolerant
        private bool _saveNavigationHistory = true;
        private bool _sendTelegramNotifications = false;
        private bool _enableOfflineMode = false;
        private double _arrivalThresholdMeters = 20.0;

        /// <summary>
        /// Avoid toll roads when possible
        /// </summary>
        public bool AvoidTolls
        {
            get => _avoidTolls;
            set => SetProperty(ref _avoidTolls, value);
        }

        /// <summary>
        /// Avoid highways when possible
        /// </summary>
        public bool AvoidHighways
        {
            get => _avoidHighways;
            set => SetProperty(ref _avoidHighways, value);
        }

        /// <summary>
        /// Avoid ferry routes
        /// </summary>
        public bool AvoidFerries
        {
            get => _avoidFerries;
            set => SetProperty(ref _avoidFerries, value);
        }

        /// <summary>
        /// Avoid unpaved/dirt roads
        /// </summary>
        public bool AvoidUnpavedRoads
        {
            get => _avoidUnpavedRoads;
            set => SetProperty(ref _avoidUnpavedRoads, value);
        }

        /// <summary>
        /// Level of voice guidance detail
        /// </summary>
        public VoiceGuidanceLevel VoiceLevel
        {
            get => _voiceLevel;
            set => SetProperty(ref _voiceLevel, value);
        }

        /// <summary>
        /// Preferred language for voice guidance
        /// </summary>
        public string PreferredLanguage
        {
            get => _preferredLanguage;
            set => SetProperty(ref _preferredLanguage, value);
        }

        /// <summary>
        /// Default route optimization strategy
        /// </summary>
        public RouteOptimization DefaultOptimization
        {
            get => _defaultOptimization;
            set => SetProperty(ref _defaultOptimization, value);
        }

        /// <summary>
        /// Voice guidance volume (0.0 to 1.0)
        /// </summary>
        public float VoiceVolume
        {
            get => _voiceVolume;
            set => SetProperty(ref _voiceVolume, Math.Clamp(value, 0.0f, 1.0f));
        }

        /// <summary>
        /// Voice speech rate (0.5 to 2.0, 1.0 is normal)
        /// </summary>
        public float VoiceSpeechRate
        {
            get => _voiceSpeechRate;
            set => SetProperty(ref _voiceSpeechRate, Math.Clamp(value, 0.5f, 2.0f));
        }

        /// <summary>
        /// Announce traffic alerts and incidents
        /// </summary>
        public bool AnnounceTrafficAlerts
        {
            get => _announceTrafficAlerts;
            set => SetProperty(ref _announceTrafficAlerts, value);
        }

        /// <summary>
        /// Announce speed limits
        /// </summary>
        public bool AnnounceSpeedLimits
        {
            get => _announceSpeedLimits;
            set => SetProperty(ref _announceSpeedLimits, value);
        }

        /// <summary>
        /// Vibrate device on arrival
        /// </summary>
        public bool VibrateOnArrival
        {
            get => _vibrateOnArrival;
            set => SetProperty(ref _vibrateOnArrival, value);
        }

        /// <summary>
        /// Show speed limit warnings when exceeding
        /// </summary>
        public bool ShowSpeedWarnings
        {
            get => _showSpeedWarnings;
            set => SetProperty(ref _showSpeedWarnings, value);
        }

        /// <summary>
        /// Automatically recalculate route when deviation detected
        /// </summary>
        public bool AutoRecalculateOnDeviation
        {
            get => _autoRecalculateOnDeviation;
            set => SetProperty(ref _autoRecalculateOnDeviation, value);
        }

        /// <summary>
        /// Route deviation sensitivity (1=very sensitive, 5=very tolerant)
        /// </summary>
        public int RecalculationSensitivity
        {
            get => _recalculationSensitivity;
            set => SetProperty(ref _recalculationSensitivity, Math.Clamp(value, 1, 5));
        }

        /// <summary>
        /// Save navigation history for analytics
        /// </summary>
        public bool SaveNavigationHistory
        {
            get => _saveNavigationHistory;
            set => SetProperty(ref _saveNavigationHistory, value);
        }

        /// <summary>
        /// Send navigation updates via Telegram
        /// </summary>
        public bool SendTelegramNotifications
        {
            get => _sendTelegramNotifications;
            set => SetProperty(ref _sendTelegramNotifications, value);
        }

        /// <summary>
        /// Enable offline navigation mode
        /// </summary>
        public bool EnableOfflineMode
        {
            get => _enableOfflineMode;
            set => SetProperty(ref _enableOfflineMode, value);
        }

        /// <summary>
        /// Distance threshold for arrival detection (meters)
        /// </summary>
        public double ArrivalThresholdMeters
        {
            get => _arrivalThresholdMeters;
            set => SetProperty(ref _arrivalThresholdMeters, Math.Clamp(value, 5.0, 100.0));
        }

        /// <summary>
        /// Whether voice guidance is effectively enabled
        /// </summary>
        public bool IsVoiceEnabled => VoiceLevel != VoiceGuidanceLevel.Off;

        /// <summary>
        /// Gets the route deviation threshold based on sensitivity setting
        /// </summary>
        public double RouteDeviationThreshold => RecalculationSensitivity switch
        {
            1 => 25.0,  // Very sensitive
            2 => 35.0,  // Sensitive
            3 => 50.0,  // Normal
            4 => 75.0,  // Tolerant
            5 => 100.0, // Very tolerant
            _ => 50.0
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Service for managing navigation preferences
    /// </summary>
    public interface INavigationPreferencesService
    {
        /// <summary>
        /// Gets the current navigation preferences
        /// </summary>
        Task<NavigationPreferences> GetPreferencesAsync();

        /// <summary>
        /// Saves navigation preferences
        /// </summary>
        Task SavePreferencesAsync(NavigationPreferences preferences);

        /// <summary>
        /// Resets preferences to default values
        /// </summary>
        Task ResetToDefaultsAsync();

        /// <summary>
        /// Event fired when preferences change
        /// </summary>
        event EventHandler<NavigationPreferences> PreferencesChanged;
    }

    /// <summary>
    /// Implementation of navigation preferences service
    /// </summary>
    public class NavigationPreferencesService : INavigationPreferencesService
    {
        private const string PREFERENCES_FILE = "navigation_preferences.json";
        private NavigationPreferences? _cachedPreferences;
        
        public event EventHandler<NavigationPreferences>? PreferencesChanged;

        public async Task<NavigationPreferences> GetPreferencesAsync()
        {
            if (_cachedPreferences != null)
                return _cachedPreferences;

            try
            {
                var preferencesPath = Path.Combine(FileSystem.AppDataDirectory, PREFERENCES_FILE);
                
                if (File.Exists(preferencesPath))
                {
                    var json = await File.ReadAllTextAsync(preferencesPath);
                    _cachedPreferences = JsonSerializer.Deserialize<NavigationPreferences>(json) ?? new NavigationPreferences();
                }
                else
                {
                    _cachedPreferences = new NavigationPreferences();
                    await SavePreferencesAsync(_cachedPreferences);
                }

                return _cachedPreferences;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Load preferences error: {ex.Message}");
                return new NavigationPreferences();
            }
        }

        public async Task SavePreferencesAsync(NavigationPreferences preferences)
        {
            try
            {
                var preferencesPath = Path.Combine(FileSystem.AppDataDirectory, PREFERENCES_FILE);
                var json = JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true });
                
                await File.WriteAllTextAsync(preferencesPath, json);
                _cachedPreferences = preferences;
                
                PreferencesChanged?.Invoke(this, preferences);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save preferences error: {ex.Message}");
            }
        }

        public async Task ResetToDefaultsAsync()
        {
            var defaultPreferences = new NavigationPreferences();
            await SavePreferencesAsync(defaultPreferences);
        }
    }
}