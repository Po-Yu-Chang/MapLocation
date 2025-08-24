using MapLocationApp.Models;

namespace MapLocationApp.Services
{
    public interface IUserSessionService
    {
        bool IsLoggedIn { get; }
        User? CurrentUser { get; }
        Task<bool> LoginAsync(User user);
        Task LogoutAsync();
        Task<User?> GetCurrentUserAsync();
        event EventHandler<User>? UserLoggedIn;
        event EventHandler? UserLoggedOut;
    }

    public class UserSessionService : IUserSessionService
    {
        private readonly IConfigService _configService;
        private User? _currentUser;

        public bool IsLoggedIn => _currentUser != null;
        public User? CurrentUser => _currentUser;

        public event EventHandler<User>? UserLoggedIn;
        public event EventHandler? UserLoggedOut;

        public UserSessionService(IConfigService configService)
        {
            _configService = configService;
            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            try
            {
                _currentUser = await _configService.GetCurrentUserAsync();
                if (_currentUser != null)
                {
                    UserLoggedIn?.Invoke(this, _currentUser);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初始化用戶會話失敗: {ex.Message}");
            }
        }

        public async Task<bool> LoginAsync(User user)
        {
            try
            {
                _currentUser = user;
                await _configService.SaveCurrentUserAsync(user);
                UserLoggedIn?.Invoke(this, user);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"登入用戶會話失敗: {ex.Message}");
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                _currentUser = null;
                await _configService.ClearCurrentUserAsync();
                UserLoggedOut?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"登出用戶會話失敗: {ex.Message}");
            }
        }

        public async Task<User?> GetCurrentUserAsync()
        {
            if (_currentUser != null)
                return _currentUser;

            try
            {
                _currentUser = await _configService.GetCurrentUserAsync();
                return _currentUser;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"取得當前用戶失敗: {ex.Message}");
                return null;
            }
        }
    }
}