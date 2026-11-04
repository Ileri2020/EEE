@echo off
setlocal
set "PROJECT_DIR=%~dp0.."
pushd "%PROJECT_DIR%"
git restore -- EESimulator\EESimulator.Scripting\PythonCodeGenerator.cs
python devrunners\repair_python_codegen.py
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
