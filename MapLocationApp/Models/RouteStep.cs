using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Models
{
    public class RouteStep : INotifyPropertyChanged
    {
        private int _index;
        private string _instruction = string.Empty;
        private double _startLatitude;
        private double _startLongitude;
        private double _endLatitude;
        private double _endLongitude;
        private double _distance;
        private TimeSpan _duration;
        private StepType _type;

        public int Index
        {
            get => _index;
            set => SetProperty(ref _index, value);
        }

        public string Instruction
        {
            get => _instruction;
            set => SetProperty(ref _instruction, value);
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

        public double Distance
        {
            get => _distance;
            set => SetProperty(ref _distance, value);
        }

        public TimeSpan Duration
        {
            get => _duration;
            set => SetProperty(ref _duration, value);
        }

        public StepType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        public string FormattedDistance => Distance < 1000 
            ? $"{Distance:F0} 公尺" 
            : $"{Distance / 1000:F1} 公里";

        public string FormattedDuration => Duration.TotalMinutes < 1 
            ? "不到1分鐘" 
            : $"{Duration.Minutes}分鐘";

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

    public enum StepType
    {
        Straight,
        TurnLeft,
        TurnRight,
        UTurn,
        RoundaboutEnter,
        RoundaboutExit,
        Merge,
        Exit
    }
}