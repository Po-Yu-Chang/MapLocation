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

    private async void OnExportDataClicked(object sender, EventArgs e)
    {
        try
        {
            await DisplayAlert("📄 匯出功能", 
                "資料匯出功能開發中...\n\n您可以聯絡我們的客服團隊來獲取您的個人資料副本。", 
                "確定");
        }
        catch (Exception ex)
        {
            await DisplayAlert("錯誤", $"匯出失敗: {ex.Message}", "確定");
        }
    }

    private async void OnDeleteDataClicked(object sender, EventArgs e)
    {
        var result = await DisplayAlert("⚠️ 確認刪除", 
            "此操作將永久刪除您的所有資料，包括：\n\n• 所有打卡記錄\n• 位置歷史\n• 個人設定\n\n此操作無法復原，確定要繼續嗎？", 
            "確定刪除", "取消");
        
        if (result)
        {
            var finalConfirm = await DisplayAlert("🗑️ 最終確認", 
                "您真的確定要刪除所有資料嗎？此操作無法復原！", 
                "是的，刪除", "不，取消");
                
            if (finalConfirm)
            {
                try
                {
                    // 清除所有本地資料
                    PrivacyPolicyChecker.ResetPrivacyPolicy();
                    
                    // 清除所有應用程式偏好設定
                    Preferences.Clear();
                    
                    // 清除安全存儲
                    SecureStorage.RemoveAll();
                    
                    await DisplayAlert("✅ 已刪除", 
                        "您的所有資料已被成功刪除。\n\n應用程式將重新啟動。", 
                        "確定");
                    
                    // 重新啟動應用程式
                    await Shell.Current.GoToAsync("//MainPage");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("❌ 錯誤", $"資料刪除失敗: {ex.Message}", "確定");
                }
            }
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