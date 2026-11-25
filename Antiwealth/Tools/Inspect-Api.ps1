$ErrorActionPreference = 'Stop'
$managed = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed'
[AppDomain]::CurrentDomain.add_AssemblyResolve({ param($s,$e)
    $p = Join-Path 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed' (($e.Name.Split(',')[0])+'.dll')
    if (Test-Path -LiteralPath $p) { [Reflection.Assembly]::LoadFrom($p) }
})
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
foreach ($name in $args) {
    $type = $assembly.GetType($name, $true)
    "TYPE $name"
    $type.GetFields($flags) | ForEach-Object { "FIELD $_" }
    $type.GetProperties($flags) | ForEach-Object { "PROPERTY $_" }
    $type.GetMethods($flags) | Where-Object { !$_.IsSpecialName } | ForEach-Object { "METHOD $_" }
}
