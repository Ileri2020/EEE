@echo off
setlocal
set "PROJECT_DIR=%~dp0.."
pushd "%PROJECT_DIR%"
python devrunners\repair_power_integration.py
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
