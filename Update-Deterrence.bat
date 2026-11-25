@echo off
setlocal EnableExtensions DisableDelayedExpansion
title Install or update Deterrence

rem This copies the latest compiled working version. It does not rebuild the mod.
set "AW_SOURCE=%~dp0Antiwealth"
set "AW_TARGET=C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\Antiwealth"

echo Deterrence installer / updater
echo Source: "%AW_SOURCE%"
echo Target: "%AW_TARGET%"
echo.

if not exist "%AW_SOURCE%\About\About.xml" goto :missing_source
if not exist "%AW_SOURCE%\Assemblies\Antiwealth.dll" goto :missing_source
where robocopy.exe >nul 2>&1
if errorlevel 1 goto :missing_robocopy
where powershell.exe >nul 2>&1
if errorlevel 1 goto :missing_powershell

powershell.exe -NoProfile -Command "try { [xml]$a=[IO.File]::ReadAllText([IO.Path]::Combine($env:AW_SOURCE,'About\About.xml')); if($a.ModMetaData.packageId -ne 'pavel.antiwealth'){exit 1}; Write-Host ('Source version: ' + $a.ModMetaData.modVersion) } catch { Write-Host $_; exit 1 }"
if errorlevel 1 goto :wrong_source

if /I "%~1"=="--check" goto :check_only

powershell.exe -NoProfile -Command "try { if([Diagnostics.Process]::GetProcessesByName('RimWorldWin64').Length -gt 0){exit 10}; exit 0 } catch { Write-Host $_; exit 2 }"
if errorlevel 10 goto :game_running
if errorlevel 1 goto :process_check_failed

if not exist "%AW_TARGET%" goto :install
if not exist "%AW_TARGET%\About\About.xml" goto :wrong_target
powershell.exe -NoProfile -Command "try { [xml]$a=[IO.File]::ReadAllText([IO.Path]::Combine($env:AW_TARGET,'About\About.xml')); if($a.ModMetaData.packageId -ne 'pavel.antiwealth'){exit 1} } catch {exit 1}"
if errorlevel 1 goto :wrong_target

for /f %%T in ('powershell.exe -NoProfile -Command "[DateTime]::Now.ToString('yyyyMMdd-HHmmss-fff')"') do set "AW_STAMP=%%T"
if not defined AW_STAMP goto :backup_failed
set "AW_BACKUP=%LOCALAPPDATA%\Antiwealth\Backups\%AW_STAMP%"
echo Backing up current installation to "%AW_BACKUP%"...
robocopy.exe "%AW_TARGET%" "%AW_BACKUP%" /E /XJ /R:2 /W:1 /COPY:DAT /DCOPY:DAT /NFL /NDL /NP
if errorlevel 8 goto :backup_failed

:install
echo Copying the current compiled version...
robocopy.exe "%AW_SOURCE%" "%AW_TARGET%" /E /XJ /R:2 /W:1 /COPY:DAT /DCOPY:DAT /XD Tests Tools .git bin obj /XF Build.ps1 Test.ps1 .gitignore /NFL /NDL /NP
if errorlevel 8 goto :copy_failed

echo Verifying the installed DLL and mod metadata...
powershell.exe -NoProfile -Command "try { $sha=[Security.Cryptography.SHA256]::Create(); try { foreach($relative in @('Assemblies\Antiwealth.dll','About\About.xml','Defs\MainButtonDefs\Antiwealth.xml')) { $s=[Convert]::ToBase64String($sha.ComputeHash([IO.File]::ReadAllBytes([IO.Path]::Combine($env:AW_SOURCE,$relative)))); $t=[Convert]::ToBase64String($sha.ComputeHash([IO.File]::ReadAllBytes([IO.Path]::Combine($env:AW_TARGET,$relative)))); if($s -ne $t){throw ('Verification failed: ' + $relative)} } } finally { $sha.Dispose() }; [xml]$a=[IO.File]::ReadAllText([IO.Path]::Combine($env:AW_TARGET,'About\About.xml')); Write-Host ('Installed Antiwealth ' + $a.ModMetaData.modVersion) } catch { Write-Host $_; exit 1 }"
if errorlevel 1 goto :verify_failed
echo.
echo Success. Enable Antiwealth after Harmony in RimWorld's mod menu.
echo Restart RimWorld after installing or updating.
echo Existing game saves and mod settings were not changed.
echo No destination files were deleted.
if defined AW_BACKUP echo Previous version: "%AW_BACKUP%"
pause
exit /b 0

:check_only
echo Preflight passed. No files were copied or changed.
exit /b 0

:missing_source
echo ERROR: The working mod folder or compiled DLL is missing.
echo Expected: "%AW_SOURCE%\Assemblies\Antiwealth.dll"
goto :failed
:wrong_source
echo ERROR: Source metadata does not identify the Antiwealth mod.
goto :failed
:wrong_target
echo ERROR: The target exists but does not identify itself as Antiwealth.
echo Stopping to protect its contents. Inspect "%AW_TARGET%" manually.
goto :failed
:missing_robocopy
echo ERROR: Windows robocopy.exe was not found.
goto :failed
:missing_powershell
echo ERROR: Windows PowerShell was not found.
goto :failed
:game_running
echo ERROR: Close RimWorld before installing or updating the mod.
goto :failed
:process_check_failed
echo ERROR: Could not check whether RimWorld is running. No files were copied.
goto :failed
:backup_failed
echo ERROR: Backup failed. The existing installation was not updated.
goto :failed
:copy_failed
echo ERROR: Copy failed. Check the errors above.
echo If access was denied, right-click this file and choose Run as administrator.
if defined AW_BACKUP echo Previous version: "%AW_BACKUP%"
goto :failed
:verify_failed
echo ERROR: Installed files failed verification. Do not enable this copy yet.
if defined AW_BACKUP echo Previous version: "%AW_BACKUP%"
goto :failed
:failed
echo.
echo Installation did not complete successfully.
if /I "%~1"=="--check" exit /b 1
pause
exit /b 1
