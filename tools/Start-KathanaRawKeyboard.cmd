@echo off
setlocal
echo Kathana raw-keyboard launch test. Close the game normally before using this launcher.
powershell.exe -NoProfile -File "%~dp0Start-KathanaRawKeyboard.ps1"
set "launch_exit=%errorlevel%"
pause
exit /b %launch_exit%
