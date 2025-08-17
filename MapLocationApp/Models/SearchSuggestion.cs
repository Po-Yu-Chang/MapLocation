using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Models
{
    public class SearchSuggestion : INotifyPropertyChanged
    {
        private string _mainText = string.Empty;
        private string _secondaryText = string.Empty;
        private double _latitude;
        private double _longitude;

        public string MainText
        {
            get => _mainText;
            set => SetProperty(ref _mainText, value);
        }

        public string SecondaryText
        {
            get => _secondaryText;
            set => SetProperty(ref _secondaryText, value);
        }

        public double Latitude
        {
            get => _latitude;
            set => SetProperty(ref _latitude, value);
        }

        public double Longitude
        {
            get => _longitude;
            set => SetProperty(ref _longitude, value);
        }

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