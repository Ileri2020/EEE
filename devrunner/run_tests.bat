@echo off
call "%~dp0..\devrunners\test.bat" %*
exit /b %ERRORLEVEL%
