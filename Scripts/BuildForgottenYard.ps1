param([ValidateSet('Generate','Review','Play','Apply')][string]$Mode='Generate',[switch]$Details)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$yardProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$yardClone=Join-Path $yardProject 'Library/CityUpdateQA/IsolatedProject'
$yardPipeline=Join-Path $yardProject 'Library/TownForgottenYardPipeline'
$yardArt='Assets/Art/TownForgottenYard'
$yardQa='Library/TownForgottenYardQA'
$yardEvidence='Docs/TownForgottenYardReview'
$yardShared=@('Assets/Project/Scenes/Game.unity','Assets/Prefabs/StarterStore/StarterStore.asset','Assets/Prefabs/TownWorld/TownLayout.asset','Assets/Prefabs/TownWorld/TownEnvironment.prefab')
if($Details){
    $yardPipeline=Join-Path $yardProject 'Library/TownYardStoryPipeline'
    $yardArt='Assets/Art/TownForgottenYard/DetailPass'
    $yardQa='Library/TownYardStoryQA'
    $yardEvidence='Docs/TownYardStoryReview'
    $yardShared=@('Assets/Prefabs/TownWorld/TownEnvironment.prefab')
}
New-Item -ItemType Directory -Path $yardPipeline -Force | Out-Null
function YardHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function YardCopy([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function YardInputs {
    $files=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $yardProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($yardProject,$file.FullName).Replace('\','/')
            if($relative -eq ($yardArt+'.meta') -or $relative.StartsWith($yardArt+'/')){continue}
            $files[$relative]=YardHash $file.FullName
        }
    }
    return $files
}
$yardLock=Join-Path $yardClone 'Temp/UnityLockfile'
if(Test-Path -LiteralPath $yardLock){
    $running=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Contains($yardClone)})
    if($running.Count -gt 0){throw 'Isolated Unity is busy.'}
    # A compiler-error exit can leave this single disposable lock file behind.
    Remove-Item -LiteralPath $yardLock -Force
}
if($Mode -eq 'Generate'){
    $baseline=YardInputs
    [IO.File]::WriteAllText((Join-Path $yardPipeline 'baseline.json'),($baseline|ConvertTo-Json -Depth 4))
    foreach($relative in $baseline.Keys){
        $source=Join-Path $yardProject $relative;$target=Join-Path $yardClone $relative
        if(-not(Test-Path -LiteralPath $target) -or (YardHash $target) -ne $baseline[$relative]){YardCopy $source $target}
    }
    foreach($relative in $yardShared){YardCopy (Join-Path $yardProject $relative) (Join-Path $yardPipeline ('Original/'+$relative))}
}
if($Mode -eq 'Play'){
    # The acceptance harness can be repaired independently; product sources/outputs remain frozen.
    $relative='Assets/Project/Editor/TownForgottenYardPlay.cs'
    $source=Join-Path $yardProject $relative;$target=Join-Path $yardClone $relative
    $baseline=Get-Content -LiteralPath (Join-Path $yardPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    YardCopy $source $target;$baseline[$relative]=YardHash $source
    [IO.File]::WriteAllText((Join-Path $yardPipeline 'baseline.json'),($baseline|ConvertTo-Json -Depth 4))
}
if($Mode -eq 'Apply'){
    $qa=Join-Path $yardClone $yardQa
    $outputs=@(Get-ChildItem -LiteralPath (Join-Path $yardClone $yardArt) -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($yardClone,$_.FullName).Replace('\','/')})+@(($yardArt+'.meta'))+$yardShared
    foreach($stage in @('result','review','play')){
        if(-not(Get-Content -LiteralPath (Join-Path $qa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Acceptance failed: $stage"}
        $accepted=@{}
        foreach($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-assets.sha256'))){
            if($line -notmatch '^([A-F0-9]{64}) (Assets/.+)$'){throw 'Invalid receipt'}
            $expected=$Matches[1];$relative=$Matches[2]
            if($relative.Contains('..') -or $relative.Contains('\') -or ($relative -notin ($yardShared+@(($yardArt+'.meta'))) -and -not $relative.StartsWith($yardArt+'/'))){throw "Unexpected output: $relative"}
            if($accepted.ContainsKey($relative) -or (YardHash (Join-Path $yardClone $relative)) -ne $expected){throw "Duplicate/stale accepted output: $relative"}
            $accepted[$relative]=$expected
        }
        if($outputs.Count -ne $accepted.Count){throw "Incomplete receipt: $stage"}
        foreach($relative in $outputs){if(-not $accepted.ContainsKey($relative)){throw "Unreviewed output: $relative"}}
    }
    $baseline=Get-Content -LiteralPath (Join-Path $yardPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $current=YardInputs
    foreach($relative in $baseline.Keys){if(-not $current.ContainsKey($relative) -or $current[$relative] -ne $baseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $current.Keys){if(-not $baseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $backup=Join-Path $yardPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    foreach($relative in $outputs){if(Test-Path -LiteralPath (Join-Path $yardProject $relative)){YardCopy (Join-Path $yardProject $relative) (Join-Path $backup $relative)}}
    # Runtime ScriptableObject references require the exact script GUID used by the clone.
    $codeMeta=@('Assets/Project/Scripts/Territory/TerritorySubdivisionRules.cs.meta','Assets/Project/Editor/TownForgottenYardAuthoring.cs.meta','Assets/Project/Editor/TownForgottenYardBatch.cs.meta','Assets/Project/Editor/TownForgottenYardPlay.cs.meta','Assets/Project/Editor/TownForgottenYardPreservation.cs.meta','Assets/Project/Editor/TownKit/TownForgottenYardModels.cs.meta')
    if($Details){$codeMeta+=@('Assets/Project/Editor/TownYardStoryBatch.cs.meta','Assets/Project/Editor/TownYardStoryPlacement.cs.meta','Assets/Project/Editor/TownKit/TownYardStoryModels.cs.meta')}
    foreach($relative in $codeMeta){
        $source=Join-Path $yardClone $relative;$target=Join-Path $yardProject $relative
        if(Test-Path -LiteralPath $target){if((YardHash $source) -ne (YardHash $target)){throw "Source GUID mismatch: $relative"}}
        else{YardCopy $source $target}
    }
    foreach($relative in $outputs | Where-Object {$_ -notin $yardShared}){YardCopy (Join-Path $yardClone $relative) (Join-Path $yardProject $relative)}
    foreach($relative in $yardShared){YardCopy (Join-Path $yardClone $relative) (Join-Path $yardProject $relative)}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $yardClone $yardEvidence) -File -Recurse){YardCopy $file.FullName (Join-Path $yardProject ([IO.Path]::GetRelativePath($yardClone,$file.FullName)))}
    foreach($file in Get-ChildItem -LiteralPath $qa -File){YardCopy $file.FullName (Join-Path $yardProject ($yardEvidence+'/'+$file.Name))}
    Write-Output "FORGOTTEN YARD APPLIED. Recoverable backup: $backup";return
}
$log=Join-Path $yardPipeline ($Mode+'.log')
$method=if($Mode -eq 'Play'){'TownForgottenYardPlay.Run'}else{'TownForgottenYardBatch.'+$Mode}
if($Details){$method=if($Mode -eq 'Play'){'TownForgottenYardPlay.RunDetails'}else{'TownYardStoryBatch.'+$Mode}}
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$yardClone+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "Forgotten yard $Mode launched. PID $($process.Id); log $log"
