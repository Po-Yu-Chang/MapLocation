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
        private readonly Lazy<IDatabaseService?> _databaseService;
        private User? _currentUser;

        public bool IsLoggedIn => _currentUser != null;
        public User? CurrentUser => _currentUser;

        public event EventHandler<User>? UserLoggedIn;
        public event EventHandler? UserLoggedOut;

        public UserSessionService(IConfigService configService)
        {
            _configService = configService;
            // Resolved lazily to avoid DI-ordering issues on startup; we only need the DB at refresh time.
            _databaseService = new Lazy<IDatabaseService?>(() =>
            {
                try { return MauiProgram.Services?.GetService(typeof(IDatabaseService)) as IDatabaseService; }
                catch { return null; }
            });
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                _currentUser = await _configService.GetCurrentUserAsync();
                if (_currentUser != null)
                {
                    // Refresh from DB so newly-added columns (e.g. role) overwrite stale cache.
                    // If DB is unreachable we fall back to the cached user — better than logging out.
                    try
                    {
                        var fresh = await (_databaseService.Value?.GetUserByIdAsync(_currentUser.Id)
                                          ?? Task.FromResult<User?>(null));
                        if (fresh != null)
                        {
                            _currentUser = fresh;
                            await _configService.SaveCurrentUserAsync(fresh);
                        }
                    }
                    catch (Exception refreshEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"重新整理使用者資料失敗（使用快取）: {refreshEx.Message}");
                    }

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