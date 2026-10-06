@echo off
setlocal
set "probe_root=%~dp0..\.."
set "probe_exe=%~dp0bin\Release\net9.0-windows10.0.19041.0\win-x64\publish\RawKeyboardProbe.exe"
if not exist "%probe_exe%" (
    echo The controlled probe has not been built. See README.md in this folder.
    pause
    exit /b 2
)
"%probe_exe%" --run --wait-for-focus 45 --report "%probe_root%\input-diagnostics\raw-keyboard-manual-focus.json"
set "probe_exit=%errorlevel%"
if not "%probe_exit%"=="0" (
    echo The controlled probe did not complete. Review the report shown above.
    pause
)
exit /b %probe_exit%
