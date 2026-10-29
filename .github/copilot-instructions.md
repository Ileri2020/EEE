# Workspace instructions

- This is a .NET 10 MAUI app for Android and Windows desktop.
- UI uses UraniumUI Material; MVVM uses CommunityToolkit.Mvvm source generators.
- Keep shared screens cross-platform and place platform-specific code under `Platforms/`.
- Register services and views in `MauiProgram.cs`; use dependency injection for pages and view models.
- Verify changes with `dotnet build EEE.csproj -f net10.0-windows10.0.19041.0`. Android builds also require an installed Android SDK platform and build tools.
