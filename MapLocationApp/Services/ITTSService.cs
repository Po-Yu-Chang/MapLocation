using System;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// Text-to-Speech service interface for navigation voice guidance
    /// </summary>
    public interface ITTSService
    {
        /// <summary>
        /// Speaks the given text in the specified language
        /// </summary>
        /// <param name="text">Text to speak</param>
        /// <param name="language">Language code (e.g., "zh-TW", "en-US")</param>
        /// <returns>Task representing the speech operation</returns>
        Task SpeakAsync(string text, string language = "zh-TW");

        /// <summary>
        /// Sets the speech rate (speed)
        /// </summary>
        /// <param name="rate">Speech rate from 0.1 to 2.0 (1.0 is normal speed)</param>
        /// <returns>Task representing the operation</returns>
        Task SetSpeechRateAsync(float rate);

        /// <summary>
        /// Sets the speech volume
        /// </summary>
        /// <param name="volume">Volume from 0.0 to 1.0</param>
        /// <returns>Task representing the operation</returns>
        Task SetVolumeAsync(float volume);

        /// <summary>
        /// Stops any currently playing speech
        /// </summary>
        /// <returns>Task representing the operation</returns>
        Task StopSpeechAsync();

        /// <summary>
        /// Gets whether TTS is supported on the current platform
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Gets whether speech is currently in progress
        /// </summary>
        bool IsSpeaking { get; }

        /// <summary>
        /// Event fired when speech completes
        /// </summary>
        event EventHandler<bool> SpeechCompleted;
    }
}