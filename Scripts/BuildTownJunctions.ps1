param([ValidateSet('Survey','Generate','Review','Play','Apply')][string]$Mode='Survey')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$junctionProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$junctionClone=Join-Path $junctionProject 'Library/CityUpdateQA/IsolatedProject'
$junctionPipeline=Join-Path $junctionProject 'Library/TownJunctionPipeline'
$junctionArt='Assets/Art/TownJunctions'
$junctionPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$junctionEvidence='Docs/TownJunctionReview'
New-Item -ItemType Directory -Path $junctionPipeline -Force | Out-Null
function JunctionHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function JunctionCopy([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function JunctionInputs {
    $files=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $junctionProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($junctionProject,$file.FullName).Replace('\','/')
            if($relative -eq ($junctionArt+'.meta') -or $relative.StartsWith($junctionArt+'/')){continue}
            $files[$relative]=JunctionHash $file.FullName
        }
    }
    return $files
}
if(Test-Path -LiteralPath (Join-Path $junctionClone 'Temp/UnityLockfile')){throw 'Isolated Unity is busy.'}
if($Mode -in @('Survey','Generate')){
    $junctionBaseline=JunctionInputs
    [IO.File]::WriteAllText((Join-Path $junctionPipeline 'baseline.json'),($junctionBaseline|ConvertTo-Json -Depth 4))
    foreach($relative in $junctionBaseline.Keys){
        $source=Join-Path $junctionProject $relative;$target=Join-Path $junctionClone $relative
        if(-not(Test-Path -LiteralPath $target) -or (JunctionHash $target) -ne $junctionBaseline[$relative]){JunctionCopy $source $target}
    }
    JunctionCopy (Join-Path $junctionProject $junctionPrefab) (Join-Path $junctionPipeline 'OriginalTown.prefab')
}
if($Mode -eq 'Apply'){
    $junctionQa=Join-Path $junctionClone 'Library/TownJunctionQA'
    $junctionHash=JunctionHash (Join-Path $junctionClone $junctionPrefab)
    foreach($stage in @('result','review','play')){
        if(-not(Get-Content -LiteralPath (Join-Path $junctionQa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Acceptance failed: $stage"}
        if((Get-Content -LiteralPath (Join-Path $junctionQa ($stage+'-input.sha256')) -Raw).Trim() -ne $junctionHash){throw "Stale prefab: $stage"}
        if($stage -eq 'result'){continue}
        $junctionAccepted=@{}
        foreach($line in Get-Content -LiteralPath (Join-Path $junctionQa ($stage+'-assets.sha256'))){
            if($line -notmatch '^([A-F0-9]{64}) (Assets/.+)$'){throw 'Invalid asset receipt.'}
            $expected=$Matches[1];$relative=$Matches[2]
            if($relative.Contains('..') -or $relative.Contains('\') -or ($relative -ne $junctionPrefab -and -not $relative.StartsWith($junctionArt+'/'))){throw "Unexpected accepted asset: $relative"}
            if($junctionAccepted.ContainsKey($relative)){throw "Duplicate accepted asset: $relative"}
            if((JunctionHash (Join-Path $junctionClone $relative)) -ne $expected){throw "Stale accepted asset: $relative"}
            $junctionAccepted[$relative]=$expected
        }
        $junctionOutputs=@(Get-ChildItem -LiteralPath (Join-Path $junctionClone $junctionArt) -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($junctionClone,$_.FullName).Replace('\','/')})+@($junctionPrefab)
        if($junctionOutputs.Count -ne $junctionAccepted.Count){throw "Incomplete accepted asset set: $stage"}
        foreach($relative in $junctionOutputs){if(-not $junctionAccepted.ContainsKey($relative)){throw "Unreviewed asset: $relative"}}
    }
    $junctionBaseline=Get-Content -LiteralPath (Join-Path $junctionPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $junctionCurrent=JunctionInputs
    foreach($relative in $junctionBaseline.Keys){if(-not $junctionCurrent.ContainsKey($relative) -or $junctionCurrent[$relative] -ne $junctionBaseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $junctionCurrent.Keys){if(-not $junctionBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $junctionBackup=Join-Path $junctionPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    JunctionCopy (Join-Path $junctionProject $junctionPrefab) (Join-Path $junctionBackup $junctionPrefab)
    foreach($scope in @($junctionArt,$junctionEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $junctionClone $scope) -Recurse -File){
            $relative=[IO.Path]::GetRelativePath($junctionClone,$file.FullName);$target=Join-Path $junctionProject $relative
            if(Test-Path -LiteralPath $target){JunctionCopy $target (Join-Path $junctionBackup $relative)}
            JunctionCopy $file.FullName $target
        }
    }
    JunctionCopy (Join-Path $junctionClone ($junctionArt+'.meta')) (Join-Path $junctionProject ($junctionArt+'.meta'))
    foreach($scope in @('Assets/Project/Editor','Assets/Project/Scripts/City')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $junctionClone $scope) -Recurse -File -Filter 'TownJunction*.cs.meta'){
            $relative=[IO.Path]::GetRelativePath($junctionClone,$file.FullName);JunctionCopy $file.FullName (Join-Path $junctionProject $relative)
        }
    }
    JunctionCopy (Join-Path $junctionClone $junctionPrefab) (Join-Path $junctionProject $junctionPrefab)
    foreach($file in Get-ChildItem -LiteralPath $junctionQa -File){JunctionCopy $file.FullName (Join-Path $junctionProject ($junctionEvidence+'/'+$file.Name))}
    [IO.File]::WriteAllText((Join-Path $junctionPipeline 'applied.json'),(@{backup=$junctionBackup;prefab=$junctionHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "JUNCTIONS APPLIED. Backup: $junctionBackup";return
}
$junctionMethod=if($Mode -eq 'Survey'){'TownJunctionSurvey.Run'}elseif($Mode -eq 'Play'){'TownJunctionPlay.Run'}else{'TownJunctionBatch.'+$Mode}
$junctionLog=Join-Path $junctionPipeline ($Mode+'.log')
$junctionProcess=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$junctionClone+'"'),'-executeMethod',$junctionMethod,'-logFile',('"'+$junctionLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Junction $Mode launched. PID $($junctionProcess.Id); log $junctionLog"
