using MapLocationApp.Services;

namespace MapLocationApp.Tests.Mocks;

/// <summary>
/// Mock implementation of ITTSService for testing navigation voice guidance
/// </summary>
public class MockTTSService : ITTSService
{
    private readonly List<string> _spokenTexts = new();
    private float _speechRate = 1.0f;
    private float _volume = 1.0f;
    private string _currentLanguage = "zh-TW";

    public bool IsSupported => true;
    public bool IsSpeaking { get; private set; }

    public event EventHandler? SpeechFinished;

    /// <summary>
    /// Get all spoken texts for test verification
    /// </summary>
    public IReadOnlyList<string> SpokenTexts => _spokenTexts.AsReadOnly();

    /// <summary>
    /// Clear spoken texts history
    /// </summary>
    public void ClearSpokenTexts()
    {
        _spokenTexts.Clear();
    }

    public Task SpeakAsync(string text, string language = "zh-TW")
    {
        if (string.IsNullOrWhiteSpace(text))
            return Task.CompletedTask;

        _spokenTexts.Add(text);
        _currentLanguage = language;
        IsSpeaking = true;

        // Simulate speech completion
        Task.Run(async () =>
        {
            await Task.Delay(100); // Simulate speech time
            IsSpeaking = false;
            SpeechFinished?.Invoke(this, EventArgs.Empty);
        });

        return Task.CompletedTask;
    }

    public Task SetSpeechRateAsync(float rate)
    {
        _speechRate = Math.Max(0.1f, Math.Min(2.0f, rate));
        return Task.CompletedTask;
    }

    public Task SetVolumeAsync(float volume)
    {
        _volume = Math.Max(0.0f, Math.Min(1.0f, volume));
        return Task.CompletedTask;
    }

    public Task StopSpeakingAsync()
    {
        IsSpeaking = false;
        return Task.CompletedTask;
    }

    public Task<bool> SetLanguageAsync(string languageCode)
    {
        _currentLanguage = languageCode ?? "zh-TW";
        return Task.FromResult(true);
    }

    /// <summary>
    /// Test helper: Get current language
    /// </summary>
    public string GetCurrentLanguage() => _currentLanguage;

    /// <summary>
    /// Test helper: Get speech rate
    /// </summary>
    public float GetSpeechRate() => _speechRate;

    /// <summary>
    /// Test helper: Get volume
    /// </summary>
    public float GetVolume() => _volume;

    /// <summary>
    /// Test helper: Reset mock to initial state
    /// </summary>
    public void Reset()
    {
        _spokenTexts.Clear();
        _speechRate = 1.0f;
        _volume = 1.0f;
        _currentLanguage = "zh-TW";
        IsSpeaking = false;
    }
}
