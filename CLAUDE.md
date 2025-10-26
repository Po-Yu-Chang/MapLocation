# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a fully implemented .NET MAUI mapping application with comprehensive features including geofencing, face recognition, navigation, team collaboration, and offline capabilities. The project uses modern .NET 9.0 and targets multiple platforms (Android, iOS, Windows, macOS).

## Development Commands

```bash
# Build the entire solution
dotnet build MapLocation.sln

# Build for specific platform
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-android
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-ios
dotnet build MapLocationApp/MapLocationApp.csproj -f net9.0-windows10.0.19041.0

# Run the application (Windows)
dotnet run --project MapLocationApp/MapLocationApp.csproj -f net9.0-windows10.0.19041.0

# Clean build artifacts
dotnet clean MapLocation.sln

# Restore NuGet packages
dotnet restore MapLocation.sln

# Add new package
dotnet add MapLocationApp/MapLocationApp.csproj package <PackageName>
```

## Architecture Overview

### Service-Oriented Architecture
The application uses dependency injection with a comprehensive service layer:

**Core Services** (registered in `MauiProgram.cs`):
- `IMapService` - Map control and rendering (Mapsui integration)
- `ILocationService` - Platform-specific location services with Windows implementation
- `IGeofenceService` - Geofencing functionality across platforms
- `IGeocodingService` - Address geocoding and reverse geocoding
- `INavigationService` - Turn-by-turn navigation with TTS
- `ITTSService` - Text-to-speech functionality

**Data Services**:
- `IDatabaseService` - MySQL database operations
- `IConfigService` - SQLite configuration storage
- `IUserSessionService` - User authentication and session management
- `CheckInStorageService` - Location check-in functionality

**Advanced Features**:
- `IFaceRecognitionService` - Face recognition (Windows-only using FaceAiSharp)
- `ITeamLocationService` - Team location sharing
- `ITelegramNotificationService` - Telegram bot integration
- `IOfflineMapService` - Offline map caching
- `LocalizationService` - Multi-language support

### Key Technologies

- **.NET 9.0 MAUI** - Cross-platform framework
- **Mapsui 4.1.9** - Map control library with SkiaSharp rendering
- **NetTopologySuite** - Spatial operations and geofencing
- **FaceAiSharp.Bundle** - Face recognition (Windows platform)
- **MySql.Data** - Database connectivity
- **SQLite** - Local configuration storage
- **CommunityToolkit.Maui** - Additional MAUI controls and camera
- **Newtonsoft.Json** - JSON serialization

### Platform-Specific Implementations

**Windows**:
- `WindowsLocationService` - Enhanced location services for Windows
- `FaceAiSharpService` - Face recognition implementation
- DirectML support for GPU acceleration

**Cross-Platform**:
- Standard location services for Android/iOS
- Platform-specific project structure under `Platforms/`

### Application Structure

**Pages/Views**:
- `MainPage` - Main dashboard with quick actions
- `MapPage` - Primary map interface with Mapsui
- `FaceRecognitionPage` - Face recognition functionality
- `LoginPage` - User authentication
- `SettingsPage` - Application configuration
- `RoutePlanningPage` - Navigation and route planning
- `CheckInPage` - Location check-in interface
- `PrivacyPolicyPage` - Privacy policy display

**Models**:
- Location and navigation models (`Route`, `RouteStep`, `NavigationSession`)
- User and authentication models (`User`, `RememberedUser`)
- Face recognition models (`FaceData`)
- Configuration models (`DatabaseConfig`, `AppSettings`)

### Database Integration

The application supports dual database configuration:
- **MySQL** - Primary database for user data and locations
- **SQLite** - Local configuration and face recognition data

### Language Support

The application includes full Traditional Chinese localization with resource files:
- `AppResources.resx` - Default (English)
- `AppResources.zh-TW.resx` - Traditional Chinese
- Additional language support for Japanese and Korean

### Build Configuration

- `Directory.Build.props` - Global build settings with nullable reference types enabled
- Platform-specific configurations in project file
- Conditional package references for platform-specific features
- Warning suppressions for MAUI-specific scenarios

## Testing

The project includes a `Tests/` directory structure but no active test projects are currently implemented. To add testing:

```bash
# Create test project
dotnet new mstest -n MapLocationApp.Tests
dotnet sln add MapLocationApp.Tests/MapLocationApp.Tests.csproj
```

## Important Notes

- Face recognition is Windows-only due to FaceAiSharp limitations
- Location services have platform-specific implementations
- The application requires location permissions on all platforms
- Telegram integration requires bot token configuration
- MySQL connection string must be configured in app settings