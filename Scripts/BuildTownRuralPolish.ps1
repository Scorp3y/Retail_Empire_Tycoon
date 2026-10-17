param([ValidateSet('Survey','Generate','Review','Play','Apply')][string]$Mode='Survey')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$ruralProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ruralClone=Join-Path $ruralProject 'Library/CityUpdateQA/IsolatedProject'
$ruralPipeline=Join-Path $ruralProject 'Library/TownRuralPolishPipeline'
$ruralArt='Assets/Art/TownRuralPolish'
$ruralPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$ruralEvidence='Docs/TownRuralPolishReview'
New-Item -ItemType Directory -Path $ruralPipeline -Force | Out-Null
function RuralHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function RuralCopy([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function RuralInputs {
    $files=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){foreach($file in Get-ChildItem -LiteralPath (Join-Path $ruralProject $scope) -File -Recurse){
        $relative=[IO.Path]::GetRelativePath($ruralProject,$file.FullName).Replace('\','/')
        if($relative -eq ($ruralArt+'.meta') -or $relative.StartsWith($ruralArt+'/')){continue}
        $files[$relative]=RuralHash $file.FullName
    }}
    return $files
}
if(Test-Path -LiteralPath (Join-Path $ruralClone 'Temp/UnityLockfile')){throw 'Isolated Unity is busy.'}
if($Mode -in @('Survey','Generate')){
    $ruralBaseline=RuralInputs
    [IO.File]::WriteAllText((Join-Path $ruralPipeline 'baseline.json'),($ruralBaseline|ConvertTo-Json -Depth 4))
    foreach($relative in $ruralBaseline.Keys){$source=Join-Path $ruralProject $relative;$target=Join-Path $ruralClone $relative;if(-not(Test-Path -LiteralPath $target) -or (RuralHash $target) -ne $ruralBaseline[$relative]){RuralCopy $source $target}}
    RuralCopy (Join-Path $ruralProject $ruralPrefab) (Join-Path $ruralPipeline 'OriginalTown.prefab')
}
if($Mode -eq 'Apply'){
    $ruralQa=Join-Path $ruralClone 'Library/TownRuralPolishQA'
    $ruralHash=RuralHash (Join-Path $ruralClone $ruralPrefab)
    foreach($stage in @('result','review','play')){
        if(-not(Get-Content -LiteralPath (Join-Path $ruralQa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Acceptance failed: $stage"}
        if((Get-Content -LiteralPath (Join-Path $ruralQa ($stage+'-input.sha256')) -Raw).Trim() -ne $ruralHash){throw "Stale prefab: $stage"}
        if($stage -eq 'result'){continue}
        $ruralAccepted=@{}
        foreach($line in Get-Content -LiteralPath (Join-Path $ruralQa ($stage+'-assets.sha256'))){
            if($line -notmatch '^([A-F0-9]{64}) (Assets/.+)$'){throw 'Invalid asset receipt.'}
            $expected=$Matches[1];$relative=$Matches[2]
            if($relative.Contains('..') -or $relative.Contains('\') -or ($relative -ne $ruralPrefab -and -not $relative.StartsWith($ruralArt+'/'))){throw "Unexpected accepted asset: $relative"}
            if($ruralAccepted.ContainsKey($relative)){throw "Duplicate accepted asset: $relative"}
            if((RuralHash (Join-Path $ruralClone $relative)) -ne $expected){throw "Stale accepted asset: $relative"}
            $ruralAccepted[$relative]=$expected
        }
        $ruralOutputs=@(Get-ChildItem -LiteralPath (Join-Path $ruralClone $ruralArt) -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($ruralClone,$_.FullName).Replace('\','/')})+@($ruralPrefab)
        if($ruralOutputs.Count -ne $ruralAccepted.Count){throw "Incomplete accepted asset set: $stage"}
        foreach($relative in $ruralOutputs){if(-not $ruralAccepted.ContainsKey($relative)){throw "Unreviewed asset: $relative"}}
    }
    $ruralBaseline=Get-Content -LiteralPath (Join-Path $ruralPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $ruralCurrent=RuralInputs
    foreach($relative in $ruralBaseline.Keys){if(-not $ruralCurrent.ContainsKey($relative) -or $ruralCurrent[$relative] -ne $ruralBaseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $ruralCurrent.Keys){if(-not $ruralBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $ruralBackup=Join-Path $ruralPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    RuralCopy (Join-Path $ruralProject $ruralPrefab) (Join-Path $ruralBackup $ruralPrefab)
    foreach($scope in @($ruralArt,$ruralEvidence)){foreach($file in Get-ChildItem -LiteralPath (Join-Path $ruralClone $scope) -Recurse -File){$relative=[IO.Path]::GetRelativePath($ruralClone,$file.FullName);$target=Join-Path $ruralProject $relative;if(Test-Path -LiteralPath $target){RuralCopy $target (Join-Path $ruralBackup $relative)};RuralCopy $file.FullName $target}}
    RuralCopy (Join-Path $ruralClone ($ruralArt+'.meta')) (Join-Path $ruralProject ($ruralArt+'.meta'))
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $ruralClone 'Assets/Project/Editor') -Recurse -File -Filter 'TownRural*.cs.meta'){$relative=[IO.Path]::GetRelativePath($ruralClone,$file.FullName);RuralCopy $file.FullName (Join-Path $ruralProject $relative)}
    RuralCopy (Join-Path $ruralClone $ruralPrefab) (Join-Path $ruralProject $ruralPrefab)
    foreach($file in Get-ChildItem -LiteralPath $ruralQa -File){RuralCopy $file.FullName (Join-Path $ruralProject ($ruralEvidence+'/'+$file.Name))}
    [IO.File]::WriteAllText((Join-Path $ruralPipeline 'applied.json'),(@{backup=$ruralBackup;prefab=$ruralHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "RURAL POLISH APPLIED. Backup: $ruralBackup";return
}
$ruralMethod=if($Mode -eq 'Survey'){'TownRuralArtSurvey.Run'}elseif($Mode -eq 'Play'){'TownRuralPolishPlay.Run'}else{'TownRuralPolishBatch.'+$Mode}
$ruralLog=Join-Path $ruralPipeline ($Mode+'.log')
$ruralProcess=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$ruralClone+'"'),'-executeMethod',$ruralMethod,'-logFile',('"'+$ruralLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Rural polish $Mode launched. PID $($ruralProcess.Id); log $ruralLog"
