namespace MapLocationApp.Views;

public partial class PrivacyPolicyPage : ContentPage
{
    public PrivacyPolicyPage()
    {
        InitializeComponent();
    }

    private async void OnAcceptClicked(object sender, EventArgs e)
    {
        // 保存用戶接受隱私政策的記錄
        Preferences.Set("PrivacyPolicyAccepted", true);
        Preferences.Set("PrivacyPolicyAcceptedDate", DateTime.UtcNow.ToString("O"));
        
        await DisplayAlert("已接受", "感謝您接受我們的隱私政策。", "確定");
        
        // 返回上一頁或導航到主頁
        await Shell.Current.GoToAsync("..");
    }

    private async void OnDeclineClicked(object sender, EventArgs e)
    {
        var result = await DisplayAlert("確認", 
            "拒絕隱私政策將無法使用位置相關功能，確定要拒絕嗎？", 
            "確定拒絕", "重新考慮");
        
        if (result)
        {
            // 保存用戶拒絕的記錄
            Preferences.Set("PrivacyPolicyAccepted", false);
            Preferences.Set("PrivacyPolicyDeclinedDate", DateTime.UtcNow.ToString("O"));
            
            // 可以選擇關閉應用程式或導航到受限功能頁面
            await DisplayAlert("已拒絕", "您已拒絕隱私政策。位置功能將被停用。", "確定");
            await Shell.Current.GoToAsync("..");
        }
    }
}

// 隱私政策檢查器服務
public static class PrivacyPolicyChecker
{
    public static bool IsPrivacyPolicyAccepted()
    {
        return Preferences.Get("PrivacyPolicyAccepted", false);
    }

    public static DateTime? GetAcceptedDate()
    {
        var dateString = Preferences.Get("PrivacyPolicyAcceptedDate", string.Empty);
        if (DateTime.TryParse(dateString, out var date))
            return date;
        return null;
    }

    public static async Task<bool> CheckAndRequestPrivacyPolicyAsync()
    {
        if (IsPrivacyPolicyAccepted())
            return true;

        // 導航到隱私政策頁面
        await Shell.Current.GoToAsync("//PrivacyPolicyPage");
        return false;
    }

    public static void ResetPrivacyPolicy()
    {
        Preferences.Remove("PrivacyPolicyAccepted");
        Preferences.Remove("PrivacyPolicyAcceptedDate");
        Preferences.Remove("PrivacyPolicyDeclinedDate");
    }
}