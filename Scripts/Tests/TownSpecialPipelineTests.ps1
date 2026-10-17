$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$specialTestProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$specialTestTokens=$null
$specialTestErrors=$null
$specialTestAst=[System.Management.Automation.Language.Parser]::ParseFile((Join-Path $specialTestProject 'Scripts/BuildTownSpecialSites.ps1'),[ref]$specialTestTokens,[ref]$specialTestErrors)
if($specialTestErrors.Count -ne 0){throw 'Stage 5 pipeline syntax failed.'}
$specialTestNames=@('Test-SpecialOutput','Test-SpecialAuthoring','Test-SpecialNewMetadata','Resolve-SpecialRelative','Assert-SpecialPass','Read-SpecialJson','Assert-SpecialCaptureEvidence')
foreach($function in $specialTestAst.FindAll({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst]},$true)) {
    if($specialTestNames -contains $function.Name){. ([scriptblock]::Create($function.Extent.Text))}
}
$specialPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$specialAssetRoot='Assets/Art/TownSpecialSites'
$specialQa=Join-Path $specialTestProject 'Docs/TownCourtyardReview'
$specialTestCount=0
function Assert-SpecialContract([bool]$condition,[string]$message){if(-not $condition){throw $message};$script:specialTestCount++}
foreach($path in @($specialPrefab,'Assets/Art/TownSpecialSites.meta','Assets/Art/TownSpecialSites/Meshes/Workshop.asset')) {
    Assert-SpecialContract (Test-SpecialOutput $path) ('Missing permitted output '+$path)
}
foreach($path in @('Assets/Art/TownCourtyards/Meshes/NorthCentre.asset','Assets/Prefabs/TownWorld/TownPalette.mat','Assets/Project/Scenes/City.unity','Assets/Art/TownSpecialSitesExtra/Meshes/test.asset')) {
    Assert-SpecialContract (-not (Test-SpecialOutput $path)) ('Protected output accepted '+$path)
}
foreach($path in @('Assets/Project/Editor/TownSpecialPlan.cs','Assets/Project/Editor/TownSpecialValidation.cs.meta','Assets/Project/Editor/TownKit/TownSpecialModels.cs','Assets/Project/Editor/TownPilotReview.cs')) {
    Assert-SpecialContract (Test-SpecialAuthoring $path) ('Missing authoring source '+$path)
}
foreach($path in @('Assets/Project/Editor/TownCourtyardValidation.cs','Assets/Project/Editor/TownResidentialPlan.cs','Assets/Project/Editor/TownKit/TownDetailModels.cs','Assets/Project/Scripts/CityTrip.cs')) {
    Assert-SpecialContract (-not (Test-SpecialAuthoring $path)) ('Previous stage/runtime source accepted '+$path)
}
Assert-SpecialContract (Test-SpecialNewMetadata 'Assets/Project/Editor/TownSpecialPlan.cs.meta') 'New metadata rejected.'
Assert-SpecialContract (-not (Test-SpecialNewMetadata 'Assets/Project/Editor/TownCourtyardPlan.cs.meta')) 'Old stage metadata accepted.'
Assert-SpecialContract ((Resolve-SpecialRelative $specialTestProject 'Assets/Project/Editor/TownSpecialPlan.cs') -eq (Join-Path $specialTestProject 'Assets/Project/Editor/TownSpecialPlan.cs')) 'Canonical path failed.'
foreach($path in @('../outside.txt','Assets/../Packages/manifest.json','C:/outside.txt','Assets\Project\test.cs')) {
    $rejected=$false
    try{Resolve-SpecialRelative $specialTestProject $path | Out-Null}catch{$rejected=$true}
    Assert-SpecialContract $rejected ('Non-canonical path accepted '+$path)
}
# Real archived native capture proves PNG signature, dimensions, complete manifests and exact file set.
Assert-SpecialCaptureEvidence 'Before'
$specialTestCount++
Assert-SpecialPass (Join-Path $specialQa 'gameplay.txt')
$specialTestCount++
'STAGE 5 PIPELINE CONTRACTS PASSED: '+$specialTestCount
