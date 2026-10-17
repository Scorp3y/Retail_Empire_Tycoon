param([ValidateSet('Inspect','Generate','Review','Drive','Play','Apply')][string]$Mode='Inspect',[switch]$Revise)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$siteProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$siteClone=Join-Path $siteProject 'Library/CityUpdateQA/IsolatedProject'
$sitePipeline=Join-Path $siteProject 'Library/TownCityCorrectionsPipeline'
$siteArt='Assets/Art/TownCityCorrections'
$sitePrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$siteEvidence='Docs/TownCityCorrectionsReview'
New-Item -ItemType Directory -Path $sitePipeline -Force | Out-Null
function SiteHash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function SiteCopy([string]$source,[string]$target){
    $sourceFile=[IO.Path]::GetFullPath($source);$targetFile=[IO.Path]::GetFullPath($target)
    $targetDirectory=[IO.Path]::GetDirectoryName($targetFile)
    [IO.Directory]::CreateDirectory($targetDirectory)|Out-Null
    if([IO.File]::Exists($targetFile) -and (SiteHash $sourceFile) -eq (SiteHash $targetFile)){return}
    # Unity may memory-map a saved prefab. Replacing its directory entry is atomic and does not
    # truncate the mapped file. Hidden staging files are ignored by the asset importer.
    $stagingFile=Join-Path $targetDirectory ('.city-corrections-'+[guid]::NewGuid().ToString('N')+'.tmp')
    try {
        [IO.File]::Copy($sourceFile,$stagingFile,$false)
        if([IO.File]::Exists($targetFile)){
            try{[IO.File]::Replace($stagingFile,$targetFile,[NullString]::Value,$true)}
            catch{
                $townTarget=[IO.Path]::GetFullPath((Join-Path $siteProject $sitePrefab))
                if($Mode -ne 'Apply' -or $targetFile -ne $townTarget){throw "Could not replace scoped output '$targetFile': $($_.Exception.Message)"}
                if((SiteHash $targetFile) -ne $baseline[$sitePrefab]){throw 'Mapped original town changed; refusing the rename fallback.'}
                # Some mapped files allow renaming but not replacement/deletion. Keep the
                # original file intact in Library, and restore its name if installation fails.
                $retainedFile=Join-Path $sitePipeline ('MappedOriginal_'+[guid]::NewGuid().ToString('N')+'.prefab')
                [IO.File]::Move($targetFile,$retainedFile)
                try{[IO.File]::Move($stagingFile,$targetFile)}
                catch{if(-not [IO.File]::Exists($targetFile)){[IO.File]::Move($retainedFile,$targetFile)};throw}
                Write-Output "Mapped original retained intact: $retainedFile"
            }
        }
        else{[IO.File]::Move($stagingFile,$targetFile)}
    } finally {
        if([IO.File]::Exists($stagingFile)){Remove-Item -LiteralPath $stagingFile -Force}
    }
}
function SiteInputs {
    $files=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $siteProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($siteProject,$file.FullName).Replace('\','/')
            if($relative -eq ($siteArt+'.meta') -or $relative.StartsWith($siteArt+'/')){continue}
            $files[$relative]=SiteHash $file.FullName
        }
    }
    return $files
}
$siteLock=Join-Path $siteClone 'Temp/UnityLockfile'
if(Test-Path -LiteralPath $siteLock){
    $running=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Contains($siteClone)})
    if($running.Count -gt 0){throw 'Isolated Unity is busy.'}
    Remove-Item -LiteralPath $siteLock -Force
}
if($Revise -and $Mode -ne 'Generate'){throw 'Revise is only valid for Generate.'}
if($Revise){
    # Iterations may revise only these owned recipes; all original input hashes stay frozen.
    $baseline=Get-Content -LiteralPath (Join-Path $sitePipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    if((SiteHash (Join-Path $siteProject $sitePrefab)) -ne $baseline[$sitePrefab]){throw 'Original main town changed during revision.'}
    foreach($relative in @('Assets/Project/Editor/TownCityCorrectionsBatch.cs','Assets/Project/Editor/TownCityCorrectionsPlacement.cs','Assets/Project/Editor/TownCityCorrectionsGeometry.cs','Assets/Project/Editor/TownKit/TownArchitectureParts.cs','Assets/Project/Editor/TownWorldUrbanBlocks.cs','Assets/Project/Editor/TownKit/TownShopPerimeterModels.cs','Assets/Project/Editor/TownSpecialPlan.cs','Assets/Project/Editor/TownSpecialPlacement.cs','Assets/Project/Editor/TownRuralPolishPlacement.cs')){
        SiteCopy (Join-Path $siteProject $relative) (Join-Path $siteClone $relative);$baseline[$relative]=SiteHash (Join-Path $siteProject $relative)
    }
    SiteCopy (Join-Path $sitePipeline 'OriginalTown.prefab') (Join-Path $siteClone $sitePrefab)
    [IO.File]::WriteAllText((Join-Path $sitePipeline 'baseline.json'),($baseline|ConvertTo-Json -Depth 4))
} elseif($Mode -in @('Inspect','Generate')){
    $baseline=SiteInputs
    [IO.File]::WriteAllText((Join-Path $sitePipeline 'baseline.json'),($baseline|ConvertTo-Json -Depth 4))
    foreach($relative in $baseline.Keys){
        $source=Join-Path $siteProject $relative;$target=Join-Path $siteClone $relative
        if(-not(Test-Path -LiteralPath $target) -or (SiteHash $target) -ne $baseline[$relative]){SiteCopy $source $target}
    }
    SiteCopy (Join-Path $siteProject $sitePrefab) (Join-Path $sitePipeline 'OriginalTown.prefab')
}
if($Mode -eq 'Apply'){
    $qa=Join-Path $siteClone 'Library/TownCityCorrectionsQA'
    $outputs=@(Get-ChildItem -LiteralPath (Join-Path $siteClone $siteArt) -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($siteClone,$_.FullName).Replace('\','/')})+@(($siteArt+'.meta'),$sitePrefab)
    foreach($stage in @('result','review')){
        if(-not(Get-Content -LiteralPath (Join-Path $qa ($stage+'.txt')) -Raw).StartsWith('PASS:')){throw "Acceptance failed: $stage"}
        $accepted=@{}
        foreach($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-assets.sha256'))){
            if($line -notmatch '^([A-F0-9]{64}) (Assets/.+)$'){throw 'Invalid receipt'}
            $expected=$Matches[1];$relative=$Matches[2]
            if($relative.Contains('..') -or $relative.Contains('\') -or ($relative -notin @($sitePrefab,($siteArt+'.meta')) -and -not $relative.StartsWith($siteArt+'/'))){throw "Unexpected output: $relative"}
            if($accepted.ContainsKey($relative) -or (SiteHash (Join-Path $siteClone $relative)) -ne $expected){throw "Stale accepted output: $relative"}
            $accepted[$relative]=$expected
        }
        if($outputs.Count -ne $accepted.Count){throw "Incomplete receipt: $stage"}
        foreach($relative in $outputs){if(-not $accepted.ContainsKey($relative)){throw "Unreviewed output: $relative"}}
    }
    # Reuse the established real shop sale/purchase/save-compatibility regression without changing it.
    $playQa=Join-Path $siteClone 'Library/TownYardStoryQA'
    if(-not(Get-Content -LiteralPath (Join-Path $qa 'drive.txt') -Raw).StartsWith('PASS:')){throw 'Actual pickup crossings failed'}
    if((Get-Content -LiteralPath (Join-Path $qa 'drive-input.sha256') -Raw).Trim() -ne (SiteHash (Join-Path $siteClone $sitePrefab))){throw 'Stale pickup drive acceptance'}
    if(-not(Get-Content -LiteralPath (Join-Path $playQa 'play.txt') -Raw).StartsWith('PASS:')){throw 'Shop gameplay regression failed'}
    $playPrefab=@(Get-Content -LiteralPath (Join-Path $playQa 'play-assets.sha256') | Where-Object {$_ -match (' '+[regex]::Escape($sitePrefab)+'$')})
    if($playPrefab.Count -ne 1 -or $playPrefab[0] -ne ((SiteHash (Join-Path $siteClone $sitePrefab))+' '+$sitePrefab)){throw 'Stale shop gameplay regression'}
    $baseline=Get-Content -LiteralPath (Join-Path $sitePipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $current=SiteInputs
    foreach($relative in $baseline.Keys){if(-not $current.ContainsKey($relative) -or $current[$relative] -ne $baseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $current.Keys){if(-not $baseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $backup=Join-Path $sitePipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    foreach($relative in $outputs){if(Test-Path -LiteralPath (Join-Path $siteProject $relative)){SiteCopy (Join-Path $siteProject $relative) (Join-Path $backup $relative)}}
    foreach($relative in @('Assets/Project/Editor/TownCityCorrectionsBatch.cs.meta','Assets/Project/Editor/TownCityCorrectionsPlacement.cs.meta','Assets/Project/Editor/TownCityCorrectionsGeometry.cs.meta')){
        $source=Join-Path $siteClone $relative;$target=Join-Path $siteProject $relative
        # These are editor-only tools, never serialized components of the art output. Preserve
        # GUIDs already assigned by the user's open editor instead of replacing its imports.
        if(-not(Test-Path -LiteralPath $target)){SiteCopy $source $target}
    }
    foreach($relative in $outputs | Where-Object {$_ -ne $sitePrefab}){SiteCopy (Join-Path $siteClone $relative) (Join-Path $siteProject $relative)}
    SiteCopy (Join-Path $siteClone $sitePrefab) (Join-Path $siteProject $sitePrefab)
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $siteClone $siteEvidence) -File -Recurse){SiteCopy $file.FullName (Join-Path $siteProject ([IO.Path]::GetRelativePath($siteClone,$file.FullName)))}
    foreach($file in Get-ChildItem -LiteralPath $qa -File){SiteCopy $file.FullName (Join-Path $siteProject ($siteEvidence+'/'+$file.Name))}
    SiteCopy (Join-Path $playQa 'play.txt') (Join-Path $siteProject ($siteEvidence+'/play.txt'))
    SiteCopy (Join-Path $playQa 'play-assets.sha256') (Join-Path $siteProject ($siteEvidence+'/play-assets.sha256'))
    foreach($name in @('StarterStore.png','SiteInGame.png')){SiteCopy (Join-Path $siteClone ('Docs/TownYardStoryReview/'+$name)) (Join-Path $siteProject ($siteEvidence+'/'+$name))}
    Write-Output "CITY CORRECTIONS APPLIED. Recoverable backup: $backup";return
}
$log=Join-Path $sitePipeline ($Mode+'.log')
$method=if($Mode -eq 'Play'){'TownForgottenYardPlay.RunDetails'}else{'TownCityCorrectionsBatch.'+$Mode}
if($Mode -eq 'Drive'){$method='TownCityCorrectionsDrive.Run'}
$process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList @('-batchmode','-projectPath',('"'+$siteClone+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"')) -WindowStyle Hidden -PassThru
Write-Output "City corrections $Mode launched. PID $($process.Id); log $log"
