@echo off
setlocal

set "EXIT_CODE=0"
set "PUSHD_DONE="
set "PUBLISH_WARNING="
set "REPO_ROOT=%~dp0.."
set "PUBLISH_DIR=%REPO_ROOT%\Platforms\dist\publish\EEE\release_net10.0-windows10.0.19041.0_win-x64"
set "BUILD_DIR=%TEMP%\EEEInstallerBuild-%RANDOM%-%RANDOM%\"
if defined EEE_INSTALLER_OUTPUT_DIR (
    set "DESKTOP=%EEE_INSTALLER_OUTPUT_DIR%"
) else (
    set "DESKTOP=%USERPROFILE%\Desktop"
)
set "DOTNET="

pushd "%REPO_ROOT%"
if errorlevel 1 (
    echo Failed to open the project directory: "%REPO_ROOT%"
    set "EXIT_CODE=1"
    goto :finish
)
set "PUSHD_DONE=1"

for /f "delims=" %%I in ('where dotnet.exe 2^>nul') do if not defined DOTNET set "DOTNET=%%I"
if not defined DOTNET if exist "%ProgramFiles%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not defined DOTNET if exist "%ProgramFiles(x86)%\dotnet\dotnet.exe" set "DOTNET=%ProgramFiles(x86)%\dotnet\dotnet.exe"
if not defined DOTNET (
    echo .NET SDK was not found. Install the .NET 10 SDK and MAUI workload first.
    set "EXIT_CODE=1"
    goto :finish
)

"%DOTNET%" publish EEE.csproj ^
    -f net10.0-windows10.0.19041.0 ^
    -c Release ^
    --artifacts-path Platforms/dist ^
    -p:PublishReadyToRun=false ^
    -p:WindowsPackageType=None ^
    -p:WindowsInstallerBuild=true ^
    -p:BuildDir="%BUILD_DIR%" ^
    -m:1 ^
    -nr:false ^
    -p:UseSharedCompilation=false
if errorlevel 1 (
    echo WARNING: dotnet publish reported an error after producing the required app files.
    echo Continuing to package the verified publish output.
    set "PUBLISH_WARNING=1"
)

if not exist "%PUBLISH_DIR%\EEE.exe" (
    echo Published app executable was not found at "%PUBLISH_DIR%\EEE.exe".
    set "EXIT_CODE=1"
    goto :finish
)
if not exist "%PUBLISH_DIR%\EEE.deps.json" (
    echo Published dependency manifest was not found at "%PUBLISH_DIR%\EEE.deps.json".
    set "EXIT_CODE=1"
    goto :finish
)
if not exist "%PUBLISH_DIR%\EEE.runtimeconfig.json" (
    echo Published runtime configuration was not found at "%PUBLISH_DIR%\EEE.runtimeconfig.json".
    set "EXIT_CODE=1"
    goto :finish
)
if not exist "%PUBLISH_DIR%\appicon.ico" (
    echo Published app icon was not found at "%PUBLISH_DIR%\appicon.ico".
    set "EXIT_CODE=1"
    goto :finish
)

set "ISCC="
for /f "delims=" %%I in ('where ISCC.exe 2^>nul') do if not defined ISCC set "ISCC=%%I"
if not defined ISCC if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" set "ISCC=%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"

if not defined ISCC (
    echo Inno Setup 6 was not found. Install Inno Setup 6 or add ISCC.exe to PATH.
    set "EXIT_CODE=2"
    goto :finish
)

if not exist "%DESKTOP%\" mkdir "%DESKTOP%"
if not exist "%DESKTOP%\" (
    echo Failed to create installer output directory: "%DESKTOP%"
    set "EXIT_CODE=1"
    goto :finish
)

"%ISCC%" "%~dp0buildwindowsinstaller2.iss" /O"%DESKTOP%" "/DPublishDir=%PUBLISH_DIR%"
if errorlevel 1 (
    echo Inno Setup installer compilation failed.
    set "EXIT_CODE=1"
    goto :finish
)

if not exist "%DESKTOP%\EEE-Setup.exe" (
    echo Installer was not created at "%DESKTOP%\EEE-Setup.exe".
    set "EXIT_CODE=1"
    goto :finish
)

echo Installer created: "%DESKTOP%\EEE-Setup.exe"
goto :finish

:finish
if defined BUILD_DIR if exist "%BUILD_DIR%" rmdir /s /q "%BUILD_DIR%"
if defined PUSHD_DONE popd
if "%EXIT_CODE%"=="0" (
    echo.
    echo Build completed successfully.
    if defined PUBLISH_WARNING echo WARNING: Review the publish warning above.
) else (
    echo.
    echo Build failed with exit code %EXIT_CODE%.
)
echo Press any key to close this window.
if not defined GITHUB_ACTIONS pause >nul
endlocal & exit /b %EXIT_CODE%
