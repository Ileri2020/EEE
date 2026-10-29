@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0.."
for %%I in ("%PROJECT_DIR%") do set "PROJECT_DIR=%%~fI"
set "PROJECT=%PROJECT_DIR%\EEE.csproj"
set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"

"%DOTNET%" --version
if errorlevel 1 exit /b 1

"%DOTNET%" workload list | "%SystemRoot%\System32\findstr.exe" /i "maui" >nul
if errorlevel 1 (
	if /i "%~1"=="--check-only" (
		echo ERROR: MAUI workload is missing. Run this script without --check-only to install it.
		exit /b 1
	)
	echo Installing the .NET MAUI workload...
	"%DOTNET%" workload install maui
	if errorlevel 1 exit /b 1
)

echo .NET SDK and MAUI workload are ready.
exit /b 0

