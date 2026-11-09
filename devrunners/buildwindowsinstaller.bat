@echo off
setlocal
PowerShell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildWindowsInstaller.ps1"
if errorlevel 1 exit /b %errorlevel%
endlocal
