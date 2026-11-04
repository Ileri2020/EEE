@echo off
setlocal
set "PROJECT_DIR=%~dp0.."
pushd "%PROJECT_DIR%"
python devrunners\repair_python_dispatch.py
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
