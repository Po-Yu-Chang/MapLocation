using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using MapLocationApp.Models;
using MapLocationApp.Services;

namespace MapLocationApp.Views
{
    /// <summary>
    /// Navigation preferences settings page
    /// </summary>
    public partial class NavigationSettingsPage : ContentPage, INotifyPropertyChanged
    {
        private readonly INavigationService _navigationService;
        private NavigationPreferences _preferences;

        // UI Properties
        private bool _avoidTolls;
        private bool _avoidHighways;
        private bool _avoidFerries;
        private VoiceGuidanceLevel _voiceLevel;
        private string _preferredLanguage;
        private RouteOptimization _defaultOptimization;
        private float _speechRate;
        private float _speechVolume;
        private bool _vibrateOnTurn;
        private bool _showLaneGuidance;
        private int _arrivalNotificationDistance;

        public NavigationSettingsPage()
        {
            InitializeComponent();
            
            _navigationService = ServiceHelper.GetService<INavigationService>();
            LoadPreferences();
            
            BindingContext = this;
        }

        #region Public Properties

        public bool AvoidTolls
        {
            get => _avoidTolls;
            set => SetProperty(ref _avoidTolls, value);
        }

        public bool AvoidHighways
        {
            get => _avoidHighways;
            set => SetProperty(ref _avoidHighways, value);
        }

        public bool AvoidFerries
        {
            get => _avoidFerries;
            set => SetProperty(ref _avoidFerries, value);
        }

        public VoiceGuidanceLevel VoiceLevel
        {
            get => _voiceLevel;
            set => SetProperty(ref _voiceLevel, value);
        }

        public string PreferredLanguage
        {
            get => _preferredLanguage;
            set => SetProperty(ref _preferredLanguage, value);
        }

        public RouteOptimization DefaultOptimization
        {
            get => _defaultOptimization;
            set => SetProperty(ref _defaultOptimization, value);
        }

        public float SpeechRate
        {
            get => _speechRate;
            set => SetProperty(ref _speechRate, value);
        }

        public float SpeechVolume
        {
            get => _speechVolume;
            set => SetProperty(ref _speechVolume, value);
        }

        public bool VibrateOnTurn
        {
            get => _vibrateOnTurn;
            set => SetProperty(ref _vibrateOnTurn, value);
        }

        public bool ShowLaneGuidance
        {
            get => _showLaneGuidance;
            set => SetProperty(ref _showLaneGuidance, value);
        }

        public int ArrivalNotificationDistance
        {
            get => _arrivalNotificationDistance;
            set => SetProperty(ref _arrivalNotificationDistance, value);
        }

        public List<VoiceGuidanceLevel> VoiceGuidanceLevels { get; } = new()
        {
            VoiceGuidanceLevel.Off,
            VoiceGuidanceLevel.Essential,
            VoiceGuidanceLevel.Normal,
            VoiceGuidanceLevel.Detailed
        };

        public List<string> AvailableLanguages { get; } = new()
        {
            "zh-TW", "zh-CN", "en-US", "ja-JP", "ko-KR"
        };

        public List<RouteOptimization> RouteOptimizations { get; } = new()
        {
            RouteOptimization.Time,
            RouteOptimization.Distance,
            RouteOptimization.EcoFriendly,
            RouteOptimization.Balanced
        };

        public string VoiceLevelDescription
        {
            get
            {
                return VoiceLevel switch
                {
                    VoiceGuidanceLevel.Off => "關閉語音導航",
                    VoiceGuidanceLevel.Essential => "僅重要指示",
                    VoiceGuidanceLevel.Normal => "標準語音導航",
                    VoiceGuidanceLevel.Detailed => "詳細語音指引",
                    _ => "標準語音導航"
                };
            }
        }

        #endregion

        #region Event Handlers

        private async void OnSaveButtonClicked(object sender, EventArgs e)
        {
            try
            {
                await SavePreferences();
                await DisplayAlert("✅ 成功", "導航設定已儲存", "確定");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ 錯誤", $"儲存設定時發生錯誤: {ex.Message}", "確定");
            }
        }

        private async void OnResetButtonClicked(object sender, EventArgs e)
        {
            try
            {
                bool confirm = await DisplayAlert("確認重設", "確定要重設為預設值嗎？", "重設", "取消");
                if (confirm)
                {
                    ResetToDefaults();
                    await DisplayAlert("✅ 成功", "已重設為預設值", "確定");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ 錯誤", $"重設設定時發生錯誤: {ex.Message}", "確定");
            }
        }

        private async void OnTestVoiceButtonClicked(object sender, EventArgs e)
        {
            try
            {
                var ttsService = ServiceHelper.GetService<ITTSService>();
                if (ttsService != null)
                {
                    await ttsService.SetSpeechRateAsync(SpeechRate);
                    await ttsService.SetVolumeAsync(SpeechVolume);
                    
                    var testMessage = PreferredLanguage switch
                    {
                        "en-US" => "This is a test of voice navigation",
                        "ja-JP" => "これは音声ナビゲーションのテストです",
                        "ko-KR" => "음성 내비게이션 테스트입니다",
                        "zh-CN" => "这是语音导航测试",
                        _ => "這是語音導航測試"
                    };

                    await ttsService.SpeakAsync(testMessage, PreferredLanguage);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("❌ 錯誤", $"測試語音時發生錯誤: {ex.Message}", "確定");
            }
        }

        private void OnVoiceLevelChanged(object sender, EventArgs e)
        {
            OnPropertyChanged(nameof(VoiceLevelDescription));
        }

        #endregion

        #region Private Methods

        private void LoadPreferences()
        {
            _preferences = _navigationService?.Preferences ?? new NavigationPreferences();

            AvoidTolls = _preferences.AvoidTolls;
            AvoidHighways = _preferences.AvoidHighways;
            AvoidFerries = _preferences.AvoidFerries;
            VoiceLevel = _preferences.VoiceLevel;
            PreferredLanguage = _preferences.PreferredLanguage;
            DefaultOptimization = _preferences.DefaultOptimization;
            SpeechRate = _preferences.SpeechRate;
            SpeechVolume = _preferences.SpeechVolume;
            VibrateOnTurn = _preferences.VibrateOnTurn;
            ShowLaneGuidance = _preferences.ShowLaneGuidance;
            ArrivalNotificationDistance = _preferences.ArrivalNotificationDistance;
        }

        private async System.Threading.Tasks.Task SavePreferences()
        {
            _preferences.AvoidTolls = AvoidTolls;
            _preferences.AvoidHighways = AvoidHighways;
            _preferences.AvoidFerries = AvoidFerries;
            _preferences.VoiceLevel = VoiceLevel;
            _preferences.PreferredLanguage = PreferredLanguage;
            _preferences.DefaultOptimization = DefaultOptimization;
            _preferences.SpeechRate = SpeechRate;
            _preferences.SpeechVolume = SpeechVolume;
            _preferences.VibrateOnTurn = VibrateOnTurn;
            _preferences.ShowLaneGuidance = ShowLaneGuidance;
            _preferences.ArrivalNotificationDistance = ArrivalNotificationDistance;

            if (_navigationService != null)
            {
                _navigationService.Preferences = _preferences;
            }

            // Save to persistent storage
            await SavePreferencesToStorage(_preferences);
        }

        private void ResetToDefaults()
        {
            var defaults = new NavigationPreferences();
            
            AvoidTolls = defaults.AvoidTolls;
            AvoidHighways = defaults.AvoidHighways;
            AvoidFerries = defaults.AvoidFerries;
            VoiceLevel = defaults.VoiceLevel;
            PreferredLanguage = defaults.PreferredLanguage;
            DefaultOptimization = defaults.DefaultOptimization;
            SpeechRate = defaults.SpeechRate;
            SpeechVolume = defaults.SpeechVolume;
            VibrateOnTurn = defaults.VibrateOnTurn;
            ShowLaneGuidance = defaults.ShowLaneGuidance;
            ArrivalNotificationDistance = defaults.ArrivalNotificationDistance;
        }

        private async System.Threading.Tasks.Task SavePreferencesToStorage(NavigationPreferences preferences)
        {
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(preferences);
                await SecureStorage.SetAsync("NavigationPreferences", json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving preferences: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task<NavigationPreferences> LoadPreferencesFromStorage()
        {
            try
            {
                var json = await SecureStorage.GetAsync("NavigationPreferences");
                if (!string.IsNullOrEmpty(json))
                {
                    return System.Text.Json.JsonSerializer.Deserialize<NavigationPreferences>(json);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading preferences: {ex.Message}");
            }

            return new NavigationPreferences();
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public new event PropertyChangedEventHandler PropertyChanged;

        protected new virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}

// Note: The XAML file would need to be created separately with appropriate UI elements
// This is just the code-behind implementation