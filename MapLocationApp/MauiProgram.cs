using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SkiaSharp.Views.Maui.Controls.Hosting;
using MapLocationApp.Services;
using MapLocationApp.Services.Interfaces;
using MapLocationApp.Views;
using System.Reflection;
using CommunityToolkit.Maui;
using Microsoft.Maui.LifecycleEvents;

namespace MapLocationApp;

public static class MauiProgram
{
	public static IServiceProvider Services { get; private set; } = null!;

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseSkiaSharp(true)
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// 新增配置檔案支援 - 使用簡化的方式
		builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

		// 註冊核心服務
		builder.Services.AddSingleton<IMapService, MapService>();
#if WINDOWS
		builder.Services.AddSingleton<ILocationService, Platforms.Windows.WindowsLocationService>();
#else
		builder.Services.AddSingleton<ILocationService, LocationService>();
#endif
		builder.Services.AddSingleton<IGeofenceService, GeofenceService>();
		builder.Services.AddSingleton<IGeocodingService, GeocodingService>();
		builder.Services.AddSingleton<CheckInStorageService>();
		builder.Services.AddSingleton<ICheckInStorageService>(provider => provider.GetRequiredService<CheckInStorageService>());

		// 註冊新功能服務
		builder.Services.AddSingleton<IOfflineMapService, OfflineMapService>();
		builder.Services.AddSingleton<IRouteService, RouteService>();
		builder.Services.AddSingleton<ITeamLocationService, TeamLocationService>();
		builder.Services.AddSingleton<IReportService, ReportService>();
		builder.Services.AddSingleton<ILocalizationService, LocalizationService>();
		builder.Services.AddSingleton<LocalizationService>();
		builder.Services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();
		// T149: Removed INotificationIntegrationService - unnecessary abstraction layer

		// 註冊 Wi-Fi 服務 (M2 — 各平台特化)
#if ANDROID
		builder.Services.AddSingleton<MapLocationApp.Services.Interfaces.IWifiService, Platforms.Android.Services.WifiService>();
#elif IOS
		builder.Services.AddSingleton<MapLocationApp.Services.Interfaces.IWifiService, Platforms.iOS.Services.WifiService>();
#elif WINDOWS
		builder.Services.AddSingleton<MapLocationApp.Services.Interfaces.IWifiService, Platforms.Windows.Services.WifiService>();
#else
		builder.Services.AddSingleton<MapLocationApp.Services.Interfaces.IWifiService, NullWifiService>();
#endif

		// 註冊認證和資料庫服務
		builder.Services.AddSingleton<IConfigService, SqliteConfigService>();
		builder.Services.AddSingleton<ISecureConfigService, SecureConfigService>(); // T011: SecureConfigService for credential management
		builder.Services.AddSingleton<IDatabaseService, MySqlDatabaseService>();
		builder.Services.AddSingleton<IUserSessionService, UserSessionService>();

		// 註冊進階導航服務
		builder.Services.AddSingleton<ITTSService, TTSService>();
		builder.Services.AddSingleton<INavigationService, NavigationService>();
		
		// 註冊進階位置服務 (裝飾者模式)
		builder.Services.AddSingleton<AdvancedLocationService>(provider =>
		{
			var baseLocationService = provider.GetRequiredService<ILocationService>();
			return new AdvancedLocationService(baseLocationService);
		});

		// 註冊人臉辨識服務
		builder.Services.AddSingleton<IFaceDatabase, FaceDatabase>();
#if WINDOWS
		builder.Services.AddSingleton<IFaceRecognitionService, Platforms.Windows.FaceAiSharpService>();
#else
		builder.Services.AddSingleton<IFaceRecognitionService>(provider => null!); // 其他平台暫不支援
#endif

		// 提前註冊關閉管理服務 (必須在 Build 前)
		builder.Services.AddSingleton<IAppShutdownService, AppShutdownService>();

		// 註冊頁面
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<MapPage>();
		builder.Services.AddTransient<CheckInPage>();
		builder.Services.AddTransient<PrivacyPolicyPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<RoutePlanningPage>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<FaceRecognitionPage>();
		builder.Services.AddTransient<GeofenceManagementPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<EditCheckInPage>();
		builder.Services.AddTransient<SchedulePage>();
		builder.Services.AddTransient<LeavePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif


		// 掛載 Windows 關閉事件以確保釋放背景服務
		builder.ConfigureLifecycleEvents(events =>
		{
#if WINDOWS
			events.AddWindows(w =>
			{
				w.OnClosed((win, args) =>
				{
					try
					{
						// 使用已建立的全域 Services 實例
						var shutdownSvc = Services?.GetService(typeof(IAppShutdownService)) as IAppShutdownService;
						shutdownSvc?.ShutdownAsync().GetAwaiter().GetResult();
					}
					catch { }
				});
			});
#endif
		});

		var app = builder.Build();
		Services = app.Services;

		// 初始化 ServiceHelper
		Views.ServiceHelper.Initialize(Services);
		
		return app;
	}
}
