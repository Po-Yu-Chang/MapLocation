using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Services;

/// <summary>
/// Reactive localization manager — implement INotifyPropertyChanged so XAML bindings
/// automatically refresh when the language changes.
/// Usage in XAML: Text="{Binding [KeyName], Source={x:Static svc:LocalizationManager.Instance}}"
/// </summary>
public class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    private LocalizationManager()
    {
        LocalizationService.Instance.CultureChanged += _ =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    /// <summary>Returns the localized string for the given resource key.</summary>
    public string this[string key] => LocalizationService.Instance.GetLocalizedString(key);

    public event PropertyChangedEventHandler? PropertyChanged;
}
