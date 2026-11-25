$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Inspect-Api.ps1')
$modAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '..\Assemblies\Antiwealth.dll'))
$settingsType = $modAssembly.GetType('Antiwealth.AntiwealthSettings')
$settings = [Activator]::CreateInstance($settingsType)
$settings.EnsureBuiltins()
$body = $settings.Rule('remains:human',10,$true)
$body.BaseAmount = 10; $body.PopulationExponent = 0.5; $body.ReferencePopulation = 7
$settings.CorpsePerCombatPower = 10
$settingsType.GetField('schemaVersion',$flags).SetValue($settings,0)
$settingsType.GetMethod('MigrateLegacySettings',$flags).Invoke($settings,@()) | Out-Null
if ($body.BaseAmount -ne 5 -or $settings.Rule('bump:human',5,$true).BaseAmount -ne 5) { throw 'Failed legacy split' }
if ($settings.Rule('bump:human',5,$true).PopulationExponent -ne 0.5 -or $settings.Rule('bump:human',5,$true).ReferencePopulation -ne 7) { throw 'Population controls lost' }
$settingsType.GetMethod('MigrateLegacySettings',$flags).Invoke($settings,@()) | Out-Null
if ($body.BaseAmount -ne 5 -or $settings.CorpsePerCombatPower -ne 5) { throw 'Migration repeated' }
foreach ($key in @('condition:SolarFlare','condition:Eclipse','condition:PsychicDrone','condition:HeatWave','condition:ColdSnap','terrain:poor','terrain:swamp','illness:Flu')) {
    if ($settings.Rule($key,999,$true).Enabled) { throw "Removed rule re-created: $key" }
}
'PASS compiled settings migration, idempotence, population controls, and removed-rule guards'

$ledgerType = $modAssembly.GetType('Antiwealth.DeterrenceLedger')
$component = [Activator]::CreateInstance($ledgerType)
$entryType = $modAssembly.GetType('Antiwealth.LedgerEntry')
$entries = $ledgerType.GetField('entries',$flags).GetValue($component)
foreach ($spec in @(
    @('illness:7:1','illness:Flu','Illness'),
    @('condition:7:2','condition:SolarFlare','Environment'),
    @('condition:7:3','condition:Eclipse','Environment'),
    @('condition:7:4','condition:PsychicDrone','Environment'),
    @('condition:7:5','condition:HeatWave','Environment'),
    @('condition:7:6','condition:ColdSnap','Environment'),
    @('terrain:poor:7','terrain:poor','Environment'),
    @('terrain:swamp:7','terrain:swamp','Environment'),
    @('condition:7:20','condition:ToxicFallout','Environment'),
    @('pollution:7','pollution:ground','Pollution'),
    @('remains:42','remains:human','Remains')
)) {
    $entry = [Activator]::CreateInstance($entryType)
    $entry.Key=$spec[0]; $entry.SourceKey=$spec[1]; $entry.Category=$spec[2]; $entry.MapId=7; $entry.Balance=100
    $entries.Add($entry)
}
$removed = $ledgerType.GetMethod('DiscardRemovedBalances',$flags).Invoke($component,@())
if ($removed -ne 8 -or $entries.Count -ne 3) { throw 'Ledger migration discarded unrelated balances' }
if (!($entries | Where-Object Key -eq 'condition:7:ToxicFallout')) { throw 'Typed fallout meter migration failed' }
'PASS compiled ledger migration preserves unrelated balances and canonicalizes fallout'

$rateMethod = $ledgerType.GetMethod('ConditionRate',$flags)
foreach ($namedWeather in @('HeatWave','ColdSnap')) {
    if ($rateMethod.Invoke($null,@($namedWeather,$settings,5)) -ne 0) { throw 'Removed named weather still grants credit' }
}
foreach ($key in @('climate:heat','climate:cold')) {
    $rule = $settings.Rule($key)
    if (!$rule.Enabled -or $rule.BaseAmount -ne 1000 -or $settings.LegacyDefault($key) -ne 1000) { throw "Unexpected $key default or reset value" }
    $rule.BaseAmount = 1375
    $settings.EnsureBuiltins()
    if ($settings.Rule($key).BaseAmount -ne 1375) { throw "Custom $key rate was overwritten" }
}
'PASS named weather removed; actual outdoor extremes remain'

$graphType = $modAssembly.GetType('Antiwealth.AntiwealthGraphs')
$curves = $graphType.GetMethod('Laboratory').Invoke($null,@($settings,5,[float]6,[float]2000,[float]5,3))
if ($curves.Count -ne 4) { throw 'Laboratory series missing' }
foreach ($curve in $curves) {
    $previous = -1.0
    foreach ($point in $curve.curve.Points) {
        if ([double]::IsNaN($point.y) -or $point.y -lt 0 -or $point.x -lt $previous) { throw 'Invalid graph data' }
        $previous = $point.x
    }
}
'PASS actual game curve objects: four finite, nonnegative, ordered series'

foreach ($target in @(
    @('RimWorld.MedicalRecipesUtility','SpawnNaturalPartIfClean'),
    @('RimWorld.ExecutionUtility','DoExecutionByCut'),
    @('RimWorld.Recipe_RemoveBodyPart','ApplyOnPawn'),
    @('RimWorld.Recipe_Surgery','CheckSurgeryFail'),
    @('Verse.Pawn','Kill'),
    @('RimWorld.StorytellerUtility','DefaultParmsNow'),
    @('RimWorld.StorytellerComp_RandomMain','GenerateParms'),
    @('RimWorld.IncidentParms','ShallowCopy'),
    @('RimWorld.IncidentParms','ExposeData'),
    @('RimWorld.IncidentWorker_Raid','TryGenerateRaidInfo'),
    @('RimWorld.IncidentWorker_RaidEnemy','TryResolveRaidFaction')
)) {
    if (!($assembly.GetType($target[0]).GetMethods($flags) | Where-Object Name -eq $target[1])) { throw "Missing patch target $target" }
}
'PASS installed-game patch targets exist; live surgery/execution flows still require in-game testing'

$stockRule = $settings.Rule('organs:local',0.75,$true)
$stockRule.BaseAmount = 99; $stockRule.PopulationExponent = 1
$settings.Sanitize()
if ($stockRule.BaseAmount -ne 5 -or $stockRule.PopulationExponent -ne 0) { throw 'Organ multiplier limits failed' }
'PASS organ multiplier clamps to 5 and stays independent of population'

$thingDefType = $assembly.GetType('Verse.ThingDef')
$thingType = $assembly.GetType('Verse.ThingWithComps')
# Constructors touch Unity textures, so only these synthetic test fixtures bypass them.
$testDef = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($thingDefType)
$assembly.GetType('Verse.Def').GetField('defName').SetValue($testDef,'Antiwealth_TestNaturalOrgan')
$thingDefType.GetField('thingClass').SetValue($testDef,$thingType)
$defListType = [System.Collections.Generic.List``1].MakeGenericType($thingDefType)
$testDefinitions = [Activator]::CreateInstance($defListType)
$testDefinitions.Add($testDef)
$installArgs = [object[]]::new(1); $installArgs[0] = $testDefinitions
$setupType = $modAssembly.GetType('Antiwealth.OrganTrackingSetup')
$setupType.GetMethod('InstallOn').Invoke($null,$installArgs) | Out-Null
$setupType.GetMethod('InstallOn').Invoke($null,$installArgs) | Out-Null
if ($thingDefType.GetField('comps').GetValue($testDef).Count -ne 1) { throw 'Duplicate provenance comps added' }
$testItems = @()
for ($index=0; $index -lt 3; $index++) {
    $item = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($thingType)
    $baseThingType = $assembly.GetType('Verse.Thing')
    $baseThingType.GetField('def').SetValue($item,$testDef)
    $baseThingType.GetField('stackCount').SetValue($item,1)
    $thingType.GetMethod('InitializeComps',$flags).Invoke($item,@()) | Out-Null
    $testItems += $item
}
$allCompsProperty = $thingType.GetProperty('AllComps',$flags)
$firstTag = $allCompsProperty.GetValue($testItems[0])[0]
$secondTag = $allCompsProperty.GetValue($testItems[1])[0]
$plainTag = $allCompsProperty.GetValue($testItems[2])[0]
$firstTag.HarvestedLocallyFromPrisoner = $true
if ($firstTag.AllowStackWith($testItems[2]) -or $plainTag.AllowStackWith($testItems[0])) { throw 'Imported organs mixed with local provenance' }
$firstTag.PostSplitOff($testItems[1])
if (!$secondTag.HarvestedLocallyFromPrisoner -or !$firstTag.AllowStackWith($testItems[1])) { throw 'Split lost provenance' }
if ([string]::IsNullOrEmpty($firstTag.CompInspectStringExtra())) { throw 'Origin is not visible in item inspection' }
'PASS real game item comps: idempotent setup, mixed-origin stack rejection, split provenance, inspection text'

$freshSettings = [Activator]::CreateInstance($settingsType)
$freshSettings.EnsureBuiltins()
if ($freshSettings.Rule('remains:animal',1,$true).BaseAmount -ne 1) { throw 'Animal default is not reduced' }
$freshSettings.Rule('remains:animal',1,$true).BaseAmount = 0.4
$freshSettings.Sanitize()
if ([Math]::Abs($freshSettings.Rule('remains:animal',1,$true).BaseAmount - 0.4) -gt 0.00001) { throw 'Custom animal weight was reset' }
'PASS animal body default and fractional custom weight'

$listingType = $assembly.GetType('Verse.Listing_Standard')
$baseListingType = $assembly.GetType('Verse.Listing')
$listing = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($listingType)
$unityCore = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityEngine.CoreModule.dll'))
$rectType = $unityCore.GetType('UnityEngine.Rect')
$listingRect = [Activator]::CreateInstance($rectType,[object[]]@([float]0,[float]0,[float]400,[float]40))
$baseListingType.GetField('listingRect',$flags).SetValue($listing,$listingRect)
$baseListingType.GetField('columnWidthInt',$flags).SetValue($listing,[float]400)
$baseListingType.GetField('curY',$flags).SetValue($listing,[float]20)
$singleArgs = [object[]]::new(1); $singleArgs[0] = $listing
$modAssembly.GetType('Antiwealth.HelpUI').GetMethod('SingleColumn').Invoke($null,$singleArgs) | Out-Null
$nextRect = $baseListingType.GetMethod('GetRect',$flags).Invoke($listing,@([float]40,[float]1))
if ($nextRect.x -ne 0 -or $nextRect.y -ne 20) { throw 'Long detail page moved controls into an invisible column' }
'PASS settings layout regression: overflowing controls remain in the scrolling column'

$bodyComponent = [Activator]::CreateInstance($ledgerType)
$bodyEntries = $ledgerType.GetField('entries',$flags).GetValue($bodyComponent)
$keptBody = [Activator]::CreateInstance($entryType)
$keptBody.Key='remains:700'; $keptBody.SourceKey='remains:human'; $keptBody.CorpseId=700; $keptBody.MapId=7
$keptBody.Active=$true; $keptBody.Balance=500; $keptBody.Buried=$true
$missingBody = [Activator]::CreateInstance($entryType)
$missingBody.Key='remains:701'; $missingBody.SourceKey='remains:human'; $missingBody.CorpseId=701; $missingBody.MapId=7
$missingBody.Active=$true; $missingBody.Balance=500
$bodyEntries.Add($keptBody); $bodyEntries.Add($missingBody)
$bodySeen = $ledgerType.GetField('seen',$flags).GetValue($bodyComponent)
$bodySeen.Add('remains:700') | Out-Null
$endUnseen = $ledgerType.GetMethod('EndUnseenSources',$flags)
$endUnseen.Invoke($bodyComponent,@([double]10,$settings)) | Out-Null
if ($missingBody.Active -or $missingBody.EndDay -ne 10 -or !$keptBody.Active) { throw 'Missing-corpse detection failed or burial ended' }
$endUnseen.Invoke($bodyComponent,@([double]11,$settings)) | Out-Null
if ($missingBody.EndDay -ne 10) { throw 'Missing corpse fade renewed on refresh' }
$missingBody.Advance($missingBody.EndDay + $missingBody.DecayDuration)
if ($missingBody.Balance -ne 0) { throw 'Removed corpse credit never expired' }
'PASS disappearance reconciliation: missing corpse fades once to zero; buried corpse remains'

$ritualRule=$freshSettings.Rule('bump:ritual',5000,$true)
if ($ritualRule.BaseAmount -ne 5000) { throw 'Ritual reward default missing' }
$workerType=$modAssembly.GetType('Antiwealth.RitualReputation')
if ($workerType.BaseType.FullName -ne 'RimWorld.RitualAttachableOutcomeEffectWorker') { throw 'Wrong ritual extension point' }
[xml]$ritualXml=Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Defs\Rituals\ReputationReward.xml') -Raw
if ($ritualXml.Defs.RitualAttachableOutcomeEffectDef.MayRequire -ne 'Ludeon.RimWorld.Ideology' -or $ritualXml.Defs.RitualAttachableOutcomeEffectDef.onlyPositiveOutcomes -ne 'true') { throw 'Ritual DLC/outcome gates missing' }
if ($freshSettings.SmoothedSurfaceMultiplier -ne 0) { throw 'Surface default must exclude smoothed wealth' }
$freshSettings.SmoothedSurfaceMultiplier=0.4; $freshSettings.Sanitize()
if ([Math]::Abs($freshSettings.SmoothedSurfaceMultiplier-0.4) -gt 0.00001) { throw 'Surface multiplier changed' }
'PASS optional positive ritual reward, installed-game worker signature, and smoothed surface factory default'

$freshSettings.IncludeHumanGear = $true
$humanRule = $freshSettings.Rule('bump:human',5,$true)
$previewArgs = @($humanRule,$freshSettings,[int]5,[float]5,[double]1,$false,[double]100,[double]1000)
$humanPreview = $modAssembly.GetType('Antiwealth.AntiwealthGraphs').GetMethod('Preview').Invoke($null,$previewArgs)
$previewPeak = ($humanPreview[0].curve.Points | Measure-Object -Property y -Maximum).Maximum
$expectedPeak = $humanRule.AmountFor(5)*100 + 1000*$freshSettings.HumanGearFraction
if ([Math]::Abs($previewPeak-$expectedPeak) -gt 0.001) { throw 'Human preview omits the configured gear value' }
$editorType = $modAssembly.GetType('Antiwealth.SettingsEditor')
$catalog = $editorType.GetMethod('BuildSources',$flags)
foreach ($hasDlc in @($false,$true)) {
    $uiSources = $catalog.Invoke($null,@($freshSettings,$hasDlc,$hasDlc))
    $ritualSource = $uiSources | Where-Object { $_.Rule.Key -eq 'bump:ritual' }
    $pollutionSource = $uiSources | Where-Object { $_.Rule.Key -eq 'pollution:ground' }
    if (!$ritualSource -or $ritualSource.Installed -ne $hasDlc -or !$pollutionSource -or $pollutionSource.Installed -ne $hasDlc) { throw 'Settings source availability does not follow the DLC gates' }
    if ($uiSources | Where-Object { $_.Rule.Key -in @('condition:HeatWave','condition:ColdSnap') }) { throw 'Named weather remains in settings catalog' }
}
'PASS compiled settings source identity: human gear preview and ritual DLC availability'

$oldDisabled = [Activator]::CreateInstance($settingsType)
$settingsType.GetField('schemaVersion',$flags).SetValue($oldDisabled,5)
$oldDisabled.Enabled = $false; $oldDisabled.BiomeWealthScaling = $true; $oldDisabled.SmoothedSurfaceMultiplier = 0.4
$settingsType.GetMethod('MigrateLegacySettings',$flags).Invoke($oldDisabled,@()) | Out-Null
if ($oldDisabled.Enabled -or $oldDisabled.BiomeWealthScaling -or $oldDisabled.IslandWealthScaling -or $oldDisabled.SmoothedSurfaceMultiplier -ne 1) { throw 'Old disabled master switch re-enabled an adjustment' }
$oldDisabled.BiomeWealthScaling = $true; $oldDisabled.SmoothedSurfaceMultiplier = 0.4
$settingsType.GetMethod('MigrateLegacySettings',$flags).Invoke($oldDisabled,@()) | Out-Null
if (!$oldDisabled.BiomeWealthScaling -or [Math]::Abs($oldDisabled.SmoothedSurfaceMultiplier-0.4) -gt 0.00001) { throw 'Formula-switch migration repeated' }
[xml]$buttonXml = Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\Defs\MainButtonDefs\Antiwealth.xml') -Raw
$button = $buttonXml.Defs.MainButtonDef
$worldButtonsPath = Join-Path (Split-Path (Split-Path $managed -Parent) -Parent) 'Data\Core\Defs\Misc\MainButtonDefs\MainButtons.xml'
[xml]$worldButtonsXml = Get-Content -LiteralPath $worldButtonsPath -Raw
$worldButton = $worldButtonsXml.Defs.MainButtonDef | Where-Object defName -eq 'World'
if ($button.buttonVisible -ne 'true' -or $button.minimized -ne 'true' -or $button.defName -ne 'Antiwealth' -or [int]$button.order -ne [int]$worldButton.order + 1) { throw 'Compact Deterrence button is missing or out of order' }
if (!(Test-Path -LiteralPath (Join-Path $PSScriptRoot ('..\Textures\' + $button.iconPath + '.png')) -PathType Leaf)) { throw 'Deterrence icon missing' }
'PASS formula-switch migration and compact Deterrence button beside World'

$meterNodes = [Collections.Generic.List[Antiwealth.MeterNode]]::new()
foreach ($item in @(@('hostile-remains','Remains',10),@('violence','Reputation',20),@('hazards','Hazards',70))) {
    $meter = [Antiwealth.MeterNode]::new()
    $meter.Key,$meter.Label,$meter.Credit = $item
    $meterNodes.Add($meter)
}
$child = [Antiwealth.MeterNode]::new()
$child.Credit = 70
$meterNodes[2].Children.Add($child)
$pieType = $modAssembly.GetType('Antiwealth.MeterPie')
$pie = [Activator]::CreateInstance($pieType)
$updatePie = $pieType.GetMethod('Update',$flags)
$sliceAt = $pieType.GetMethod('SliceAt',$flags)
$drawPixels = $pieType.GetMethod('ChartPixels',$flags)
$pieArgs = [object[]]::new(1)
$pieArgs[0] = $meterNodes
$updatePie.Invoke($pie,$pieArgs)
if ($sliceAt.Invoke($pie,@([double]0,[double]1)) -ne 0 -or $sliceAt.Invoke($pie,@([double]1,[double]0)) -ne 1 -or $sliceAt.Invoke($pie,@([double]0,[double]-1)) -ne 2) { throw 'Pie chart hover or top-level meter weighting is incorrect' }
$pixels = $drawPixels.Invoke($pie,@())
$width = [int][Math]::Sqrt($pixels.Count)
$middle = [int]($width / 2)
$east = $middle * $width + $middle + 25
$repColor = $pieType.GetMethod('ColorFor',$flags).Invoke($null,@('violence'))
if ($pixels[0].a -ne 0 -or $pixels[$east].a -ne 255 -or $pixels[$east].r -ne $repColor.r) { throw 'Chart colors do not match the legend or hover regions' }
foreach ($meter in $meterNodes) { $meter.Credit = 0 }
$updatePie.Invoke($pie,$pieArgs)
$pixels = $drawPixels.Invoke($pie,@())
if ($sliceAt.Invoke($pie,@([double]1,[double]0)) -ne -1 -or $pixels[$east].a -ne 0 -or $pixels[($middle * $width + $width - 3)].a -ne 255) { throw 'Zero deterrence must draw an empty ring with no active slice' }
$meterNodes[2].Credit = 100
$updatePie.Invoke($pie,$pieArgs)
if ($sliceAt.Invoke($pie,@([double]0,[double]1)) -ne 2 -or $sliceAt.Invoke($pie,@([double]-1,[double]0)) -ne 2) { throw 'A single nonzero meter should fill the pie' }
'PASS meter pie shares, hover angles, legend colors, empty state, and single-meter refresh'

$ink = $modAssembly.GetType('Antiwealth.EquationImage')
$stripe = [UnityEngine.Color32[]]::new(6)
foreach ($i in 1,4) { $stripe[$i] = [UnityEngine.Color32]::new(255,255,255,255) }
$mask = $ink.GetConstructors($flags)[0].Invoke(@(3,2,$stripe))
$raster = $ink.GetMethod('Rasterize',$flags)
$original = $raster.Invoke($mask,@(3,2))
$reduced = $raster.Invoke($mask,@(2,1))
if (($original.a -join ',') -ne '0,255,0,0,255,0' -or ($reduced.a -join ',') -ne '85,85') {
    throw 'Equation rasterization lost coverage at native size or fractional scaling'
}
'PASS unsharpened equation rasterization at integer and fractional sizes'

$curveSettings = [Activator]::CreateInstance($settingsType)
$curveSettings.FadeByHistory = $true
$curveSettings.GrowthShape = 0
$historyLedger = [Activator]::CreateInstance($ledgerType)
$historyEntries = $ledgerType.GetField('entries',$flags).GetValue($historyLedger)
foreach ($spec in @(@('new hazard',-1,$true),@('legacy hazard',-1,$false),@('body',5,$true))) {
    $e = [Activator]::CreateInstance($entryType)
    $e.Key=$spec[0]; $e.CorpseId=$spec[1]; $e.HasArea=$spec[2]; $e.Active=$true
    $e.Balance=7500; $e.ActiveArea=11250; $e.LastSeenDay=3
    if ($e.CorpseId -lt 0) { $e.LastRawRate=2500 }
    $historyEntries.Add($e)
}
$ledgerType.GetMethod('EndUnseenSources',$flags).Invoke($historyLedger,@([double]4,$curveSettings)) | Out-Null
if ($historyEntries[0].ActiveArea -ne 20000 -or $historyEntries[0].Balance -ne 10000 -or $historyEntries[0].DecayDuration -ne 10 -or
    $historyEntries[2].DecayDuration -ne (10 * [Math]::Sqrt(0.75))) { throw 'Ending a hazard missed its last active interval or applied area to a body' }
$curveSettings.FadeReferenceArea = 80000
foreach ($e in ($historyEntries | Where-Object CorpseId -lt 0)) { $e.Active=$true; $e.LastRawRate=2500; $e.LastSeenDay=4 }
$ledgerType.GetMethod('EndUnseenSources',$flags).Invoke($historyLedger,@([double]4,$curveSettings)) | Out-Null
if ($historyEntries[0].DecayDuration -ne 5 -or $historyEntries[1].DecayDuration -ne 10) { throw 'Legacy hazard history was fabricated or new area ignored' }
'PASS live hazard history, final observation interval, legacy fallback and maintained-body fading'

$reviewSettings = [Activator]::CreateInstance($settingsType)
$reviewSettings.EnsureBuiltins()
$reviewSettings.Rule('condition:AdditionalZero',0,$false) | Out-Null
$reviewSettings.Rule('condition:ConfiguredCustom',123,$false) | Out-Null
$reviewSettings.Rule('condition:AcidRain',4000,$true) | Out-Null
$reviewSettings.Rule('condition:ToxicRain',4000,$true) | Out-Null
$conditionType = $assembly.GetTypes() | Where-Object Name -eq 'GameConditionDef' | Select-Object -First 1
$conditionDatabase = $assembly.GetTypes() | Where-Object { $_.FullName -eq 'Verse.DefDatabase`1' } | Select-Object -First 1
$conditionDatabase = $conditionDatabase.MakeGenericType(@($conditionType))
$addCondition = $conditionDatabase.GetMethods($flags) | Where-Object { $_.Name -eq 'Add' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType -eq $conditionType }
foreach ($condition in @(@('AdditionalZero','additional zero'), @('ConfiguredCustom','configured custom'))) {
    $definition = [Activator]::CreateInstance($conditionType)
    $definition.defName = $condition[0]
    $definition.label = $condition[1]
    $addCondition.Invoke($null,@($definition)) | Out-Null
}
$reviewSources = $catalog.Invoke($null,@($reviewSettings,$true,$true))
if ($reviewSources | Where-Object { $_.Rule.Key -in @('condition:AcidRain','condition:ToxicRain') }) { throw 'Absent modded rain condition was exposed' }
$visibleMethod = $editorType.GetMethod('SourceVisible',$flags)
$animalSource = $reviewSources | Where-Object { $_.Rule.Key -eq 'remains:animal' }
$animalSource.Rule.Enabled = $false
if (!$visibleMethod.Invoke($null,@($animalSource.PSObject.BaseObject,$false,'',$null))) { throw 'Disabled animal source disappears' }
$zeroExtra = $reviewSources | Where-Object { $_.Rule.Key -eq 'condition:AdditionalZero' }
$configuredExtra = $reviewSources | Where-Object { $_.Rule.Key -eq 'condition:ConfiguredCustom' }
if ($visibleMethod.Invoke($null,@($zeroExtra.PSObject.BaseObject,$false,'',$null))) { throw 'Unconfigured additional condition clutters primary sources' }
if (!$visibleMethod.Invoke($null,@($zeroExtra.PSObject.BaseObject,$true,'',$null)) -or !$visibleMethod.Invoke($null,@($zeroExtra.PSObject.BaseObject,$false,'Additional',$null))) { throw 'Additional conditions cannot be found' }
if (!$visibleMethod.Invoke($null,@($configuredExtra.PSObject.BaseObject,$false,'',$null))) { throw 'Configured custom source disappears when off' }
if ($animalSource.Group -ne 'Bodies' -or ($reviewSources | Where-Object { $_.Rule.Key -eq 'organs:local' }).Group -ne 'Organs') { throw 'Remains scopes are not separate' }
'PASS source discovery: disabled sources stay visible; extra conditions remain searchable'

$acidDefinition = [Activator]::CreateInstance($conditionType)
$acidDefinition.defName = 'AcidRain'; $acidDefinition.label = 'acid rain'
$addCondition.Invoke($null,@($acidDefinition)) | Out-Null
$loadedConditionSettings = [Activator]::CreateInstance($settingsType)
$loadedConditionSettings.DiscoverRules()
$loadedConditionSources = $catalog.Invoke($null,@($loadedConditionSettings,$true,$true))
$acidSource = $loadedConditionSources | Where-Object { $_.Rule.Key -eq 'condition:AcidRain' }
if (!$acidSource -or !$acidSource.Installed -or $acidSource.Rule.BaseAmount -ne 4000) { throw 'An installed modded rain condition was lost' }
'PASS modded rain controls appear only when their condition definition exists'

foreach ($device in @('PsychicDroner','ToxicSpewer')) {
    if ($reviewSources | Where-Object { $_.Rule.Key -eq "condition:$device" }) { throw "Missing condition listed: $device" }
    $definition = [Activator]::CreateInstance($conditionType)
    $definition.defName = $device
    $definition.label = $device
    $addCondition.Invoke($null,@($definition)) | Out-Null
    if ($rateMethod.Invoke($null,@($device.PSObject.BaseObject,$loadedConditionSettings,5)) -ne 1000) { throw "Incorrect device rate: $device" }
}
if ($rateMethod.Invoke($null,@('PsychicDrone',$loadedConditionSettings,5)) -ne 0) { throw 'Ordinary psychic drones gained deterrence' }
$deviceSources = $catalog.Invoke($null,@($loadedConditionSettings,$true,$true)) | Where-Object { $_.Rule.Key -in @('condition:PsychicDroner','condition:ToxicSpewer') }
if ($deviceSources.Count -ne 2 -or ($deviceSources | Where-Object Extra)) { throw 'Royalty devices missing from main Hazards list' }
if ($loadedConditionSettings.Rule('bump:animal').BaseAmount -ne 3 -or $loadedConditionSettings.Rule('remains:animal').BaseAmount -ne 1) { throw 'Animal reputation and body defaults mixed up' }
'PASS regional device discovery and gameplay rates, ordinary psychic drone exclusion, and animal defaults'

$exampleMethod = $editorType.GetMethod('ExampleAmount',$flags)
$organSource = $reviewSources | Where-Object { $_.Rule.Key -eq 'organs:local' }
$humanBody = $reviewSources | Where-Object { $_.Rule.Key -eq 'remains:human' }
$humanAct = $reviewSources | Where-Object { $_.Rule.Key -eq 'bump:human' }
$hiveSource = $reviewSources | Where-Object { $_.Rule.Key -eq 'infestation:hives' }
if ($exampleMethod.Invoke($null,@($organSource.PSObject.BaseObject,$reviewSettings,5)) -ne 750 -or $exampleMethod.Invoke($null,@($humanBody.PSObject.BaseObject,$reviewSettings,5)) -ne 500 -or $exampleMethod.Invoke($null,@($humanAct.PSObject.BaseObject,$reviewSettings,5)) -ne 2000) { throw 'Comparison examples do not match stock/body/gear values' }
$hiveExample=$exampleMethod.Invoke($null,@($hiveSource.PSObject.BaseObject,$reviewSettings,5))
if ([Math]::Abs($hiveExample-20000*[Math]::Log(1.1)) -gt 0.00001) { throw 'Hazard example shows starting rate rather than one day of real growth' }
$reviewSettings.Corpses=$false
if ($exampleMethod.Invoke($null,@($humanBody.PSObject.BaseObject,$reviewSettings,5)) -ne 0 -or $exampleMethod.Invoke($null,@($organSource.PSObject.BaseObject,$reviewSettings,5)) -ne 750) { throw 'Body switch incorrectly controls kept organs in examples' }
$reviewSettings.Reputation=$false
if ($exampleMethod.Invoke($null,@($humanAct.PSObject.BaseObject,$reviewSettings,5)) -ne 0) { throw 'Comparison ignores Reputation scope' }
$reviewSettings.Conditions=$false
if ($exampleMethod.Invoke($null,@($hiveSource.PSObject.BaseObject,$reviewSettings,5)) -ne 0) { throw 'Comparison ignores Hazard scope' }
'PASS comparison uses real lifecycle calculations and independent category switches'

$previewRuleMethod=$editorType.GetMethod('PreviewRule',$flags)
$humanBody.Rule.BaseAmount=4.37; $humanBody.Rule.PopulationExponent=[float](1.0/3); $humanBody.Rule.ReferencePopulation=7
$disabledPreview=$previewRuleMethod.Invoke($null,@($humanBody.PSObject.BaseObject,$reviewSettings))
if ($disabledPreview.Enabled -or !$humanBody.Rule.Enabled -or [Math]::Abs($humanBody.Rule.BaseAmount-4.37) -gt 0.00001) { throw 'Preview mutated the saved source or ignored its category switch' }
$reviewSettings.Corpses=$true; $reviewSettings.Enabled=$false; $reviewSettings.IncludeHumanGear=$true; $reviewSettings.Reputation=$true
$humanAct.Rule.BaseAmount=0
if ($exampleMethod.Invoke($null,@($humanAct.PSObject.BaseObject,$reviewSettings,5)) -ne 1500) { throw 'Gear-only death gain is incorrectly treated as off' }
$configuredPreview=$previewRuleMethod.Invoke($null,@($humanBody.PSObject.BaseObject,$reviewSettings))
if (!$configuredPreview.Enabled -or $configuredPreview.PopulationExponent -ne $humanBody.Rule.PopulationExponent -or $configuredPreview.ReferencePopulation -ne 7) { throw 'Preview changes custom colony scaling or obeys the Formula subtraction switch' }
'PASS previews preserve custom values, respect scope, and retain gear-only death gains'

$viewMethod=$graphType.GetMethod('LaboratoryView')
foreach ($mode in @(0,1,2)) {
    $viewArgs=@($settings,[int]$mode,[int]5,[float]6,[float]2000,[float]3,[int]3,[float]0)
    $plot=$viewMethod.Invoke($null,$viewArgs)
    $expectedCount=@(3,2,4)[$mode]
    if ($plot.Count -ne $expectedCount -or $viewArgs[7] -le 0) { throw 'Incorrect plot mode or time span' }
    foreach ($series in $plot) {
        $previous=-1.0
        foreach ($point in $series.curve.Points) {
            if ([double]::IsNaN($point.y) -or $point.y -lt 0 -or $point.x -lt $previous) { throw 'Invalid reviewed plot' }
            $previous=$point.x
        }
    }
    if ($mode -eq 0) {
        for ($i=0;$i -lt $plot[0].curve.Points.Count;$i++) {
            if ([Math]::Abs($plot[2].curve.Points[$i].y-$plot[0].curve.Points[$i].y-$plot[1].curve.Points[$i].y) -gt 0.01) { throw 'Hazard-only total includes Reputation' }
        }
    }
    if ($mode -eq 1 -and ($plot[0].curve.Points[0].y -ne 2000 -or $plot[1].curve.Points[0].y -ne 2000)) { throw 'Repeated-acts plot does not share the same first gain' }
}
'PASS hazard-only, repeated-act, and combined native plot series'

$overrideType = $modAssembly.GetType('Antiwealth.FactionOverride')
$override = [Activator]::CreateInstance($overrideType)
$override.FactionKey = 'TestRaiders'
$globalSettings = [Activator]::CreateInstance($settingsType)
$globalSettings.Sanitize()
if ($globalSettings.FactionOverridesEnabled -or $globalSettings.FactionOverrides.Count -ne 0) { throw 'Faction overrides must be opt-in' }
if ($globalSettings.ShowAdvancedColonyScaling -or [Math]::Abs($globalSettings.IslandMultiplier - 0.2) -gt 0.00001) { throw 'Incorrect island multiplier or advanced UI default' }
if ($globalSettings.IslandMaxTiles -ne 180 -or !$globalSettings.IslandRequiresNoOtherSettlements) { throw 'Island qualification defaults are incorrect' }
$capture = $overrideType.GetMethod('Capture',$flags)
$resolve = $overrideType.GetMethod('Resolve',$flags)
$edit = $overrideType.GetMethod('StoreEdits',$flags)
$baseline = $capture.Invoke($null,@($globalSettings))
if ($baseline.ContainsKey('ShowAdvancedColonyScaling')) { throw 'Display preference was made into a faction override' }
$draft = $resolve.Invoke($override,@($globalSettings))
$draft.Enabled = $false
$draft.Rule('remains:animal',1,$true).BaseAmount = 2.5
$draft.GrowthShape = 0.75
$draft.IslandMultiplier = 0.5
$draft.IslandMaxTiles = 22
$after = $capture.Invoke($null,@($draft))
if (!$edit.Invoke($override,@($baseline,$baseline,$after)) -or $override.Count -ne 5) { throw 'Overrides were not recorded per edited field' }
$globalSettings.Rule('remains:animal',1,$true).ReferencePopulation = 9
$globalSettings.Rule('remains:animal',1,$true).BaseAmount = 7
$globalSettings.BiomeExponent = 2.5
$resolved = $resolve.Invoke($override,@($globalSettings))
$animal = $resolved.Rule('remains:animal',1,$true)
if ($resolved.Enabled -or $animal.BaseAmount -ne 2.5 -or $animal.ReferencePopulation -ne 9 -or $resolved.GrowthShape -ne 0.75 -or $resolved.BiomeExponent -ne 2.5 -or $resolved.IslandMultiplier -ne 0.5 -or $resolved.IslandMaxTiles -ne 22 -or [Math]::Abs($globalSettings.IslandMultiplier - 0.2) -gt 0.00001) { throw 'Faction resolution blocked inheritance or lost a local edit' }
if (!$globalSettings.Enabled -or $globalSettings.Rule('remains:animal',1,$true).BaseAmount -ne 7) { throw 'Editing a faction changed Global' }
$currentGlobal = $capture.Invoke($null,@($globalSettings))
$before = $capture.Invoke($null,@($resolved))
$resolved.Rule('remains:animal',1,$true).BaseAmount = 7
$edit.Invoke($override,@($currentGlobal,$before,$capture.Invoke($null,@($resolved)))) | Out-Null
if ($override.Count -ne 4) { throw 'Setting a field back to Global did not clear the override' }
$globalSettings.FactionOverrides.Add($override)
$presetType = $modAssembly.GetType('Antiwealth.SettingsPreset')
$preset = [Activator]::CreateInstance($presetType)
$preset.Name = 'Island setup'
$presetType.GetMethod('SaveFrom',$flags).Invoke($preset,@($globalSettings)) | Out-Null
$globalSettings.Presets.Add($preset)
$globalSettings.IslandMaxTiles = 17
$globalSettings.Rule('remains:animal',1,$true).BaseAmount = 11
$settingsType.GetMethod('ResetFaction',$flags).Invoke($globalSettings,@('TestRaiders')) | Out-Null
if ($globalSettings.FactionOverrides.Count -ne 0) { throw 'Reset left faction overrides behind' }
$presetType.GetMethod('LoadInto',$flags).Invoke($preset,@($globalSettings)) | Out-Null
if ($globalSettings.Presets.Count -ne 1 -or $globalSettings.IslandMaxTiles -ne 180 -or $globalSettings.Rule('remains:animal',1,$true).BaseAmount -ne 7 -or $globalSettings.FactionOverrides.Count -ne 1) { throw 'Preset failed to restore an isolated settings snapshot' }
$loadedProfile = $resolve.Invoke($globalSettings.FactionOverrides[0].PSObject.BaseObject,@($globalSettings))
if ($loadedProfile.IslandMaxTiles -ne 22) { throw 'Preset lost the faction island limit' }
'PASS named presets preserve the list and restore Global values and faction overrides'
'PASS field-level inheritance, faction isolation, and reset to Global'

$presetFolder = Join-Path ([IO.Path]::GetTempPath()) ('deterrence-presets-' + [Guid]::NewGuid().ToString('N'))
$presetFilesType = $modAssembly.GetType('Antiwealth.PresetFiles')
$presetFiles = $presetFilesType.GetConstructors($flags)[0].Invoke(@($presetFolder.PSObject.BaseObject))
$readPresets = $presetFilesType.GetMethod('ReadAll',$flags)
$importPreset = $presetFilesType.GetMethod('ImportLegacy',$flags)
$savePreset = $presetFilesType.GetMethod('Save',$flags)
$deletePreset = $presetFilesType.GetMethod('Delete',$flags)
$builtIn = $presetType.GetMethod('CreateDefaults',$flags).Invoke($null,@())
$resetSettings = [Activator]::CreateInstance($settingsType)
$resetSettings.EnsureBuiltins()
$resetSettings.SmoothedSurfaceMultiplier = 0.7
$resetSettings.IslandMultiplier = 0.8
$resetSettings.IslandMaxTiles = 9
$resetSettings.Rule('bump:animal').BaseAmount = 99
$resetSettings.FactionOverrides.Add($override)
$presetType.GetMethod('LoadInto',$flags).Invoke($builtIn,@($resetSettings)) | Out-Null
if ($resetSettings.SmoothedSurfaceMultiplier -ne 0 -or [Math]::Abs($resetSettings.IslandMultiplier - 0.2) -gt 0.00001 -or $resetSettings.IslandMaxTiles -ne 180) { throw 'Default preset lost terrain or smoothed surface defaults' }
if ($resetSettings.Rule('bump:animal').BaseAmount -ne 3 -or $resetSettings.FactionOverrides.Count -ne 0) { throw 'Default preset failed to restore settings' }
$resetSettings.Rule('bump:animal').BaseAmount = 12
$presetType.GetMethod('LoadInto',$flags).Invoke($builtIn,@($resetSettings)) | Out-Null
if ($resetSettings.Rule('bump:animal').BaseAmount -ne 3) { throw 'Active edits changed the built-in snapshot' }
foreach ($operation in @($savePreset,$deletePreset)) {
    $operationArgs = if ($operation.Name -eq 'Save') { @($preset,$builtIn) } else { @($builtIn) }
    try {
        $operation.Invoke($presetFiles,[object[]]$operationArgs) | Out-Null
        throw 'A file operation accepted the built-in preset'
    } catch {
        if ($_.Exception.GetBaseException() -isnot [IO.IOException]) { throw }
    }
}
'PASS built-in preset restores independent defaults and rejects replacement and deletion'
$fileErrors = [Collections.Generic.List[string]]::new()
$readArgs = [object[]]::new(1)
$readArgs[0] = $fileErrors
try {
    $importPreset.Invoke($presetFiles,@($preset)) | Out-Null
    $importPreset.Invoke($presetFiles,@($preset)) | Out-Null
    $savedPresets = $readPresets.Invoke($presetFiles,$readArgs)
    if ($savedPresets.Count -ne 1 -or $fileErrors.Count -ne 0) { throw 'Legacy import duplicated or lost a preset' }
    $existingPreset = $savedPresets[0].PSObject.BaseObject
    $presetType.GetMethod('LoadInto',$flags).Invoke($existingPreset,@($globalSettings)) | Out-Null
    if ($globalSettings.Rule('remains:animal',1,$true).BaseAmount -ne 7 -or $globalSettings.FactionOverrides[0].Count -ne 4) {
        throw 'Legacy XML import lost Global values or faction overrides'
    }

    $manualPath = Join-Path $presetFolder 'manual.xml'
    $manualXml = @'
<DeterrencePreset>
  <Name>Hand edited</Name>
  <Description>Island colony</Description>
  <Global>
    <FactionOverridesEnabled>true</FactionOverridesEnabled>
    <IslandMultiplier>0.25</IslandMultiplier>
    <GrowthByRate>true</GrowthByRate><SlowdownDays>14</SlowdownDays><FadeByHistory>true</FadeByHistory><FadeReferenceArea>30000</FadeReferenceArea>
    <Source key="remains:animal" amount="2.5" />
  </Global>
  <Factions>
    <Faction key="TestRaiders"><Curve>SquareRoot</Curve><RecoveryCurve>Linear</RecoveryCurve></Faction>
  </Factions>
</DeterrencePreset>
'@
    [IO.File]::WriteAllText($manualPath,$manualXml)
    [IO.File]::WriteAllText((Join-Path $presetFolder 'invalid.xml'),'<bad />')
    $savedPresets = $readPresets.Invoke($presetFiles,$readArgs)
    if ($savedPresets.Count -ne 2 -or $fileErrors.Count -ne 1) { throw 'Invalid preset obscured readable files' }
    $manual = $savedPresets[0].PSObject.BaseObject
    $presetType.GetMethod('LoadInto',$flags).Invoke($manual,@($globalSettings)) | Out-Null
    $manualProfile = $resolve.Invoke($globalSettings.FactionOverrides[0].PSObject.BaseObject,@($globalSettings))
    if ($manual.Name -ne 'Hand edited' -or $manual.Description -ne 'Island colony' -or !$globalSettings.FactionOverridesEnabled -or
        $globalSettings.IslandMultiplier -ne 0.25 -or $manualProfile.GrowthShape -ne 0.5 -or
        !$manualProfile.GrowthByRate -or $manualProfile.SlowdownDays -ne 14 -or !$manualProfile.FadeByHistory -or $manualProfile.FadeReferenceArea -ne 30000 -or
        $globalSettings.Rule('remains:animal',1,$true).BaseAmount -ne 2.5) { throw 'Could not load sparse hand-edited preset' }

    $manual.Name = 'Renamed'
    $savePreset.Invoke($presetFiles,@($manual,$manual)) | Out-Null
    if (([xml][IO.File]::ReadAllText($manualPath)).DeterrencePreset.Name -ne 'Renamed') { throw 'XML replace failed' }
    [IO.File]::WriteAllText($manualPath,$manualXml)
    try {
        $deletePreset.Invoke($presetFiles,@($manual)) | Out-Null
        throw 'Deleting a stale selection removed a hand-edited file'
    } catch {
        if ($_.Exception.InnerException -isnot [IO.IOException]) { throw }
    }
    if ([IO.File]::ReadAllText($manualPath) -cne $manualXml) { throw 'Stale deletion modified the file' }
} finally {
    if ([IO.Directory]::Exists($presetFolder)) {
        foreach ($path in [IO.Directory]::GetFiles($presetFolder)) { [IO.File]::Delete($path) }
        [IO.Directory]::Delete($presetFolder)
    }
}
'PASS editable XML presets: migration, sparse files, malformed-file isolation, and stale edit protection'

$editor = [Activator]::CreateInstance($editorType)
$editorType.GetField('previewPopulation',$flags).SetValue($editor,[int]12)
$exampleColonists = $editorType.GetProperty('ExampleColonists',$flags)
if ($exampleColonists.GetValue($editor) -ne 5) { throw 'Hidden preview uses a previously selected population' }
$editorType.GetField('showColonyControls',$flags).SetValue($editor,$true)
if ($exampleColonists.GetValue($editor) -ne 12) { throw 'Advanced preview failed to restore the editable population' }
'PASS basic examples use the default population and keep the advanced preview value'

$branchMethod = $ledgerType.GetMethod('Branch',$flags)
$branch = $branchMethod.Invoke($bodyComponent,@('TestProfile',[double]11))
$branchEntries = $ledgerType.GetField('entries',$flags).GetValue($branch)
$branchKept = $branchEntries | Where-Object Key -eq 'remains:700'
$branchMissing = $branchEntries | Where-Object Key -eq 'remains:701'
if ($branch.ProfileId -ne 'TestProfile' -or $branchKept.LastSeenDay -ne 11 -or $branchKept.Balance -ne 500 -or $branchMissing.EndDay -ne 10) { throw 'Branch lost its starting balance, sample time, or saved fade' }
$branchKept.Balance = 123
if ($keptBody.Balance -ne 500) { throw 'Faction and Global share a mutable ledger entry' }
'PASS faction balances branch independently and retain existing fades'

$raidPointsType = $modAssembly.GetType('Antiwealth.FactionRaidPoints')
$parmsType = $assembly.GetType('RimWorld.IncidentParms')
$raidWorker = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($assembly.GetType('RimWorld.IncidentWorker_RaidEnemy'))
$map = [Runtime.CompilerServices.RuntimeHelpers]::GetUninitializedObject($assembly.GetType('Verse.Map'))
$assembly.GetType('Verse.Map').GetField('uniqueID',$flags).SetValue($map,[int]42)
$parms = [Activator]::CreateInstance($parmsType)
$parms.target = $map
$parms.points = 1000
$modSettingsField = $modAssembly.GetType('Antiwealth.AntiwealthMod').GetField('Settings',$flags)
$originalSettings = $modSettingsField.GetValue($null)
$begin = $raidPointsType.GetMethod('BeginRaid',$flags)
$end = $raidPointsType.GetMethod('EndRaid',$flags)
$remember = $raidPointsType.GetMethod('Remember',$flags)
try {
    $globalSettings.FactionOverridesEnabled = $true
    $modSettingsField.SetValue($null,$globalSettings)
    if ($null -ne $begin.Invoke($null,@($raidWorker,$parms))) { throw 'Explicit untracked points were allowed' }
    $remember.Invoke($null,@($parms,[float]1000,[int]42)) | Out-Null
    $parms.points = 700
    if ($null -ne $begin.Invoke($null,@($raidWorker,$parms))) { throw 'A replaced budget was treated as a storyteller budget' }
    $raidPointsType.GetMethod('TrackRandomMainMultiplier',$flags).Invoke($null,@($parms)) | Out-Null
    $copy = $parms.ShallowCopy()
    $raidPointsType.GetMethod('CopyBudget',$flags).Invoke($null,@($parms,$copy)) | Out-Null
    $copy.questTag = 'ScriptedRaid'
    if ($null -ne $begin.Invoke($null,@($raidWorker,$copy))) { throw 'Quest budget was eligible for faction sizing' }
    $copy.questTag = $null
    $frame = $begin.Invoke($null,@($raidWorker,$copy))
    if ($null -eq $frame) { throw 'Recognized storyteller budget did not reach faction sizing' }
    $frameType = $frame.GetType()
    $frameType.GetField('Applied',$flags).SetValue($frame,$true)
    $copy.points = 1400
    $end.Invoke($null,@($frame,$false)) | Out-Null
    if ($copy.points -ne 700) { throw 'Failed raid attempt did not restore original points' }
    $globalSettings.FactionOverridesEnabled = $false
    if ($null -ne $begin.Invoke($null,@($raidWorker,$copy))) { throw 'Turning faction overrides off did not bypass sizing' }
} finally {
    $modSettingsField.SetValue($null,$originalSettings)
}
'PASS raid budget provenance, Randy/copy tracking, quest bypass, and failed-attempt rollback'


