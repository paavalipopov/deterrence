$ErrorActionPreference = 'Stop'

# Use RIMWORLD_GAME when set, then the test-bed game beside the checkout, then the Steam install.
$testBed = Join-Path $PSScriptRoot '..\..\..\game'
$gameRoot = if ($env:RIMWORLD_GAME) { $env:RIMWORLD_GAME }
    elseif (Test-Path -LiteralPath (Join-Path $testBed 'RimWorldWin64.exe')) { (Resolve-Path -LiteralPath $testBed).Path }
    else { 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld' }
$managed = Join-Path $gameRoot 'RimWorldWin64_Data\Managed'
[AppDomain]::CurrentDomain.add_AssemblyResolve({ param($s,$e)
    $p = Join-Path $managed (($e.Name.Split(',')[0])+'.dll')
    if (Test-Path -LiteralPath $p) { [Reflection.Assembly]::LoadFrom($p) }
}.GetNewClosure())
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
foreach ($name in $args) {
    $type = $assembly.GetType($name, $true)
    "TYPE $name"
    $type.GetFields($flags) | ForEach-Object { "FIELD $_" }
    $type.GetProperties($flags) | ForEach-Object { "PROPERTY $_" }
    $type.GetMethods($flags) | Where-Object { !$_.IsSpecialName } | ForEach-Object { "METHOD $_" }
}
