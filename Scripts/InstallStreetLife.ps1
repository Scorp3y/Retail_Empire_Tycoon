param([string]$Project = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa = Join-Path $projectRoot 'Library/CityUpdateQA/IsolatedProject'
$report = Join-Path $qa 'Library/StreetLifeQA'
function Hash([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash }
foreach ($mode in @('city','shop')) {
    if (!(Get-Content -LiteralPath (Join-Path $report "$mode.txt") -Raw).StartsWith('PASS ')) { throw "Missing passing $mode Play validation." }
}
$cityInputs = Get-Content -LiteralPath (Join-Path $report 'city-inputs.sha256')
$shopInputs = Get-Content -LiteralPath (Join-Path $report 'shop-inputs.sha256')
if (($cityInputs -join "`n") -ne ($shopInputs -join "`n")) { throw 'City and Game were not tested against identical inputs.' }
$town = 'Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$install = @($town,'Assets/Art/StreetLife.meta')
$install += Get-ChildItem -LiteralPath (Join-Path $qa 'Assets/Art/StreetLife') -File -Recurse |
    ForEach-Object { [IO.Path]::GetRelativePath($qa,$_.FullName).Replace('\','/') }
foreach ($line in $cityInputs) {
    $expected=$line.Substring(0,64); $relative=$line.Substring(65)
    if ((Hash (Join-Path $qa $relative)) -ne $expected) { throw "QA input changed after validation: $relative" }
    if ($relative -notin $install -and (Hash (Join-Path $projectRoot $relative)) -ne $expected) { throw "Main input differs from tested version: $relative" }
}
foreach ($relative in @($town,'Assets/Project/Scenes/Game.unity','Assets/Project/Scenes/City.unity','Assets/Art/TrafficFleet/Data/RoadNetwork.asset')) {
    if ((Hash (Join-Path $projectRoot $relative)) -ne (Hash (Join-Path $projectRoot ('Library/StreetLifeUpdate/Before/'+$relative)))) {
        throw "Protected main file changed since preparation; preserve the user's work: $relative"
    }
}
$backup = Join-Path $projectRoot ('Library/StreetLifeUpdate/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$receipt = @()
foreach ($relative in $install) {
    $target = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
    if (!$target.StartsWith($projectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Installation escaped the project.' }
    if (Test-Path -LiteralPath $target) {
        $before=Join-Path $backup $relative
        New-Item -ItemType Directory -Path (Split-Path $before -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $before
    }
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $qa $relative) -Destination $target -Force
    $expected=Hash (Join-Path $qa $relative)
    if ((Hash $target) -ne $expected) { throw "Copy verification failed: $relative" }
    $receipt += [PSCustomObject]@{path=$relative;sha256=$expected}
}
$receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'Library/StreetLifeUpdate/installed.json') -Encoding utf8
$docs=Join-Path $projectRoot 'Docs/StreetLife'
New-Item -ItemType Directory -Path $docs -Force | Out-Null
Get-ChildItem -LiteralPath $report -File | Copy-Item -Destination $docs -Force
Get-ChildItem -LiteralPath (Join-Path $qa 'Docs/StreetLife') -File | Copy-Item -Destination $docs -Force
Write-Output "Installed $($receipt.Count) tested asset files. Scenes, road geometry and player saves were not modified. Backup: $backup"
