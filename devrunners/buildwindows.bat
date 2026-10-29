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

call "%~dp0checkinstallrequiredpackages.bat" --check-only
if errorlevel 1 exit /b 1

if exist "%OUTPUT_DIR%\publish\EEE\release_net10.0-windows10.0.19041.0" (
	rmdir /s /q "%OUTPUT_DIR%\publish\EEE\release_net10.0-windows10.0.19041.0"
)
if exist "%OUTPUT_DIR%\windows" (
	rmdir /s /q "%OUTPUT_DIR%\windows"
)

pushd "%PROJECT_DIR%"
"%DOTNET%" publish "%PROJECT%" -f "%FRAMEWORK%" -c Release --artifacts-path "%OUTPUT_DIR%" -p:PublishReadyToRun=false %*
set "RESULT=%ERRORLEVEL%"
if %RESULT% equ 0 (
	if exist "%OUTPUT_DIR%\publish\EEE\release_net10.0-windows10.0.19041.0" (
		if not exist "%OUTPUT_DIR%\windows" mkdir "%OUTPUT_DIR%\windows"
		"%SystemRoot%\System32\robocopy.exe" "%OUTPUT_DIR%\publish\EEE\release_net10.0-windows10.0.19041.0" "%OUTPUT_DIR%\windows" /E /PURGE >nul
	)
)
popd
exit /b %RESULT%
