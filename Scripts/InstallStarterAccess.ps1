param([string]$Project=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/StarterAccessQA'
$before=Join-Path $root 'Library/StarterAccessUpdate/Before'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
foreach($name in @('build','play','layout','migration','city','placement','cycle','workspace','language')){
    $path=Join-Path $report "$name.txt"
    if(!(Get-Content -LiteralPath $path -Raw).StartsWith('PASS ') -or (Get-Item $path).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh $name validation."}
}
foreach($relative in @('Library/MultiStoreyQA/result.txt','Library/TrafficFleetQA/play.txt')){
    $path=Join-Path $qa $relative
    if(!(Get-Content -LiteralPath $path -Raw).StartsWith('PASS') -or (Get-Item $path).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh regression: $relative"}
}
$outputs=@(Get-Content -LiteralPath (Join-Path $report 'outputs.txt'))
if(($outputs | Sort-Object) -join ';' -ne 'Assets/Prefabs/StarterStore/StarterStore.asset;Assets/Project/Scenes/Game.unity'){throw 'Unexpected output scope.'}
$set=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
$receipts=Get-Content -LiteralPath (Join-Path $report 'play-inputs.sha256')
foreach($line in $receipts){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "Tested input changed: $relative"}
    if(!$set.Contains($relative) -and (Hash (Join-Path $root $relative)) -ne $expected){throw "Main dependency/source differs: $relative"}
}
$changed=@($outputs | Where-Object {(Hash (Join-Path $root $_)) -ne (Hash (Join-Path $qa $_))})
foreach($relative in $changed){
    if((Hash (Join-Path $before $relative)) -ne (Hash (Join-Path $root $relative))){throw "User-edited asset since snapshot: $relative"}
}
$backup=Join-Path $root ('Library/StarterAccessUpdate/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach($relative in $changed){
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output escaped workspace.'}
    $copy=Join-Path $backup $relative
    New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null
    $staged=Join-Path (Split-Path $target -Parent) ('.codex-install-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy((Join-Path $qa $relative),$staged)
        # Atomic replacement also supports Unity's memory-mapped asset readers.
        [IO.File]::Replace($staged,$target,$copy)
    }finally{if(Test-Path -LiteralPath $staged){Remove-Item -LiteralPath $staged}}
}
$installed=@($outputs | ForEach-Object {
    $hash=Hash (Join-Path $qa $_)
    if((Hash (Join-Path $root $_)) -ne $hash){throw "Final output mismatch: $_"}
    [PSCustomObject]@{path=$_;sha256=$hash}
})
$installed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'Library/StarterAccessUpdate/installed.json') -Encoding utf8
$docs=Join-Path $root 'Docs/StarterAccess';New-Item -ItemType Directory -Path $docs -Force | Out-Null
foreach($name in @('build','play','layout','migration','city','placement','cycle','workspace','language')){
    Copy-Item -LiteralPath (Join-Path $report ($name+'.txt')) -Destination $docs -Force
}
Copy-Item -LiteralPath (Join-Path $report 'play-inputs.sha256') -Destination $docs -Force
foreach($name in @('Overview','Workspace_Floor1','Workspace_Floor2','CeilingOverWall','WallSeparation','CityStore','StoreAndParking')){
    Copy-Item -LiteralPath (Join-Path $qa ('Docs/StarterAccess/'+$name+'.png')) -Destination $docs -Force
}
Copy-Item -LiteralPath (Join-Path $qa 'Library/MultiStoreyQA/result.txt') -Destination (Join-Path $docs 'Storeys.txt') -Force
Copy-Item -LiteralPath (Join-Path $qa 'Library/TrafficFleetQA/play.txt') -Destination (Join-Path $docs 'TrafficFleet.txt') -Force
Write-Output "Verified $($outputs.Count) assets; installed $($changed.Count). Backup: $backup. No player save or Git operation."
