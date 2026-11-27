#Requires -Version 5.1
<#
Collects everything the test laptop needs into one zip for a flash drive.
- Game: the complete RimWorld install with its DLCs. The Mods\Deterrence development link is skipped.
- Workshop mods: the ones enabled in game, plus Harmony and Adaptive Storage, placed in the game's
  Mods folder under their Workshop IDs so they load without Steam and keep their settings files.
- Profile: the game's save-data folder (config, mod settings, HugsLib, presets), without logs.
  Saves are left out unless -IncludeSaves is given.
- Code: the Deterrence checkout this script sits in, skipping bin, obj and .vs.
- Setup: Setup-TestUser.ps1 and TestUser.config.psd1 from this script's folder.

Run it with no arguments; the zip lands on the desktop:
    powershell -ExecutionPolicy Bypass -File .\Pack-TestBed.ps1
#>
[CmdletBinding()]
param(
    [string]$Game = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$Workshop = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100',
    [string]$UserData = (Join-Path $env:USERPROFILE 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios'),
    [string]$Repo,
    [string]$OutZip = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'RimTestPack.zip'),
    [switch]$IncludeSaves
)

$ErrorActionPreference = 'Stop'
$harmonyId = '2009463077'
$alwaysInclude = $harmonyId, '3033901359'
$modsConfig = Join-Path $UserData 'Config\ModsConfig.xml'

# Copy a folder tree with robocopy, skipping junctions and optionally folders and files.
function Copy-Tree([string]$From, [string]$To, [string[]]$ExcludeDirs, [string[]]$ExcludeFiles) {
    $options = @('/E', '/XJ', '/R:1', '/W:1', '/NFL', '/NDL', '/NJH', '/NJS', '/NP')
    if ($ExcludeDirs) { $options += '/XD'; $options += $ExcludeDirs }
    if ($ExcludeFiles) { $options += '/XF'; $options += $ExcludeFiles }
    robocopy $From $To @options | Out-Null

    # Robocopy exit codes of 8 and above mean failure.
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed copying $From (exit code $LASTEXITCODE)." }
}

# Read a mod's packageId from About\About.xml, or $null if unreadable.
function Get-PackageId([string]$ModDir) {
    try {
        $about = [xml](Get-Content -Raw -LiteralPath (Join-Path $ModDir 'About\About.xml'))
        $about.ModMetaData.packageId.Trim().ToLowerInvariant()
    } catch { $null }
}

# Find the Deterrence checkout by walking up from this script's folder.
if (-not $Repo) {
    $dir = $PSScriptRoot
    while ($dir -and -not (Test-Path -LiteralPath (Join-Path $dir 'Deterrence\Build.ps1'))) {
        $dir = Split-Path -Parent $dir
    }
    $Repo = $dir
}

# Check inputs before copying several GB.
if (Get-Process -Name RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Close RimWorld and run this again.' }
$setupFiles = 'Setup-TestUser.ps1', 'TestUser.config.psd1' | ForEach-Object { Join-Path $PSScriptRoot $_ }
foreach ($path in @((Join-Path $Game 'RimWorldWin64.exe'), (Join-Path $Workshop $harmonyId)) + $setupFiles) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Not found: $path" }
}
if (Test-Path -LiteralPath $OutZip) { throw "$OutZip already exists. Delete it and run again." }

# Pick workshop mods: the ones enabled in game, plus the two the tests use.
$workshopMods = @(Get-ChildItem -LiteralPath $Workshop -Directory)
if (Test-Path -LiteralPath $modsConfig) {
    # RimWorld adds _steam to workshop ids that clash with a local mod.
    $active = @(([xml](Get-Content -Raw -LiteralPath $modsConfig)).ModsConfigData.activeMods.li |
        ForEach-Object { $_.Trim().ToLowerInvariant() -replace '_steam$', '' })
    $workshopMods = @($workshopMods | Where-Object {
        ($alwaysInclude -contains $_.Name) -or ($active -contains (Get-PackageId $_.FullName))
    })
} else {
    Write-Warning "No ModsConfig.xml at $modsConfig; packing all workshop mods."
}

# Stage the layout of C:\RimTest, with the setup scripts kept beside it.
$stage = Join-Path $env:TEMP ('RimTestPack-' + [guid]::NewGuid().ToString('N'))
$testRoot = Join-Path $stage 'RimTest'
try {
    Write-Host 'Copying the game...'
    Copy-Tree $Game (Join-Path $testRoot 'game') @((Join-Path $Game 'ModBackups'))

    # Local mods keep their Workshop folder names, which mod settings files are named after.
    Write-Host "Copying $($workshopMods.Count) workshop mods..."
    foreach ($mod in $workshopMods) {
        Copy-Tree $mod.FullName (Join-Path $testRoot "game\Mods\$($mod.Name)")
    }

    if (Test-Path -LiteralPath $UserData) {
        Write-Host 'Copying the game profile...'
        $skipDirs = @()
        if (-not $IncludeSaves) { $skipDirs += (Join-Path $UserData 'Saves') }
        Copy-Tree $UserData (Join-Path $testRoot 'profile') $skipDirs @('*.log', 'steam_autocloud.vdf')
    } else {
        Write-Warning "No game profile at $UserData; skipping it."
    }

    if ($Repo) {
        Write-Host "Copying the Deterrence checkout from $Repo..."
        Copy-Tree $Repo (Join-Path $testRoot 'repo') @('bin', 'obj', '.vs')
    } else {
        Write-Warning 'No Deterrence checkout found above this script; clone it on the laptop instead.'
    }

    New-Item -ItemType Directory -Path (Join-Path $stage 'setup') | Out-Null
    Copy-Item -LiteralPath $setupFiles -Destination (Join-Path $stage 'setup')

    # Zip with .NET directly, which is faster than Compress-Archive and handles large files.
    Write-Host 'Zipping...'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($stage, $OutZip)
}
finally {
    # Remove the staging copy, also after a failure.
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}

$sizeGB = (Get-Item -LiteralPath $OutZip).Length / 1GB
Write-Host ('Created {0} ({1:N1} GB).' -f $OutZip, $sizeGB)
if ($sizeGB -ge 4) {
    Write-Warning 'The zip is over 4 GB; a FAT32 flash drive cannot hold it. Use exFAT or NTFS.'
}
Write-Host 'On the laptop: extract the zip, then run setup\Setup-TestUser.ps1 from an administrator PowerShell.'
