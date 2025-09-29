# Repository Guidelines

## Project Structure & Module Organization
MapLocationApp/ hosts the MAUI solution. Place domain contracts in Models/ and Services/, wire UI state in ViewModels/, and pair each screen in Views/ with its XAML code-behind. Platform-specific bootstrapping stays under Platforms/ (shared render helpers live in PlatformsShared/). Store static assets inside Resources/ and configuration overrides in ppsettings.json. Reserve Tests/ for automated suites that mirror the namespace of the code under test.

## Build, Test, and Development Commands
- dotnet restore — hydrate NuGet packages after cloning or updating workloads.
- dotnet build MapLocationApp/MapLocationApp.csproj — default cross-target build; add -f net9.0-windows10.0.19041.0 for WinUI.
- dotnet build -t:Run -f net9.0-windows10.0.19041.0 — launch the Windows head for local smoke checks.
- dotnet publish -f net9.0-android -c Release — produce a store-ready Android package (swap target for iOS/Mac Catalyst as needed).
- dotnet test — execute once test projects exist; gate PRs on a clean run.

## Coding Style & Naming Conventions
- 4-space indentation, Allman braces, file-scoped namespaces (
amespace MapLocationApp.Services;).
- PascalCase for types and public members, camelCase for locals, _camelCase for private fields, I prefix for interfaces, and Async suffix for awaited methods.
- Keep nullable annotations accurate; the project enforces <Nullable>enable</Nullable> through Directory.Build.props.
- Limit comments to intent-level notes; retain bilingual UI copy only where customer-facing.

## Testing Guidelines
Add unit or integration fixtures under Tests/ using {TypeName}Tests.cs to mirror production namespaces. Prefer xUnit or MSTest and mock external dependencies (MySQL, HTTP, GPS, face models) via the interfaces in Services/. Prioritise geofence triggers, route planning, face recognition pipelines, and offline cache flows; record manual checklists in the repo when automation is not yet practical.

## Commit & Pull Request Guidelines
- Write concise, imperative summaries (Traditional Chinese is welcome), for example 實作統一深色主題.
- Keep commits focused; split structural changes from behavioural updates.
- Document PR intent, impacted platforms, and evidence (dotnet test, emulator screenshots, screen recordings).
- Link issues or tasks, highlight schema/permission updates, and pull in domain owners for security or location-sensitive modifications.

## Environment & Configuration Tips
Load secrets from the host environment rather than committing them; ppsettings.json should only contain safe defaults. Align platform manifest updates with the copy in Views/PrivacyPolicyPage.xaml to keep permissions transparent. Before packaging, run dotnet clean and avoid checking build artefacts into the repo—large reference data should live in Resources/Raw.
