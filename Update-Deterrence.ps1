param(
    [string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [switch]$Check
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot 'Deterrence')).Path
$game = (Resolve-Path -LiteralPath $GameRoot).Path
$mods = (Resolve-Path -LiteralPath (Join-Path $game 'Mods')).Path
$target = Join-Path $mods 'Deterrence'
$legacy = Join-Path $mods 'Antiwealth'
$backupRoot = Join-Path $game 'ModBackups\Deterrence'
$required = 'About\About.xml','Assemblies\Deterrence.dll','Defs\MainButtonDefs\Deterrence.xml','About\Preview.png'

function Read-ModId($folder) {
    $about = [xml][IO.File]::ReadAllText((Join-Path $folder 'About\About.xml'))
    return $about.ModMetaData.packageId
}

function Assert-OrdinaryFolder($path) {
    if ((Get-Item -LiteralPath $path).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "Linked folder requires a manual installation: $path"
    }
}

if ($source -eq $target -or $source -eq $legacy) { throw 'Extract the download outside the installed mod folders.' }
if ((Read-ModId $source) -ne 'pavel.deterrence') { throw "Unexpected source mod in $source" }
foreach ($name in $required) {
    if (!(Test-Path -LiteralPath (Join-Path $source $name) -PathType Leaf)) { throw "Missing source file: $name" }
}
Assert-OrdinaryFolder $mods
foreach ($folder in $legacy,$target) {
    if (!(Test-Path -LiteralPath $folder)) { continue }
    Assert-OrdinaryFolder $folder
    $expected = if ($folder -eq $legacy) { 'pavel.antiwealth' } else { 'pavel.deterrence' }
    if ((Read-ModId $folder) -ne $expected) { throw "Unexpected mod in $folder" }
}
$ids = @($source,$target,$legacy | ForEach-Object {
    $file = Join-Path $_ 'About\PublishedFileId.txt'
    if (Test-Path -LiteralPath $file) { [IO.File]::ReadAllText($file).Trim() }
} | Where-Object { $_ } | Select-Object -Unique)
if ($ids.Count -gt 1) { throw 'The installed copies belong to different Workshop items. Select the correct one manually.' }
if ($Check) { Write-Host "Preflight passed for $source -> $target"; return }
if (Get-Process -Name RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Close RimWorld before updating.' }

# Staging and backups stay on the game volume, outside Mods. Directory renames
# leave an entire installation intact even if a file is locked or copying fails.
foreach ($folder in (Join-Path $game 'ModBackups'),$backupRoot) {
    if (Test-Path -LiteralPath $folder) { Assert-OrdinaryFolder $folder }
}
$backup = Join-Path $backupRoot ((Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$stage = Join-Path $backup 'Staged'
New-Item -Type Directory -Path $stage -Force | Out-Null
foreach ($item in (Get-ChildItem -LiteralPath $source -Force)) {
    if ($item.Name -in @('Tests','Tools','.git','.gitignore','Build.ps1','Test.ps1')) { continue }
    Copy-Item -LiteralPath $item.FullName -Destination $stage -Recurse
}
foreach ($name in $required) {
    if ((Get-FileHash -LiteralPath (Join-Path $source $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $stage $name)).Hash) { throw "Verification failed for $name" }
}
if ($ids.Count -eq 1) { [IO.File]::WriteAllText((Join-Path $stage 'About\PublishedFileId.txt'),$ids[0]) }
$moved = @()
try {
    foreach ($folder in $legacy,$target) {
        if (!(Test-Path -LiteralPath $folder)) { continue }
        $saved = Join-Path $backup (Split-Path $folder -Leaf)
        if ((Split-Path $folder -Parent) -ne $mods -or (Split-Path $saved -Parent) -ne $backup) { throw 'Invalid backup path.' }
        [IO.Directory]::Move($folder,$saved)
        $moved += $folder
    }
    if ((Split-Path $target -Parent) -ne $mods -or (Split-Path $stage -Parent) -ne $backup) { throw 'Invalid installation path.' }
    [IO.Directory]::Move($stage,$target)
} catch {
    foreach ($folder in $moved) {
        $saved = Join-Path $backup (Split-Path $folder -Leaf)
        if ((Split-Path $folder -Parent) -eq $mods -and (Split-Path $saved -Parent) -eq $backup -and !(Test-Path -LiteralPath $folder)) {
            [IO.Directory]::Move($saved,$folder)
        }
    }
    throw
}
Write-Host "Installed Deterrence in $target. Enable Deterrence after Harmony."
if ($moved.Count) { Write-Host "Previous installation: $backup" }
