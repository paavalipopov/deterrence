param(
    [string]$GameRoot,
    [string]$HarmonyPath
)
$ErrorActionPreference = 'Stop'

# Use RIMWORLD_GAME when set, then the test-bed game beside the checkout, then the Steam install.
if (!$GameRoot) { $GameRoot = $env:RIMWORLD_GAME }
if (!$GameRoot) {
    $testBed = Join-Path $PSScriptRoot '..\..\game'
    $GameRoot = if (Test-Path -LiteralPath (Join-Path $testBed 'RimWorldWin64.exe')) { (Resolve-Path -LiteralPath $testBed).Path }
        else { 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld' }
}

# Find Harmony in the Workshop folder or among the game's local mods.
if (!$HarmonyPath) {
    $candidates = @((Join-Path $GameRoot '..\..\workshop\content\294100\2009463077\1.6\Assemblies\0Harmony.dll'),
        (Join-Path $GameRoot 'Mods\2009463077\1.6\Assemblies\0Harmony.dll'))
    $HarmonyPath = @($candidates | Where-Object { Test-Path -LiteralPath $_ }) + $candidates | Select-Object -First 1
}
$managed = Join-Path $GameRoot 'RimWorldWin64_Data\Managed'
if (!(Test-Path -LiteralPath (Join-Path $managed 'Assembly-CSharp.dll'))) { throw 'RimWorld managed assemblies not found. Supply -GameRoot.' }
if (!(Test-Path -LiteralPath $HarmonyPath)) { throw 'Harmony not found. Supply -HarmonyPath to the installed 0Harmony.dll.' }
$sdkLine = @(& dotnet --list-sdks)[-1]
$sdkVersion = ($sdkLine -split ' ')[0]
$sdkRoot = $sdkLine.Substring($sdkLine.IndexOf('[') + 1).TrimEnd(']')
$compiler = Join-Path $sdkRoot "$sdkVersion\Roslyn\bincore\csc.dll"
New-Item -ItemType Directory -Path (Join-Path $PSScriptRoot 'Assemblies') -Force | Out-Null
$compilerArgs = @('/noconfig', '/nostdlib+', '/target:library', '/langversion:latest', '/optimize+', '/deterministic+', '/warn:4', '/warnaserror+', "/out:$(Join-Path $PSScriptRoot 'Assemblies\Deterrence.dll')")
$compilerArgs += Get-ChildItem -LiteralPath $managed -File -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName }
$compilerArgs += '/reference:' + (Resolve-Path -LiteralPath $HarmonyPath).Path
$compilerArgs += Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'Source') -File -Filter '*.cs' | ForEach-Object { $_.FullName }
& dotnet $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "Compilation failed ($LASTEXITCODE)." }
Write-Output 'Built Assemblies\Deterrence.dll against the installed RimWorld and Harmony assemblies.'
