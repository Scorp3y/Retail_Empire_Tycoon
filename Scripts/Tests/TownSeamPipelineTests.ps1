$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$seamTestProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$seamTestTokens=$null
$seamTestErrors=$null
$seamTestAst=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $seamTestProject 'Scripts/BuildTownSeamRepairs.ps1'),[ref]$seamTestTokens,[ref]$seamTestErrors)
if($seamTestErrors.Count -ne 0){throw 'Stage 6 pipeline syntax failed.'}
$seamTestNames=@('Test-SeamOutput','Test-SeamAuthoring','Test-SeamNewMetadata','Resolve-SeamRelative','Assert-SeamPass','Read-SeamJson','Assert-SeamCaptureEvidence')
foreach($function in $seamTestAst.FindAll({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst]},$true)) {
    if($seamTestNames -contains $function.Name){. ([scriptblock]::Create($function.Extent.Text))}
}
$seamPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$seamAssetRoot='Assets/Art/TownSeamRepairs'
$seamQa=Join-Path $seamTestProject 'Docs/TownSpecialReview'
$seamTestCount=0
function Assert-SeamContract([bool]$condition,[string]$message){if(-not $condition){throw $message};$script:seamTestCount++}
foreach($path in @($seamPrefab,'Assets/Art/TownSeamRepairs.meta','Assets/Art/TownSeamRepairs/Meshes/Workshop.asset')) {
    Assert-SeamContract (Test-SeamOutput $path) ('Missing permitted output '+$path)
}
foreach($path in @('Assets/Art/TownCourtyards/Meshes/NorthCentre.asset','Assets/Prefabs/TownWorld/TownPalette.mat','Assets/Project/Scenes/City.unity','Assets/Art/TownSeamRepairsExtra/Meshes/test.asset')) {
    Assert-SeamContract (-not (Test-SeamOutput $path)) ('Protected output accepted '+$path)
}
foreach($path in @('Assets/Project/Editor/TownSeamPlan.cs','Assets/Project/Editor/TownSeamValidation.cs.meta','Assets/Project/Editor/TownPilotReview.cs','Assets/Project/Editor/TownPilotScopePreservation.cs')) {
    Assert-SeamContract (Test-SeamAuthoring $path) ('Missing authoring source '+$path)
}
foreach($path in @('Assets/Project/Editor/TownCourtyardValidation.cs','Assets/Project/Editor/TownResidentialPlan.cs','Assets/Project/Editor/TownKit/TownDetailModels.cs','Assets/Project/Scripts/CityTrip.cs')) {
    Assert-SeamContract (-not (Test-SeamAuthoring $path)) ('Previous stage/runtime source accepted '+$path)
}
Assert-SeamContract (Test-SeamNewMetadata 'Assets/Project/Editor/TownSeamPlan.cs.meta') 'New metadata rejected.'
Assert-SeamContract (-not (Test-SeamNewMetadata 'Assets/Project/Editor/TownCourtyardPlan.cs.meta')) 'Old stage metadata accepted.'
Assert-SeamContract ((Resolve-SeamRelative $seamTestProject 'Assets/Project/Editor/TownSeamPlan.cs') -eq (Join-Path $seamTestProject 'Assets/Project/Editor/TownSeamPlan.cs')) 'Canonical path failed.'
foreach($path in @('../outside.txt','Assets/../Packages/manifest.json','C:/outside.txt','Assets\Project\test.cs')) {
    $rejected=$false
    try{Resolve-SeamRelative $seamTestProject $path | Out-Null}catch{$rejected=$true}
    Assert-SeamContract $rejected ('Non-canonical path accepted '+$path)
}
# Real archived native capture proves PNG signature, dimensions, complete manifests and exact file set.
Assert-SeamCaptureEvidence 'Before'
$seamTestCount++
Assert-SeamPass (Join-Path $seamQa 'gameplay.txt')
$seamTestCount++
'STAGE 6 PIPELINE CONTRACTS PASSED: '+$seamTestCount
