param([ValidateSet('Inspect','Generate','Revise','Enrich','Arrange','Review','Play','Apply')][string]$Mode='Inspect')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$showroomProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$showroomClone=Join-Path $showroomProject 'Library/CityUpdateQA/IsolatedProject'
$showroomPipeline=Join-Path $showroomProject 'Library/TownShowroomPipeline'
$showroomPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$showroomUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$showroomAssets='Assets/Art/TownShowroom'
$showroomEvidence='Docs/TownShowroomReview'
New-Item -ItemType Directory -Path $showroomPipeline -Force | Out-Null
function Get-ShowroomHash([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Copy-ShowroomFile([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function Get-ShowroomInputs {
    $inputs=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $showroomProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($showroomProject,$file.FullName).Replace('\','/')
            if($relative -eq ($showroomAssets+'.meta') -or $relative.StartsWith($showroomAssets+'/')){continue}
            $inputs[$relative]=Get-ShowroomHash $file.FullName
        }
    }
    return $inputs
}
if(Test-Path -LiteralPath (Join-Path $showroomClone 'Temp/UnityLockfile')){throw 'The isolated Unity editor is busy; do not interrupt it.'}
if($Mode -in @('Inspect','Generate','Revise','Enrich','Arrange')){
    $showroomBaseline=Get-ShowroomInputs
    [IO.File]::WriteAllText((Join-Path $showroomPipeline 'baseline.json'),($showroomBaseline|ConvertTo-Json -Depth 5))
    foreach($relative in $showroomBaseline.Keys){
        $source=Join-Path $showroomProject $relative;$destination=Join-Path $showroomClone $relative
        if(-not(Test-Path -LiteralPath $destination) -or (Get-ShowroomHash $destination) -ne $showroomBaseline[$relative]){Copy-ShowroomFile $source $destination}
    }
    if($Mode -in @('Revise','Enrich','Arrange')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $showroomProject $showroomAssets) -File -Recurse){
            Copy-ShowroomFile $file.FullName (Join-Path $showroomClone ([IO.Path]::GetRelativePath($showroomProject,$file.FullName)))
        }
    }
    Copy-ShowroomFile (Join-Path $showroomProject $showroomPrefab) (Join-Path $showroomPipeline 'OriginalTown.prefab')
    $showroomMethod='TownShowroomBatch.'+$Mode
} elseif($Mode -eq 'Play' -or $Mode -eq 'Review'){
    $showroomMethod=if($Mode -eq 'Play'){'TownShowroomPlayValidation.Run'}else{'TownShowroomBatch.Review'}
} else {
    $showroomQa=Join-Path $showroomClone 'Library/TownShowroomQA'
    if(-not(Get-Content -LiteralPath (Join-Path $showroomQa 'result.txt') -Raw).StartsWith('SHOWROOM PASSED')){throw 'Native showroom authoring has not passed.'}
    if(-not(Get-Content -LiteralPath (Join-Path $showroomQa 'play.txt') -Raw).StartsWith('PASS:')){throw 'Actual pickup test has not passed.'}
    if(-not(Get-Content -LiteralPath (Join-Path $showroomQa 'review.txt') -Raw).StartsWith('PASS:')){throw 'Fresh saved-scene visual review has not passed.'}
    $showroomHash=Get-ShowroomHash (Join-Path $showroomClone $showroomPrefab)
    foreach($receipt in @('play-input.sha256','review-input.sha256')){
        if((Get-Content -LiteralPath (Join-Path $showroomQa $receipt) -Raw).Trim() -ne $showroomHash){throw "Stale acceptance receipt: $receipt"}
    }
    $showroomBaseline=Get-Content -LiteralPath (Join-Path $showroomPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $showroomCurrent=Get-ShowroomInputs
    foreach($relative in $showroomBaseline.Keys){if(-not $showroomCurrent.ContainsKey($relative) -or $showroomCurrent[$relative] -ne $showroomBaseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $showroomCurrent.Keys){if(-not $showroomBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $showroomBackup=Join-Path $showroomPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    Copy-ShowroomFile (Join-Path $showroomProject $showroomPrefab) (Join-Path $showroomBackup $showroomPrefab)
    foreach($scope in @($showroomAssets,$showroomEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $showroomClone $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($showroomClone,$file.FullName);$target=Join-Path $showroomProject $relative
            if(Test-Path -LiteralPath $target){Copy-ShowroomFile $target (Join-Path $showroomBackup $relative)}
            Copy-ShowroomFile $file.FullName $target
        }
    }
    Copy-ShowroomFile (Join-Path $showroomClone ($showroomAssets+'.meta')) (Join-Path $showroomProject ($showroomAssets+'.meta'))
    foreach($file in @('TownShowroomBatch.cs.meta','TownShowroomPlacement.cs.meta','TownShowroomPlayValidation.cs.meta','TownKit/TownShowroomModels.cs.meta')){
        Copy-ShowroomFile (Join-Path $showroomClone ('Assets/Project/Editor/'+$file)) (Join-Path $showroomProject ('Assets/Project/Editor/'+$file))
    }
    Copy-ShowroomFile (Join-Path $showroomClone $showroomPrefab) (Join-Path $showroomProject $showroomPrefab)
    foreach($file in @('result.txt','protection.txt','placement.txt','access.txt','play.txt','review.txt','dressing.txt','yard-layout.txt')){
        if(-not(Test-Path -LiteralPath (Join-Path $showroomQa $file))){if($file -in @('dressing.txt','yard-layout.txt')){continue};throw "Missing acceptance report: $file"}
        Copy-ShowroomFile (Join-Path $showroomQa $file) (Join-Path $showroomProject ($showroomEvidence+'/'+$file))
    }
    [IO.File]::WriteAllText((Join-Path $showroomPipeline 'applied.json'),(@{backup=$showroomBackup;prefab=$showroomHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "SHOWROOM APPLIED. Recoverable backup: $showroomBackup";return
}
$showroomLog=Join-Path $showroomPipeline ($Mode+'.log')
$showroomProcess=Start-Process -FilePath $showroomUnity -ArgumentList @('-batchmode','-projectPath',('"'+$showroomClone+'"'),'-executeMethod',$showroomMethod,'-logFile',('"'+$showroomLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Showroom $Mode launched. PID $($showroomProcess.Id); log $showroomLog"
