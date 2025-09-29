using System;
using System.Threading.Tasks;

namespace MapLocationApp.Services
{
    /// <summary>
    /// 應用程式關閉時統一釋放/停止背景服務，避免 .NET Host 殘留。
    /// </summary>
    public interface IAppShutdownService
    {
        Task ShutdownAsync();
    }

    public class AppShutdownService : IAppShutdownService
    {
        private readonly IServiceProvider _provider;

        public AppShutdownService(IServiceProvider provider)
        {
            _provider = provider;
        }

        public async Task ShutdownAsync()
        {
            try
            {
                // 停止定位追蹤
                if (_provider.GetService(typeof(ILocationService)) is ILocationService loc)
                {
                    await loc.StopLocationUpdatesAsync();
                }

                // 停止團隊位置分享 (自訂具體實作方法)
                if (_provider.GetService(typeof(ITeamLocationService)) is ITeamLocationService teamSvc)
                {
                    // 若為具體型別，呼叫 StopAllLocationSharing
                    if (teamSvc is TeamLocationService concrete)
                    {
                        concrete.StopAllLocationSharing();
                    }
                }

                // 釋放人臉辨識資源
                if (_provider.GetService(typeof(IFaceRecognitionService)) is IDisposable faceDisp)
                {
                    faceDisp.Dispose();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Shutdown 釋放資源時發生例外: {ex.Message}");
            }
        }
    }
}
