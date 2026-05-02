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
        private string? _avatarPath;
        private string? _phoneNumber;
        private TimeSpan? _workHoursStart;
        private TimeSpan? _workHoursEnd;
        private UserRole _role = UserRole.Employee;

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

        /// <summary>頭像檔案路徑（相對於 FileSystem.AppDataDirectory）。</summary>
        public string? AvatarPath
        {
            get => _avatarPath;
            set => SetProperty(ref _avatarPath, value);
        }

        public string? PhoneNumber
        {
            get => _phoneNumber;
            set => SetProperty(ref _phoneNumber, value);
        }

        /// <summary>預設上班時間（用於上下班提醒與排班預設值）。</summary>
        public TimeSpan? WorkHoursStart
        {
            get => _workHoursStart;
            set => SetProperty(ref _workHoursStart, value);
        }

        /// <summary>預設下班時間。</summary>
        public TimeSpan? WorkHoursEnd
        {
            get => _workHoursEnd;
            set => SetProperty(ref _workHoursEnd, value);
        }

        /// <summary>使用者角色，控制 admin-only 功能可見性。</summary>
        public UserRole Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }

        public bool IsAdmin => _role == UserRole.Admin;

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