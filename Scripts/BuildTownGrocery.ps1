param([ValidateSet('Inspect','Generate','Green','Review','Play','Apply')][string]$Mode='Inspect')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$groceryProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$groceryClone=Join-Path $groceryProject 'Library/CityUpdateQA/IsolatedProject'
$groceryPipeline=Join-Path $groceryProject 'Library/TownGroceryPipeline'
$groceryPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$groceryUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$groceryAssets='Assets/Art/TownGrocery'
$groceryEvidence='Docs/TownGroceryReview'
New-Item -ItemType Directory -Path $groceryPipeline -Force | Out-Null
function Get-GroceryHash([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Copy-GroceryFile([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function Get-GroceryInputs {
    $inputs=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $groceryProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($groceryProject,$file.FullName).Replace('\','/')
            if($relative -eq ($groceryAssets+'.meta') -or $relative.StartsWith($groceryAssets+'/')){continue}
            $inputs[$relative]=Get-GroceryHash $file.FullName
        }
    }
    return $inputs
}
if(Test-Path -LiteralPath (Join-Path $groceryClone 'Temp/UnityLockfile')){throw 'The isolated Unity editor is busy; do not interrupt it.'}
if($Mode -in @('Inspect','Generate','Green')){
    $groceryBaseline=Get-GroceryInputs
    [IO.File]::WriteAllText((Join-Path $groceryPipeline 'baseline.json'),($groceryBaseline|ConvertTo-Json -Depth 5))
    foreach($relative in $groceryBaseline.Keys){
        $source=Join-Path $groceryProject $relative;$destination=Join-Path $groceryClone $relative
        if(-not(Test-Path -LiteralPath $destination) -or (Get-GroceryHash $destination) -ne $groceryBaseline[$relative]){Copy-GroceryFile $source $destination}
    }
    if($Mode -in @('Generate','Green') -and (Test-Path -LiteralPath (Join-Path $groceryProject $groceryAssets))){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $groceryProject $groceryAssets) -File -Recurse){
            Copy-GroceryFile $file.FullName (Join-Path $groceryClone ([IO.Path]::GetRelativePath($groceryProject,$file.FullName)))
        }
    }
    Copy-GroceryFile (Join-Path $groceryProject $groceryPrefab) (Join-Path $groceryPipeline 'OriginalTown.prefab')
    $groceryMethod='TownGroceryBatch.'+$Mode
} elseif($Mode -eq 'Play' -or $Mode -eq 'Review'){
    $groceryMethod=if($Mode -eq 'Play'){'TownGroceryPlayValidation.Run'}else{'TownGroceryBatch.Review'}
} else {
    $groceryQa=Join-Path $groceryClone 'Library/TownGroceryQA'
    if(-not(Get-Content -LiteralPath (Join-Path $groceryQa 'result.txt') -Raw).StartsWith('GROCERY PASSED')){throw 'Native grocery authoring has not passed.'}
    if(-not(Get-Content -LiteralPath (Join-Path $groceryQa 'play.txt') -Raw).StartsWith('PASS:')){throw 'Actual pickup test has not passed.'}
    if(-not(Get-Content -LiteralPath (Join-Path $groceryQa 'review.txt') -Raw).StartsWith('PASS:')){throw 'Fresh saved-scene visual review has not passed.'}
    $groceryHash=Get-GroceryHash (Join-Path $groceryClone $groceryPrefab)
    foreach($receipt in @('play-input.sha256','review-input.sha256')){
        if((Get-Content -LiteralPath (Join-Path $groceryQa $receipt) -Raw).Trim() -ne $groceryHash){throw "Stale acceptance receipt: $receipt"}
    }
    $groceryBaseline=Get-Content -LiteralPath (Join-Path $groceryPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $groceryCurrent=Get-GroceryInputs
    foreach($relative in $groceryBaseline.Keys){if(-not $groceryCurrent.ContainsKey($relative) -or $groceryCurrent[$relative] -ne $groceryBaseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $groceryCurrent.Keys){if(-not $groceryBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $groceryBackup=Join-Path $groceryPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    Copy-GroceryFile (Join-Path $groceryProject $groceryPrefab) (Join-Path $groceryBackup $groceryPrefab)
    foreach($scope in @($groceryAssets,$groceryEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $groceryClone $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($groceryClone,$file.FullName);$target=Join-Path $groceryProject $relative
            if(Test-Path -LiteralPath $target){Copy-GroceryFile $target (Join-Path $groceryBackup $relative)}
            Copy-GroceryFile $file.FullName $target
        }
    }
    Copy-GroceryFile (Join-Path $groceryClone ($groceryAssets+'.meta')) (Join-Path $groceryProject ($groceryAssets+'.meta'))
    foreach($file in @('TownGroceryBatch.cs.meta','TownGroceryPlacement.cs.meta','TownGroceryPlayValidation.cs.meta','TownWesternGreenery.cs.meta','TownKit/TownGroceryModels.cs.meta')){
        Copy-GroceryFile (Join-Path $groceryClone ('Assets/Project/Editor/'+$file)) (Join-Path $groceryProject ('Assets/Project/Editor/'+$file))
    }
    Copy-GroceryFile (Join-Path $groceryClone $groceryPrefab) (Join-Path $groceryProject $groceryPrefab)
    foreach($file in @('result.txt','protection.txt','placement.txt','access.txt','play.txt','review.txt')){
        if(-not(Test-Path -LiteralPath (Join-Path $groceryQa $file))){throw "Missing acceptance report: $file"}
        Copy-GroceryFile (Join-Path $groceryQa $file) (Join-Path $groceryProject ($groceryEvidence+'/'+$file))
    }
    if(Test-Path -LiteralPath (Join-Path $groceryQa 'greenery.txt')){
        Copy-GroceryFile (Join-Path $groceryQa 'greenery.txt') (Join-Path $groceryProject ($groceryEvidence+'/greenery.txt'))
    }
    [IO.File]::WriteAllText((Join-Path $groceryPipeline 'applied.json'),(@{backup=$groceryBackup;prefab=$groceryHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "GROCERY APPLIED. Recoverable backup: $groceryBackup";return
}
$groceryLog=Join-Path $groceryPipeline ($Mode+'.log')
$groceryProcess=Start-Process -FilePath $groceryUnity -ArgumentList @('-batchmode','-projectPath',('"'+$groceryClone+'"'),'-executeMethod',$groceryMethod,'-logFile',('"'+$groceryLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Grocery $Mode launched. PID $($groceryProcess.Id); log $groceryLog"
