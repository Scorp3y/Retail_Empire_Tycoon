param([string]$Project=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/PlayerVehicleQA'
$before=Join-Path $root 'Library/PlayerVehicleUpdate/Before'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
foreach($name in @('build','play','rules','purchase','physics','city','showroom','protection')){
    $path=Join-Path $report ($name+'.txt')
    if(!(Test-Path -LiteralPath $path) -or !(Get-Content -LiteralPath $path -Raw).StartsWith('PASS') -or (Get-Item $path).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh $name validation."}
}
$outputs=@(Get-Content -LiteralPath (Join-Path $report 'outputs.txt') | Sort-Object -Unique)
foreach($relative in $outputs){
    if($relative -notmatch '^Assets/Art/PlayerVehicles(\.meta|/)' -and $relative -notmatch '^Assets/Resources/PlayerVehicles(\.meta|/)' -and $relative -notmatch '^Assets/Art/ShopUi/Items/player_[a-z_]+\.png(\.meta)?$' -and $relative -notmatch '^Assets/Art/TownShowroom/Meshes/FleetTrim\d+\.asset(\.meta)?$' -and $relative -ne 'Assets/Prefabs/TownWorld/TownEnvironment.prefab'){throw "Unexpected output: $relative"}
}
if($outputs.Count -gt 180){throw 'Unexpectedly large output set.'}
$outputSet=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
$mismatches=@()
foreach($line in Get-Content -LiteralPath (Join-Path $report 'inputs.sha256')){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "Tested QA input changed: $relative"}
    if(!$outputSet.Contains($relative) -and (!(Test-Path -LiteralPath (Join-Path $root $relative)) -or (Hash (Join-Path $root $relative)) -ne $expected)){$mismatches+=$relative}
}
if($mismatches.Count -gt 0){$mismatches | Set-Content -LiteralPath (Join-Path $root 'Library/PlayerVehicleUpdate/input-mismatches.txt');throw "Main has $($mismatches.Count) inputs different from tested QA. See input-mismatches.txt."}
# Catalog/prefab script references must resolve to the exact authoring GUIDs.
foreach($source in @('PlayerVehicleDefinition','PlayerVehicleModel','PlayerVehicleAppearance','PlayerVehicleFleet')){
    $relative="Assets/Project/Scripts/City/$source.cs.meta"
    if(!(Test-Path -LiteralPath (Join-Path $root $relative)) -or (Hash (Join-Path $root $relative)) -ne (Hash (Join-Path $qa $relative))){throw "Vehicle script metadata mismatch: $relative"}
}
$town='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
if((Hash (Join-Path $root $town)) -ne (Hash (Join-Path $before $town))){throw 'Town prefab changed since task baseline. Preserve user changes before installation.'}
$backup=Join-Path $root ('Library/PlayerVehicleUpdate/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$changed=@($outputs | Where-Object {!(Test-Path -LiteralPath (Join-Path $root $_)) -or (Hash (Join-Path $root $_)) -ne (Hash (Join-Path $qa $_))})
foreach($relative in $changed){
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output escaped workspace.'}
    if((Test-Path -LiteralPath $target) -and $relative -ne $town){throw "New asset already exists with different content: $relative"}
}
foreach($relative in $changed){
    $target=Join-Path $root $relative;$source=Join-Path $qa $relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    $staged=Join-Path (Split-Path $target -Parent) ('.codex-vehicle-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy($source,$staged)
        if(Test-Path -LiteralPath $target){
            $copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null
            [IO.File]::Replace($staged,$target,$copy)
        }else{[IO.File]::Move($staged,$target)}
    }finally{if(Test-Path -LiteralPath $staged){Remove-Item -LiteralPath $staged}}
    if((Hash $target) -ne (Hash $source)){throw "Installed content differs: $relative"}
}
$outputs | ForEach-Object {[PSCustomObject]@{path=$_;sha256=Hash (Join-Path $root $_)}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'Library/PlayerVehicleUpdate/installed.json') -Encoding utf8
$docs=Join-Path $root 'Docs/PlayerVehicles';New-Item -ItemType Directory -Path $docs -Force | Out-Null
Copy-Item -Path (Join-Path $report '*.txt'),(Join-Path $report '*.sha256') -Destination $docs -Force
Copy-Item -Path (Join-Path $qa 'Docs/PlayerVehicles/*.png') -Destination $docs -Force
Write-Output "Installed $($changed.Count) files from $($outputs.Count) checked outputs. Backup: $backup. Player saves and Git untouched."
