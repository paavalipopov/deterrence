@echo off
setlocal
if /i "%~1"=="--check" goto check
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-Deterrence.ps1" %*
set "result=%errorlevel%"
pause
exit /b %result%
:check
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Update-Deterrence.ps1" -Check
exit /b %errorlevel%
