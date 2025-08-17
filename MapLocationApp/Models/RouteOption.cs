using System.ComponentModel;
using System.Runtime.CompilerServices;
using MapLocationApp.Services;

namespace MapLocationApp.Models
{
    public class RouteOption : INotifyPropertyChanged
    {
        private string _duration = string.Empty;
        private string _distance = string.Empty;
        private string _description = string.Empty;
        private string _trafficColor = "#4CAF50";
        private string _trafficInfo = string.Empty;
        private bool _isSelected;
        private Route? _route;

        public string Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public string Distance
        {
            get => _distance;
            set => SetProperty(ref _distance, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public string TrafficColor
        {
            get => _trafficColor;
            set => SetProperty(ref _trafficColor, value);
        }

        public string TrafficInfo
        {
            get => _trafficInfo;
            set => SetProperty(ref _trafficInfo, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public Route? Route
        {
            get => _route;
            set => SetProperty(ref _route, value);
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