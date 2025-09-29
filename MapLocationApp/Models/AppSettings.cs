using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Models
{
    public class AppSettings : INotifyPropertyChanged
    {
        private bool _enableNotifications = true;
        private bool _enableGpsTracking = true;
        private bool _autoCheckOut = false;
        private int _checkOutReminderMinutes = 480;
        private string _language = "zh-TW";
        private string _theme = "Auto";

        public bool EnableNotifications
        {
            get => _enableNotifications;
            set => SetProperty(ref _enableNotifications, value);
        }

        public bool EnableGpsTracking
        {
            get => _enableGpsTracking;
            set => SetProperty(ref _enableGpsTracking, value);
        }

        public bool AutoCheckOut
        {
            get => _autoCheckOut;
            set => SetProperty(ref _autoCheckOut, value);
        }

        public int CheckOutReminderMinutes
        {
            get => _checkOutReminderMinutes;
            set => SetProperty(ref _checkOutReminderMinutes, value);
        }

        public string Language
        {
            get => _language;
            set => SetProperty(ref _language, value);
        }

        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        public TimeSpan CheckOutReminderTime => TimeSpan.FromMinutes(CheckOutReminderMinutes);

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
}