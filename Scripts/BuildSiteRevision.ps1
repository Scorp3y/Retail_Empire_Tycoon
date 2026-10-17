param([ValidateSet('Generate','RefreshRuntime','Review','Drive','Play','Rural','Signals','Apply')][string]$Mode='Generate')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$siteProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$siteClone=Join-Path $siteProject 'Library/CityUpdateQA/IsolatedProject'
$sitePipeline=Join-Path $siteProject 'Library/TownSiteRevisionPipeline'
$siteArt='Assets/Art/TownSiteRevision'
$sitePrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$siteStarter='Assets/Prefabs/StarterStore/StarterStore.asset'
$siteEvidence='Docs/TownSiteRevisionReview'
function SiteHash([string]$p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash}
function SiteCopy([string]$source,[string]$target){
    $source=[IO.Path]::GetFullPath($source);$target=[IO.Path]::GetFullPath($target)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null
    if([IO.File]::Exists($target) -and (SiteHash $source) -eq (SiteHash $target)){return}
    $stage=Join-Path ([IO.Path]::GetDirectoryName($target)) ('.site-revision-'+[guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy($source,$stage,$false)
        if([IO.File]::Exists($target)){
            try{[IO.File]::Replace($stage,$target,[NullString]::Value,$true)}
            catch{
                $mainOutput=$Mode -eq 'Apply' -and $target -in @((Join-Path $siteProject $sitePrefab),(Join-Path $siteProject $siteStarter))
                $cloneReset=$Mode -eq 'Generate' -and $target -in @((Join-Path $siteClone $sitePrefab),(Join-Path $siteClone $siteStarter))
                if(-not($mainOutput -or $cloneReset)){throw}
                if($mainOutput){
                    $relative=[IO.Path]::GetRelativePath($siteProject,$target).Replace('\','/')
                    if((SiteHash $target) -ne $baseline[$relative]){throw 'Mapped input changed; refusing replacement.'}
                }
                $retained=Join-Path $sitePipeline ('MappedOriginal_'+[guid]::NewGuid().ToString('N')+[IO.Path]::GetExtension($target))
                [IO.File]::Move($target,$retained)
                try{[IO.File]::Move($stage,$target)}catch{if(-not [IO.File]::Exists($target)){[IO.File]::Move($retained,$target)};throw}
            }
        }else{[IO.File]::Move($stage,$target)}
    }finally{if([IO.File]::Exists($stage)){Remove-Item -LiteralPath $stage -Force}}
}
function SiteInputs {
    $result=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $siteProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($siteProject,$file.FullName).Replace('\','/')
            if($relative -eq ($siteArt+'.meta') -or $relative.StartsWith($siteArt+'/')){continue}
            $result[$relative]=SiteHash $file.FullName
        }
    }
    return $result
}
$running=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Contains($siteClone)})
if($running.Count -gt 0){throw 'Isolated Unity is busy.'}
$lock=Join-Path $siteClone 'Temp/UnityLockfile';if(Test-Path -LiteralPath $lock){Remove-Item -LiteralPath $lock -Force}
$baseline=Get-Content -LiteralPath (Join-Path $sitePipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
if($Mode -eq 'RefreshRuntime'){
    # A save-restoration rule does not alter authored geometry. It must still be compiled and
    # exercised by the fresh native Review/Play stages before the same art can be installed.
    foreach($relative in @('Assets/Project/Scripts/BuildSystem/FloorPainter.cs')){
        SiteCopy (Join-Path $siteProject $relative) (Join-Path $siteClone $relative);$baseline[$relative]=SiteHash (Join-Path $siteProject $relative)
    }
    [IO.File]::WriteAllText((Join-Path $sitePipeline 'baseline.json'),($baseline|ConvertTo-Json -Depth 4));return
}
if($Mode -eq 'Generate'){
    if((SiteHash (Join-Path $siteProject $sitePrefab)) -ne (SiteHash (Join-Path $sitePipeline 'OriginalTown.prefab'))){throw 'Main accepted town changed during work.'}
    $owned=@('Assets/Project/Editor/TownSiteRevisionBatch.cs','Assets/Project/Editor/TownSiteRevisionPlacement.cs','Assets/Project/Editor/TownGroundOverlapRepair.cs','Assets/Project/Editor/TownSiteRevisionPlay.cs','Assets/Project/Editor/TownCityCorrectionsGeometry.cs','Assets/Project/Editor/TownRuralPolishPlacement.cs','Assets/Project/Scripts/City/TownJunctionSignals.cs','Assets/Project/Scripts/City/TownSignalChunks.cs','Assets/Project/Scripts/City/TownSignalChunks.cs.meta','Assets/Project/Scripts/City/CityWorld.cs','Assets/Project/Scripts/City/TownWorldLayout.cs','Assets/Project/Scripts/Territory/TerritoryPlotLayout.cs','Assets/Project/Scripts/BuildSystem/StarterStoreBlueprint.cs','Assets/Project/Scripts/BuildSystem/StarterStoreInitialization.cs','Assets/Project/Scripts/BuildSystem/FloorPainter.cs')
    foreach($relative in $owned){
        $source=Join-Path $siteProject $relative;if(-not(Test-Path -LiteralPath $source)){continue}
        SiteCopy $source (Join-Path $siteClone $relative);$baseline[$relative]=SiteHash $source
    }
    SiteCopy (Join-Path $sitePipeline 'OriginalTown.prefab') (Join-Path $siteClone $sitePrefab)
    SiteCopy (Join-Path $sitePipeline 'OriginalStarter.asset') (Join-Path $siteClone $siteStarter)
    [IO.File]::WriteAllText((Join-Path $sitePipeline 'baseline.json'),($baseline|ConvertTo-Json -Depth 4))
}
if($Mode -eq 'Apply'){
    $qa=Join-Path $siteClone 'Library/TownSiteRevisionQA'
    $outputs=@(Get-ChildItem -LiteralPath (Join-Path $siteClone $siteArt) -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($siteClone,$_.FullName).Replace('\','/')})+@(($siteArt+'.meta'),$sitePrefab,$siteStarter)
    foreach($stage in @('result','review')){
        if(-not(Get-Content -LiteralPath (Join-Path $qa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Failed acceptance: $stage"}
        $receipt=@{}
        foreach($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-assets.sha256'))){
            if($line -notmatch '^([A-F0-9]{64}) (Assets/.+)$'){throw 'Bad receipt'}
            $hash=$Matches[1];$relative=$Matches[2]
            if($relative -notin $outputs -or $receipt.ContainsKey($relative) -or (SiteHash (Join-Path $siteClone $relative)) -ne $hash){throw "Stale/unexpected output: $relative"}
            $receipt[$relative]=$hash
        }
        if($receipt.Count -ne $outputs.Count){throw 'Incomplete acceptance receipt'}
    }
    foreach($pair in @(@('Library/TownCityCorrectionsQA','drive.txt','drive-input.sha256'),@('Library/TownSiteRevisionQA','signals-play.txt','signals-input.sha256'),@('Library/TownRuralPolishQA','play.txt','play-input.sha256'))){
        $folder=Join-Path $siteClone $pair[0]
        if(-not(Get-Content -LiteralPath (Join-Path $folder $pair[1]) -Raw).StartsWith('PASS:') -or (Get-Content -LiteralPath (Join-Path $folder $pair[2]) -Raw).Trim() -ne (SiteHash (Join-Path $siteClone $sitePrefab))){throw "Missing/stale runtime acceptance: $($pair[1])"}
    }
    $shopQa=Join-Path $siteClone 'Library/TownYardStoryQA'
    if(-not(Get-Content -LiteralPath (Join-Path $shopQa 'play.txt') -Raw).StartsWith('PASS:')){throw 'Shop gameplay failed'}
    foreach($relative in @($sitePrefab)){
        $expected=(SiteHash (Join-Path $siteClone $relative))+' '+$relative
        if($expected -notin (Get-Content -LiteralPath (Join-Path $shopQa 'play-assets.sha256'))){throw "Stale shop gameplay input: $relative"}
    }
    $shopInputs=Join-Path $qa 'shop-input-assets.sha256'
    if((Get-Item -LiteralPath (Join-Path $shopQa 'play.txt')).LastWriteTime -lt (Get-Item -LiteralPath $shopInputs).LastWriteTime){throw 'Shop receipt predates its recorded inputs'}
    foreach($relative in @($sitePrefab,$siteStarter)){
        if(((SiteHash (Join-Path $siteClone $relative))+' '+$relative) -notin (Get-Content -LiteralPath $shopInputs)){throw "Shop input changed after launch: $relative"}
    }
    $current=SiteInputs
    foreach($relative in $baseline.Keys){if(-not $current.ContainsKey($relative) -or $current[$relative] -ne $baseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $current.Keys){if(-not $baseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    foreach($relative in $baseline.Keys | Where-Object {$_ -match '^Assets/Project/Scripts/.+\.cs$'}){
        if((SiteHash (Join-Path $siteClone $relative)) -ne $baseline[$relative]){throw "Native QA used different runtime source: $relative"}
    }
    $backup=Join-Path $sitePipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    foreach($relative in $outputs){if(Test-Path -LiteralPath (Join-Path $siteProject $relative)){SiteCopy (Join-Path $siteProject $relative) (Join-Path $backup $relative)}}
    foreach($relative in $outputs|Where-Object {$_ -notin @($sitePrefab,$siteStarter)}){SiteCopy (Join-Path $siteClone $relative) (Join-Path $siteProject $relative)}
    foreach($relative in @($siteStarter,$sitePrefab)){SiteCopy (Join-Path $siteClone $relative) (Join-Path $siteProject $relative)}
    foreach($folder in @($siteEvidence,'Library/TownSiteRevisionQA')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $siteClone $folder) -File){SiteCopy $file.FullName (Join-Path $siteProject ($siteEvidence+'/'+$file.Name))}
    }
    foreach($name in @('play.txt','play-assets.sha256')){SiteCopy (Join-Path $shopQa $name) (Join-Path $siteProject ($siteEvidence+'/'+$name))}
    foreach($pair in @(@('Library/TownCityCorrectionsQA/drive.txt','supplier-drives.txt'),@('Library/TownCityCorrectionsQA/drives.txt','supplier-drive-details.txt'),@('Library/TownRuralPolishQA/play.txt','rural-play.txt'),@('Library/TownRuralPolishQA/drives.txt','rural-drives.txt'))){SiteCopy (Join-Path $siteClone $pair[0]) (Join-Path $siteProject ($siteEvidence+'/'+$pair[1]))}
    foreach($name in @('StarterStore.png','SiteInGame.png')){SiteCopy (Join-Path $siteClone ('Docs/TownYardStoryReview/'+$name)) (Join-Path $siteProject ($siteEvidence+'/'+$name))}
    Write-Output "SITE REVISION APPLIED. Recoverable originals: $backup";return
}
$method=switch($Mode){'Play'{'TownForgottenYardPlay.RunDetails'} 'Drive'{'TownCityCorrectionsDrive.Run'} 'Rural'{'TownRuralPolishPlay.Run'} 'Signals'{'TownSiteRevisionPlay.Run'} default{'TownSiteRevisionBatch.'+$Mode}}
if($Mode -eq 'Play'){
    # The reused shop harness's own receipt covers town/yard art, not the starter snapshot.
    # Bind its next run to that snapshot before Unity starts; Apply checks both hashes and time.
    [IO.File]::WriteAllLines((Join-Path $siteClone 'Library/TownSiteRevisionQA/shop-input-assets.sha256'),[string[]](@($sitePrefab,$siteStarter)|ForEach-Object {(SiteHash (Join-Path $siteClone $_))+' '+$_}))
}
$log=Join-Path $sitePipeline ($Mode+'.log')
$p=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$siteClone+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "Site revision $Mode launched. PID $($p.Id); log $log"
