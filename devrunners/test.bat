@echo off
setlocal
set "PROJECT_DIR=%~dp0.."
for %%I in ("%PROJECT_DIR%") do set "PROJECT_DIR=%%~fI"
set "TEST_PROJECT=%PROJECT_DIR%\EESimulator\EESimulator.Tests\EESimulator.Tests.csproj"
set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"
set "BUILD_FLAGS="
set "TEST_ARGS="

if /i "%DOTNET%"=="dotnet" (
	where dotnet >nul 2>nul
	if errorlevel 1 (
		echo ERROR: .NET SDK not found. Install the .NET 10 SDK and restart the terminal.
		goto finish_error
	)
)

if not exist "%TEST_PROJECT%" (
	echo ERROR: Could not find "%TEST_PROJECT%".
	goto finish_error
)

set "LOG_DIR=%PROJECT_DIR%\Platforms\dist\logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
set "LOG_FILE=%LOG_DIR%\component-tests.log"

:parse_args
if "%~1"=="" goto args_parsed
if /i "%~1"=="--reuse-build" (
	set "BUILD_FLAGS=--no-build --no-restore"
) else (
	set "TEST_ARGS=%TEST_ARGS% "%~1""
)
shift
goto parse_args

:args_parsed
pushd "%PROJECT_DIR%"
echo ================================================================================
echo EEE COMPONENT TEST RUNNER
echo Target: %TEST_PROJECT%
echo Full output log: %LOG_FILE%
if defined BUILD_FLAGS echo Mode: reusing the existing build (--no-build --no-restore)
echo ================================================================================
"%DOTNET%" test "%TEST_PROJECT%" %BUILD_FLAGS% --logger "console;verbosity=normal" %TEST_ARGS% > "%LOG_FILE%" 2>&1
set "RESULT=%ERRORLEVEL%"
popd

echo.
type "%LOG_FILE%"
echo.
echo ================================================================================
if %RESULT% equ 0 (
	echo ALL TESTS COMPLETED SUCCESSFULLY.
) else (
	echo TESTS FAILED. Exit code: %RESULT%
	echo Review the errors above or open "%LOG_FILE%".
)
echo ================================================================================
echo.
set "CLOSE_INPUT="
set /p "CLOSE_INPUT=Use Ctrl+A then Ctrl+C to copy output. Type Q and press Enter when you are ready to close. "
if /i not "%CLOSE_INPUT%"=="Q" goto wait_to_close
exit /b %RESULT%

:wait_to_close
set "CLOSE_INPUT="
set /p "CLOSE_INPUT=Type Q and press Enter when you are ready to close. "
if /i not "%CLOSE_INPUT%"=="Q" goto wait_to_close
exit /b %RESULT%

:finish_error
echo.
set "CLOSE_INPUT="
set /p "CLOSE_INPUT=Use Ctrl+A then Ctrl+C to copy the error. Type Q and press Enter to close. "
if /i not "%CLOSE_INPUT%"=="Q" goto finish_error
exit /b 1
