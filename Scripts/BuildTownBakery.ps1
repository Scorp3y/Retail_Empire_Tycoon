param([ValidateSet('Generate','Revise','RemoveDetails','Review','Play','Apply')][string]$Mode='Generate')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$bakeryProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$bakeryClone=Join-Path $bakeryProject 'Library/CityUpdateQA/IsolatedProject'
$bakeryPipeline=Join-Path $bakeryProject 'Library/TownBakeryPipeline'
$bakeryPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$bakeryUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$bakeryAssets='Assets/Art/TownBakery'
$bakeryEvidence='Docs/TownBakeryReview'
New-Item -ItemType Directory -Path $bakeryPipeline -Force | Out-Null
function Get-BakeryHash([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Copy-BakeryFile([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function Get-BakeryInputs {
    $inputs=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $bakeryProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($bakeryProject,$file.FullName).Replace('\','/')
            if($relative -eq ($bakeryAssets+'.meta') -or $relative.StartsWith($bakeryAssets+'/')){continue}
            $inputs[$relative]=Get-BakeryHash $file.FullName
        }
    }
    return $inputs
}
if($Mode -eq 'Generate' -or $Mode -eq 'Revise' -or $Mode -eq 'RemoveDetails'){
    if(Test-Path -LiteralPath (Join-Path $bakeryClone 'Temp/UnityLockfile')){throw 'The isolated editor may be busy; do not interrupt it.'}
    if(-not (Test-Path -LiteralPath (Join-Path $bakeryClone 'ProjectSettings/ProjectVersion.txt'))){throw 'The prepared isolated project is missing.'}
    $bakeryBaseline=Get-BakeryInputs
    [IO.File]::WriteAllText((Join-Path $bakeryPipeline 'baseline.json'),($bakeryBaseline|ConvertTo-Json -Depth 5))
    # Synchronise actual current inputs; leave the main editor and its working scene untouched.
    foreach($relative in $bakeryBaseline.Keys){
        $source=Join-Path $bakeryProject $relative;$destination=Join-Path $bakeryClone $relative
        if(-not (Test-Path -LiteralPath $destination) -or (Get-BakeryHash $destination) -ne $bakeryBaseline[$relative]){Copy-BakeryFile $source $destination}
    }
    Copy-BakeryFile (Join-Path $bakeryProject $bakeryPrefab) (Join-Path $bakeryPipeline 'OriginalTown.prefab')
    $bakeryLog=Join-Path $bakeryPipeline 'Generate.log'
    $bakeryMethod=switch($Mode){'Revise'{'TownBakeryBatch.Revise'} 'RemoveDetails'{'TownBakeryBatch.RemoveDetails'} default{'TownBakeryBatch.Generate'}}
    $bakeryProcess=Start-Process -FilePath $bakeryUnity -ArgumentList @('-batchmode','-projectPath',('"'+$bakeryClone+'"'),'-executeMethod',$bakeryMethod,'-logFile',('"'+$bakeryLog+'"')) -WindowStyle Hidden -PassThru
    [IO.File]::WriteAllText((Join-Path $bakeryPipeline 'launch.json'),(@{pid=$bakeryProcess.Id;log=$bakeryLog;baseline=(Get-BakeryHash (Join-Path $bakeryPipeline 'baseline.json'))}|ConvertTo-Json))
    Write-Output "Bakery generation launched in isolated Unity. PID $($bakeryProcess.Id); log $bakeryLog"
    return
}
if($Mode -eq 'Play' -or $Mode -eq 'Review'){
    if(Test-Path -LiteralPath (Join-Path $bakeryClone 'Temp/UnityLockfile')){throw 'The isolated editor may still be busy.'}
    $bakeryPlayLog=Join-Path $bakeryPipeline ($Mode+'.log')
    $bakeryCheck=if($Mode -eq 'Review'){'TownBakeryBatch.Review'}else{'TownBakeryPlayValidation.Run'}
    $bakeryProcess=Start-Process -FilePath $bakeryUnity -ArgumentList @('-batchmode','-projectPath',('"'+$bakeryClone+'"'),'-executeMethod',$bakeryCheck,'-logFile',('"'+$bakeryPlayLog+'"')) -WindowStyle Hidden -PassThru
    Write-Output "Native bakery pickup acceptance launched. PID $($bakeryProcess.Id); log $bakeryPlayLog"
    return
}
$bakeryResult=Join-Path $bakeryClone 'Library/TownBakeryQA/result.txt'
if(-not (Test-Path -LiteralPath $bakeryResult) -or -not (Get-Content -LiteralPath $bakeryResult -Raw).StartsWith('BAKERY PASSED')){throw 'Isolated bakery generation and validation have not passed.'}
$bakeryPlayResult=Join-Path $bakeryClone 'Library/TownBakeryQA/play.txt'
if(-not (Test-Path -LiteralPath $bakeryPlayResult) -or -not (Get-Content -LiteralPath $bakeryPlayResult -Raw).StartsWith('PASS:')){throw 'The actual native pickup has not passed the new rear docking test.'}
if((Get-Content -LiteralPath (Join-Path $bakeryClone 'Library/TownBakeryQA/play-input.sha256') -Raw).Trim() -ne (Get-BakeryHash (Join-Path $bakeryClone $bakeryPrefab))){throw 'Pickup acceptance belongs to a different saved town prefab.'}
$bakeryBaseline=Get-Content -LiteralPath (Join-Path $bakeryPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
$bakeryCurrent=Get-BakeryInputs
foreach($relative in $bakeryBaseline.Keys){if(-not $bakeryCurrent.ContainsKey($relative) -or $bakeryCurrent[$relative] -ne $bakeryBaseline[$relative]){throw "Main input changed during generation: $relative"}}
foreach($relative in $bakeryCurrent.Keys){if(-not $bakeryBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected new main input: $relative"}}
$bakeryBackup=Join-Path $bakeryPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
Copy-BakeryFile (Join-Path $bakeryProject $bakeryPrefab) (Join-Path $bakeryBackup $bakeryPrefab)
foreach($scope in @($bakeryAssets,$bakeryEvidence)){
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $bakeryClone $scope) -File -Recurse){
        $relative=[IO.Path]::GetRelativePath($bakeryClone,$file.FullName)
        $target=Join-Path $bakeryProject $relative
        if(Test-Path -LiteralPath $target){Copy-BakeryFile $target (Join-Path $bakeryBackup $relative)}
        Copy-BakeryFile $file.FullName $target
    }
}
Copy-BakeryFile (Join-Path $bakeryClone ($bakeryAssets+'.meta')) (Join-Path $bakeryProject ($bakeryAssets+'.meta'))
foreach($relative in @('Assets/Project/Editor/TownBakeryBatch.cs.meta','Assets/Project/Editor/TownBakeryPlacement.cs.meta','Assets/Project/Editor/TownBakeryTypography.cs.meta','Assets/Project/Editor/TownBakeryPlayValidation.cs.meta','Assets/Project/Editor/TownKit/TownBakeryModels.cs.meta')){
    Copy-BakeryFile (Join-Path $bakeryClone $relative) (Join-Path $bakeryProject $relative)
}
Copy-BakeryFile (Join-Path $bakeryClone $bakeryPrefab) (Join-Path $bakeryProject $bakeryPrefab)
Copy-BakeryFile $bakeryResult (Join-Path $bakeryProject 'Docs/TownBakeryReview/result.txt')
foreach($file in @('access.txt','protection.txt','placement.txt','play.txt')){Copy-BakeryFile (Join-Path $bakeryClone ('Library/TownBakeryQA/'+$file)) (Join-Path $bakeryProject ('Docs/TownBakeryReview/'+$file))}
[IO.File]::WriteAllText((Join-Path $bakeryPipeline 'applied.json'),(@{backup=$bakeryBackup;prefab=(Get-BakeryHash (Join-Path $bakeryProject $bakeryPrefab));utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
Write-Output "BAKERY APPLIED. Recoverable prefab backup: $bakeryBackup"
