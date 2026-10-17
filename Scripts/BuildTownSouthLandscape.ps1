param([ValidateSet('Survey','Generate','Review','Apply')][string]$Mode='Survey',[switch]$EastBank)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$southProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$southClone=Join-Path $southProject 'Library/CityUpdateQA/IsolatedProject'
$southPipeline=Join-Path $southProject 'Library/TownSouthLandscapePipeline'
$southArt='Assets/Art/TownSouthLandscape'
$southEvidence='Docs/TownSouthLandscapeReview'
$southPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$southLayout='Assets/Prefabs/TownWorld/TownLayout.asset'
$southQaFolder='Library/TownSouthLandscapeQA'
$southMethod='TownSouthLandscapeBatch'
$southWorldOutputs=@($southLayout,$southPrefab)
if($EastBank){
    if($Mode -eq 'Survey'){throw 'East-bank completion supports Generate, Review and Apply.'}
    $southPipeline=Join-Path $southProject 'Library/TownEastBankPipeline'
    $southArt='Assets/Art/TownSouthLandscape/EastBank'
    $southEvidence='Docs/TownEastBankReview'
    $southQaFolder='Library/TownEastBankQA'
    $southMethod='TownEastBankBatch'
    $southWorldOutputs=@($southPrefab)
}
New-Item -ItemType Directory -Path $southPipeline -Force | Out-Null
function SouthHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function SouthCopy([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function SouthInputs {
    $files=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $southProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($southProject,$file.FullName).Replace('\','/')
            if($relative -eq ($southArt+'.meta') -or $relative.StartsWith($southArt+'/')){continue}
            $files[$relative]=SouthHash $file.FullName
        }
    }
    return $files
}
if(Test-Path -LiteralPath (Join-Path $southClone 'Temp/UnityLockfile')){throw 'Isolated Unity is busy.'}
if($Mode -in @('Survey','Generate')){
    $southBaseline=SouthInputs
    [IO.File]::WriteAllText((Join-Path $southPipeline 'baseline.json'),($southBaseline|ConvertTo-Json -Depth 4))
    foreach($relative in $southBaseline.Keys){
        $source=Join-Path $southProject $relative;$target=Join-Path $southClone $relative
        if(-not(Test-Path -LiteralPath $target) -or (SouthHash $target) -ne $southBaseline[$relative]){SouthCopy $source $target}
    }
    foreach($relative in @($southPrefab,$southLayout)){SouthCopy (Join-Path $southProject $relative) (Join-Path $southPipeline ('Original/'+$relative))}
}
if($Mode -eq 'Apply'){
    $southQa=Join-Path $southClone $southQaFolder
    $outputs=@(Get-ChildItem -LiteralPath (Join-Path $southClone $southArt) -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($southClone,$_.FullName).Replace('\','/')})+@(($southArt+'.meta'))+$southWorldOutputs
    foreach($stage in @('result','review')){
        if(-not(Get-Content -LiteralPath (Join-Path $southQa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Acceptance failed: $stage"}
        $accepted=@{}
        foreach($line in Get-Content -LiteralPath (Join-Path $southQa ($stage+'-assets.sha256'))){
            if($line -notmatch '^([A-F0-9]{64}) (Assets/.+)$'){throw 'Invalid receipt'}
            $expected=$Matches[1];$relative=$Matches[2]
            if($relative.Contains('..') -or $relative.Contains('\') -or ($relative -notin ($southWorldOutputs+@(($southArt+'.meta'))) -and -not $relative.StartsWith($southArt+'/'))){throw "Unexpected output: $relative"}
            if($accepted.ContainsKey($relative) -or (SouthHash (Join-Path $southClone $relative)) -ne $expected){throw "Duplicate/stale accepted output: $relative"}
            $accepted[$relative]=$expected
        }
        if($outputs.Count -ne $accepted.Count){throw "Incomplete receipt: $stage"}
        foreach($relative in $outputs){if(-not $accepted.ContainsKey($relative)){throw "Unreviewed output: $relative"}}
    }
    $baseline=Get-Content -LiteralPath (Join-Path $southPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $current=SouthInputs
    foreach($relative in $baseline.Keys){if(-not $current.ContainsKey($relative) -or $current[$relative] -ne $baseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $current.Keys){if(-not $baseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $backup=Join-Path $southPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    foreach($relative in $outputs){
        $target=Join-Path $southProject $relative
        if(Test-Path -LiteralPath $target){SouthCopy $target (Join-Path $backup $relative)}
    }
    # Resources first; only after complete validation do the two shared world references get replaced.
    foreach($relative in $outputs | Where-Object {$_ -notin @($southPrefab,$southLayout)}){SouthCopy (Join-Path $southClone $relative) (Join-Path $southProject $relative)}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $southClone $southEvidence) -File -Recurse){SouthCopy $file.FullName (Join-Path $southProject ([IO.Path]::GetRelativePath($southClone,$file.FullName)))}
    foreach($file in Get-ChildItem -LiteralPath $southQa -File){SouthCopy $file.FullName (Join-Path $southProject ($southEvidence+'/'+$file.Name))}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $southClone 'Assets/Project/Editor') -File -Filter 'TownSouthLandscape*.cs.meta'){
        SouthCopy $file.FullName (Join-Path $southProject ('Assets/Project/Editor/'+$file.Name))
    }
    if($EastBank){SouthCopy (Join-Path $southClone 'Assets/Project/Editor/TownEastBankBatch.cs.meta') (Join-Path $southProject 'Assets/Project/Editor/TownEastBankBatch.cs.meta')}
    foreach($relative in $southWorldOutputs){SouthCopy (Join-Path $southClone $relative) (Join-Path $southProject $relative)}
    [IO.File]::WriteAllText((Join-Path $southPipeline 'applied.json'),(@{backup=$backup;prefab=(SouthHash (Join-Path $southProject $southPrefab));layout=(SouthHash (Join-Path $southProject $southLayout));utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "SOUTH LANDSCAPE APPLIED. Backup: $backup";return
}
$log=Join-Path $southPipeline ($Mode+'.log')
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$southClone+'"'),'-executeMethod',($southMethod+'.'+$Mode),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "South landscape $Mode launched. PID $($process.Id); log $log"
