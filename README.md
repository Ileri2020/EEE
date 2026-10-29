# EEE

A starter .NET MAUI application targeting Android and Windows desktop, using UraniumUI Material and CommunityToolkit.Mvvm.

## Requirements

- .NET 10 SDK
- .NET MAUI workload
- Android: Android SDK and an emulator or connected device
- Windows desktop: Windows 10 version 1809 (build 17763) or later

## Run on Windows

In VS Code, run the **EEE: Run Windows** task. From a terminal in the project directory, run `dotnet run --framework net10.0-windows10.0.19041.0`.

## Build

Run `devrunners\buildwindows.bat` to publish the Windows executable, or `devrunners\builandroid.bat` to publish the Android APK. Both artifacts are placed under `Platforms\dist`. Android publishing requires the Android SDK platform/build tools and an accepted SDK license. Configure an Android device/emulator to deploy and run the APK.

The home screen demonstrates a Material text field, an observable welcome message, and MVVM counter commands. Add screens under the app namespace and register services in `MauiProgram.cs`.
