param([ValidateSet('Inspect','Generate','Review','Play','Apply')][string]$Mode='Inspect')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$equipmentProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$equipmentClone=Join-Path $equipmentProject 'Library/CityUpdateQA/IsolatedProject'
$equipmentPipeline=Join-Path $equipmentProject 'Library/TownEquipmentPipeline'
$equipmentPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$equipmentUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$equipmentAssets='Assets/Art/TownEquipment'
$equipmentEvidence='Docs/TownEquipmentReview'
New-Item -ItemType Directory -Path $equipmentPipeline -Force | Out-Null
function Get-EquipmentHash([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Copy-EquipmentFile([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function Get-EquipmentInputs {
    $inputs=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $equipmentProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($equipmentProject,$file.FullName).Replace('\','/')
            if($relative -eq ($equipmentAssets+'.meta') -or $relative.StartsWith($equipmentAssets+'/')){continue}
            $inputs[$relative]=Get-EquipmentHash $file.FullName
        }
    }
    return $inputs
}
if(Test-Path -LiteralPath (Join-Path $equipmentClone 'Temp/UnityLockfile')){throw 'The isolated Unity editor is busy; do not interrupt it.'}
if($Mode -in @('Inspect','Generate')){
    $equipmentBaseline=Get-EquipmentInputs
    [IO.File]::WriteAllText((Join-Path $equipmentPipeline 'baseline.json'),($equipmentBaseline|ConvertTo-Json -Depth 5))
    foreach($relative in $equipmentBaseline.Keys){
        $source=Join-Path $equipmentProject $relative;$destination=Join-Path $equipmentClone $relative
        if(-not(Test-Path -LiteralPath $destination) -or (Get-EquipmentHash $destination) -ne $equipmentBaseline[$relative]){Copy-EquipmentFile $source $destination}
    }
    if($Mode -eq 'Generate' -and (Test-Path -LiteralPath (Join-Path $equipmentProject $equipmentAssets))){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $equipmentProject $equipmentAssets) -File -Recurse){
            Copy-EquipmentFile $file.FullName (Join-Path $equipmentClone ([IO.Path]::GetRelativePath($equipmentProject,$file.FullName)))
        }
    }
    Copy-EquipmentFile (Join-Path $equipmentProject $equipmentPrefab) (Join-Path $equipmentPipeline 'OriginalTown.prefab')
    $equipmentMethod='TownEquipmentBatch.'+$Mode
} elseif($Mode -eq 'Play' -or $Mode -eq 'Review'){
    $equipmentMethod=if($Mode -eq 'Play'){'TownEquipmentPlayValidation.Run'}else{'TownEquipmentBatch.Review'}
} else {
    $equipmentQa=Join-Path $equipmentClone 'Library/TownEquipmentQA'
    if(-not(Get-Content -LiteralPath (Join-Path $equipmentQa 'result.txt') -Raw).StartsWith('EQUIPMENT PASSED')){throw 'Native equipment authoring has not passed.'}
    if(-not(Get-Content -LiteralPath (Join-Path $equipmentQa 'play.txt') -Raw).StartsWith('PASS:')){throw 'Actual pickup test has not passed.'}
    if(-not(Get-Content -LiteralPath (Join-Path $equipmentQa 'review.txt') -Raw).StartsWith('PASS:')){throw 'Fresh saved-scene visual review has not passed.'}
    $equipmentHash=Get-EquipmentHash (Join-Path $equipmentClone $equipmentPrefab)
    foreach($receipt in @('play-input.sha256','review-input.sha256')){
        if((Get-Content -LiteralPath (Join-Path $equipmentQa $receipt) -Raw).Trim() -ne $equipmentHash){throw "Stale acceptance receipt: $receipt"}
    }
    $equipmentBaseline=Get-Content -LiteralPath (Join-Path $equipmentPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $equipmentCurrent=Get-EquipmentInputs
    foreach($relative in $equipmentBaseline.Keys){if(-not $equipmentCurrent.ContainsKey($relative) -or $equipmentCurrent[$relative] -ne $equipmentBaseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $equipmentCurrent.Keys){if(-not $equipmentBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $equipmentBackup=Join-Path $equipmentPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    Copy-EquipmentFile (Join-Path $equipmentProject $equipmentPrefab) (Join-Path $equipmentBackup $equipmentPrefab)
    foreach($scope in @($equipmentAssets,$equipmentEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $equipmentClone $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($equipmentClone,$file.FullName);$target=Join-Path $equipmentProject $relative
            if(Test-Path -LiteralPath $target){Copy-EquipmentFile $target (Join-Path $equipmentBackup $relative)}
            Copy-EquipmentFile $file.FullName $target
        }
    }
    Copy-EquipmentFile (Join-Path $equipmentClone ($equipmentAssets+'.meta')) (Join-Path $equipmentProject ($equipmentAssets+'.meta'))
    foreach($file in @('TownEquipmentBatch.cs.meta','TownEquipmentPlacement.cs.meta','TownEquipmentPlayValidation.cs.meta','TownKit/TownEquipmentModels.cs.meta')){
        Copy-EquipmentFile (Join-Path $equipmentClone ('Assets/Project/Editor/'+$file)) (Join-Path $equipmentProject ('Assets/Project/Editor/'+$file))
    }
    Copy-EquipmentFile (Join-Path $equipmentClone $equipmentPrefab) (Join-Path $equipmentProject $equipmentPrefab)
    foreach($file in @('result.txt','protection.txt','placement.txt','access.txt','play.txt','review.txt')){
        if(-not(Test-Path -LiteralPath (Join-Path $equipmentQa $file))){throw "Missing acceptance report: $file"}
        Copy-EquipmentFile (Join-Path $equipmentQa $file) (Join-Path $equipmentProject ($equipmentEvidence+'/'+$file))
    }
    [IO.File]::WriteAllText((Join-Path $equipmentPipeline 'applied.json'),(@{backup=$equipmentBackup;prefab=$equipmentHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "EQUIPMENT APPLIED. Recoverable backup: $equipmentBackup";return
}
$equipmentLog=Join-Path $equipmentPipeline ($Mode+'.log')
$equipmentProcess=Start-Process -FilePath $equipmentUnity -ArgumentList @('-batchmode','-projectPath',('"'+$equipmentClone+'"'),'-executeMethod',$equipmentMethod,'-logFile',('"'+$equipmentLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Equipment $Mode launched. PID $($equipmentProcess.Id); log $equipmentLog"
