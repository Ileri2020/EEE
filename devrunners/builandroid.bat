@echo off
call "%~dp0buildandroid.bat" %*
exit /b %ERRORLEVEL%
