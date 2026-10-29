@echo off
setlocal
set "PATH=%SystemRoot%\System32;%PATH%"
set "PROJECT_DIR=%~dp0.."
for %%I in ("%PROJECT_DIR%") do set "PROJECT_DIR=%%~fI"
set "PROJECT=%PROJECT_DIR%\EEE.csproj"
set "FRAMEWORK=net10.0-android"
set "OUTPUT_DIR=%PROJECT_DIR%\Platforms\dist"
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

call "%~dp0checkinstallrequiredpackages.bat" --check-only
if errorlevel 1 exit /b 1

if not defined ANDROID_HOME set "ANDROID_HOME=%LOCALAPPDATA%\Android\Sdk"
if not defined ANDROID_SDK_ROOT set "ANDROID_SDK_ROOT=%ANDROID_HOME%"
if not defined JAVA_HOME if exist "%ProgramFiles%\Java\jdk-21.0.10\bin\java.exe" set "JAVA_HOME=%ProgramFiles%\Java\jdk-21.0.10"
if not defined JAVA_HOME if exist "%ProgramFiles%\Java\jdk-21\bin\java.exe" set "JAVA_HOME=%ProgramFiles%\Java\jdk-21"
if not defined JAVA_HOME (
	echo ERROR: Android publishing requires JDK 17 or newer. Set JAVA_HOME to a supported JDK.
	exit /b 1
)
if not exist "%ANDROID_HOME%\platforms\android-36\android.jar" (
	echo ERROR: Android API 36 is missing from "%ANDROID_HOME%".
	echo Install Android SDK Platform 36 and Build Tools 36.0.0 with Android SDK Manager, then retry.
	exit /b 1
)

if exist "%OUTPUT_DIR%\publish\EEE\release_net10.0-android" (
	rmdir /s /q "%OUTPUT_DIR%\publish\EEE\release_net10.0-android"
)
if exist "%OUTPUT_DIR%\*.apk" (
	del /f /q "%OUTPUT_DIR%\*.apk"
)

pushd "%PROJECT_DIR%"
"%DOTNET%" publish "%PROJECT%" -f "%FRAMEWORK%" -c Release --artifacts-path "%OUTPUT_DIR%" -p:PublishReadyToRun=false -p:PublishTrimmed=false -p:RunAOTCompilation=false -p:AndroidPackageFormats=apk -p:RuntimeIdentifiers=android-x64 %*
set "RESULT=%ERRORLEVEL%"
if %RESULT% equ 0 (
	if exist "%OUTPUT_DIR%\publish\EEE\release_net10.0-android\com.companyname.eee-Signed.apk" (
		copy /y "%OUTPUT_DIR%\publish\EEE\release_net10.0-android\*.apk" "%OUTPUT_DIR%\" >nul
	)
)
popd
exit /b %RESULT%
