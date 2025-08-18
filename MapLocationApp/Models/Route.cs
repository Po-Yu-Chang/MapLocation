using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Models
{
    public class Route : INotifyPropertyChanged
    {
        private string _id = string.Empty;
        private double _startLatitude;
        private double _startLongitude;
        private double _endLatitude;
        private double _endLongitude;
        private string? _startAddress;
        private string? _endAddress;
        private double _distance;
        private TimeSpan _estimatedDuration;
        private DateTime _createdAt;
        private List<RouteStep> _steps = new();
        private RouteType _type;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public double StartLatitude
        {
            get => _startLatitude;
            set => SetProperty(ref _startLatitude, value);
        }

        public double StartLongitude
        {
            get => _startLongitude;
            set => SetProperty(ref _startLongitude, value);
        }

        public double EndLatitude
        {
            get => _endLatitude;
            set => SetProperty(ref _endLatitude, value);
        }

        public double EndLongitude
        {
            get => _endLongitude;
            set => SetProperty(ref _endLongitude, value);
        }

        public string? StartAddress
        {
            get => _startAddress;
            set => SetProperty(ref _startAddress, value);
        }

        public string? EndAddress
        {
            get => _endAddress;
            set => SetProperty(ref _endAddress, value);
        }

        public double Distance
        {
            get => _distance;
            set => SetProperty(ref _distance, value);
        }

        public TimeSpan EstimatedDuration
        {
            get => _estimatedDuration;
            set => SetProperty(ref _estimatedDuration, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public List<RouteStep> Steps
        {
            get => _steps;
            set => SetProperty(ref _steps, value);
        }

        public RouteType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        // 計算屬性
        public string FormattedDistance => Distance < 1 
            ? $"{Distance * 1000:F0} 公尺" 
            : $"{Distance:F1} 公里";

        public string FormattedDuration => EstimatedDuration.TotalHours >= 1 
            ? $"{EstimatedDuration.Hours}小時{EstimatedDuration.Minutes}分鐘"
            : $"{EstimatedDuration.Minutes}分鐘";

        public string DisplayStartAddress => StartAddress ?? $"{StartLatitude:F4}, {StartLongitude:F4}";
        public string DisplayEndAddress => EndAddress ?? $"{EndLatitude:F4}, {EndLongitude:F4}";

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