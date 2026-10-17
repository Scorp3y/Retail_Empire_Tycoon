param([ValidateSet('Build','Sync','Review','Play','Apply')][string]$Mode='Build')
$ErrorActionPreference='Stop'
$trainProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$trainClone=Join-Path $trainProject 'Library/CityUpdateQA/IsolatedProject'
$trainStore=Join-Path $trainProject 'Library/RegionalTrainPipeline'
$trainRoot='Assets/Art/RegionalTrain'
$trainTown='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$trainEditor='Assets/Project/Editor/RegionalTrainBatch.cs'
New-Item -ItemType Directory -Path $trainStore -Force|Out-Null
function TrainHash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
function TrainCopy([string]$source,[string]$target){
    $source=[IO.Path]::GetFullPath($source);$target=[IO.Path]::GetFullPath($target)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
    if((Test-Path -LiteralPath $target) -and (TrainHash $source) -eq (TrainHash $target)){return}
    $stage=Join-Path ([IO.Path]::GetDirectoryName($target)) ('.train-'+[guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy($source,$stage)
        if([IO.File]::Exists($target)){
            try{[IO.File]::Replace($stage,$target,[NullString]::Value,$true)}catch{
                $isClone=$target.StartsWith(($trainClone+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)
                if(-not $isClone -and $target -ne (Join-Path $trainProject $trainTown)){throw}
                $retained=Join-Path $trainStore ('Retained_'+[guid]::NewGuid().ToString('N')+'.bak')
                [IO.File]::Move($target,$retained)
                try{[IO.File]::Move($stage,$target)}catch{if(-not [IO.File]::Exists($target)){[IO.File]::Move($retained,$target)};throw}
            }
        }else{[IO.File]::Move($stage,$target)}
    }finally{if(Test-Path -LiteralPath $stage){Remove-Item -LiteralPath $stage -Force}}
}
function TrainInputs {
    $result=@{}
    foreach($dir in @('Assets','Packages','ProjectSettings')){
        foreach($f in Get-ChildItem -LiteralPath (Join-Path $trainProject $dir) -File -Recurse){
            $r=[IO.Path]::GetRelativePath($trainProject,$f.FullName).Replace('\','/')
            if($r -eq ($trainRoot+'.meta') -or $r.StartsWith($trainRoot+'/')){continue}
            $result[$r]=TrainHash $f.FullName
        }
    }
    return $result
}
$trainBusy=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"|Where-Object {$_.CommandLine -and $_.CommandLine.Contains($trainClone)})
if($trainBusy.Count){throw 'Disposable Unity clone is busy'}
$trainBaseline=Join-Path $trainStore 'baseline.json'
if($Mode -eq 'Build'){
    if(-not(Test-Path -LiteralPath $trainBaseline)){
        $baseline=TrainInputs
        foreach($r in $baseline.Keys){TrainCopy (Join-Path $trainProject $r) (Join-Path $trainClone $r)}
        TrainCopy (Join-Path $trainProject $trainTown) (Join-Path $trainStore 'OriginalTown.prefab')
    }else{
        $baseline=Get-Content -LiteralPath $trainBaseline -Raw|ConvertFrom-Json -AsHashtable
        if((TrainHash (Join-Path $trainProject $trainTown)) -ne $baseline[$trainTown]){throw 'Main town changed since snapshot'}
        foreach($r in @($trainEditor,($trainEditor+'.meta'))){if(Test-Path -LiteralPath (Join-Path $trainProject $r)){TrainCopy (Join-Path $trainProject $r) (Join-Path $trainClone $r);$baseline[$r]=TrainHash (Join-Path $trainProject $r)}}
    }
    foreach($f in Get-ChildItem -LiteralPath (Join-Path $trainProject ($trainRoot+'/Models')) -File -Recurse){
        $r=[IO.Path]::GetRelativePath($trainProject,$f.FullName);TrainCopy $f.FullName (Join-Path $trainClone $r)
    }
    foreach($r in @(($trainRoot+'.meta'),($trainRoot+'/Models.meta'))){if(Test-Path -LiteralPath (Join-Path $trainProject $r)){TrainCopy (Join-Path $trainProject $r) (Join-Path $trainClone $r)}}
    TrainCopy (Join-Path $trainStore 'OriginalTown.prefab') (Join-Path $trainClone $trainTown)
    [IO.File]::WriteAllText($trainBaseline,($baseline|ConvertTo-Json -Depth 4))
    $sources=@{};foreach($r in @('Scripts/Blender/BuildRegionalTrain.py','ArtSource/RegionalTrain/RegionalTrain.blend','ArtSource/RegionalTrain/build-report.json',($trainRoot+'/Models/RegionalCab.fbx'),($trainRoot+'/Models/RegionalCoach.fbx'))){$sources[$r]=TrainHash (Join-Path $trainProject $r)}
    [IO.File]::WriteAllText((Join-Path $trainStore 'sources.json'),($sources|ConvertTo-Json))
}
if($Mode -eq 'Sync'){
    $baseline=Get-Content -LiteralPath $trainBaseline -Raw|ConvertFrom-Json -AsHashtable
    TrainCopy (Join-Path $trainProject $trainEditor) (Join-Path $trainClone $trainEditor)
    $baseline[$trainEditor]=TrainHash (Join-Path $trainProject $trainEditor)
    [IO.File]::WriteAllText($trainBaseline,($baseline|ConvertTo-Json -Depth 4));return
}
if($Mode -eq 'Apply'){
    $outputs=@();$qa=Join-Path $trainClone 'Library/RegionalTrainQA'
    foreach($stage in @('build','review','play')){
        if(-not(Get-Content -LiteralPath (Join-Path $qa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Failed $stage"}
        $paths=@();foreach($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-assets.sha256'))){
            $h,$r=$line -split ' ',2
            if($r -ne $trainTown -and $r -ne ($trainRoot+'.meta') -and -not $r.StartsWith($trainRoot+'/')){throw 'Unexpected output'}
            # URP adds its material-version subasset after initial authoring. Build records
            # generation; Review and Play must both accept the exact finalized material bytes.
            $finalizedUrpMaterial=$stage -eq 'build' -and $r -eq ($trainRoot+'/Materials/Train.mat')
            if((TrainHash (Join-Path $trainClone $r)) -ne $h -and -not $finalizedUrpMaterial){throw "Stale $stage output: $r"};$paths+=$r
        }
        if($outputs.Count -and (Compare-Object $outputs $paths)){throw 'Receipt coverage mismatch'};$outputs=$paths
        if((Get-Content -LiteralPath (Join-Path $trainStore ($stage+'-source.sha256')) -Raw).Trim() -ne (TrainHash (Join-Path $trainProject $trainEditor))){throw "Stale $stage editor source"}
        if((Get-Item -LiteralPath (Join-Path $qa ($stage+'.txt'))).LastWriteTimeUtc -lt (Get-Item -LiteralPath (Join-Path $trainStore ($stage+'-source.sha256'))).LastWriteTimeUtc){throw "No fresh $stage result"}
    }
    $sources=Get-Content -LiteralPath (Join-Path $trainStore 'sources.json') -Raw|ConvertFrom-Json -AsHashtable
    foreach($r in $sources.Keys){
        $currentHash=TrainHash (Join-Path $trainProject $r)
        if($currentHash -eq $sources[$r]){continue}
        # A Blender UI resave need not invalidate tested FBX. Never regenerate an artist's
        # saved work: require a read-only comparison of every module's geometry and colors.
        if($r -ne 'ArtSource/RegionalTrain/RegionalTrain.blend'){throw "Blender source changed: $r"}
        $audit=Get-Content -LiteralPath (Join-Path $trainStore 'source-audit.json') -Raw|ConvertFrom-Json -AsHashtable
        if($audit.status -ne 'PASS' -or $audit.blend_sha256 -ne $currentHash -or $audit.auditor_sha256 -ne (TrainHash (Join-Path $trainProject 'Scripts/Blender/AuditRegionalTrain.py'))){throw 'Saved Blender source has not passed a current read-only audit'}
        foreach($id in @('RegionalCab','RegionalCoach')){
            if($audit.models[$id].fbx_sha256 -ne $sources[($trainRoot+'/Models/'+$id+'.fbx')]){throw "Source audit used a different FBX: $id"}
        }
    }
    $baseline=Get-Content -LiteralPath $trainBaseline -Raw|ConvertFrom-Json -AsHashtable;$current=TrainInputs
    foreach($r in $baseline.Keys){if(-not $current.ContainsKey($r) -or $current[$r] -ne $baseline[$r]){throw "Protected main input changed: $r"}}
    foreach($r in $current.Keys){if(-not $baseline.ContainsKey($r) -and -not $r.EndsWith('.meta')){throw "Unexpected main input: $r"}}
    $backup=Join-Path $trainStore ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    TrainCopy (Join-Path $trainProject $trainTown) (Join-Path $backup $trainTown)
    foreach($r in $outputs|Where-Object {$_ -ne $trainTown}){TrainCopy (Join-Path $trainClone $r) (Join-Path $trainProject $r)}
    TrainCopy (Join-Path $trainClone $trainTown) (Join-Path $trainProject $trainTown)
    foreach($dir in @('Library/RegionalTrainQA','Docs/TrainBlenderReview')){foreach($f in Get-ChildItem -LiteralPath (Join-Path $trainClone $dir) -File){TrainCopy $f.FullName (Join-Path $trainProject ('Docs/TrainBlenderReview/'+$f.Name))}}
    if(Test-Path -LiteralPath (Join-Path $trainStore 'source-audit.json')){TrainCopy (Join-Path $trainStore 'source-audit.json') (Join-Path $trainProject 'Docs/TrainBlenderReview/source-audit.json')}
    Write-Output "APPLIED; backup: $backup";return
}
if((TrainHash (Join-Path $trainProject $trainEditor)) -ne (TrainHash (Join-Path $trainClone $trainEditor))){throw 'Main/clone importer source mismatch'}
[IO.File]::WriteAllText((Join-Path $trainStore ($Mode.ToLower()+'-source.sha256')),(TrainHash (Join-Path $trainProject $trainEditor)))
$lock=Join-Path $trainClone 'Temp/UnityLockfile';if(Test-Path -LiteralPath $lock){Remove-Item -LiteralPath $lock -Force}
$log=Join-Path $trainStore ($Mode+'.log')
$p=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$trainClone+'"'),'-executeMethod',('RegionalTrainBatch.'+$Mode),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "$Mode PID $($p.Id); $log"
