@echo off
setlocal
set "PROJECT_DIR=%~dp0.."
pushd "%PROJECT_DIR%"
git restore -- ViewModels\EEWorkspaceViewModel.cs
python -c "from pathlib import Path; p=Path('ViewModels/EEWorkspaceViewModel.cs'); s=p.read_text(encoding='utf-8'); s=s.replace('EESimulator.Core.Components.Electrical.ClockComponent', 'EESimulator.Core.Components.Electronics.ClockComponent'); p.write_text(s, encoding='utf-8')"
set "RESULT=%ERRORLEVEL%"
popd
exit /b %RESULT%
