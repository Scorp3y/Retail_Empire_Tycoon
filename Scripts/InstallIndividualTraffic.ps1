param(
    [ValidateSet('Prepare','Sync','Build','Review','Play','Apply')][string]$Mode='Prepare',
    [switch]$SyncVersionLabel
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$clone = Join-Path $project 'Library/CityUpdateQA/IsolatedProject'
$state = Join-Path $project 'Library/IndividualTrafficImport'
$root = 'Assets/Art/TrafficFleet'
$town = 'Assets/Prefabs/TownWorld/TownEnvironment.prefab'
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
function CopyChecked([string]$source,[string]$destination) {
    if ((Test-Path -LiteralPath $destination) -and (Hash $source) -eq (Hash $destination)) { return }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
    [IO.File]::Copy($source,$destination,$true)
}
function Files([string]$folder) { @(Get-ChildItem -LiteralPath $folder -File -Recurse) }
function Snapshot([string[]]$folders) {
    $result=@{}
    foreach ($folder in $folders) {
        foreach ($file in Files (Join-Path $project $folder)) {
            $relative=[IO.Path]::GetRelativePath($project,$file.FullName).Replace('\','/')
            $result[$relative]=Hash $file.FullName
        }
    }
    return $result
}
function RequireUnchanged($snapshot,[string]$base) {
    foreach ($relative in $snapshot.Keys) {
        if ((Hash (Join-Path $base $relative)) -ne $snapshot[$relative]) { throw "Changed input: $relative" }
    }
}
$busy=@(Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Contains($clone)
})
if ($busy.Count) { throw 'Isolated Unity is still running; finish that stage first.' }
if ($Mode -eq 'Prepare') {
    if (Test-Path -LiteralPath (Join-Path $state 'baseline.json')) { throw 'Snapshot exists. Use Sync to continue.' }
    $baseline=Snapshot @('Assets','Packages','ProjectSettings')
    [IO.File]::WriteAllText((Join-Path $state 'baseline.json'),($baseline | ConvertTo-Json -Depth 4))
    foreach ($relative in $baseline.Keys) { CopyChecked (Join-Path $project $relative) (Join-Path $clone $relative) }
    [IO.File]::WriteAllText((Join-Path $state 'source-hashes.json'),((Snapshot @('ArtSource/TrafficVehicles')) | ConvertTo-Json))
}
if ($Mode -in @('Prepare','Sync')) {
    $exports=Join-Path $state 'Exports'
    foreach ($file in Get-ChildItem -LiteralPath $exports -Filter '*.fbx') {
        CopyChecked $file.FullName (Join-Path $clone ($root+'/Models/'+$file.Name))
    }
    foreach ($file in Get-ChildItem -LiteralPath $exports -Filter '*.png') {
        CopyChecked $file.FullName (Join-Path $clone ($root+'/Textures/'+$file.Name))
    }
    CopyChecked (Join-Path $exports 'fleet.json') (Join-Path $clone 'ArtSource/IndividualTraffic/fleet.json')
    foreach ($relative in @('Assets/Project/Editor/IndividualTrafficBatch.cs',
        'Assets/Project/Scripts/City/TownRoadTraffic.cs')) {
        CopyChecked (Join-Path $project $relative) (Join-Path $clone $relative)
        if (Test-Path -LiteralPath (Join-Path $project ($relative+'.meta'))) {
            CopyChecked (Join-Path $project ($relative+'.meta')) (Join-Path $clone ($relative+'.meta'))
        }
    }
    if ($Mode -eq 'Sync') {
        $baselinePath=Join-Path $state 'baseline.json'
        $baseline=Get-Content -Raw -LiteralPath $baselinePath | ConvertFrom-Json -AsHashtable
        foreach ($relative in @('Assets/Project/Editor/IndividualTrafficBatch.cs',
            'Assets/Project/Scripts/City/TownRoadTraffic.cs')) {
            $baseline[$relative]=Hash (Join-Path $project $relative)
            if (Test-Path -LiteralPath (Join-Path $project ($relative+'.meta'))) {
                $baseline[$relative+'.meta']=Hash (Join-Path $project ($relative+'.meta'))
            }
        }
        if ($SyncVersionLabel) {
            $relative='Assets/Project/Scripts/UI/Shop/GameVersionLabel.cs'
            $current=Join-Path $project $relative
            $previous=Join-Path $clone $relative
            if ((Hash $previous) -ne $baseline[$relative]) { throw 'Version-label comparison base changed.' }
            $pattern='public const string CurrentVersion\s*=\s*"[^"\r\n]*";'
            $currentText=Get-Content -Raw -LiteralPath $current
            $previousText=Get-Content -Raw -LiteralPath $previous
            if ($currentText -notmatch $pattern -or $previousText -notmatch $pattern -or
                [regex]::Replace($currentText,$pattern,'VERSION-CONSTANT') -ne
                [regex]::Replace($previousText,$pattern,'VERSION-CONSTANT')) {
                throw 'Version label has non-cosmetic changes; inspect before synchronizing.'
            }
            CopyChecked $current $previous
            $baseline[$relative]=Hash $current
            [IO.File]::WriteAllText((Join-Path $state 'preserved-version-label.txt'),
                'Preserved concurrent version-constant-only change; synchronized to QA without modifying the main file.')
        }
        [IO.File]::WriteAllText($baselinePath,($baseline | ConvertTo-Json -Depth 4))
    }
    $inputs=@{}
    foreach ($file in Files $exports) { $inputs[[IO.Path]::GetRelativePath($project,$file.FullName)]=Hash $file.FullName }
    [IO.File]::WriteAllText((Join-Path $state 'export-hashes.json'),($inputs | ConvertTo-Json))
    Write-Output 'Prepared isolated import; installed fleet and saved scenes untouched.'
    return
}
if ($Mode -eq 'Apply') {
    $qa=Join-Path $clone 'Library/TrafficFleetQA'
    foreach ($stage in @('build','review')) {
        if (-not (Get-Content -Raw -LiteralPath (Join-Path $clone "Library/IndividualTrafficQA/$stage.txt")).StartsWith('PASS:')) {
            throw "No passing individual $stage stage."
        }
    }
    if (-not (Get-Content -Raw -LiteralPath (Join-Path $qa 'play.txt')).StartsWith('PASS:')) { throw 'Play validation missing.' }
    foreach ($snapshotName in @('baseline.json','source-hashes.json','export-hashes.json')) {
        RequireUnchanged (Get-Content -Raw -LiteralPath (Join-Path $state $snapshotName) | ConvertFrom-Json -AsHashtable) $project
    }
    foreach ($stage in @('individual-review','play')) {
        foreach ($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-assets.sha256'))) {
            $expected,$relative=$line -split ' ',2
            if ($relative -ne $town -and $relative -ne ($root+'.meta') -and -not $relative.StartsWith($root+'/')) {
                throw "Out-of-scope output: $relative"
            }
            if ((Hash (Join-Path $clone $relative)) -ne $expected) { throw "Stale $stage evidence: $relative" }
        }
        foreach ($line in Get-Content -LiteralPath (Join-Path $qa ($stage+'-code.sha256'))) {
            $expected,$relative=$line -split ' ',2
            if ((Hash (Join-Path $project $relative)) -ne $expected) { throw "Tested code differs: $relative" }
        }
    }
    $baseline=Get-Content -Raw -LiteralPath (Join-Path $state 'baseline.json') | ConvertFrom-Json -AsHashtable
    foreach ($relative in @($town,($root+'/Data/RoadNetwork.asset'),'Assets/Project/Scenes/City.unity','Assets/Project/Scenes/Game.unity')) {
        if ((Hash (Join-Path $clone $relative)) -ne $baseline[$relative]) { throw "Protected scene changed: $relative" }
    }
    foreach ($relative in $baseline.Keys | Where-Object { $_.StartsWith($root+'/') -and $_.EndsWith('.meta') }) {
        $mainGuid=(Select-String -LiteralPath (Join-Path $project $relative) -Pattern '^guid: ').Line
        $cloneGuid=(Select-String -LiteralPath (Join-Path $clone $relative) -Pattern '^guid: ').Line
        if ($mainGuid -ne $cloneGuid) { throw "Existing GUID changed: $relative" }
    }
    $backup=Join-Path $state ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    foreach ($file in Files (Join-Path $clone $root)) {
        $relative=[IO.Path]::GetRelativePath($clone,$file.FullName)
        $destination=Join-Path $project $relative
        if ((Test-Path -LiteralPath $destination) -and (Hash $destination) -eq (Hash $file.FullName)) { continue }
        if (Test-Path -LiteralPath $destination) { CopyChecked $destination (Join-Path $backup $relative) }
        CopyChecked $file.FullName $destination
    }
    foreach ($file in Files (Join-Path $clone $root)) {
        $relative=[IO.Path]::GetRelativePath($clone,$file.FullName)
        if ((Hash $file.FullName) -ne (Hash (Join-Path $project $relative))) { throw "Installation mismatch: $relative" }
    }
    foreach ($folder in @('Library/IndividualTrafficQA','Library/TrafficFleetQA','Docs/TrafficFleetReview')) {
        foreach ($file in Files (Join-Path $clone $folder)) {
            $relative=[IO.Path]::GetRelativePath((Join-Path $clone $folder),$file.FullName)
            CopyChecked $file.FullName (Join-Path $project ('Docs/IndividualTrafficReview/'+$folder.Split('/')[-1]+'/'+$relative))
        }
    }
    Write-Output "APPLIED: twenty individual models, preserved town/network/scenes. Backup: $backup"
    return
}
$method=switch ($Mode) { 'Build' {'IndividualTrafficBatch.Build'} 'Review' {'IndividualTrafficBatch.Review'} 'Play' {'TrafficFleetPlay.Run'} }
$lock=Join-Path $clone 'Temp/UnityLockfile'
if (Test-Path -LiteralPath $lock) { Remove-Item -LiteralPath $lock -Force }
$log=Join-Path $state ($Mode+'.log')
$process=Start-Process 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' `
    -ArgumentList @('-batchmode','-projectPath',('"'+$clone+'"'),'-executeMethod',$method,'-logFile',('"'+$log+'"')) `
    -WindowStyle Hidden -PassThru
Write-Output "$Mode started, PID $($process.Id), log $log"
