namespace MapLocationApp;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		
		// 載入並套用儲存的主題設定
		LoadThemeSettings();
	}

	private void LoadThemeSettings()
	{
		try
		{
			// 從 Preferences 讀取儲存的主題設定
			var savedTheme = Preferences.Get("AppTheme", (int)AppTheme.Unspecified);
			var theme = (AppTheme)savedTheme;
			
			// 套用主題
			this.UserAppTheme = theme;
		}
		catch (Exception ex)
		{
			// 如果載入失敗，使用預設主題（跟隨系統）
			System.Diagnostics.Debug.WriteLine($"載入主題設定失敗: {ex.Message}");
			this.UserAppTheme = AppTheme.Unspecified;
		}
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}