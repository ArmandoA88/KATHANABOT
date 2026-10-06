@echo off
setlocal
set "probe_exe=%~dp0bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\GameInventoryProbe.exe"
if not exist "%probe_exe%" (
    echo Build the standalone GameInventoryProbe first. See README.md beside this launcher.
    pause
    exit /b 2
)
"%probe_exe%" --run --launch-report "%~dp0..\..\input-diagnostics\kathana-raw-keyboard-launch.json" --report-dir "%~dp0..\..\input-diagnostics"
set "probe_result=%errorlevel%"
pause
exit /b %probe_result%
