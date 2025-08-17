# MapLocation .NET MAUI Application
MapLocation is a comprehensive .NET MAUI cross-platform mapping application with geofencing capabilities, location tracking, and check-in functionality. The application supports Android (primarily), with Windows support when built on Windows, and provides offline mapping, route planning, team location sharing, and multilingual support.

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## Working Effectively

### Environment Setup
- **Install .NET 9.0 SDK**: `sudo apt-get update && sudo apt-get install -y dotnet-sdk-9.0`
- **Install MAUI Android workload**: `sudo dotnet workload install maui-android --skip-sign-check` -- takes 10-15 minutes. NEVER CANCEL. Set timeout to 20+ minutes.
- **Verify installation**: `dotnet --version` should show `9.0.203` or later
- **Verify workloads**: `dotnet workload list` should show `maui-android`

### Bootstrap and Build Commands
1. **Navigate to repository**: `cd /path/to/MapLocation`
2. **Restore packages**: `dotnet restore MapLocationApp/MapLocationApp.csproj` -- takes 45-50 seconds. NEVER CANCEL. Set timeout to 90+ seconds.
3. **Build for Android**: `dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-android` -- takes 65-75 seconds. NEVER CANCEL. Set timeout to 120+ seconds.
4. **Clean project**: `dotnet clean` -- takes 1-2 seconds

### Release Build Commands
- **Android APK**: `dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-android -c Release -p:AndroidPackageFormat=apk` -- takes 90+ seconds. NEVER CANCEL. Set timeout to 180+ seconds.
- **Android Publish**: `dotnet publish MapLocationApp/MapLocationApp.csproj -f net9.0-android -c Release` -- takes 90+ seconds. NEVER CANCEL. Set timeout to 180+ seconds.

### Platform Support
- **Linux**: Android builds only (tested and working)
- **Windows**: Android + Windows builds (when running on Windows)
- **macOS**: Android + iOS + macOS builds (when running on macOS)

### CRITICAL Build Notes
- **NEVER CANCEL** long-running commands - builds can take 1-2 minutes
- **Package Warning**: Expect NU1608 warning about Mapsui.Maui 4.1.9 vs Microsoft.Maui.Controls 9.0.82 version mismatch - this is known and non-breaking
- **Missing appsettings.json**: If build fails with XA2001 error, create `MapLocationApp/appsettings.json` with basic configuration
- **iOS/macOS builds**: Only work on macOS with appropriate workloads installed

## Validation

### Manual Testing Scenarios
Since this is a MAUI application, you cannot run it directly in the Linux environment, but you should always validate:

1. **Build Validation**: Always ensure `dotnet build` completes without errors
2. **Package Restoration**: Verify `dotnet restore` succeeds with only version warnings
3. **Clean Build**: Test `dotnet clean && dotnet build` workflow
4. **Configuration Validation**: Ensure `appsettings.json` exists and is properly formatted

### Expected Build Output
- **Build Time**: 65-75 seconds for debug, 90+ seconds for release
- **Warnings**: 5-9 warnings expected (version mismatches, ProGuard, page size warnings)
- **Output**: APK/AAB files in `MapLocationApp/bin/Debug/net9.0-android/` or `bin/Release/net9.0-android/`

### No Testing Framework
- This project has no unit test projects - `dotnet test` will run but find no tests
- Manual testing requires Android emulator or physical device
- Always validate builds succeed as primary validation method

### Performance Expectations
- **Clean**: ~1 second
- **Restore**: 45-50 seconds (first time may be longer)
- **Debug Build**: 65-75 seconds  
- **Release Build**: 90+ seconds
- **Workload Installation**: 10-15 minutes

## Common Tasks

### Repository Structure
```
MapLocation/
├── .github/                    # GitHub configuration
├── MapLocationApp/            # Main MAUI application
│   ├── Models/               # Data models (TileProvider, GeofenceRegion)
│   ├── Services/             # Business logic (19 service files)
│   ├── Views/               # XAML pages (Map, CheckIn, PrivacyPolicy, etc.)
│   ├── Platforms/           # Platform-specific code
│   ├── Resources/           # Images, fonts, localization files
│   ├── MapLocationApp.csproj # Project file
│   └── appsettings.json     # Configuration file
├── MapLocation.sln          # Solution file
├── CLAUDE.md               # Claude AI guidance
└── Documentation files (.md)
```

### Key Project Files
- **MapLocationApp.csproj**: Targets `net9.0-android` (iOS/Windows when on appropriate OS)
- **appsettings.json**: API settings, Telegram config, map defaults
- **MauiProgram.cs**: Dependency injection and service configuration
- **App.xaml/App.xaml.cs**: Application lifecycle management

### Package Dependencies
- **Mapsui.Maui 4.1.9**: Map rendering engine
- **Microsoft.Maui.Controls 9.0.82**: MAUI framework
- **SkiaSharp**: 2D graphics rendering
- **Microsoft.Extensions.Configuration.Json**: Configuration management

### Service Architecture
The application uses 19 service classes implementing various features:
- **MapService**: Multi-provider tile support (OpenStreetMap, CartoDB)
- **LocationService**: GPS tracking and permissions
- **GeofenceService**: Geographic boundary monitoring
- **OfflineMapService**: Local tile caching
- **RouteService**: Route planning and navigation
- **TeamLocationService**: Real-time team collaboration
- **ReportService**: Analytics and data export
- **LocalizationService**: Multi-language support (5 languages)
- **TelegramNotificationService**: Push notifications

### Configuration Requirements
Always ensure `appsettings.json` exists with:
```json
{
  "ApiSettings": {
    "BaseUrl": "https://api.example.com",
    "Timeout": 30
  },
  "TelegramSettings": {
    "BotToken": "",
    "ChatId": ""
  },
  "MapSettings": {
    "DefaultZoom": 15,
    "DefaultLatitude": 25.0330,
    "DefaultLongitude": 121.5654
  }
}
```

### Troubleshooting Common Issues
1. **SDK Missing**: Install .NET 9.0 SDK first
2. **Workload Missing**: Install maui-android workload
3. **iOS Build Fails**: Requires macOS environment
4. **Package Version Warnings**: Expected and non-breaking
5. **appsettings.json Missing**: Create with basic configuration
6. **Long Build Times**: Normal for MAUI projects, never cancel

### Environment Commands Reference
```bash
# Check environment
dotnet --version                    # Should be 9.0.203+
dotnet workload list               # Should show maui-android

# Project commands
dotnet restore MapLocationApp/MapLocationApp.csproj
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-android
dotnet clean
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-android -c Release

# Package management
dotnet add MapLocationApp/MapLocationApp.csproj package <PackageName>
dotnet remove MapLocationApp/MapLocationApp.csproj package <PackageName>
```

Always build and exercise your changes by running the full build process after making any modifications to validate they work correctly.