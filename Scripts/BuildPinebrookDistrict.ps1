param([ValidateSet('Prepare','Generate','Review','Play','Apply','BuildAndApply')][string]$Mode='Prepare')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$pinebrookProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pinebrookClone=Join-Path $pinebrookProject 'Library/CityUpdateQA/IsolatedProject'
$pinebrookPipeline=Join-Path $pinebrookProject 'Library/PinebrookDistrictPipeline'
$pinebrookPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$pinebrookArt='Assets/Art/Pinebrook'
New-Item -ItemType Directory -Path $pinebrookPipeline -Force | Out-Null
function PinebrookHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function PinebrookCopy([string]$source,[string]$target){
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
    if((Test-Path -LiteralPath $target) -and (PinebrookHash $source) -eq (PinebrookHash $target)){return}
    # Replace a directory entry instead of truncating an image/asset mapped by an open viewer.
    $temporary=Join-Path ([IO.Path]::GetDirectoryName($target)) ('.'+[IO.Path]::GetFileName($target)+'.'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy($source,$temporary)
        # Asset/image indexing can briefly hold the destination without delete sharing.
        # Retry the atomic replacement; never fall back to truncating a mapped asset.
        for($copyAttempt=0;$copyAttempt -lt 8;$copyAttempt++){
            try{
                if(Test-Path -LiteralPath $target){[IO.File]::Replace($temporary,$target,[NullString]::Value)}else{[IO.File]::Move($temporary,$target)}
                break
            }catch{
                if($copyAttempt -eq 7 -or $_.Exception.InnerException -isnot [IO.IOException]){throw}
                Start-Sleep -Milliseconds (250+50*$copyAttempt)
            }
        }
    }catch{throw "Could not copy Pinebrook file '$target': $($_.Exception.Message)"}
    finally{if(Test-Path -LiteralPath $temporary){Remove-Item -LiteralPath $temporary -Force}}
}
function PinebrookPrepare {
    if(Test-Path -LiteralPath (Join-Path $pinebrookClone 'Temp/UnityLockfile')){throw 'Isolated Unity is busy.'}
    $python=Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    if(-not(Test-Path -LiteralPath $python)){$python=(Get-Command python.exe -ErrorAction Stop).Source}
    & $python (Join-Path $pinebrookProject 'Scripts/PreparePinebrookInputs.py')
    if($LASTEXITCODE -ne 0){throw 'Could not refresh the isolated project inputs.'}
    $baseline=@{prefab=(PinebrookHash (Join-Path $pinebrookProject $pinebrookPrefab));utc=[DateTime]::UtcNow.ToString('O')}
    [IO.File]::WriteAllText((Join-Path $pinebrookPipeline 'baseline.json'),($baseline|ConvertTo-Json))
    PinebrookCopy (Join-Path $pinebrookProject $pinebrookPrefab) (Join-Path $pinebrookPipeline 'OriginalTown.prefab')
    PinebrookCopy (Join-Path $pinebrookProject $pinebrookPrefab) (Join-Path $pinebrookClone $pinebrookPrefab)
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $pinebrookProject 'Assets/Project/Editor') -Filter 'Pinebrook*.cs' -File){PinebrookCopy $file.FullName (Join-Path $pinebrookClone ('Assets/Project/Editor/'+$file.Name))}
    foreach($relative in @('Assets/Project/Scripts/City/CityMapGraphic.cs','Assets/Project/Editor/TownPilotScopePreservation.cs')){PinebrookCopy (Join-Path $pinebrookProject $relative) (Join-Path $pinebrookClone $relative)}
    PinebrookCopy (Join-Path $pinebrookProject ($pinebrookArt+'/Layout/PinebrookLayout.json')) (Join-Path $pinebrookClone ($pinebrookArt+'/Layout/PinebrookLayout.json'))
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $pinebrookProject 'ArtSource/Pinebrook/DistrictModels') -File){if($file.Extension -eq '.fbx' -or $file.Name -eq 'PinebrookPalette.png' -or $file.Name -eq 'Models.json'){PinebrookCopy $file.FullName (Join-Path $pinebrookClone ($pinebrookArt+'/Models/'+$file.Name))}}
}
function PinebrookRun([string]$stage) {
    $method=if($stage -eq 'Play'){'PinebrookDistrictPlay.Run'}else{'PinebrookDistrictBuild.'+$stage}
    $log=Join-Path $pinebrookPipeline ($stage+'.log')
    $process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$pinebrookClone+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
    $process.WaitForExit();if($process.ExitCode -ne 0){throw "Pinebrook $stage failed. See $log"}
}
function PinebrookApply {
    $baseline=Get-Content -LiteralPath (Join-Path $pinebrookPipeline 'baseline.json') -Raw | ConvertFrom-Json
    if((PinebrookHash (Join-Path $pinebrookProject $pinebrookPrefab)) -ne $baseline.prefab){throw 'The main town changed during generation; refusing to overwrite it.'}
    $qa=Join-Path $pinebrookClone 'Library/PinebrookDistrictQA';$prefabHash=PinebrookHash (Join-Path $pinebrookClone $pinebrookPrefab)
    foreach($stage in @('result','review','play')) {
        if(-not(Get-Content -LiteralPath (Join-Path $qa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Pinebrook $stage did not pass."}
        if((Get-Content -LiteralPath (Join-Path $qa ($stage+'-prefab.sha256')) -Raw).Trim() -ne $prefabHash){throw "Stale $stage prefab receipt."}
        foreach($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-assets.sha256'))) {
            if($line -notmatch '^([A-F0-9]{64}) (Assets/Art/Pinebrook/.+)$'){throw 'Invalid owned asset receipt.'}
            $expected=$Matches[1];$relative=$Matches[2];if($relative.Contains('..') -or $relative.Contains('\')){throw 'Invalid asset path.'}
            if((PinebrookHash (Join-Path $pinebrookClone $relative)) -ne $expected){throw "Asset changed since $stage review: $relative"}
        }
    }
    $pendingPath=Join-Path $pinebrookPipeline 'pending-apply.json'
    if(Test-Path -LiteralPath $pendingPath){
        $pending=Get-Content -LiteralPath $pendingPath -Raw | ConvertFrom-Json
        $backup=[IO.Path]::GetFullPath($pending.backup)
        if($pending.baseline -ne $baseline.prefab -or $pending.prefab -ne $prefabHash -or -not $backup.StartsWith($pinebrookPipeline+[IO.Path]::DirectorySeparatorChar+'BeforeApply_',[StringComparison]::OrdinalIgnoreCase)){throw 'Pending Pinebrook application belongs to a different result.'}
    }else{
        $backup=Join-Path $pinebrookPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
        [IO.File]::WriteAllText($pendingPath,(@{baseline=$baseline.prefab;prefab=$prefabHash;backup=$backup}|ConvertTo-Json))
    }
    if(-not(Test-Path -LiteralPath (Join-Path $backup $pinebrookPrefab))){PinebrookCopy (Join-Path $pinebrookProject $pinebrookPrefab) (Join-Path $backup $pinebrookPrefab)}
    foreach($folder in @($pinebrookArt,'Docs/Pinebrook/Final')) {
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $pinebrookClone $folder) -File -Recurse) {
            $relative=$file.FullName.Substring($pinebrookClone.Length+1);$target=Join-Path $pinebrookProject $relative
            if((Test-Path -LiteralPath $target) -and -not(Test-Path -LiteralPath (Join-Path $backup $relative))){PinebrookCopy $target (Join-Path $backup $relative)}
            PinebrookCopy $file.FullName $target
        }
    }
    if(Test-Path -LiteralPath (Join-Path $pinebrookClone ($pinebrookArt+'.meta'))){PinebrookCopy (Join-Path $pinebrookClone ($pinebrookArt+'.meta')) (Join-Path $pinebrookProject ($pinebrookArt+'.meta'))}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $pinebrookClone 'Assets/Project/Editor') -Filter 'Pinebrook*.cs.meta' -File){PinebrookCopy $file.FullName (Join-Path $pinebrookProject ('Assets/Project/Editor/'+$file.Name))}
    foreach($file in Get-ChildItem -LiteralPath $qa -File){PinebrookCopy $file.FullName (Join-Path $pinebrookProject ('Docs/Pinebrook/Final/QA/'+$file.Name))}
    PinebrookCopy (Join-Path $pinebrookClone $pinebrookPrefab) (Join-Path $pinebrookProject $pinebrookPrefab)
    [IO.File]::WriteAllText((Join-Path $pinebrookPipeline 'applied.json'),(@{prefab=$prefabHash;backup=$backup;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Remove-Item -LiteralPath $pendingPath -Force
    Write-Output "Pinebrook applied. Backup: $backup"
}
switch($Mode) {
    'Prepare' {PinebrookPrepare}
    'Generate' {PinebrookRun 'Generate'}
    'Review' {PinebrookRun 'Review'}
    'Play' {PinebrookRun 'Play'}
    'Apply' {PinebrookApply}
    'BuildAndApply' {PinebrookPrepare;PinebrookRun 'Generate';PinebrookRun 'Review';PinebrookRun 'Play';PinebrookApply}
}
