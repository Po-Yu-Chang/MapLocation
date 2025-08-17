using Microsoft.Extensions.Logging;
using SkiaSharp.Views.Maui.Controls.Hosting;
using MapLocationApp.Services;
using MapLocationApp.Views;

namespace MapLocationApp;

public static class MauiProgram
{
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

		// 註冊服務
		builder.Services.AddSingleton<IMapService, MapService>();
#if WINDOWS
		builder.Services.AddSingleton<ILocationService, Platforms.Windows.WindowsLocationService>();
#else
		builder.Services.AddSingleton<ILocationService, LocationService>();
#endif
		builder.Services.AddSingleton<IGeofenceService, GeofenceService>();
		builder.Services.AddSingleton<IGeocodingService, GeocodingService>();
		builder.Services.AddSingleton<ICheckInStorageService, CheckInStorageService>();

		// 註冊頁面
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<MapPage>();
		builder.Services.AddTransient<CheckInPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
