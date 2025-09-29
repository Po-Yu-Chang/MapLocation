using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MapLocationApp.Models
{
    public class DatabaseConfig : INotifyPropertyChanged
    {
        private string _host = string.Empty;
        private int _port = 3306;
        private string _databaseName = string.Empty;
        private string _username = string.Empty;
        private string _password = string.Empty;

        public string Host
        {
            get => _host;
            set => SetProperty(ref _host, value);
        }

        public int Port
        {
            get => _port;
            set => SetProperty(ref _port, value);
        }

        public string DatabaseName
        {
            get => _databaseName;
            set => SetProperty(ref _databaseName, value);
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

        public string ConnectionString => 
            $"Server={Host};Port={Port};Database={DatabaseName};Uid={Username};Pwd={Password};";

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