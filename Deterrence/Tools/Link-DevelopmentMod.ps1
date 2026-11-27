param([string]$GameRoot = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$game = (Resolve-Path -LiteralPath $GameRoot).Path
$mods = (Resolve-Path -LiteralPath (Join-Path $game 'Mods')).Path
$target = Join-Path $mods 'Deterrence'
function Assert-Deterrence($folder) {
    $metadata = [xml][IO.File]::ReadAllText((Join-Path $folder 'About\About.xml'))
    if ($metadata.ModMetaData.packageId -ne 'pavel.deterrence') { throw "Unexpected mod at $folder" }
}
Assert-Deterrence $source
if ($source -eq $target -or !(Test-Path -LiteralPath (Join-Path $source 'Assemblies\Deterrence.dll'))) { throw 'Run this from the built development checkout.' }
$installed = Get-Item -LiteralPath $target -Force -ErrorAction SilentlyContinue
if ($installed -and ($installed.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
    if ($installed.LinkType -eq 'Junction' -and $installed.Target -contains $source) { Write-Output "Already linked to $source"; return }
    throw "A different link exists at $target"
}
if (Get-Process -Name RimWorldWin64 -ErrorAction SilentlyContinue) { throw 'Close RimWorld and run this again.' }
if ($installed) {
    Assert-Deterrence $target
    $idFile = 'About\PublishedFileId.txt'
    if (Test-Path -LiteralPath (Join-Path $target $idFile)) {
        if (!(Test-Path -LiteralPath (Join-Path $source $idFile)) -or
            (Get-Content -Raw -LiteralPath (Join-Path $source $idFile)).Trim() -ne
            (Get-Content -Raw -LiteralPath (Join-Path $target $idFile)).Trim()) { throw 'Check the installed Workshop ID before linking.' }
    }
    $backup = Join-Path $game ('ModBackups\Deterrence\before-dev-link-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $backup | Out-Null
    $saved = Join-Path $backup 'Deterrence'
    if ((Split-Path $installed.FullName -Parent) -ne $mods -or (Split-Path $saved -Parent) -ne $backup) { throw 'Invalid backup path.' }
}
$stage = Join-Path $mods ('Deterrence-link-' + [guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Junction -Path $stage -Target $source | Out-Null
    if ($installed) {
        Move-Item -LiteralPath $installed.FullName -Destination $saved
        Write-Output "Previous installation saved at $saved"
    }
    [IO.Directory]::Move($stage,$target)
} catch {
    if ($installed -and (Test-Path -LiteralPath $saved) -and !(Test-Path -LiteralPath $target)) {
        Move-Item -LiteralPath $saved -Destination $target
    }
    throw
} finally {
    if (Test-Path -LiteralPath $stage) { [IO.Directory]::Delete($stage) }
}
Write-Output "RimWorld now loads $source"
