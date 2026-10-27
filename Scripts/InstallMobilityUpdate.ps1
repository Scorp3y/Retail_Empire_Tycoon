param([string]$Project = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $projectRoot 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/MobilityUpdateQA'
function Hash([string]$path) { (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
foreach($stage in @('build','play','city')) {
    if(!(Get-Content -LiteralPath (Join-Path $report "$stage.txt") -Raw).StartsWith('PASS ')){throw "Missing successful $stage validation."}
}
if(!(Get-Content -LiteralPath (Join-Path $qa 'Library/ShopperQA/validation.txt') -Raw).Contains('PASS shoppers integrated into real shop purchase/leave flow.')){throw 'Shopper regression must pass.'}
if(!(Get-Content -LiteralPath (Join-Path $qa 'Library/TrafficFleetQA/play.txt') -Raw).StartsWith('PASS:')){throw 'Native traffic regression must pass.'}
foreach($stage in @('city','shop')) {
    if(!(Get-Content -LiteralPath (Join-Path $qa "Library/StreetLifeQA/$stage.txt") -Raw).StartsWith('PASS ')){throw "Street pedestrian $stage regression must pass."}
    foreach($line in Get-Content -LiteralPath (Join-Path $qa "Library/StreetLifeQA/$stage-inputs.sha256")) {
        $relative=$line.Substring(65)
        if((Hash (Join-Path $qa $relative)) -ne $line.Substring(0,64)){throw "Street pedestrian tested input changed: $relative"}
    }
}
$play=Get-Content -LiteralPath (Join-Path $report 'play-inputs.sha256')
$city=Get-Content -LiteralPath (Join-Path $report 'city-inputs.sha256')
if(($play -join "`n") -ne ($city -join "`n")){throw 'Parking and city checks used different inputs.'}
foreach($line in $play) {
    $relative=$line.Substring(65);if((Hash (Join-Path $qa $relative)) -ne $line.Substring(0,64)){throw "Tested input changed: $relative"}
}
$scopes=@('Assets/Art/MobilityUpdate','Assets/Resources/Parking','Assets/Prefabs/City','Assets/Prefabs/Structures','Assets/Prefabs/StoreUpgrade','Assets/Prefabs/StarterStore')
$candidates=@('Assets/Art/MobilityUpdate.meta','Assets/Resources/Parking.meta','Assets/Prefabs/TownWorld/TownEnvironment.prefab','Assets/Project/Scenes/Game.unity','Assets/Project/Scenes/City.unity')
foreach($scope in $scopes) {$candidates+=Get-ChildItem -LiteralPath (Join-Path $qa $scope) -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($qa,$_.FullName).Replace('\','/')}}
$changed=@($candidates | Sort-Object -Unique | Where-Object {!(Test-Path -LiteralPath (Join-Path $projectRoot $_)) -or (Hash (Join-Path $qa $_)) -ne (Hash (Join-Path $projectRoot $_))})
if($changed.Count -gt 50){throw "Unexpectedly broad asset update ($($changed.Count) files); inspect scope before installation."}
foreach($relative in $changed) {
    $current=Join-Path $projectRoot $relative
    if(Test-Path -LiteralPath $current) {
        $before=Join-Path $projectRoot ('Library/MobilityUpdate/Before/'+$relative)
        if(!(Test-Path -LiteralPath $before) -or (Hash $current) -ne (Hash $before)){throw "User-edited asset since snapshot: $relative"}
    } elseif(!$relative.StartsWith('Assets/Art/MobilityUpdate') -and !$relative.StartsWith('Assets/Resources/Parking')) {throw "Unexpected new asset: $relative"}
}
$assetSet=[Collections.Generic.HashSet[string]]::new([string[]]$candidates,[StringComparer]::OrdinalIgnoreCase)
foreach($line in $play) {
    $relative=$line.Substring(65)
    if(!$assetSet.Contains($relative) -and (Hash (Join-Path $projectRoot $relative)) -ne $line.Substring(0,64)){throw "Main code/dependency differs from tested version: $relative"}
}
$backup=Join-Path $projectRoot ('Library/MobilityUpdate/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$receipt=@()
foreach($relative in $changed) {
    $target=[IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
    if(!$target.StartsWith($projectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Installation escaped workspace.'}
    if(Test-Path -LiteralPath $target) {$old=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $old -Parent) -Force | Out-Null;Copy-Item -LiteralPath $target -Destination $old}
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $qa $relative) -Destination $target -Force
    $expected=Hash (Join-Path $qa $relative);if((Hash $target) -ne $expected){throw "Installation verification failed: $relative"}
    $receipt+=[PSCustomObject]@{path=$relative;sha256=$expected}
}
$receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'Library/MobilityUpdate/installed.json') -Encoding utf8
$docs=Join-Path $projectRoot 'Docs/MobilityUpdate';New-Item -ItemType Directory -Path $docs -Force | Out-Null
foreach($name in @('build.txt','play.txt','city.txt','city-details.txt','parking.txt','queue.txt','bus-platforms.txt','play-inputs.sha256','city-inputs.sha256')) {Copy-Item -LiteralPath (Join-Path $report $name) -Destination $docs -Force}
foreach($name in @('StoreJoints.png','Parking.png','CityStore.png','StreetScale.png','BusStop.png','UrbanBusStop.png','CrossingClosed.png','CrossingOpen.png')) {Copy-Item -LiteralPath (Join-Path $qa ('Docs/MobilityUpdate/'+$name)) -Destination $docs -Force}
Copy-Item -LiteralPath (Join-Path $qa 'Library/ShopperQA/validation.txt') -Destination (Join-Path $docs 'Shoppers.txt') -Force
foreach($name in @('play.txt','districts.txt','signal-and-obstacle.txt','crossing.txt')) {Copy-Item -LiteralPath (Join-Path $qa ('Library/TrafficFleetQA/'+$name)) -Destination (Join-Path $docs ('Traffic-'+$name)) -Force}
foreach($name in @('city.txt','shop.txt','city-details.txt','shop-details.txt')) {Copy-Item -LiteralPath (Join-Path $qa ('Library/StreetLifeQA/'+$name)) -Destination (Join-Path $docs ('Street-'+$name)) -Force}
Write-Output "Installed and verified $($changed.Count) scoped asset files. Backup: $backup. Player saves and Git were not touched."
