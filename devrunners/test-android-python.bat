@echo off
setlocal
set "PROJECT_DIR=%~dp0.."
for %%I in ("%PROJECT_DIR%") do set "PROJECT_DIR=%%~fI"
set "PACKAGE=com.companyname.eee"
set "DOTNET=%ProgramFiles%\dotnet\dotnet.exe"
if not exist "%DOTNET%" set "DOTNET=dotnet"

set "ADB="
for /f "delims=" %%A in ('where adb 2^>nul') do if not defined ADB set "ADB=%%A"
if not defined ADB (
	echo ANDROID PYTHON3 TEST SKIPPED: adb was not found on PATH.
	exit /b 0
)

set "ANDROID_DEVICE="
for /f "skip=1 tokens=1,2" %%A in ('"%ADB%" devices') do if "%%B"=="device" set "ANDROID_DEVICE=%%A"
if not defined ANDROID_DEVICE (
	echo ANDROID PYTHON3 TEST SKIPPED: connect an Android emulator or device to run it.
	exit /b 0
)

pushd "%PROJECT_DIR%"
echo Building Android app for Python3Android smoke test on %ANDROID_DEVICE%...
"%DOTNET%" build EEE.csproj -f net10.0-android
if errorlevel 1 goto android_error

set "APK=%PROJECT_DIR%\Platforms\dist\bin\EEE\debug_net10.0-android\com.companyname.eee-Signed.apk"
if not exist "%APK%" set "APK=%PROJECT_DIR%\Platforms\dist\bin\EEE\debug_net10.0-android\com.companyname.eee.apk"
if not exist "%APK%" (
	echo ANDROID PYTHON3 TEST FAILED: Android APK was not produced.
	goto android_error
)

"%ADB%" install -r "%APK%"
if errorlevel 1 goto android_error
"%ADB%" shell am force-stop %PACKAGE%
"%ADB%" shell run-as %PACKAGE% rm -f files/eee-python3android-smoke.request files/eee-python3android-smoke.result
"%ADB%" shell run-as %PACKAGE% touch files/eee-python3android-smoke.request
if errorlevel 1 goto android_error
"%ADB%" shell monkey -p %PACKAGE% 1
if errorlevel 1 goto android_error

set "RESULT_FILE=%TEMP%\eee-python3android-smoke-result.txt"
set /a WAIT_COUNT=0
:wait_for_android_result
"%ADB%" shell run-as %PACKAGE% cat files/eee-python3android-smoke.result > "%RESULT_FILE%" 2>nul
findstr /B /C:"PASS|" "%RESULT_FILE%" >nul 2>nul
if not errorlevel 1 (
	echo ANDROID PYTHON3 TEST PASSED:
	type "%RESULT_FILE%"
	"%ADB%" shell run-as %PACKAGE% rm -f files/eee-python3android-smoke.result
	popd
	exit /b 0
)
findstr /B /C:"FAIL|" "%RESULT_FILE%" >nul 2>nul
if not errorlevel 1 (
	echo ANDROID PYTHON3 TEST FAILED:
	type "%RESULT_FILE%"
	popd
	exit /b 1
)
set /a WAIT_COUNT+=1
if %WAIT_COUNT% geq 180 (
	echo ANDROID PYTHON3 TEST FAILED: timed out waiting for the smoke-test result.
	"%ADB%" logcat -d -s EEE_PYTHON3ANDROID_TEST:E
	goto android_error
)
timeout /t 2 /nobreak >nul
goto wait_for_android_result

:android_error
popd
exit /b 1