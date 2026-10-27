param([string]$Project=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/StorePolishQA'
$before=Join-Path $root 'Library/StorePolishUpdate/Before'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
$receipts=@()
foreach($stage in @('play','traffic-city','traffic-shop')){
    $result=Join-Path $report "$stage.txt"
    if(!(Get-Content -LiteralPath $result -Raw).StartsWith('PASS ') -or (Get-Item $result).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh $stage validation."}
    $lines=Get-Content -LiteralPath (Join-Path $report "$stage-inputs.sha256")
    if($receipts.Count -gt 0 -and ($lines -join "`n") -ne ($receipts -join "`n")){throw 'Final tests used different source/assets.'}
    $receipts=$lines
}
foreach($name in @('build','stock','city','placement','cycle','handoff','reroute')){
    if(!(Get-Content -LiteralPath (Join-Path $report "$name.txt") -Raw).StartsWith('PASS ')){throw "Missing $name validation."}
}
foreach($path in @('Library/MultiStoreyQA/result.txt','Library/TrafficFleetQA/play.txt')){
    $result=Join-Path $qa $path
    if(!(Get-Content -LiteralPath $result -Raw).StartsWith('PASS') -or (Get-Item $result).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh regression: $path"}
}
$outputs=@(Get-Content (Join-Path $report 'outputs.txt'))
$outputs+='Assets/Art/StorePolish.meta'
$outputs+=Get-ChildItem (Join-Path $qa 'Assets/Art/StorePolish') -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($qa,$_.FullName).Replace('\','/')}
$outputs=@($outputs | Sort-Object -Unique)
if($outputs.Count -gt 100){throw "Unexpected output scope: $($outputs.Count)"}
$set=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
foreach($line in $receipts){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "Tested input changed: $relative"}
    # This pre-existing QA-only visual inspection harness is not shipped or executed by these tests.
    if($relative -eq 'Assets/Project/Editor/ShowroomVisualAudit.cs'){continue}
    if(!$set.Contains($relative) -and (Hash (Join-Path $root $relative)) -ne $expected){throw "Main dependency/source differs: $relative"}
}
$changed=@($outputs | Where-Object {!(Test-Path -LiteralPath (Join-Path $root $_)) -or (Hash (Join-Path $root $_)) -ne (Hash (Join-Path $qa $_))})
foreach($relative in $changed){
    $target=Join-Path $root $relative
    if(Test-Path -LiteralPath $target){
        $snapshot=Join-Path $before $relative
        if(!(Test-Path -LiteralPath $snapshot) -or (Hash $snapshot) -ne (Hash $target)){throw "User-edited asset since snapshot: $relative"}
    }elseif(!$relative.StartsWith('Assets/Art/StorePolish')){throw "Unexpected new output: $relative"}
}
$backup=Join-Path $root ('Library/StorePolishUpdate/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$installed=@()
foreach($relative in $changed){
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output escaped workspace.'}
    if(Test-Path -LiteralPath $target){
        $copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $copy
    }
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $qa $relative) -Destination $target -Force
    $hash=Hash (Join-Path $qa $relative);if((Hash $target) -ne $hash){throw "Install hash mismatch: $relative"}
    $installed+=[PSCustomObject]@{path=$relative;sha256=$hash}
}
$installed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'Library/StorePolishUpdate/installed.json') -Encoding utf8
$docs=Join-Path $root 'Docs/StorePolish';New-Item -ItemType Directory -Path $docs -Force | Out-Null
foreach($name in @('build.txt','stock.txt','city.txt','placement.txt','cycle.txt','play.txt','traffic-city.txt','traffic-shop.txt','handoff.txt','reroute.txt','play-inputs.sha256','traffic-city-inputs.sha256','traffic-shop-inputs.sha256')){
    Copy-Item -LiteralPath (Join-Path $report $name) -Destination $docs -Force
}
foreach($name in @('StoreAndParking.png','Gate.png','CityStore.png')){
    Copy-Item -LiteralPath (Join-Path $qa ('Docs/StorePolish/'+$name)) -Destination $docs -Force
}
Copy-Item -LiteralPath (Join-Path $qa 'Library/MultiStoreyQA/result.txt') -Destination (Join-Path $docs 'Storeys.txt') -Force
Copy-Item -LiteralPath (Join-Path $qa 'Library/TrafficFleetQA/play.txt') -Destination (Join-Path $docs 'TrafficFleet.txt') -Force
Write-Output "Installed and hash-verified $($changed.Count) scoped asset files. Backup: $backup. No player save or Git operation."
