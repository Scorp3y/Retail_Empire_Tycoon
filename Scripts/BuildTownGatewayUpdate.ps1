param([ValidateSet('Inspect','Generate','Review','Play','Apply')][string]$Mode='Inspect')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$updateProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$updateClone=Join-Path $updateProject 'Library/CityUpdateQA/IsolatedProject'
$updatePipeline=Join-Path $updateProject 'Library/TownGatewayPipeline'
$updatePrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$updateArt='Assets/Art/TownGatewayUpdate'
$updateEvidence='Docs/TownGatewayReview'
$updateUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
New-Item -ItemType Directory -Path $updatePipeline -Force | Out-Null
function Get-UpdateHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Copy-UpdateFile([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function Get-UpdateInputs {
    $inputs=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $updateProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($updateProject,$file.FullName).Replace('\','/')
            if($relative -eq ($updateArt+'.meta') -or $relative.StartsWith($updateArt+'/')){continue}
            $inputs[$relative]=Get-UpdateHash $file.FullName
        }
    }
    return $inputs
}
if(Test-Path -LiteralPath (Join-Path $updateClone 'Temp/UnityLockfile')){throw 'Isolated Unity is busy; do not interrupt it.'}
if($Mode -in @('Inspect','Generate')){
    $updateBaseline=Get-UpdateInputs
    [IO.File]::WriteAllText((Join-Path $updatePipeline 'baseline.json'),($updateBaseline|ConvertTo-Json -Depth 5))
    foreach($relative in $updateBaseline.Keys){
        $source=Join-Path $updateProject $relative;$target=Join-Path $updateClone $relative
        if(-not(Test-Path -LiteralPath $target) -or (Get-UpdateHash $target) -ne $updateBaseline[$relative]){Copy-UpdateFile $source $target}
    }
    Copy-UpdateFile (Join-Path $updateProject $updatePrefab) (Join-Path $updatePipeline 'OriginalTown.prefab')
}
if($Mode -eq 'Apply'){
    $updateQa=Join-Path $updateClone 'Library/TownGatewayQA'
    $updateHash=Get-UpdateHash (Join-Path $updateClone $updatePrefab)
    foreach($receipt in @('result','review','play')){
        if(-not(Get-Content -LiteralPath (Join-Path $updateQa ($receipt+'.txt')) -Raw).StartsWith('PASS:')){throw "Acceptance failed: $receipt"}
        if((Get-Content -LiteralPath (Join-Path $updateQa ($receipt+'-input.sha256')) -Raw).Trim() -ne $updateHash){throw "Stale acceptance: $receipt"}
        # Unity upgrades a newly-authored URP material on first reopen. Generate proves scope
        # preservation; Review and Play both accept the final imported resources byte-for-byte.
        if($receipt -eq 'result'){continue}
        foreach($line in Get-Content -LiteralPath (Join-Path $updateQa ($receipt+'-assets.sha256'))){
            if($line -notmatch '^([A-F0-9]{64}) (Assets/.+)$'){throw "Invalid asset receipt: $receipt"}
            $expected=$Matches[1];$relative=$Matches[2]
            if($relative.Contains('..') -or (Get-UpdateHash (Join-Path $updateClone $relative)) -ne $expected){throw "Stale accepted asset: $relative"}
        }
    }
    $updateBaseline=Get-Content -LiteralPath (Join-Path $updatePipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $updateCurrent=Get-UpdateInputs
    foreach($relative in $updateBaseline.Keys){if(-not $updateCurrent.ContainsKey($relative) -or $updateCurrent[$relative] -ne $updateBaseline[$relative]){throw "Main input changed: $relative"}}
    $updateBackup=Join-Path $updatePipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    Copy-UpdateFile (Join-Path $updateProject $updatePrefab) (Join-Path $updateBackup $updatePrefab)
    foreach($scope in @($updateArt,$updateEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $updateClone $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($updateClone,$file.FullName);$target=Join-Path $updateProject $relative
            if(Test-Path -LiteralPath $target){Copy-UpdateFile $target (Join-Path $updateBackup $relative)}
            Copy-UpdateFile $file.FullName $target
        }
    }
    Copy-UpdateFile (Join-Path $updateClone ($updateArt+'.meta')) (Join-Path $updateProject ($updateArt+'.meta'))
    foreach($relative in $updateCurrent.Keys){if(-not $updateBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main file: $relative"}}
    foreach($relative in @('Assets/Project/Editor/TownGatewayBatch.cs.meta','Assets/Project/Editor/TownGatewayPlacement.cs.meta','Assets/Project/Editor/TownGatewayPlayValidation.cs.meta','Assets/Project/Editor/TownKit/TownFarmUpdateModels.cs.meta','Assets/Project/Editor/TownKit/TownFuelUpdateModels.cs.meta','Assets/Project/Scripts/City/TownLevelCrossing.cs.meta')){
        Copy-UpdateFile (Join-Path $updateClone $relative) (Join-Path $updateProject $relative)
    }
    Copy-UpdateFile (Join-Path $updateClone $updatePrefab) (Join-Path $updateProject $updatePrefab)
    foreach($file in Get-ChildItem -LiteralPath $updateQa -File){Copy-UpdateFile $file.FullName (Join-Path $updateProject ($updateEvidence+'/'+$file.Name))}
    [IO.File]::WriteAllText((Join-Path $updatePipeline 'applied.json'),(@{backup=$updateBackup;prefab=$updateHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "GATEWAY UPDATE APPLIED. Recoverable backup: $updateBackup";return
}
$updateMethod=if($Mode -eq 'Play'){'TownGatewayPlayValidation.Run'}else{'TownGatewayBatch.'+$Mode}
$updateLog=Join-Path $updatePipeline ($Mode+'.log')
$updateProcess=Start-Process -FilePath $updateUnity -ArgumentList @('-batchmode','-projectPath',('"'+$updateClone+'"'),'-executeMethod',$updateMethod,'-logFile',('"'+$updateLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Gateway update $Mode launched. PID $($updateProcess.Id); log $updateLog"
