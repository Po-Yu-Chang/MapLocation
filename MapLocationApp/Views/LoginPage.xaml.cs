using MapLocationApp.Services;
using MapLocationApp.Models;

namespace MapLocationApp.Views;

public partial class LoginPage : ContentPage
{
    private readonly IDatabaseService _databaseService;
    private readonly IConfigService _configService;
    private bool _isLoading = false;

    public LoginPage(IDatabaseService databaseService, IConfigService configService)
    {
        InitializeComponent();
        _databaseService = databaseService;
        _configService = configService;
        
        InitializeAsync();
    }

    private async void InitializeAsync()
    {
        await LoadRememberedCredentials();
        await TestDatabaseConnection();
    }

    private async Task LoadRememberedCredentials()
    {
        try
        {
            var rememberedUser = await _configService.GetRememberedUserAsync();
            if (rememberedUser != null)
            {
                UsernameEntry.Text = rememberedUser.Username;
                if (rememberedUser.RememberMe)
                {
                    PasswordEntry.Text = rememberedUser.Password;
                    RememberMeCheckbox.IsChecked = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"載入記住的帳號失敗: {ex.Message}");
        }
    }

    private async Task TestDatabaseConnection()
    {
        try
        {
            var isConnected = await _databaseService.TestConnectionAsync();
            if (isConnected)
            {
                System.Diagnostics.Debug.WriteLine("資料庫連線成功");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("資料庫連線失敗");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    System.Diagnostics.Debug.WriteLine("警告: 無法連接到資料庫，請檢查網路連線");
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"測試資料庫連線時發生錯誤: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                System.Diagnostics.Debug.WriteLine($"資料庫連線錯誤: {ex.Message}");
            });
        }
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (_isLoading) return;

        await PerformLogin();
    }

    private async Task PerformLogin()
    {
        try
        {
            _isLoading = true;
            UpdateLoginButtonState(true);

            var username = UsernameEntry.Text?.Trim();
            var password = PasswordEntry.Text?.Trim();

            if (string.IsNullOrEmpty(username))
            {
                await ShowErrorMessage("請輸入使用者名稱");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                await ShowErrorMessage("請輸入密碼");
                return;
            }

            var loginResult = await _databaseService.AuthenticateUserAsync(username, password);
            
            if (loginResult.Success)
            {
                if (RememberMeCheckbox.IsChecked)
                {
                    await _configService.SaveRememberedUserAsync(new RememberedUser
                    {
                        Username = username,
                        Password = password,
                        RememberMe = true
                    });
                }
                else
                {
                    await _configService.ClearRememberedUserAsync();
                }

                if (loginResult.User != null)
                {
                    var userSessionService = ServiceHelper.GetService<IUserSessionService>();
                    await userSessionService.LoginAsync(loginResult.User);
                }
                
                ShowSuccessMessage("登入成功");
                
                await Task.Delay(500);
                
                await Shell.Current.GoToAsync("//CheckInPage");
            }
            else
            {
                await ShowErrorMessage(loginResult.ErrorMessage ?? "登入失敗，請檢查帳號密碼");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"登入過程發生錯誤: {ex.Message}");
            await ShowErrorMessage("登入過程發生錯誤，請稍後再試");
        }
        finally
        {
            _isLoading = false;
            UpdateLoginButtonState(false);
        }
    }

    private void UpdateLoginButtonState(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoginButton.IsEnabled = !isLoading;
            LoginButton.Text = isLoading ? "登入中..." : "登入";
        });
    }

    private async Task ShowErrorMessage(string message)
    {
        try
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlert("錯誤", message, "確定");
            });
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"錯誤訊息: {message}");
        }
    }

    private void ShowSuccessMessage(string message)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                System.Diagnostics.Debug.WriteLine($"成功訊息: {message}");
            });
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine($"成功訊息: {message}");
        }
    }

    private void OnForgotPasswordTapped(object sender, TappedEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                System.Diagnostics.Debug.WriteLine("忘記密碼功能尚未實作");
            });
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine("忘記密碼功能尚未實作");
        }
    }

    private void OnRegisterTapped(object sender, TappedEventArgs e)
    {
        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                System.Diagnostics.Debug.WriteLine("註冊功能尚未實作");
            });
        }
        catch
        {
            System.Diagnostics.Debug.WriteLine("註冊功能尚未實作");
        }
    }
}