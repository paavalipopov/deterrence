param([Parameter(Mandatory)][string]$TestGame,[Parameter(Mandatory)][string]$Profile,[string]$AdaptiveStorageMod,[switch]$Ideology,[string]$LegacySave,[string]$HarmonyPath = (Join-Path $TestGame 'Mods\Harmony\1.6\Assemblies\0Harmony.dll'))
$ErrorActionPreference='Stop'
# Fall back to Harmony under its Workshop ID, as packed for the test bed.
$workshopHarmony=Join-Path $TestGame 'Mods\2009463077\1.6\Assemblies\0Harmony.dll'
if (!(Test-Path -LiteralPath $HarmonyPath) -and (Test-Path -LiteralPath $workshopHarmony)) {$HarmonyPath=$workshopHarmony}
$project=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testBin=Join-Path $TestGame 'Mods\DeterrenceStorageTest'
$about=Join-Path $testBin 'About'
$assemblies=Join-Path $testBin 'Assemblies'
$production='C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
if ((Resolve-Path $TestGame).Path -eq $production -or !(Test-Path (Join-Path $TestGame 'Mods\Deterrence\About\About.xml'))) {throw 'Supply a disposable RimWorld copy containing Harmony and Deterrence.'}
if (!(Test-Path -LiteralPath $Profile)) {New-Item -ItemType Directory -Path $Profile|Out-Null}
$config=Join-Path $Profile Config
if ((Test-Path -LiteralPath (Join-Path $config 'ModsConfig.xml')) -and !(Test-Path -LiteralPath (Join-Path $Profile 'corpse-test-profile'))) {throw 'Use a new test profile.'}
New-Item -ItemType Directory -Path $config,$about,$assemblies -Force|Out-Null
'Deterrence test'|Set-Content -LiteralPath (Join-Path $Profile 'corpse-test-profile')
'<ModMetaData><name>Deterrence storage tests</name><author>Local tests</author><packageId>local.deterrencestoragetest</packageId><supportedVersions><li>1.6</li></supportedVersions></ModMetaData>'|Set-Content (Join-Path $about 'About.xml')
$env:DETERRENCE_TEST_LEGACY='0'
if ($LegacySave) {
    $saves=Join-Path $Profile 'Saves'
    New-Item -Type Directory -Path $saves -Force | Out-Null
    $beforePath=Join-Path $saves 'AntiwealthBeforeRename.rws'
    $afterPath=Join-Path $saves 'DeterrenceAfterRename.rws'
    if (Test-Path -LiteralPath (Join-Path $config 'Mod_Deterrence_DeterrenceMod.xml')) {throw 'Use a fresh profile for the rename test.'}
    Copy-Item -LiteralPath $LegacySave -Destination $beforePath
    '<SettingsBlock><ModSettings Class="Antiwealth.AntiwealthSettings"><schemaVersion>6</schemaVersion><corpsePerCombatPower>5</corpsePerCombatPower><islandMaxTiles>137</islandMaxTiles></ModSettings></SettingsBlock>' | Set-Content -LiteralPath (Join-Path $config 'Mod_Antiwealth_AntiwealthMod.xml')
    $env:DETERRENCE_TEST_LEGACY='1'
}
$extra=''
if ($Ideology) {
    if (!(Test-Path (Join-Path $TestGame 'Data\Ideology\About\About.xml'))) {throw 'The test game needs the Ideology expansion for this option.'}
    $extra='<li>ludeon.rimworld.ideology</li>'
}
if ($AdaptiveStorageMod) {
    $asf=Join-Path $TestGame 'Mods\AdaptiveStorageFixtureFramework'
    if (!(Test-Path -LiteralPath $asf)) {Copy-Item -LiteralPath $AdaptiveStorageMod -Destination $asf -Recurse}
    $defs=Join-Path $testBin 'Defs'
    New-Item -ItemType Directory -Path $defs -Force|Out-Null
    Copy-Item -LiteralPath (Join-Path $project 'Tests\AdaptiveStorageFixture.xml') -Destination $defs -Force
    $extra+='<li>adaptive.storage.framework</li>'
}
("<ModsConfigData><version>1.6.4871 rev590</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li>$extra<li>pavel.deterrence</li><li>local.deterrencestoragetest</li></activeMods><knownExpansions/></ModsConfigData>")|Set-Content (Join-Path $config 'ModsConfig.xml')
$cloneMod=Join-Path $TestGame 'Mods\Deterrence'
foreach($name in 'Assemblies\Deterrence.dll','About\About.xml'){Copy-Item -LiteralPath (Join-Path $project $name) -Destination (Join-Path $cloneMod $name) -Force}
$managed=Join-Path $TestGame 'RimWorldWin64_Data\Managed'
$sdkLine=@(& dotnet --list-sdks)[-1]
$ver=($sdkLine -split ' ')[0];$sdkRoot=$sdkLine.Substring($sdkLine.IndexOf('[')+1).TrimEnd(']')
$csc=Join-Path $sdkRoot "$ver\Roslyn\bincore\csc.dll"
$cc=@('/noconfig','/nostdlib+','/target:library',('/out:'+(Join-Path $assemblies 'StorageTest.dll')))
$cc+=Get-ChildItem -LiteralPath $managed -Filter *.dll|ForEach-Object {'/reference:'+$_.FullName}
$cc+='/reference:'+(Join-Path $project 'Assemblies\Deterrence.dll')
$cc+='/reference:'+(Resolve-Path -LiteralPath $HarmonyPath).Path
$cc+=Join-Path $project 'Tests\CorpseStorageGame.cs'
& dotnet $csc @cc
if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
$env:DETERRENCE_TEST_RESULT=Join-Path $Profile ('result-'+[Guid]::NewGuid().ToString('N')+'.txt')
$exe=Join-Path $TestGame 'RimWorldWin64.exe'
$log=Join-Path $Profile 'storage-test.log'
$proc=Start-Process -FilePath $exe -WorkingDirectory $TestGame -WindowStyle Hidden -PassThru -ArgumentList @('-quicktest','-batchmode',('-savedatafolder="'+$Profile+'"'),'-logFile',('"'+$log+'"'))
"Started isolated game $($proc.Id)"
try{
    $limit=[DateTime]::UtcNow.AddMinutes(6)
    while (!(Test-Path -LiteralPath $env:DETERRENCE_TEST_RESULT) -and !$proc.HasExited -and [DateTime]::UtcNow -lt $limit) {Start-Sleep -Seconds 2}
    if(!(Test-Path -LiteralPath $env:DETERRENCE_TEST_RESULT)){Get-Content -LiteralPath $log -Tail 25;throw 'The game did not report a test result.'}
    $result=Get-Content -LiteralPath $env:DETERRENCE_TEST_RESULT
    $result
    if($result[-1] -ne 'SUCCESS'){throw 'Storage test failed.'}
} finally {
    if(!$proc.HasExited -and !$proc.WaitForExit(3000) -and $proc.Path -eq $exe){$proc.Kill()}
}

if ($LegacySave) {
    $before=[xml][IO.File]::ReadAllText($beforePath)
    $after=[xml][IO.File]::ReadAllText($afterPath)
    $old=$before.SelectSingleNode("//*[@Class='Antiwealth.AntiwealthGameComponent']")
    $new=$after.SelectSingleNode("//*[@Class='Deterrence.DeterrenceGameComponent']")
    if (!$old -or !$new) {throw 'The renamed game component did not round-trip.'}
    foreach ($tag in 'antiwealthEntries','antiwealthCreditedDeaths','antiwealthGearSnapshots','antiwealthFactionLedgers') {
        foreach ($entry in $old.SelectNodes("$tag/li")) {
            if ($entry.OuterXml -notin @($new.SelectNodes("$tag/li") | ForEach-Object OuterXml)) {throw "Lost legacy $tag entry."}
        }
    }
    $tag='deterrenceCorpseStorage'
    $expected=@($before.SelectNodes("//$tag") | ForEach-Object OuterXml | Sort-Object)
    $actual=@($after.SelectNodes("//$tag") | ForEach-Object OuterXml | Sort-Object)
    if (!$expected.Count -or (Compare-Object $expected $actual)) {throw "Legacy $tag data changed."}
    # Previously unrecorded bodies may acquire an inferred record on load.
    foreach ($record in $before.SelectNodes('//deterrenceBurialRecord')) {
        $pawnId=$record.ParentNode.SelectSingleNode('id').InnerText
        $current=$after.SelectSingleNode("//*[id='$pawnId']/deterrenceBurialRecord")
        if (!$current -or $record.OuterXml -ne $current.OuterXml) {throw "Lost burial record for $pawnId"}
    }
    'PASS old colony ledgers, history, death IDs, burial records and storage filters survive the rename'
}
