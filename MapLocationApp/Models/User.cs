using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Models
{
    public class User : INotifyPropertyChanged
    {
        private int _id;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string? _email;
        private string? _fullName;
        private string? _department;
        private string? _position;
        private bool _isActive = true;
        private bool _mustChangePassword = false;
        private DateTime _createdAt;
        private DateTime? _lastLoginAt;

        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        public string? Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        public string? FullName
        {
            get => _fullName;
            set => SetProperty(ref _fullName, value);
        }

        public string? Department
        {
            get => _department;
            set => SetProperty(ref _department, value);
        }

        public string? Position
        {
            get => _position;
            set => SetProperty(ref _position, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public bool MustChangePassword
        {
            get => _mustChangePassword;
            set => SetProperty(ref _mustChangePassword, value);
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }

        public DateTime? LastLoginAt
        {
            get => _lastLoginAt;
            set => SetProperty(ref _lastLoginAt, value);
        }

        public string DisplayName => !string.IsNullOrEmpty(FullName) ? FullName : Username;

        public string DepartmentPosition => 
            !string.IsNullOrEmpty(Department) && !string.IsNullOrEmpty(Position) 
                ? $"{Department} / {Position}" 
                : Department ?? Position ?? "";

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