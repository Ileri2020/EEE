
@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0.."
for %%I in ("%PROJECT_DIR%") do set "PROJECT_DIR=%%~fI"
set "GITHUB_REMOTE=https://github.com/Ileri2020/EEE.git"

set "COMMIT_MSG=c sharp full setup only for android and desktop"
set "CONFIRM="

:parse_args
if "%~1"=="" goto done_args
if /i "%~1"=="-y" (
	set "CONFIRM=y"
	shift
	goto parse_args
)
if /i "%~1"=="--yes" (
	set "CONFIRM=y"
	shift
	goto parse_args
)
if not defined COMMIT_MSG (
	set "COMMIT_MSG=%~1"
	shift
	goto parse_args
)
shift
goto parse_args
:done_args

if not defined COMMIT_MSG (
	echo Usage: %~nx0 "commit message" [-y]
	exit /b 2
)

git -C "%PROJECT_DIR%" rev-parse --is-inside-work-tree >nul 2>nul
if errorlevel 1 (
	echo Initializing Git repository in "%PROJECT_DIR%"...
	git -C "%PROJECT_DIR%" init -b main 2>nul
	if errorlevel 1 (
		git -C "%PROJECT_DIR%" init
		if errorlevel 1 exit /b 1
		git -C "%PROJECT_DIR%" branch -M main
		if errorlevel 1 exit /b 1
	)
)

git -C "%PROJECT_DIR%" remote get-url origin >nul 2>nul
if errorlevel 1 (
	git -C "%PROJECT_DIR%" remote add origin "%GITHUB_REMOTE%"
	if errorlevel 1 exit /b 1
)

git -C "%PROJECT_DIR%" status --short --branch
if errorlevel 1 exit /b 1
echo.
if not defined CONFIRM (
	set /p "CONFIRM=Stage all changes, commit with the supplied message, and push? [y/N] "
)
if /i not "%CONFIRM%"=="y" (
	echo Cancelled; no changes were staged or pushed.
	exit /b 0
)

git -C "%PROJECT_DIR%" add -A
if errorlevel 1 exit /b 1

git -C "%PROJECT_DIR%" diff --cached --quiet
if not errorlevel 1 (
	echo Nothing to commit.
	exit /b 0
)

git -C "%PROJECT_DIR%" commit -m "%COMMIT_MSG%"
if errorlevel 1 exit /b 1

git -C "%PROJECT_DIR%" branch --show-current >nul
if errorlevel 1 exit /b 1

git -C "%PROJECT_DIR%" push -u origin HEAD
exit /b %ERRORLEVEL%