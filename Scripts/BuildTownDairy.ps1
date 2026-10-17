param([ValidateSet('Inspect','Generate','Review','Play','Apply')][string]$Mode='Inspect')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$dairyProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$dairyClone=Join-Path $dairyProject 'Library/CityUpdateQA/IsolatedProject'
$dairyPipeline=Join-Path $dairyProject 'Library/TownDairyPipeline'
$dairyPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$dairyUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$dairyAssets='Assets/Art/TownDairy'
$dairyEvidence='Docs/TownDairyReview'
New-Item -ItemType Directory -Path $dairyPipeline -Force | Out-Null
function Get-DairyHash([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Copy-DairyFile([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function Get-DairyInputs {
    $inputs=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $dairyProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($dairyProject,$file.FullName).Replace('\','/')
            if($relative -eq ($dairyAssets+'.meta') -or $relative.StartsWith($dairyAssets+'/')){continue}
            $inputs[$relative]=Get-DairyHash $file.FullName
        }
    }
    return $inputs
}
if(Test-Path -LiteralPath (Join-Path $dairyClone 'Temp/UnityLockfile')){throw 'The isolated Unity editor is busy; do not interrupt it.'}
if($Mode -in @('Inspect','Generate')){
    $dairyBaseline=Get-DairyInputs
    [IO.File]::WriteAllText((Join-Path $dairyPipeline 'baseline.json'),($dairyBaseline|ConvertTo-Json -Depth 5))
    foreach($relative in $dairyBaseline.Keys){
        $source=Join-Path $dairyProject $relative;$target=Join-Path $dairyClone $relative
        if(-not(Test-Path -LiteralPath $target) -or (Get-DairyHash $target) -ne $dairyBaseline[$relative]){Copy-DairyFile $source $target}
    }
    if(Test-Path -LiteralPath (Join-Path $dairyProject $dairyAssets)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $dairyProject $dairyAssets) -File -Recurse){Copy-DairyFile $file.FullName (Join-Path $dairyClone ([IO.Path]::GetRelativePath($dairyProject,$file.FullName)))}
    }
    Copy-DairyFile (Join-Path $dairyProject $dairyPrefab) (Join-Path $dairyPipeline 'OriginalTown.prefab')
    $dairyMethod='TownDairyBatch.'+$Mode
} elseif($Mode -in @('Play','Review')){
    $dairyMethod=if($Mode -eq 'Play'){'TownDairyPlayValidation.Run'}else{'TownDairyBatch.Review'}
} else {
    $dairyQa=Join-Path $dairyClone 'Library/TownDairyQA'
    foreach($receipt in @('result.txt','play.txt','review.txt')){
        if(-not(Get-Content -LiteralPath (Join-Path $dairyQa $receipt) -Raw).StartsWith('PASS:')){throw "Dairy acceptance failed: $receipt"}
    }
    $dairyHash=Get-DairyHash (Join-Path $dairyClone $dairyPrefab)
    foreach($receipt in @('play-input.sha256','review-input.sha256')){
        if((Get-Content -LiteralPath (Join-Path $dairyQa $receipt) -Raw).Trim() -ne $dairyHash){throw "Stale dairy acceptance: $receipt"}
    }
    $dairyBaseline=Get-Content -LiteralPath (Join-Path $dairyPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $dairyCurrent=Get-DairyInputs
    foreach($relative in $dairyBaseline.Keys){if(-not $dairyCurrent.ContainsKey($relative) -or $dairyCurrent[$relative] -ne $dairyBaseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $dairyCurrent.Keys){if(-not $dairyBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $dairyBackup=Join-Path $dairyPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    Copy-DairyFile (Join-Path $dairyProject $dairyPrefab) (Join-Path $dairyBackup $dairyPrefab)
    foreach($scope in @($dairyAssets,$dairyEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $dairyClone $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($dairyClone,$file.FullName);$target=Join-Path $dairyProject $relative
            if(Test-Path -LiteralPath $target){Copy-DairyFile $target (Join-Path $dairyBackup $relative)}
            Copy-DairyFile $file.FullName $target
        }
    }
    Copy-DairyFile (Join-Path $dairyClone ($dairyAssets+'.meta')) (Join-Path $dairyProject ($dairyAssets+'.meta'))
    foreach($relative in @('Assets/Project/Editor/TownDairyBatch.cs.meta','Assets/Project/Editor/TownDairyPlacement.cs.meta','Assets/Project/Editor/TownKit/TownDairyModels.cs.meta','Assets/Project/Editor/TownDairyPlayValidation.cs.meta')){Copy-DairyFile (Join-Path $dairyClone $relative) (Join-Path $dairyProject $relative)}
    Copy-DairyFile (Join-Path $dairyClone $dairyPrefab) (Join-Path $dairyProject $dairyPrefab)
    foreach($receipt in @('result.txt','play.txt','review.txt','placement.txt','protection.txt','access.txt')){Copy-DairyFile (Join-Path $dairyQa $receipt) (Join-Path $dairyProject ($dairyEvidence+'/'+$receipt))}
    [IO.File]::WriteAllText((Join-Path $dairyPipeline 'applied.json'),(@{backup=$dairyBackup;prefab=$dairyHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "DAIRY PLANT APPLIED. Recoverable backup: $dairyBackup";return
}
$dairyLog=Join-Path $dairyPipeline ($Mode+'.log')
$dairyProcess=Start-Process -FilePath $dairyUnity -ArgumentList @('-batchmode','-projectPath',('"'+$dairyClone+'"'),'-executeMethod',$dairyMethod,'-logFile',('"'+$dairyLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Dairy plant $Mode launched. PID $($dairyProcess.Id); log $dairyLog"
