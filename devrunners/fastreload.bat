@echo off
setlocal
set "PATH=%SystemRoot%\System32;%PATH%"
set "PROJECT_DIR=%~dp0.."
for %%I in ("%PROJECT_DIR%") do set "PROJECT_DIR=%%~fI"
set "PROJECT=%PROJECT_DIR%\EEE.csproj"
set "FRAMEWORK=net10.0-windows10.0.19041.0"
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

pushd "%PROJECT_DIR%"
if /i "%~1"=="--help" (
	"%DOTNET%" watch --help
	set "RESULT=%ERRORLEVEL%"
	popd
	exit /b %RESULT%
)
echo Starting .NET watch for Windows. Press Ctrl+C to stop.
set "ArtifactsPath=%OUTPUT_DIR%"
"%DOTNET%" watch --project "%PROJECT%" run --framework "%FRAMEWORK%" %*
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
