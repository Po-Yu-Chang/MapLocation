using System;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Text-to-Speech service interface for voice navigation
    /// Supports multi-language voice guidance with configurable speech rate and volume
    /// </summary>
    public interface ITTSService
    {
        /// <summary>
        /// Speaks the given text in the specified language
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="language">Language code (e.g., "zh-TW", "en-US")</param>
        /// <returns>Task that completes when speech is finished</returns>
        Task SpeakAsync(string text, string language = "zh-TW");

        /// <summary>
        /// Sets the speech rate (speed of speaking)
        /// </summary>
        /// <param name="rate">Speech rate from 0.0 to 2.0 (1.0 is normal)</param>
        /// <returns>Task that completes when rate is set</returns>
        Task SetSpeechRateAsync(float rate);

        /// <summary>
        /// Sets the speech volume
        /// </summary>
        /// <param name="volume">Volume from 0.0 to 1.0</param>
        /// <returns>Task that completes when volume is set</returns>
        Task SetVolumeAsync(float volume);

        /// <summary>
        /// Stops any current speech
        /// </summary>
        /// <returns>Task that completes when speech is stopped</returns>
        Task StopAsync();

        /// <summary>
        /// Gets whether TTS is supported on the current platform
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Gets whether TTS is currently speaking
        /// </summary>
        bool IsSpeaking { get; }

        /// <summary>
        /// Event fired when TTS finishes speaking
        /// </summary>
        event EventHandler<bool> SpeechFinished;
    }

    /// <summary>
    /// Localized TTS service that supports multiple languages
    /// </summary>
    public class LocalizedTTSService : ITTSService
    {
        private readonly Dictionary<string, string> _languageCodes = new()
        {
            { "zh-TW", "zh-TW" },  // 繁體中文
            { "zh-CN", "zh-CN" },  // 簡體中文
            { "en-US", "en-US" },  // 英文
            { "ja-JP", "ja-JP" },  // 日文
            { "ko-KR", "ko-KR" }   // 韓文
        };

        private float _currentRate = 1.0f;
        private float _currentVolume = 1.0f;
        private bool _isSpeaking = false;

        public bool IsSupported => Microsoft.Maui.Authentication.WebAuthenticatorResult.SUPPORTED;
        public bool IsSpeaking => _isSpeaking;

        public event EventHandler<bool>? SpeechFinished;

        public async Task SpeakAsync(string text, string language = "zh-TW")
        {
            try
            {
                if (!IsSupported || string.IsNullOrWhiteSpace(text))
                    return;

                _isSpeaking = true;

                // Use Microsoft.Maui.Essentials.TextToSpeech when available in real MAUI environment
                // For now, simulate the TTS functionality
                await SimulateTTSAsync(text, language);

                _isSpeaking = false;
                SpeechFinished?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                _isSpeaking = false;
                System.Diagnostics.Debug.WriteLine($"TTS Error: {ex.Message}");
                SpeechFinished?.Invoke(this, false);
            }
        }

        public async Task SetSpeechRateAsync(float rate)
        {
            _currentRate = Math.Clamp(rate, 0.1f, 3.0f);
            await Task.CompletedTask;
        }

        public async Task SetVolumeAsync(float volume)
        {
            _currentVolume = Math.Clamp(volume, 0.0f, 1.0f);
            await Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            _isSpeaking = false;
            await Task.CompletedTask;
        }

        private async Task SimulateTTSAsync(string text, string language)
        {
            // Simulate TTS delay based on text length and speech rate
            var baseDelay = text.Length * 50; // 50ms per character
            var adjustedDelay = (int)(baseDelay / _currentRate);
            
            await Task.Delay(Math.Max(adjustedDelay, 500)); // Minimum 500ms delay
        }

        /// <summary>
        /// Gets the platform-specific language code
        /// </summary>
        public string GetLanguageCode(string requestedLanguage)
        {
            return _languageCodes.TryGetValue(requestedLanguage, out var code) 
                ? code 
                : "zh-TW"; // Default to Traditional Chinese
        }
    }
}