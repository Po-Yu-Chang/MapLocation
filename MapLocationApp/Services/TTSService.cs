using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Globalization;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Cross-platform Text-to-Speech service implementation
    /// Uses Microsoft.Maui.Essentials TextToSpeech for cross-platform support
    /// </summary>
    public class TTSService : ITTSService
    {
        private readonly Dictionary<string, string> _languageCodes = new()
        {
            { "zh-TW", "zh-TW" },  // 繁體中文
            { "zh-CN", "zh-CN" },  // 簡體中文
            { "en-US", "en-US" },  // 英文
            { "ja-JP", "ja-JP" },  // 日文
            { "ko-KR", "ko-KR" }   // 韓文
        };

        private float _speechRate = 1.0f;
        private float _volume = 1.0f;
        private bool _isSpeaking = false;

        public bool IsSupported => true; // TextToSpeech is supported on all MAUI platforms

        public bool IsSpeaking => _isSpeaking;

        public event EventHandler<bool> SpeechCompleted;

        public async Task SpeakAsync(string text, string language = "zh-TW")
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                _isSpeaking = true;

                // Get the appropriate language code
                var localeCode = _languageCodes.ContainsKey(language) ? _languageCodes[language] : "zh-TW";

                // Create speech settings using proper MAUI Essentials API
                var speechOptions = new SpeechOptions
                {
                    Volume = _volume,
                    Pitch = 1.0f, // Normal pitch
                    Locale = CultureInfo.GetCultureInfo(localeCode)
                };

                // Use Microsoft.Maui.Essentials TextToSpeech
                await TextToSpeech.Default.SpeakAsync(text, speechOptions);

                SpeechCompleted?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS Error: {ex.Message}");
                SpeechCompleted?.Invoke(this, false);
            }
            finally
            {
                _isSpeaking = false;
            }
        }

        public async Task SetSpeechRateAsync(float rate)
        {
            if (rate < 0.1f || rate > 2.0f)
                throw new ArgumentOutOfRangeException(nameof(rate), "Speech rate must be between 0.1 and 2.0");

            _speechRate = rate;
            await Task.CompletedTask;
        }

        public async Task SetVolumeAsync(float volume)
        {
            if (volume < 0.0f || volume > 1.0f)
                throw new ArgumentOutOfRangeException(nameof(volume), "Volume must be between 0.0 and 1.0");

            _volume = volume;
            await Task.CompletedTask;
        }

        public async Task StopSpeechAsync()
        {
            try
            {
                // Stop any ongoing speech
                if (_isSpeaking)
                {
                    // TextToSpeech doesn't have a direct stop method, but we can manage the flag
                    _isSpeaking = false;
                }
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TTS Stop Error: {ex.Message}");
            }
        }
    }
}