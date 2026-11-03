@echo off
setlocal
set "PATH=%SystemRoot%\System32;%PATH%"
set "PROJECT_DIR=%~dp0.."
for %%I in ("%PROJECT_DIR%") do set "PROJECT_DIR=%%~fI"
set "PROJECT=%PROJECT_DIR%\EEE.csproj"
set "FRAMEWORK=net10.0-windows10.0.19041.0"
rem Keep watch outputs outside the source tree so resource generation cannot overflow dotnet-watch.
set "OUTPUT_DIR=%LOCALAPPDATA%\EEE\watch-artifacts"
set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"
if /i "%DOTNET%"=="dotnet" if not exist "%SystemRoot%\System32\where.exe" (
	echo ERROR: .NET SDK not found. Install the .NET 10 SDK and restart the terminal.
	exit /b 1
)

if not exist "%PROJECT%" (
	echo ERROR: Could not find "%PROJECT%".
	exit /b 1
)

if /i "%~1"=="android" goto :android

pushd "%PROJECT_DIR%"
if /i "%~1"=="--help" (
	echo Usage: fastreload.bat [android [device-id]]
	echo   No argument: watch and launch the Windows app.
	echo   android:     deploy/watch the Android app and open a crash-log window.
	echo   device-id:   optional serial from adb devices when multiple phones are connected.
	"%DOTNET%" watch --help
	set "RESULT=%ERRORLEVEL%"
	popd
	exit /b %RESULT%
)
echo Building and launching the Windows app with automatic restart on code changes.
echo Press Ctrl+C to stop the app and file watcher.
set "DOTNET_USE_POLLING_FILE_WATCHER=1"
"%DOTNET%" watch --no-hot-reload --project "%PROJECT%" --framework "%FRAMEWORK%" --artifacts-path "%OUTPUT_DIR%" run %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%

:android
set "FRAMEWORK=net10.0-android"
set "OUTPUT_DIR=%PROJECT_DIR%\Platforms\dist"
if not defined ANDROID_HOME set "ANDROID_HOME=%LOCALAPPDATA%\Android\Sdk"
if not defined ANDROID_SDK_ROOT set "ANDROID_SDK_ROOT=%ANDROID_HOME%"
set "ADB=%ANDROID_SDK_ROOT%\platform-tools\adb.exe"
if not exist "%ADB%" set "ADB=%ANDROID_HOME%\platform-tools\adb.exe"
if not exist "%ADB%" (
	echo ERROR: Android Debug Bridge was not found. Install Android SDK Platform-Tools.
	exit /b 1
)
if not defined JAVA_HOME if exist "%ProgramFiles%\Java\jdk-21\bin\java.exe" set "JAVA_HOME=%ProgramFiles%\Java\jdk-21"
if not defined JAVA_HOME if exist "%ProgramFiles%\Java\jdk-17\bin\java.exe" set "JAVA_HOME=%ProgramFiles%\Java\jdk-17"
if not defined JAVA_HOME (
	echo ERROR: Android build requires JDK 17 or newer. Set JAVA_HOME and retry.
	exit /b 1
)
if not exist "%ANDROID_SDK_ROOT%\platforms\android-36\android.jar" (
	echo ERROR: Android SDK Platform 36 was not found at "%ANDROID_SDK_ROOT%".
	echo Install Android SDK Platform 36 and Build Tools 36.0.0, then retry.
	exit /b 1
)

set "DEVICE=%~2"
if not defined DEVICE (
	for /f "skip=1 tokens=1,2" %%A in ('"%ADB%" devices') do (
		if "%%B"=="device" if not defined DEVICE set "DEVICE=%%A"
	)
)
if not defined DEVICE (
	echo ERROR: No authorized Android device found.
	echo Connect your phone by USB, enable Developer options and USB debugging, then accept the authorization prompt.
	"%ADB%" devices -l
	exit /b 1
)

echo Deploying EESimulator to Android device %DEVICE%.
echo A second terminal will show Android error logs if startup fails.
"%ADB%" -s "%DEVICE%" logcat -c
start "EEE Android crash log" "%ComSpec%" /k ""%ADB%" -s "%DEVICE%" logcat -v time *:E"
pushd "%PROJECT_DIR%"
"%DOTNET%" watch --no-hot-reload --project "%PROJECT%" --framework "%FRAMEWORK%" --device "%DEVICE%" --artifacts-path "%OUTPUT_DIR%" run
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
