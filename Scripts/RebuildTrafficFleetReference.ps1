param([ValidateSet('Prepare','Sync','Remake','Review','Play','Apply')][string]$Mode='Prepare')
$ErrorActionPreference='Stop'
$revisionProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$revisionClone=Join-Path $revisionProject 'Library/CityUpdateQA/IsolatedProject'
$revisionStore=Join-Path $revisionProject 'Library/TrafficFleetRevision'
$revisionRoot='Assets/Art/TrafficFleet'
$revisionTown='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
New-Item -ItemType Directory -Path $revisionStore -Force|Out-Null
function RevisionHash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
function RevisionCopy([string]$a,[string]$b){
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($b))|Out-Null
    if((Test-Path -LiteralPath $b) -and (RevisionHash $a) -eq (RevisionHash $b)){return}
    [IO.File]::Copy($a,$b,$true)
}
if(@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"|Where-Object {$_.CommandLine -and $_.CommandLine.Contains($revisionClone)}).Count){throw 'Isolated Unity is busy'}
$protectedPath=Join-Path $revisionStore 'protected.json'
$protectedNames=@($revisionTown,($revisionRoot+'/Data/RoadNetwork.asset'),'Assets/Project/Scenes/City.unity','Assets/Project/Scenes/Game.unity',
    'Assets/Prefabs/StarterStore/StarterStore.asset','Assets/Art/TownForgottenYard/Land24.asset','Assets/Prefabs/TownWorld/TownLayout.asset',
    'Assets/Prefabs/City/Pickup.prefab','Assets/Project/Scripts/City/TownRoadTraffic.cs','Assets/Project/Scripts/City/TrafficVehicle.cs',
    'Assets/Project/Scripts/City/TrafficRoadNetwork.cs','Assets/Project/Scripts/City/TownRailTraffic.cs','Assets/Project/Scripts/City/TownLevelCrossing.cs')
if($Mode -eq 'Prepare'){
    if(Test-Path -LiteralPath $protectedPath){throw 'Revision snapshot already exists; use Sync/Remake to iterate safely'}
    $protected=@{};foreach($r in $protectedNames){$protected[$r]=RevisionHash (Join-Path $revisionProject $r);RevisionCopy (Join-Path $revisionProject $r) (Join-Path $revisionClone $r)}
    [IO.File]::WriteAllText($protectedPath,($protected|ConvertTo-Json))
    foreach($f in Get-ChildItem -LiteralPath (Join-Path $revisionProject $revisionRoot) -File -Recurse){$r=[IO.Path]::GetRelativePath($revisionProject,$f.FullName);RevisionCopy $f.FullName (Join-Path $revisionClone $r)}
    RevisionCopy (Join-Path $revisionProject ($revisionRoot+'.meta')) (Join-Path $revisionClone ($revisionRoot+'.meta'))
    $original=@{};foreach($f in Get-ChildItem -LiteralPath (Join-Path $revisionProject $revisionRoot) -File -Recurse){$r=[IO.Path]::GetRelativePath($revisionProject,$f.FullName).Replace('\','/');$original[$r]=RevisionHash $f.FullName}
    [IO.File]::WriteAllText((Join-Path $revisionStore 'original-assets.json'),($original|ConvertTo-Json))
}
if($Mode -eq 'Prepare' -or $Mode -eq 'Sync'){
    foreach($dir in @('Assets/Project/Scripts/City','Assets/Project/Editor')){
        foreach($f in Get-ChildItem -LiteralPath (Join-Path $revisionProject $dir) -File -Filter '*Traffic*.cs'){
            $r=[IO.Path]::GetRelativePath($revisionProject,$f.FullName)
            RevisionCopy $f.FullName (Join-Path $revisionClone $r)
            $meta=$r+'.meta';$mainMeta=Join-Path $revisionProject $meta;$cloneMeta=Join-Path $revisionClone $meta
            if(-not(Test-Path -LiteralPath $mainMeta) -and (Test-Path -LiteralPath $cloneMeta)){RevisionCopy $cloneMeta $mainMeta}
            if(Test-Path -LiteralPath $mainMeta){RevisionCopy $mainMeta $cloneMeta}
        }
    }
    foreach($f in Get-ChildItem -LiteralPath (Join-Path $revisionProject 'ArtSource/TrafficFleet-v2/Exports') -File -Filter '*.fbx'){
        RevisionCopy $f.FullName (Join-Path $revisionClone ($revisionRoot+'/Models/'+$f.Name))
    }
    RevisionCopy (Join-Path $revisionProject 'ArtSource/TrafficFleet-v2/fleet-report.json') (Join-Path $revisionClone 'ArtSource/TrafficFleet-v2/fleet-report.json')
    $sources=@{};foreach($f in @(Get-ChildItem -LiteralPath (Join-Path $revisionProject 'ArtSource/TrafficFleet-v2') -File -Recurse)+@(Get-Item (Join-Path $revisionProject 'Scripts/Blender/BuildTrafficFleetReference.py'))+@(Get-Item (Join-Path $revisionProject 'Scripts/Blender/BuildTrafficFleet.py'))){$sources[[IO.Path]::GetRelativePath($revisionProject,$f.FullName)]=RevisionHash $f.FullName}
    [IO.File]::WriteAllText((Join-Path $revisionStore 'sources.json'),($sources|ConvertTo-Json))
    Write-Output 'Prepared model-only revision; main traffic assets untouched';return
}
if($Mode -eq 'Apply'){
    $qa=Join-Path $revisionClone 'Library/TrafficFleetQA'
    $outputs=@()
    foreach($stage in @('remake','review','play')){
        if(-not(Get-Content -LiteralPath (Join-Path $qa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Missing passing $stage"}
        foreach($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-assets.sha256'))){
            $hash,$r=$line -split ' ',2
            if($r -ne $revisionTown -and $r -ne ($revisionRoot+'.meta') -and -not $r.StartsWith($revisionRoot+'/')){throw 'Out-of-scope output'}
            if($stage -ne 'remake' -and (RevisionHash (Join-Path $revisionClone $r)) -ne $hash){throw "Stale $stage asset: $r"}
            if($stage -eq 'play'){$outputs+=$r}
        }
        if($stage -ne 'remake'){
            foreach($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-code.sha256'))){$hash,$r=$line -split ' ',2;if((RevisionHash (Join-Path $revisionProject $r)) -ne $hash){throw "Stale tested code: $r"}}
        }
    }
    $protected=Get-Content -LiteralPath $protectedPath -Raw|ConvertFrom-Json -AsHashtable
    foreach($r in $protected.Keys){if((RevisionHash (Join-Path $revisionProject $r)) -ne $protected[$r] -or (RevisionHash (Join-Path $revisionClone $r)) -ne $protected[$r]){throw "Protected traffic/town file changed: $r"}}
    $original=Get-Content -LiteralPath (Join-Path $revisionStore 'original-assets.json') -Raw|ConvertFrom-Json -AsHashtable
    foreach($r in $original.Keys){if((RevisionHash (Join-Path $revisionProject $r)) -ne $original[$r]){throw "Main model edited during revision: $r"}}
    $sources=Get-Content -LiteralPath (Join-Path $revisionStore 'sources.json') -Raw|ConvertFrom-Json -AsHashtable
    foreach($r in $sources.Keys){if((RevisionHash (Join-Path $revisionProject $r)) -ne $sources[$r]){throw "Source changed: $r"}}
    # Existing GUIDs must remain stable, including all twenty prefab identities used by traffic.
    foreach($r in $original.Keys|Where-Object {$_.EndsWith('.meta')}){
        $oldGuid=(Select-String -LiteralPath (Join-Path $revisionProject $r) -Pattern '^guid: ').Line
        $newGuid=(Select-String -LiteralPath (Join-Path $revisionClone $r) -Pattern '^guid: ').Line
        if($oldGuid -ne $newGuid){throw "Asset GUID changed: $r"}
    }
    $backup=Join-Path $revisionStore ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    foreach($r in $outputs|Where-Object {$_ -ne $revisionTown -and -not $_.StartsWith($revisionRoot+'/Data/')}){
        $a=Join-Path $revisionClone $r;$b=Join-Path $revisionProject $r
        if((Test-Path -LiteralPath $b) -and (RevisionHash $a) -eq (RevisionHash $b)){continue}
        if(Test-Path -LiteralPath $b){RevisionCopy $b (Join-Path $backup $r)}
        RevisionCopy $a $b
    }
    foreach($dir in @('Library/TrafficFleetQA','Docs/TrafficFleetReview')){
        foreach($f in Get-ChildItem -LiteralPath (Join-Path $revisionClone $dir) -File -Recurse){$relative=[IO.Path]::GetRelativePath((Join-Path $revisionClone $dir),$f.FullName);RevisionCopy $f.FullName (Join-Path $revisionProject ('Docs/TrafficFleetReferenceReview/'+$relative))}
    }
    Write-Output "APPLIED MODEL REVISION; town/network/runtime unchanged; backup $backup";return
}
$method=switch($Mode){'Remake'{'TrafficFleetBatch.RebuildModels'}'Review'{'TrafficFleetBatch.Review'}'Play'{'TrafficFleetPlay.Run'}}
$lock=Join-Path $revisionClone 'Temp/UnityLockfile';if(Test-Path -LiteralPath $lock){Remove-Item -LiteralPath $lock -Force}
$log=Join-Path $revisionStore ($Mode+'.log')
$process=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$revisionClone+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "$Mode PID $($process.Id); $log"
