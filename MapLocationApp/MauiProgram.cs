using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using SkiaSharp.Views.Maui.Controls.Hosting;
using MapLocationApp.Services;
using MapLocationApp.Views;
using System.Reflection;

namespace MapLocationApp;

public static class MauiProgram
{
	public static IServiceProvider Services { get; private set; } = null!;

	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
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
		builder.Services.AddSingleton<LocalizationService>();
		builder.Services.AddSingleton<ITelegramNotificationService, TelegramNotificationService>();
		builder.Services.AddSingleton<INotificationIntegrationService, NotificationIntegrationService>();

		// 註冊導航相關服務
		builder.Services.AddSingleton<ITTSService, LocalizedTTSService>();
		builder.Services.AddSingleton<IAdvancedLocationService>(provider => 
		{
			var baseLocationService = provider.GetRequiredService<ILocationService>();
			return new AdvancedLocationService(baseLocationService);
		});
		builder.Services.AddSingleton<IRouteTrackingService, RouteTrackingService>();
		builder.Services.AddSingleton<ITrafficService>(provider => 
		{
			// In production, get API key from configuration
			var configuration = provider.GetRequiredService<IConfiguration>();
			var apiKey = configuration["GoogleMapsApiKey"] ?? "demo-key";
			return new GoogleTrafficService(apiKey);
		});
		builder.Services.AddSingleton<INavigationService, NavigationService>();
		builder.Services.AddSingleton<IDestinationArrivalService, DestinationArrivalService>();
		builder.Services.AddSingleton<INavigationPreferencesService, NavigationPreferencesService>();
		builder.Services.AddSingleton<ILaneGuidanceService, LaneGuidanceService>();
		builder.Services.AddSingleton<INavigationPerformanceMonitor, NavigationPerformanceMonitor>();
		builder.Services.AddSingleton<INavigationErrorHandler, NavigationErrorHandler>();

		// 註冊頁面
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<MapPage>();
		builder.Services.AddTransient<CheckInPage>();
		builder.Services.AddTransient<PrivacyPolicyPage>();
		builder.Services.AddTransient<SettingsPage>();
		builder.Services.AddTransient<RoutePlanningPage>();
		builder.Services.AddTransient<NavigationPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		var app = builder.Build();
		Services = app.Services;
		return app;
	}
}
