param([ValidateSet('Inspect','Generate','Enrich','Review','Play','Apply')][string]$Mode='Inspect')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$floristProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$floristClone=Join-Path $floristProject 'Library/CityUpdateQA/IsolatedProject'
$floristPipeline=Join-Path $floristProject 'Library/TownFloristPipeline'
$floristPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$floristUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$floristAssets='Assets/Art/TownFlorist'
$floristEvidence='Docs/TownFloristReview'
New-Item -ItemType Directory -Path $floristPipeline -Force | Out-Null
function Get-FloristHash([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Copy-FloristFile([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function Get-FloristInputs {
    $inputs=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $floristProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($floristProject,$file.FullName).Replace('\','/')
            if($relative -eq ($floristAssets+'.meta') -or $relative.StartsWith($floristAssets+'/')){continue}
            $inputs[$relative]=Get-FloristHash $file.FullName
        }
    }
    return $inputs
}
if($Mode -eq 'Inspect' -or $Mode -eq 'Generate' -or $Mode -eq 'Enrich'){
    if(Test-Path -LiteralPath (Join-Path $floristClone 'Temp/UnityLockfile')){throw 'The isolated editor is busy; do not interrupt it.'}
    $floristBaseline=Get-FloristInputs
    [IO.File]::WriteAllText((Join-Path $floristPipeline 'baseline.json'),($floristBaseline|ConvertTo-Json -Depth 5))
    foreach($relative in $floristBaseline.Keys){
        $source=Join-Path $floristProject $relative;$destination=Join-Path $floristClone $relative
        if(-not (Test-Path -LiteralPath $destination) -or (Get-FloristHash $destination) -ne $floristBaseline[$relative]){Copy-FloristFile $source $destination}
    }
    Copy-FloristFile (Join-Path $floristProject $floristPrefab) (Join-Path $floristPipeline 'OriginalTown.prefab')
    if($Mode -eq 'Enrich'){
        # An incremental revision must start with the installed assets, not a stale failed clone run.
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $floristProject $floristAssets) -File -Recurse){
            Copy-FloristFile $file.FullName (Join-Path $floristClone ([IO.Path]::GetRelativePath($floristProject,$file.FullName)))
        }
    }
    $floristMethod=if($Mode -eq 'Inspect'){'TownFloristBatch.Inspect'}elseif($Mode -eq 'Enrich'){'TownFloristBatch.Enrich'}else{'TownFloristBatch.Generate'}
} elseif($Mode -eq 'Play' -or $Mode -eq 'Review'){
    if(Test-Path -LiteralPath (Join-Path $floristClone 'Temp/UnityLockfile')){throw 'The isolated editor is busy.'}
    $floristMethod=if($Mode -eq 'Play'){'TownBakeryPlayValidation.RunFlorist'}else{'TownFloristBatch.Review'}
} else {
    if(Test-Path -LiteralPath (Join-Path $floristClone 'Temp/UnityLockfile')){throw 'The isolated editor is busy.'}
    $floristResult=Join-Path $floristClone 'Library/TownFloristQA/result.txt'
    if(-not(Test-Path -LiteralPath $floristResult) -or -not(Get-Content -LiteralPath $floristResult -Raw).StartsWith('FLORIST PASSED')){throw 'Native florist generation has not passed.'}
    $floristPlay=Join-Path $floristClone 'Library/TownFloristQA/play.txt'
    if(-not(Test-Path -LiteralPath $floristPlay) -or -not(Get-Content -LiteralPath $floristPlay -Raw).StartsWith('PASS:')){throw 'Actual pickup validation has not passed.'}
    if((Get-Content -LiteralPath (Join-Path $floristClone 'Library/TownFloristQA/play-input.sha256') -Raw).Trim() -ne (Get-FloristHash (Join-Path $floristClone $floristPrefab))){throw 'Pickup evidence refers to a different prefab.'}
    $floristBaseline=Get-Content -LiteralPath (Join-Path $floristPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $floristCurrent=Get-FloristInputs
    foreach($relative in $floristBaseline.Keys){if(-not $floristCurrent.ContainsKey($relative) -or $floristCurrent[$relative] -ne $floristBaseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $floristCurrent.Keys){if(-not $floristBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $floristBackup=Join-Path $floristPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    Copy-FloristFile (Join-Path $floristProject $floristPrefab) (Join-Path $floristBackup $floristPrefab)
    foreach($scope in @($floristAssets,$floristEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $floristClone $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($floristClone,$file.FullName);$destination=Join-Path $floristProject $relative
            if(Test-Path -LiteralPath $destination){Copy-FloristFile $destination (Join-Path $floristBackup $relative)}
            Copy-FloristFile $file.FullName $destination
        }
    }
    Copy-FloristFile (Join-Path $floristClone ($floristAssets+'.meta')) (Join-Path $floristProject ($floristAssets+'.meta'))
    foreach($file in @('TownFloristBatch.cs.meta','TownFloristPlacement.cs.meta','TownStaticTypography.cs.meta','TownKit/TownFloristModels.cs.meta')){
        Copy-FloristFile (Join-Path $floristClone ('Assets/Project/Editor/'+$file)) (Join-Path $floristProject ('Assets/Project/Editor/'+$file))
    }
    Copy-FloristFile (Join-Path $floristClone $floristPrefab) (Join-Path $floristProject $floristPrefab)
    foreach($file in @('result.txt','protection.txt','placement.txt','access.txt','play.txt','garden.txt')){
        $source=Join-Path $floristClone ('Library/TownFloristQA/'+$file)
        if(Test-Path -LiteralPath $source){Copy-FloristFile $source (Join-Path $floristProject ($floristEvidence+'/'+$file))}
    }
    [IO.File]::WriteAllText((Join-Path $floristPipeline 'applied.json'),(@{backup=$floristBackup;prefab=(Get-FloristHash (Join-Path $floristProject $floristPrefab));utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "FLORIST APPLIED. Recoverable prefab backup: $floristBackup"
    return
}
$floristLog=Join-Path $floristPipeline ($Mode+'.log')
$floristProcess=Start-Process -FilePath $floristUnity -ArgumentList @('-batchmode','-projectPath',('"'+$floristClone+'"'),'-executeMethod',$floristMethod,'-logFile',('"'+$floristLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Florist $Mode launched. PID $($floristProcess.Id); log $floristLog"
