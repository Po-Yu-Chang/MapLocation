using System.Globalization;
using System.Resources;
using MapLocationApp.Services.Interfaces;

namespace MapLocationApp.Services
{
    public class LocalizationService : ILocalizationService
    {
        private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
        public static LocalizationService Instance => _instance.Value;

        private ResourceManager _resourceManager;
        private CultureInfo _currentCulture;

        public LocalizationService()
        {
            _resourceManager = new ResourceManager("MapLocationApp.Resources.Languages.AppResources", typeof(LocalizationService).Assembly);
            _currentCulture = CultureInfo.CurrentCulture;
        }

        public string GetLocalizedString(string key)
        {
            try
            {
                var value = _resourceManager.GetString(key, _currentCulture);
                return value ?? key; // 如果找不到翻譯，回傳原始key
            }
            catch
            {
                return key;
            }
        }

        public Task<bool> SetLanguageAsync(string languageCode)
        {
            try
            {
                _currentCulture = new CultureInfo(languageCode);
                CultureInfo.CurrentCulture = _currentCulture;
                CultureInfo.CurrentUICulture = _currentCulture;

                // 通知文化變更
                CultureChanged?.Invoke(_currentCulture);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set culture: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public void SetCulture(string cultureCode)
        {
            try
            {
                _currentCulture = new CultureInfo(cultureCode);
                CultureInfo.CurrentCulture = _currentCulture;
                CultureInfo.CurrentUICulture = _currentCulture;

                // 通知文化變更
                CultureChanged?.Invoke(_currentCulture);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set culture: {ex.Message}");
            }
        }

        public Task<List<string>> GetSupportedLanguagesAsync()
        {
            var languages = new List<string>
            {
                "zh-TW",
                "zh-CN",
                "en-US",
                "th-TH"
            };
            return Task.FromResult(languages);
        }

        public Task<string> GetCurrentLanguageAsync()
        {
            return Task.FromResult(_currentCulture.Name);
        }

        public CultureInfo GetCurrentCulture()
        {
            return _currentCulture;
        }

        public List<CultureInfo> GetSupportedCultures()
        {
            return new List<CultureInfo>
            {
                new CultureInfo("en-US"),
                new CultureInfo("zh-TW"),
                new CultureInfo("zh-CN"),
                new CultureInfo("ja-JP"),
                new CultureInfo("ko-KR")
            };
        }

        public event Action<CultureInfo>? CultureChanged;

        // 便利方法
        public string this[string key] => GetLocalizedString(key);
    }

    // 標記延伸，用於 XAML 綁定
    public class LocalizeExtension : IMarkupExtension
    {
        public string Key { get; set; }

        public object ProvideValue(IServiceProvider serviceProvider)
        {
            if (Key == null)
                return "";

            return LocalizationService.Instance.GetLocalizedString(Key);
        }
    }
}