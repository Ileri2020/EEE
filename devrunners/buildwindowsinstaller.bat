@echo off
setlocal
cd /d "%~dp0.."

where dotnet >nul 2>nul
if errorlevel 1 (
    echo .NET SDK was not found. Install the .NET 10 SDK and MAUI workload first.
    exit /b 1
)

dotnet publish EEE.csproj ^
    -f net10.0-windows10.0.19041.0 ^
    -c Release ^
    --artifacts-path Platforms/dist ^
    -p:WindowsPackageType=MSIX ^
    -p:GenerateAppxPackageOnBuild=true ^
    -p:AppxPackageSigningEnabled=false ^
    -p:AppxBundle=Never ^
    -p:AppxPackageDir=Platforms\installer\

if errorlevel 1 (
    echo Windows installer packaging failed.
    exit /b %errorlevel%
)

echo Installer output: Platforms\installer
endlocal
