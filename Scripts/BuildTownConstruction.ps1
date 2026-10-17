param([ValidateSet('Inspect','Generate','Review','Play','Apply')][string]$Mode='Inspect')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$constructionProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$constructionClone=Join-Path $constructionProject 'Library/CityUpdateQA/IsolatedProject'
$constructionPipeline=Join-Path $constructionProject 'Library/TownConstructionPipeline'
$constructionPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$constructionUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$constructionAssets='Assets/Art/TownConstruction'
$constructionEvidence='Docs/TownConstructionReview'
New-Item -ItemType Directory -Path $constructionPipeline -Force | Out-Null
function Get-ConstructionHash([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Copy-ConstructionFile([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function Get-ConstructionInputs {
    $inputs=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $constructionProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($constructionProject,$file.FullName).Replace('\','/')
            if($relative -eq ($constructionAssets+'.meta') -or $relative.StartsWith($constructionAssets+'/')){continue}
            $inputs[$relative]=Get-ConstructionHash $file.FullName
        }
    }
    return $inputs
}
if(Test-Path -LiteralPath (Join-Path $constructionClone 'Temp/UnityLockfile')){throw 'The isolated Unity editor is busy; do not interrupt it.'}
if($Mode -in @('Inspect','Generate')){
    $constructionBaseline=Get-ConstructionInputs
    [IO.File]::WriteAllText((Join-Path $constructionPipeline 'baseline.json'),($constructionBaseline|ConvertTo-Json -Depth 5))
    foreach($relative in $constructionBaseline.Keys){
        $source=Join-Path $constructionProject $relative;$destination=Join-Path $constructionClone $relative
        if(-not(Test-Path -LiteralPath $destination) -or (Get-ConstructionHash $destination) -ne $constructionBaseline[$relative]){Copy-ConstructionFile $source $destination}
    }
    if($Mode -eq 'Generate' -and (Test-Path -LiteralPath (Join-Path $constructionProject $constructionAssets))){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $constructionProject $constructionAssets) -File -Recurse){
            Copy-ConstructionFile $file.FullName (Join-Path $constructionClone ([IO.Path]::GetRelativePath($constructionProject,$file.FullName)))
        }
    }
    Copy-ConstructionFile (Join-Path $constructionProject $constructionPrefab) (Join-Path $constructionPipeline 'OriginalTown.prefab')
    $constructionMethod='TownConstructionBatch.'+$Mode
} elseif($Mode -eq 'Play' -or $Mode -eq 'Review'){
    $constructionMethod=if($Mode -eq 'Play'){'TownConstructionPlayValidation.Run'}else{'TownConstructionBatch.Review'}
} else {
    $constructionQa=Join-Path $constructionClone 'Library/TownConstructionQA'
    if(-not(Get-Content -LiteralPath (Join-Path $constructionQa 'result.txt') -Raw).StartsWith('CONSTRUCTION PASSED')){throw 'Native construction authoring has not passed.'}
    if(-not(Get-Content -LiteralPath (Join-Path $constructionQa 'play.txt') -Raw).StartsWith('PASS:')){throw 'Actual pickup test has not passed.'}
    if(-not(Get-Content -LiteralPath (Join-Path $constructionQa 'review.txt') -Raw).StartsWith('PASS:')){throw 'Fresh saved-scene visual review has not passed.'}
    $constructionHash=Get-ConstructionHash (Join-Path $constructionClone $constructionPrefab)
    foreach($receipt in @('play-input.sha256','review-input.sha256')){
        if((Get-Content -LiteralPath (Join-Path $constructionQa $receipt) -Raw).Trim() -ne $constructionHash){throw "Stale acceptance receipt: $receipt"}
    }
    $constructionBaseline=Get-Content -LiteralPath (Join-Path $constructionPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $constructionCurrent=Get-ConstructionInputs
    foreach($relative in $constructionBaseline.Keys){if(-not $constructionCurrent.ContainsKey($relative) -or $constructionCurrent[$relative] -ne $constructionBaseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $constructionCurrent.Keys){if(-not $constructionBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $constructionBackup=Join-Path $constructionPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    Copy-ConstructionFile (Join-Path $constructionProject $constructionPrefab) (Join-Path $constructionBackup $constructionPrefab)
    foreach($scope in @($constructionAssets,$constructionEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $constructionClone $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($constructionClone,$file.FullName);$target=Join-Path $constructionProject $relative
            if(Test-Path -LiteralPath $target){Copy-ConstructionFile $target (Join-Path $constructionBackup $relative)}
            Copy-ConstructionFile $file.FullName $target
        }
    }
    Copy-ConstructionFile (Join-Path $constructionClone ($constructionAssets+'.meta')) (Join-Path $constructionProject ($constructionAssets+'.meta'))
    foreach($file in @('TownConstructionBatch.cs.meta','TownConstructionPlacement.cs.meta','TownConstructionPlayValidation.cs.meta','TownKit/TownConstructionModels.cs.meta')){
        Copy-ConstructionFile (Join-Path $constructionClone ('Assets/Project/Editor/'+$file)) (Join-Path $constructionProject ('Assets/Project/Editor/'+$file))
    }
    Copy-ConstructionFile (Join-Path $constructionClone $constructionPrefab) (Join-Path $constructionProject $constructionPrefab)
    foreach($file in @('result.txt','protection.txt','placement.txt','access.txt','play.txt','review.txt')){
        if(-not(Test-Path -LiteralPath (Join-Path $constructionQa $file))){throw "Missing acceptance report: $file"}
        Copy-ConstructionFile (Join-Path $constructionQa $file) (Join-Path $constructionProject ($constructionEvidence+'/'+$file))
    }
    [IO.File]::WriteAllText((Join-Path $constructionPipeline 'applied.json'),(@{backup=$constructionBackup;prefab=$constructionHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "CONSTRUCTION APPLIED. Recoverable backup: $constructionBackup";return
}
$constructionLog=Join-Path $constructionPipeline ($Mode+'.log')
$constructionProcess=Start-Process -FilePath $constructionUnity -ArgumentList @('-batchmode','-projectPath',('"'+$constructionClone+'"'),'-executeMethod',$constructionMethod,'-logFile',('"'+$constructionLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Construction $Mode launched. PID $($constructionProcess.Id); log $constructionLog"
