param([ValidateSet('Survey','Generate','Review','Play','Apply')][string]$Mode='Survey',[switch]$RoadsOnly)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$forestProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$forestClone=Join-Path $forestProject 'Library/CityUpdateQA/IsolatedProject'
$forestPipeline=Join-Path $forestProject 'Library/TownForestPipeline'
$forestArt='Assets/Art/TownForestQuarter'
$forestPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$forestEvidence='Docs/TownForestReview'
if($RoadsOnly){
    if($Mode -in @('Survey','Play')){throw 'Visual-only road correction supports Generate, Review and Apply.'}
    $forestPipeline=Join-Path $forestProject 'Library/TownForestRoadPipeline'
    $forestArt='Assets/Art/TownForestQuarter/RoadStyle'
    $forestEvidence='Docs/TownForestRoadReview'
}
New-Item -ItemType Directory -Path $forestPipeline -Force | Out-Null
function ForestHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function ForestCopy([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function ForestInputs {
    $files=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $forestProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($forestProject,$file.FullName).Replace('\','/')
            if($relative -eq ($forestArt+'.meta') -or $relative.StartsWith($forestArt+'/')){continue}
            $files[$relative]=ForestHash $file.FullName
        }
    }
    return $files
}
if(Test-Path -LiteralPath (Join-Path $forestClone 'Temp/UnityLockfile')){throw 'Isolated Unity is busy.'}
if($Mode -in @('Survey','Generate')){
    $forestBaseline=ForestInputs
    [IO.File]::WriteAllText((Join-Path $forestPipeline 'baseline.json'),($forestBaseline|ConvertTo-Json -Depth 4))
    foreach($relative in $forestBaseline.Keys){
        $source=Join-Path $forestProject $relative;$target=Join-Path $forestClone $relative
        if(-not(Test-Path -LiteralPath $target) -or (ForestHash $target) -ne $forestBaseline[$relative]){ForestCopy $source $target}
    }
    ForestCopy (Join-Path $forestProject $forestPrefab) (Join-Path $forestPipeline 'OriginalTown.prefab')
}
if($Mode -eq 'Apply'){
    $forestQa=Join-Path $forestClone $(if($RoadsOnly){'Library/TownForestRoadQA'}else{'Library/TownForestQA'})
    $forestHash=ForestHash (Join-Path $forestClone $forestPrefab)
    $stages=if($RoadsOnly){@('result','review')}else{@('result','review','play')}
    foreach($stage in $stages){
        if(-not(Get-Content -LiteralPath (Join-Path $forestQa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Acceptance failed: $stage"}
        if((Get-Content -LiteralPath (Join-Path $forestQa ($stage+'-input.sha256')) -Raw).Trim() -ne $forestHash){throw "Stale prefab: $stage"}
        $accepted=@{}
        foreach($line in Get-Content -LiteralPath (Join-Path $forestQa ($stage+'-assets.sha256'))){
            if($line -notmatch '^([A-F0-9]{64}) (Assets/.+)$'){throw 'Invalid asset receipt.'}
            $expected=$Matches[1];$relative=$Matches[2]
            if($relative.Contains('..') -or $relative.Contains('\') -or ($relative -ne $forestPrefab -and -not $relative.StartsWith($forestArt+'/'))){throw "Unexpected accepted asset: $relative"}
            if($accepted.ContainsKey($relative)){throw "Duplicate accepted asset: $relative"}
            if((ForestHash (Join-Path $forestClone $relative)) -ne $expected){throw "Stale accepted asset: $relative"}
            $accepted[$relative]=$expected
        }
        $outputs=@(Get-ChildItem -LiteralPath (Join-Path $forestClone $forestArt) -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($forestClone,$_.FullName).Replace('\','/')})+@($forestPrefab)
        if($outputs.Count -ne $accepted.Count){throw "Incomplete asset receipt: $stage"}
        foreach($relative in $outputs){if(-not $accepted.ContainsKey($relative)){throw "Unreviewed asset: $relative"}}
    }
    $baseline=Get-Content -LiteralPath (Join-Path $forestPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $current=ForestInputs
    foreach($relative in $baseline.Keys){if(-not $current.ContainsKey($relative) -or $current[$relative] -ne $baseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $current.Keys){if(-not $baseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $backup=Join-Path $forestPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    ForestCopy (Join-Path $forestProject $forestPrefab) (Join-Path $backup $forestPrefab)
    foreach($scope in @($forestArt,$forestEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $forestClone $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($forestClone,$file.FullName);$target=Join-Path $forestProject $relative
            if(Test-Path -LiteralPath $target){ForestCopy $target (Join-Path $backup $relative)}
            ForestCopy $file.FullName $target
        }
    }
    ForestCopy (Join-Path $forestClone ($forestArt+'.meta')) (Join-Path $forestProject ($forestArt+'.meta'))
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $forestClone 'Assets/Project/Editor') -Recurse -File -Filter 'TownForest*.cs.meta'){
        $relative=[IO.Path]::GetRelativePath($forestClone,$file.FullName);ForestCopy $file.FullName (Join-Path $forestProject $relative)
    }
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $forestClone 'Assets/Project/Scripts/City') -File -Filter 'TownForest*.cs.meta'){
        $relative=[IO.Path]::GetRelativePath($forestClone,$file.FullName);ForestCopy $file.FullName (Join-Path $forestProject $relative)
    }
    ForestCopy (Join-Path $forestClone $forestPrefab) (Join-Path $forestProject $forestPrefab)
    foreach($file in Get-ChildItem -LiteralPath $forestQa -File){ForestCopy $file.FullName (Join-Path $forestProject ($forestEvidence+'/'+$file.Name))}
    [IO.File]::WriteAllText((Join-Path $forestPipeline 'applied.json'),(@{backup=$backup;prefab=$forestHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "FOREST UPDATE APPLIED. RoadsOnly: $RoadsOnly. Backup: $backup";return
}
$method=if($Mode -eq 'Play'){'TownForestPlay.Run'}else{'TownForestBatch.'+$Mode}
if($RoadsOnly){$method='TownForestRoadBatch.'+$Mode}
$log=Join-Path $forestPipeline ($Mode+'.log')
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$forestClone+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "Forest $Mode launched. PID $($process.Id); log $log"
