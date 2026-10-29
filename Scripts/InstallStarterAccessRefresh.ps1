param([string]$Project=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/StarterAccessRefreshQA'
$before=Join-Path $root 'Library/StarterAccessRefresh/Before'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
foreach($name in @('build','play','economy','catalog','dock','frontage')){
    $path=Join-Path $report ($name+'.txt')
    if(!(Get-Content -LiteralPath $path -Raw).StartsWith('PASS') -or (Get-Item $path).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh $name validation."}
}
foreach($path in @('Library/MultiStoreyQA/result.txt','Library/ElevatorModelQA/play.txt')){
    $test=Join-Path $qa $path
    if(!(Get-Content -LiteralPath $test -Raw).StartsWith('PASS') -or (Get-Item $test).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh regression: $path"}
}
$outputs=@(Get-Content -LiteralPath (Join-Path $report 'outputs.txt') | Sort-Object -Unique)
foreach($relative in $outputs){
    $allowed=$relative -match '^Assets/Art/StarterAccessRefresh(\.meta|/)'
    $allowed=$allowed -or $relative -match '^Assets/Project/Scenes/(Game|City)\.unity$'
    $allowed=$allowed -or $relative -in @('Assets/Prefabs/TownWorld/TownEnvironment.prefab','Assets/Prefabs/StarterStore/StarterStore.asset','Assets/Art/EscalatorModel/Escalator.prefab','Assets/Art/MobilityPolish/DownEscalator.prefab')
    $allowed=$allowed -or $relative -match '^Assets/Resources/MultiStorey/(escalator_01|escalator_down_01)\.asset$'
    $allowed=$allowed -or $relative -match '^Assets/Art/ShopUi/Items/(escalator_01|escalator_down_01)\.png(\.meta)?$'
    if(!$allowed){throw "Unexpected output: $relative"}
}
if($outputs.Count -gt 32){throw 'Unexpectedly large output set.'}
$set=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
$mismatches=@()
foreach($line in Get-Content -LiteralPath (Join-Path $report 'inputs.sha256')){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "QA input changed since test: $relative"}
    if(!$set.Contains($relative) -and (!(Test-Path -LiteralPath (Join-Path $root $relative)) -or (Hash (Join-Path $root $relative)) -ne $expected)){$mismatches+=$relative}
}
if($mismatches.Count){$mismatches | Set-Content -LiteralPath (Join-Path $root 'Library/StarterAccessRefresh/input-mismatches.txt');throw "Main inputs differ from tested QA: $($mismatches.Count)."}
$changed=@($outputs | Where-Object {!(Test-Path -LiteralPath (Join-Path $root $_)) -or (Hash (Join-Path $root $_)) -ne (Hash (Join-Path $qa $_))})
foreach($relative in $changed){
    $target=Join-Path $root $relative
    if(Test-Path -LiteralPath $target){$snapshot=Join-Path $before $relative;if(!(Test-Path -LiteralPath $snapshot) -or (Hash $snapshot) -ne (Hash $target)){throw "Asset edited after baseline: $relative"}}
}
$backup=Join-Path $root ('Library/StarterAccessRefresh/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach($relative in $changed){
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output escaped workspace.'}
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    $staged=Join-Path (Split-Path $target -Parent) ('.codex-starter-access-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy((Join-Path $qa $relative),$staged)
        if(Test-Path -LiteralPath $target){$copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null;[IO.File]::Replace($staged,$target,$copy)}
        else{[IO.File]::Move($staged,$target)}
    }finally{if(Test-Path -LiteralPath $staged){Remove-Item -LiteralPath $staged}}
    if((Hash $target) -ne (Hash (Join-Path $qa $relative))){throw "Install mismatch: $relative"}
}
$outputs | ForEach-Object {[PSCustomObject]@{path=$_;sha256=Hash (Join-Path $root $_)}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'Library/StarterAccessRefresh/installed.json') -Encoding utf8
foreach($name in @('StarterAccessRefreshBatch','StarterAccessRefreshPlay')){
    $relative='Assets/Project/Editor/'+$name+'.cs.meta'
    if(!(Test-Path -LiteralPath (Join-Path $root $relative))){Copy-Item -LiteralPath (Join-Path $qa $relative) -Destination (Join-Path $root $relative)}
}
$docs=Join-Path $root 'Docs/StarterAccessRefresh';New-Item -ItemType Directory -Path $docs -Force | Out-Null
Copy-Item -Path (Join-Path $report '*.txt'),(Join-Path $report '*.sha256') -Destination $docs -Force
Copy-Item -Path (Join-Path $qa 'Docs/StarterAccessRefresh/*.png') -Destination $docs -Force
Copy-Item -LiteralPath (Join-Path $qa 'Library/MultiStoreyQA/result.txt') -Destination (Join-Path $docs 'Storeys.txt') -Force
Copy-Item -LiteralPath (Join-Path $qa 'Library/ElevatorModelQA/play.txt') -Destination (Join-Path $docs 'ElevatorCycle.txt') -Force
Write-Output "Installed $($changed.Count) files from $($outputs.Count) verified outputs. Backup: $backup. No save or Git operation."
