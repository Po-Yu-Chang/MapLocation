# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a documentation repository for the MapLocation project - a .NET MAUI mapping application with geofencing capabilities. The repository currently contains comprehensive technical design documentation in Traditional Chinese.

## Repository Structure

- `MapLocation_Enhanced.md` - Main technical design document containing:
  - .NET MAUI + Mapsui implementation details
  - Android/iOS geofencing platform considerations
  - OpenStreetMap tile usage compliance
  - Backend architecture recommendations
  - Privacy policy templates
  - Platform-specific manifest examples

## Key Technologies Documented

- **.NET MAUI** - Cross-platform framework
- **Mapsui** - Map control library for .NET MAUI
- **SkiaSharp** - Required for Mapsui rendering
- **Geofencing** - Android GeofencingClient & iOS Core Location
- **OpenStreetMap** - Tile provider with attribution requirements
- **MapLibre** - Vector tiles (advanced option)

## Development Commands

Since this is a documentation repository, typical commands would be:

```bash
# If implementing the documented application:
dotnet add package Mapsui.Maui
dotnet add package SkiaSharp.Views.Maui

# For .NET MAUI project creation:
dotnet new maui -n MapLocationApp
```

## Architecture Notes

The documented application follows a client-server architecture:
1. **Client Side**: .NET MAUI app with map controls and geofencing
2. **Backend**: Event-driven architecture using queues for geofence events
3. **Data Flow**: Location events → API Gateway → Queue → Worker → Database → Notifications

## Platform Considerations

- **Android**: Uses GeofencingClient, requires foreground service for background monitoring
- **iOS**: Limited to ~20 concurrent geofences, uses CLLocationManager
- **Tile Attribution**: Must display "© OpenStreetMap contributors" visibly on map
- **Privacy**: Comprehensive privacy policy templates included for location data handling

## Documentation Language

The technical documentation is written in Traditional Chinese and contains detailed implementation guidance, code snippets, and compliance requirements for a production-ready mapping application.