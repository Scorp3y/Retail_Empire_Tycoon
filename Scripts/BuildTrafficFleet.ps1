param([ValidateSet('Build','Sync','Review','Play','Apply')][string]$Mode='Build')
$ErrorActionPreference='Stop'
$fleetProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$fleetClone=Join-Path $fleetProject 'Library/CityUpdateQA/IsolatedProject'
$fleetStore=Join-Path $fleetProject 'Library/TrafficFleetPipeline'
$fleetRoot='Assets/Art/TrafficFleet'
$fleetTown='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
New-Item -ItemType Directory -Path $fleetStore -Force|Out-Null
function FleetHash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
function FleetCopy([string]$source,[string]$destination){
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))|Out-Null
    if((Test-Path -LiteralPath $destination) -and (FleetHash $source) -eq (FleetHash $destination)){return}
    [IO.File]::Copy($source,$destination,$true)
}
function FleetInputs {
    $result=@{}
    foreach($dir in @('Assets','Packages','ProjectSettings')){
        foreach($f in Get-ChildItem -LiteralPath (Join-Path $fleetProject $dir) -File -Recurse){
            $r=[IO.Path]::GetRelativePath($fleetProject,$f.FullName).Replace('\','/')
            if($r -eq ($fleetRoot+'.meta') -or $r.StartsWith($fleetRoot+'/')){continue}
            $result[$r]=FleetHash $f.FullName
        }
    }
    return $result
}
if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"|Where-Object {$_.CommandLine -and $_.CommandLine.Contains($fleetClone)}).Count){throw 'Isolated Unity is busy'}
$baselinePath=Join-Path $fleetStore 'baseline.json'
if($Mode -eq 'Build'){
    if(-not(Test-Path -LiteralPath $baselinePath)){
        $baseline=FleetInputs
        foreach($r in $baseline.Keys){FleetCopy (Join-Path $fleetProject $r) (Join-Path $fleetClone $r)}
        FleetCopy (Join-Path $fleetProject $fleetTown) (Join-Path $fleetStore 'OriginalTown.prefab')
    }else{
        $baseline=Get-Content -LiteralPath $baselinePath -Raw|ConvertFrom-Json -AsHashtable
        if((FleetHash (Join-Path $fleetProject $fleetTown)) -ne $baseline[$fleetTown]){throw 'Town changed since baseline'}
        foreach($r in @($baseline.Keys|Where-Object {$_ -like '*Traffic*.cs' -or $_ -like '*Traffic*.cs.meta'})){
            FleetCopy (Join-Path $fleetProject $r) (Join-Path $fleetClone $r);$baseline[$r]=FleetHash (Join-Path $fleetProject $r)
        }
    }
    [IO.File]::WriteAllText($baselinePath,($baseline|ConvertTo-Json -Depth 4))
    foreach($f in Get-ChildItem -LiteralPath (Join-Path $fleetProject ($fleetRoot+'/Models')) -File -Recurse){
        $r=[IO.Path]::GetRelativePath($fleetProject,$f.FullName);FleetCopy $f.FullName (Join-Path $fleetClone $r)
    }
    foreach($r in @('ArtSource/TrafficFleet/fleet-report.json','Scripts/Blender/BuildTrafficFleet.py')){FleetCopy (Join-Path $fleetProject $r) (Join-Path $fleetClone $r)}
    $sources=@{};foreach($f in @(Get-Item (Join-Path $fleetProject 'ArtSource/TrafficFleet/TrafficFleet.blend'))+@(Get-Item (Join-Path $fleetProject 'ArtSource/TrafficFleet/fleet-report.json'))+@(Get-ChildItem (Join-Path $fleetProject ($fleetRoot+'/Models')) -File -Filter '*.fbx')){$sources[[IO.Path]::GetRelativePath($fleetProject,$f.FullName)]=FleetHash $f.FullName}
    [IO.File]::WriteAllText((Join-Path $fleetStore 'sources.json'),($sources|ConvertTo-Json))
    FleetCopy (Join-Path $fleetStore 'OriginalTown.prefab') (Join-Path $fleetClone $fleetTown)
}
if($Mode -eq 'Sync'){
    $baseline=Get-Content -LiteralPath $baselinePath -Raw|ConvertFrom-Json -AsHashtable
    foreach($dir in @('Assets/Project/Scripts/City','Assets/Project/Editor')){
        foreach($f in Get-ChildItem -LiteralPath (Join-Path $fleetProject $dir) -File -Filter '*Traffic*.cs*'){
            $r=[IO.Path]::GetRelativePath($fleetProject,$f.FullName).Replace('\','/')
            FleetCopy $f.FullName (Join-Path $fleetClone $r);$baseline[$r]=FleetHash $f.FullName
            if($r.EndsWith('.cs')){
                $meta=$r+'.meta';$mainMeta=Join-Path $fleetProject $meta;$cloneMeta=Join-Path $fleetClone $meta
                if(-not(Test-Path -LiteralPath $mainMeta) -and (Test-Path -LiteralPath $cloneMeta)){FleetCopy $cloneMeta $mainMeta}
                if(Test-Path -LiteralPath $mainMeta){FleetCopy $mainMeta $cloneMeta;$baseline[$meta]=FleetHash $mainMeta}
            }
        }
    }
    $crossingCode='Assets/Project/Scripts/City/TownLevelCrossing.cs'
    FleetCopy (Join-Path $fleetProject $crossingCode) (Join-Path $fleetClone $crossingCode);$baseline[$crossingCode]=FleetHash (Join-Path $fleetProject $crossingCode)
    [IO.File]::WriteAllText($baselinePath,($baseline|ConvertTo-Json -Depth 4));return
}
if($Mode -eq 'Apply'){
    $qa=Join-Path $fleetClone 'Library/TrafficFleetQA';$outputs=@()
    foreach($stage in @('review','play')){
        if(-not(Get-Content -LiteralPath (Join-Path $qa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "No passing $stage"}
        $paths=@();foreach($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-assets.sha256'))){
            $hash,$r=$line -split ' ',2
            if($r -ne $fleetTown -and $r -ne ($fleetRoot+'.meta') -and -not $r.StartsWith($fleetRoot+'/')){throw 'Unexpected output'}
            if((FleetHash (Join-Path $fleetClone $r)) -ne $hash){throw "Stale $stage bytes: $r"};$paths+=$r
        }
        if($outputs.Count -and (Compare-Object $outputs $paths)){throw 'Receipt mismatch'};$outputs=$paths
    }
    $baseline=Get-Content -LiteralPath $baselinePath -Raw|ConvertFrom-Json -AsHashtable
    foreach($r in $baseline.Keys){if((FleetHash (Join-Path $fleetProject $r)) -ne $baseline[$r]){throw "Protected main input changed: $r"}}
    $sources=Get-Content -LiteralPath (Join-Path $fleetStore 'sources.json') -Raw|ConvertFrom-Json -AsHashtable
    foreach($r in $sources.Keys){if((FleetHash (Join-Path $fleetProject $r)) -ne $sources[$r]){throw "Blender source changed: $r"}}
    # Runtime/editor source must be exactly the version used by both native validation stages.
    foreach($stage in @('review','play')){
        foreach($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-code.sha256'))){$hash,$r=$line -split ' ',2;if((FleetHash (Join-Path $fleetProject $r)) -ne $hash){throw "Stale tested code: $r"}}
    }
    $backup=Join-Path $fleetStore ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    FleetCopy (Join-Path $fleetProject $fleetTown) (Join-Path $backup $fleetTown)
    foreach($r in $outputs|Where-Object {$_ -ne $fleetTown}){
        $target=Join-Path $fleetProject $r
        if(Test-Path -LiteralPath $target){FleetCopy $target (Join-Path $backup $r)}
        FleetCopy (Join-Path $fleetClone $r) $target
    }
    FleetCopy (Join-Path $fleetClone $fleetTown) (Join-Path $fleetProject $fleetTown)
    foreach($dir in @('Library/TrafficFleetQA','Docs/TrafficFleetReview')){
        foreach($f in Get-ChildItem -LiteralPath (Join-Path $fleetClone $dir) -File){FleetCopy $f.FullName (Join-Path $fleetProject ('Docs/TrafficFleetReview/'+$f.Name))}
    }
    Write-Output "APPLIED; recoverable backup: $backup";return
}
$lock=Join-Path $fleetClone 'Temp/UnityLockfile';if(Test-Path -LiteralPath $lock){Remove-Item -LiteralPath $lock -Force}
$log=Join-Path $fleetStore ($Mode+'.log')
$process=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$fleetClone+'"'),'-executeMethod',('TrafficFleet'+$(if($Mode -eq 'Play'){'Play.Run'}else{'Batch.'+$Mode})),'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "$Mode PID $($process.Id); $log"
