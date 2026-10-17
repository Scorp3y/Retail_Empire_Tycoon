param([ValidateSet('Generate','Sync','Review','Drive','Rural','Shop','Play','Apply')][string]$Mode='Generate')
$ErrorActionPreference='Stop'
$lifeProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$lifeClone=Join-Path $lifeProject 'Library/CityUpdateQA/IsolatedProject'
$lifeStore=Join-Path $lifeProject 'Library/TownLifePipeline'
$lifeRoot='Assets/Art/TownLifeUpdate'
$lifeTown='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$lifePickup='Assets/Prefabs/City/Pickup.prefab'
New-Item -ItemType Directory -Path $lifeStore -Force|Out-Null
function LifeHash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
function LifeCopy([string]$a,[string]$b){
    $a=[IO.Path]::GetFullPath($a);$b=[IO.Path]::GetFullPath($b);[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($b))|Out-Null
    if([IO.File]::Exists($b) -and (LifeHash $a) -eq (LifeHash $b)){return}
    $stage=Join-Path ([IO.Path]::GetDirectoryName($b)) ('.life-'+[guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy($a,$stage,$false)
        if([IO.File]::Exists($b)){
            try{[IO.File]::Replace($stage,$b,[NullString]::Value,$true)}catch{
                $allowed=@((Join-Path $lifeProject $lifeTown),(Join-Path $lifeProject $lifePickup),(Join-Path $lifeClone $lifeTown),(Join-Path $lifeClone $lifePickup))
                $isDisposableClone=$b.StartsWith(($lifeClone+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)
                if($b -notin $allowed -and -not $isDisposableClone){throw ("Cannot replace scoped file: "+$b+"; "+$_.Exception.Message)}
                $retained=Join-Path $lifeStore ('Retained_'+[guid]::NewGuid().ToString('N')+'.prefab');[IO.File]::Move($b,$retained)
                try{[IO.File]::Move($stage,$b)}catch{if(-not [IO.File]::Exists($b)){[IO.File]::Move($retained,$b)};throw}
            }
        }else{[IO.File]::Move($stage,$b)}
    }finally{if([IO.File]::Exists($stage)){Remove-Item -LiteralPath $stage -Force}}
}
function Inputs {
    $result=@{};foreach($dir in @('Assets','Packages','ProjectSettings')){foreach($f in Get-ChildItem -LiteralPath (Join-Path $lifeProject $dir) -File -Recurse){$r=[IO.Path]::GetRelativePath($lifeProject,$f.FullName).Replace('\','/');if($r -eq ($lifeRoot+'.meta') -or $r.StartsWith($lifeRoot+'/')){continue};$result[$r]=LifeHash $f.FullName}};return $result
}
function ReviewedInputs {
    $result=@{}
    foreach($f in Get-ChildItem -LiteralPath (Join-Path $lifeProject 'Assets/Project') -Filter '*.cs' -File -Recurse){
        $r=[IO.Path]::GetRelativePath($lifeProject,$f.FullName).Replace('\','/')
        $h=LifeHash $f.FullName
        if($h -ne (LifeHash (Join-Path $lifeClone $r))){throw "Native source differs from main: $r"}
        $result[$r]=$h
    }
    foreach($r in @($lifeTown,$lifePickup)){$result[$r]=LifeHash (Join-Path $lifeClone $r)}
    return $result
}
$busy=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'"|Where-Object {$_.CommandLine -and $_.CommandLine.Contains($lifeClone)})
if($busy.Count){throw 'Native clone is busy'}
$lock=Join-Path $lifeClone 'Temp/UnityLockfile';if(Test-Path -LiteralPath $lock){Remove-Item -LiteralPath $lock -Force}
$baselineFile=Join-Path $lifeStore 'baseline.json'
if($Mode -eq 'Sync'){
    $baseline=Get-Content $baselineFile -Raw|ConvertFrom-Json -AsHashtable
    foreach($r in @('Assets/Project/Editor/TownLifeUpdateBatch.cs','Assets/Project/Editor/TownLifeModels.cs','Assets/Project/Editor/TownLifePlay.cs','Assets/Project/Scripts/City/PickupDrive.cs','Assets/Project/Scripts/City/TownRailTraffic.cs','Assets/Project/Scripts/City/FarmAnimal.cs','Assets/Project/Scripts/City/FarmAnimal.cs.meta')){if(Test-Path -LiteralPath (Join-Path $lifeProject $r)){LifeCopy (Join-Path $lifeProject $r) (Join-Path $lifeClone $r);$baseline[$r]=LifeHash (Join-Path $lifeProject $r)}}
    [IO.File]::WriteAllText($baselineFile,($baseline|ConvertTo-Json -Depth 4));return
}
if($Mode -eq 'Generate'){
    if(-not(Test-Path -LiteralPath $baselineFile)){
        $baseline=Inputs
        foreach($r in $baseline.Keys){LifeCopy (Join-Path $lifeProject $r) (Join-Path $lifeClone $r)}
        LifeCopy (Join-Path $lifeProject $lifeTown) (Join-Path $lifeStore 'OriginalTown.prefab');LifeCopy (Join-Path $lifeProject $lifePickup) (Join-Path $lifeStore 'OriginalPickup.prefab')
    }else{
        $baseline=Get-Content $baselineFile -Raw|ConvertFrom-Json -AsHashtable
        if((LifeHash (Join-Path $lifeProject $lifeTown)) -ne $baseline[$lifeTown] -or (LifeHash (Join-Path $lifeProject $lifePickup)) -ne $baseline[$lifePickup]){throw 'Accepted main prefab changed'}
        foreach($r in @('Assets/Project/Editor/TownLifeUpdateBatch.cs','Assets/Project/Editor/TownLifeModels.cs','Assets/Project/Editor/TownLifePlay.cs','Assets/Project/Scripts/City/PickupDrive.cs','Assets/Project/Scripts/City/TownRailTraffic.cs','Assets/Project/Scripts/City/FarmAnimal.cs','Assets/Project/Scripts/City/FarmAnimal.cs.meta')){if(Test-Path -LiteralPath (Join-Path $lifeProject $r)){LifeCopy (Join-Path $lifeProject $r) (Join-Path $lifeClone $r);$baseline[$r]=LifeHash (Join-Path $lifeProject $r)}}
    }
    LifeCopy (Join-Path $lifeStore 'OriginalTown.prefab') (Join-Path $lifeClone $lifeTown);LifeCopy (Join-Path $lifeStore 'OriginalPickup.prefab') (Join-Path $lifeClone $lifePickup)
    [IO.File]::WriteAllText($baselineFile,($baseline|ConvertTo-Json -Depth 4))
}
if($Mode -eq 'Apply'){
    $baseline=Get-Content $baselineFile -Raw|ConvertFrom-Json -AsHashtable;$qa=Join-Path $lifeClone 'Library/TownLifeQA'
    foreach($s in @('result','review','play')){if(-not(Get-Content (Join-Path $qa ($s+'.txt')) -Raw).StartsWith('PASS:')){throw "Failed $s"}}
    $outputs=@();foreach($s in @('result','review','play')){
        $paths=@();foreach($line in Get-Content (Join-Path $qa ($s+'-assets.sha256'))){$h,$r=$line -split ' ',2;if($r -notin @($lifeTown,$lifePickup,($lifeRoot+'.meta')) -and -not $r.StartsWith($lifeRoot+'/')){throw 'Unexpected output'};if((LifeHash (Join-Path $lifeClone $r)) -ne $h){throw "Stale $s output $r"};$paths+=$r};if($outputs.Count -and (Compare-Object $outputs $paths)){throw 'Receipt coverage mismatch'};$outputs=$paths
    }
    foreach($pair in @(@('Library/TownCityCorrectionsQA/drive.txt','Library/TownCityCorrectionsQA/drive-input.sha256'),@('Library/TownRuralPolishQA/play.txt','Library/TownRuralPolishQA/play-input.sha256'))){if(-not(Get-Content (Join-Path $lifeClone $pair[0]) -Raw).StartsWith('PASS:') -or (Get-Content (Join-Path $lifeClone $pair[1]) -Raw).Trim() -ne (LifeHash (Join-Path $lifeClone $lifeTown))){throw 'Missing native drive acceptance'}}
    if(-not(Get-Content (Join-Path $lifeClone 'Library/TownYardStoryQA/play.txt') -Raw).StartsWith('PASS:')){throw 'Shop failed'}
    $reviewed=ReviewedInputs
    foreach($stage in @('Review','Play','Drive','Rural','Shop')){
        $proof=Join-Path $lifeStore ($stage+'-inputs.json')
        $record=Get-Content -LiteralPath $proof -Raw|ConvertFrom-Json -AsHashtable
        if(Compare-Object @($reviewed.Keys|Sort-Object) @($record.Keys|Sort-Object)){throw "Incomplete $stage inputs"}
        foreach($r in $reviewed.Keys){if($record[$r] -ne $reviewed[$r]){throw "Stale $stage test: $r"}}
        $reportPath=switch($stage){Review{'Library/TownLifeQA/review.txt'} Play{'Library/TownLifeQA/play.txt'} Drive{'Library/TownCityCorrectionsQA/drive.txt'} Rural{'Library/TownRuralPolishQA/play.txt'} Shop{'Library/TownYardStoryQA/play.txt'}}
        if((Get-Item -LiteralPath (Join-Path $lifeClone $reportPath)).LastWriteTimeUtc -lt (Get-Item -LiteralPath $proof).LastWriteTimeUtc){throw "No fresh $stage result"}
    }
    $current=Inputs;foreach($r in $baseline.Keys){if(-not $current.ContainsKey($r) -or $current[$r] -ne $baseline[$r]){throw "Main input changed: $r"}}
    foreach($r in $current.Keys){if(-not $baseline.ContainsKey($r) -and -not $r.EndsWith('.meta')){throw "Unexpected main input: $r"}}
    $backup=Join-Path $lifeStore ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'));foreach($r in @($lifeTown,$lifePickup)){LifeCopy (Join-Path $lifeProject $r) (Join-Path $backup $r)}
    foreach($r in $outputs|Where-Object {$_ -notin @($lifeTown,$lifePickup)}){LifeCopy (Join-Path $lifeClone $r) (Join-Path $lifeProject $r)}
    foreach($r in @($lifePickup,$lifeTown)){LifeCopy (Join-Path $lifeClone $r) (Join-Path $lifeProject $r)}
    foreach($dir in @('Docs/TownLifeReview','Library/TownLifeQA')){foreach($f in Get-ChildItem (Join-Path $lifeClone $dir) -File){LifeCopy $f.FullName (Join-Path $lifeProject ('Docs/TownLifeReview/'+$f.Name))}}
    $regression=@{
        'CityDrives.txt'='Library/TownCityCorrectionsQA/drives.txt'
        'CityAcceptance.txt'='Library/TownCityCorrectionsQA/drive.txt'
        'RuralDrives.txt'='Library/TownRuralPolishQA/drives.txt'
        'RuralAcceptance.txt'='Library/TownRuralPolishQA/play.txt'
        'ShopAcceptance.txt'='Library/TownYardStoryQA/play.txt'
        'StarterStore.png'='Docs/TownYardStoryReview/StarterStore.png'
        'SiteInGame.png'='Docs/TownYardStoryReview/SiteInGame.png'
    }
    foreach($name in $regression.Keys){LifeCopy (Join-Path $lifeClone $regression[$name]) (Join-Path $lifeProject ('Docs/TownLifeReview/'+$name))}
    Write-Output "APPLIED; backup: $backup";return
}
$method=switch($Mode){Drive{'TownCityCorrectionsDrive.Run'} Rural{'TownRuralPolishPlay.Run'} Shop{'TownForgottenYardPlay.RunDetails'} Play{'TownLifePlay.Run'} default{'TownLifeUpdateBatch.'+$Mode}}
if($Mode -ne 'Generate'){
    [IO.File]::WriteAllText((Join-Path $lifeStore ($Mode+'-inputs.json')),((ReviewedInputs)|ConvertTo-Json -Depth 4))
}
$log=Join-Path $lifeStore ($Mode+'.log');$p=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$lifeClone+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "$Mode PID $($p.Id); $log"
