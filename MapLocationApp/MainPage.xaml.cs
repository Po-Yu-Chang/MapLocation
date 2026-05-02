using MapLocationApp.Views;
using MapLocationApp.Animations;
using Microsoft.Maui.Controls;

namespace MapLocationApp;

public partial class MainPage : ContentPage
{
    private readonly List<View> _animatedElements = new();
    private bool _isPageLoaded = false;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        UpdateDashboard();

        if (!_isPageLoaded)
        {
            await InitializeAnimations();
            _isPageLoaded = true;
        }
    }

    private async void UpdateDashboard()
    {
        try
        {
            // Localized greeting based on time of day; fallback to a neutral system label.
            var hour = DateTime.Now.Hour;
            string greetKey = hour < 5 ? "GreetingNight"
                            : hour < 12 ? "GreetingMorning"
                            : hour < 18 ? "GreetingAfternoon"
                            : "GreetingEvening";
            GreetingLabel.Text = Services.LocalizationService.Instance.GetLocalizedString(greetKey);

            TodayDateLabel.Text = DateTime.Today.ToString("yyyy / MM / dd · ddd");

            // Pull this month's records and compute simple KPIs.
            var session = Services.ServiceHelper.GetService<Services.IUserSessionService>();
            var db = Services.ServiceHelper.GetService<Services.IDatabaseService>();
            var user = await (session?.GetCurrentUserAsync() ?? Task.FromResult<Models.User?>(null));
            if (user == null || db == null)
            {
                MonthCountLabel.Text = "0";
                WeekHoursLabel.Text = "0.0";
                TodayStatusLabel.Text = Services.LocalizationService.Instance.GetLocalizedString("NotLoggedIn");
                return;
            }

            var firstOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var monthRecords = (await db.GetCheckInRecordsAsync(user.Id))
                .Where(r => r.CheckInTime >= firstOfMonth)
                .ToList();
            MonthCountLabel.Text = monthRecords.Count.ToString();

            // ISO week: Monday = start.
            var daysFromMonday = ((int)DateTime.Today.DayOfWeek + 6) % 7;
            var weekStart = DateTime.Today.AddDays(-daysFromMonday);
            var weekHours = monthRecords
                .Where(r => r.CheckInTime >= weekStart && r.CheckOutTime.HasValue)
                .Sum(r => (r.CheckOutTime!.Value - r.CheckInTime).TotalHours);
            WeekHoursLabel.Text = weekHours.ToString("F1");

            var todayRecord = monthRecords.FirstOrDefault(r => r.CheckInTime.Date == DateTime.Today);
            TodayStatusLabel.Text = todayRecord == null
                ? Services.LocalizationService.Instance.GetLocalizedString("NotCheckedInToday")
                : todayRecord.CheckOutTime.HasValue
                    ? string.Format(Services.LocalizationService.Instance.GetLocalizedString("ClockedInOutAt"),
                        todayRecord.CheckInTime.ToString("HH:mm"),
                        todayRecord.CheckOutTime.Value.ToString("HH:mm"))
                    : string.Format(Services.LocalizationService.Instance.GetLocalizedString("ClockedInAt"),
                        todayRecord.CheckInTime.ToString("HH:mm"));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateDashboard failed: {ex.Message}");
        }
    }

    private async Task InitializeAnimations()
    {
        // 收集所有需要動畫的卡片元素
        var cards = this.GetVisualTreeDescendants()
            .OfType<Border>()
            .Where(b => b.StyleId == "LiquidGlassCard" || b.Style?.ToString().Contains("LiquidGlass") == true)
            .ToArray();

        // 添加按鈕動畫效果
        var buttons = this.GetVisualTreeDescendants()
            .OfType<Button>()
            .ToArray();

        foreach (var button in buttons)
        {
            AddButtonAnimations(button);
        }

        // 執行頁面載入動畫
        if (cards.Length > 0)
        {
            await LiquidGlassAnimations.PageLoadAnimation(cards);
        }

        // 啟動狀態指示器動畫
        var statusIndicators = this.GetVisualTreeDescendants()
            .OfType<Border>()
            .Where(b => b.Style?.ToString().Contains("StatusIndicator") == true)
            .ToArray();

        foreach (var indicator in statusIndicators)
        {
            LiquidGlassAnimations.StartBreathingAnimation(indicator, 3000);
        }
    }

    private void AddButtonAnimations(Button button)
    {
        // 添加觸控動畫
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) =>
        {
            await LiquidGlassAnimations.ButtonPressAnimation(button);
        };

        // 添加懸停效果 (主要用於桌面平台)
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += async (s, e) =>
        {
            await LiquidGlassAnimations.CardHoverAnimation(button, true);
        };
        pointerGesture.PointerExited += async (s, e) =>
        {
            await LiquidGlassAnimations.CardHoverAnimation(button, false);
        };

        button.GestureRecognizers.Add(pointerGesture);
    }

    private async void OnMapClicked(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            await LiquidGlassAnimations.ButtonPressAnimation(button);
            await Task.Delay(100); // 讓動畫完成
        }
        await Shell.Current.GoToAsync("//MapPage");
    }

    private async void OnCheckInClicked(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            await LiquidGlassAnimations.ButtonPressAnimation(button);
            await Task.Delay(100);
        }
        await Shell.Current.GoToAsync("//CheckInPage");
    }

    private async void OnPrivacyClicked(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            await LiquidGlassAnimations.ButtonPressAnimation(button);
            await Task.Delay(100);
        }
        await Shell.Current.GoToAsync("//PrivacyPolicyPage");
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            await LiquidGlassAnimations.ButtonPressAnimation(button);
            await Task.Delay(100);
        }
        await Shell.Current.GoToAsync("//SettingsPage");
    }

    private async void OnFaceRecognitionClicked(object? sender, EventArgs e)
    {
        if (sender is Button button)
        {
            await LiquidGlassAnimations.ButtonPressAnimation(button);
            await Task.Delay(100);
        }
        await Shell.Current.GoToAsync("//FaceRecognitionPage");
    }

    private async void OnQuickLocationClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button) return;

        try
        {
            // 按鈕動畫
            await LiquidGlassAnimations.ButtonPressAnimation(button);

            // 更新按鈕狀態
            button.Text = "🔄 正在獲取位置...";
            button.IsEnabled = false;

            // 開始載入動畫
            await LiquidGlassAnimations.ElasticLoadAnimation(button);

            var location = await Geolocation.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.Medium,
                Timeout = TimeSpan.FromSeconds(10)
            });

            if (location != null)
            {
                // 成功動畫
                await LiquidGlassAnimations.SuccessFeedbackAnimation(button);

                await DisplayAlert("位置資訊",
                    $"緯度: {location.Latitude:F6}\n經度: {location.Longitude:F6}\n精確度: ±{location.Accuracy:F0}公尺",
                    "確定");
            }
            else
            {
                // 錯誤動畫
                await LiquidGlassAnimations.ErrorFeedbackAnimation(button);
                await DisplayAlert("錯誤", "無法獲取位置", "確定");
            }
        }
        catch (Exception ex)
        {
            // 錯誤動畫
            await LiquidGlassAnimations.ErrorFeedbackAnimation(button);
            await DisplayAlert("錯誤", $"位置獲取失敗: {ex.Message}", "確定");
        }
        finally
        {
            // 恢復按鈕狀態
            button.Text = "📍 獲取位置";
            button.IsEnabled = true;

            // 重置動畫
            button.Scale = 1.0;
            button.TranslationX = 0;
            button.TranslationY = 0;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // 停止所有動畫
        var statusIndicators = this.GetVisualTreeDescendants()
            .OfType<Border>()
            .Where(b => b.Style?.ToString().Contains("StatusIndicator") == true)
            .ToArray();

        foreach (var indicator in statusIndicators)
        {
            LiquidGlassAnimations.StopBreathingAnimation(indicator);
        }
    }
}
