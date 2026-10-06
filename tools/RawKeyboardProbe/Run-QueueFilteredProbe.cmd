@echo off
setlocal
set "probe_root=%~dp0..\.."
set "probe_exe=%~dp0bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\RawKeyboardProbe.exe"
if not exist "%probe_exe%" (
    echo The controlled probe has not been built. See README.md in this folder.
    pause
    exit /b 2
)
echo New queue filtering test: baseline first, then filtering. Click the test window once.
"%probe_exe%" --run --wait-for-focus 45 --filter-owned-queue --report "%probe_root%\input-diagnostics\raw-keyboard-queue-filtered.json"
set "probe_exit=%errorlevel%"
if not "%probe_exit%"=="0" echo The probe stopped early. Review the status and report above.
pause
exit /b %probe_exit%
