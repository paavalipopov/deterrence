param([Parameter(Mandatory)][string]$TestGame,[Parameter(Mandatory)][string]$Profile,[string]$AdaptiveStorageMod,[switch]$Ideology)
$ErrorActionPreference='Stop'
$project=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testBin=Join-Path $TestGame 'Mods\DeterrenceStorageTest'
$about=Join-Path $testBin 'About'
$assemblies=Join-Path $testBin 'Assemblies'
$production='C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
if ((Resolve-Path $TestGame).Path -eq $production -or !(Test-Path (Join-Path $TestGame 'Mods\Antiwealth\About\About.xml'))) {throw 'Supply a disposable RimWorld copy containing Harmony and Deterrence.'}
if (!(Test-Path -LiteralPath $Profile)) {New-Item -ItemType Directory -Path $Profile|Out-Null}
$config=Join-Path $Profile Config
if ((Test-Path -LiteralPath (Join-Path $config 'ModsConfig.xml')) -and !(Test-Path -LiteralPath (Join-Path $Profile 'corpse-test-profile'))) {throw 'Use a new test profile.'}
New-Item -ItemType Directory -Path $config,$about,$assemblies -Force|Out-Null
'Deterrence test'|Set-Content -LiteralPath (Join-Path $Profile 'corpse-test-profile')
'<ModMetaData><name>Deterrence storage tests</name><author>Local tests</author><packageId>local.deterrencestoragetest</packageId><supportedVersions><li>1.6</li></supportedVersions></ModMetaData>'|Set-Content (Join-Path $about 'About.xml')
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
("<ModsConfigData><version>1.6.4871 rev590</version><activeMods><li>brrainz.harmony</li><li>ludeon.rimworld</li>$extra<li>pavel.antiwealth</li><li>local.deterrencestoragetest</li></activeMods><knownExpansions/></ModsConfigData>")|Set-Content (Join-Path $config 'ModsConfig.xml')
$cloneMod=Join-Path $TestGame 'Mods\Antiwealth'
foreach($name in 'Assemblies\Antiwealth.dll','About\About.xml'){Copy-Item -LiteralPath (Join-Path $project $name) -Destination (Join-Path $cloneMod $name) -Force}
$managed=Join-Path $TestGame 'RimWorldWin64_Data\Managed'
$sdkLine=@(& dotnet --list-sdks)[-1]
$ver=($sdkLine -split ' ')[0];$sdkRoot=$sdkLine.Substring($sdkLine.IndexOf('[')+1).TrimEnd(']')
$csc=Join-Path $sdkRoot "$ver\Roslyn\bincore\csc.dll"
$cc=@('/noconfig','/nostdlib+','/target:library',('/out:'+(Join-Path $assemblies 'StorageTest.dll')))
$cc+=Get-ChildItem -LiteralPath $managed -Filter *.dll|ForEach-Object {'/reference:'+$_.FullName}
$cc+='/reference:'+(Join-Path $project 'Assemblies\Antiwealth.dll')
$cc+='/reference:'+(Resolve-Path (Join-Path $project '..\2009463077\1.6\Assemblies\0Harmony.dll')).Path
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
