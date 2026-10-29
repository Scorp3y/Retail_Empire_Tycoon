param([string]$Project=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/MobilityPolishQA'
$before=Join-Path $root 'Library/MobilityPolishUpdate/Before'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
foreach($name in @('build','play','transports','vehicles','showroom')){
    $path=Join-Path $report ($name+'.txt')
    if(!(Get-Content -LiteralPath $path -Raw).StartsWith('PASS') -or (Get-Item $path).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh $name validation."}
}
foreach($path in @('Library/MultiStoreyQA/result.txt','Library/ElevatorModelQA/play.txt')){
    $test=Join-Path $qa $path
    if(!(Get-Content -LiteralPath $test -Raw).StartsWith('PASS') -or (Get-Item $test).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh regression: $path"}
}
$outputs=@(Get-Content -LiteralPath (Join-Path $report 'outputs.txt') | Sort-Object -Unique)
foreach($relative in $outputs){
    if($relative -notmatch '^Assets/Art/(ElevatorModel|EscalatorModel|StoreyTransport|MobilityPolish)(\.meta|/)' -and $relative -notmatch '^Assets/Resources/MultiStorey/(elevator_01|stairs_01|escalator_01|escalator_down_01)\.asset(\.meta)?$' -and $relative -notmatch '^Assets/Art/ShopUi/Items/(elevator_01|stairs_01|escalator_01|escalator_down_01)\.png(\.meta)?$' -and $relative -ne 'Assets/Prefabs/TownWorld/TownEnvironment.prefab'){throw "Unexpected output: $relative"}
}
if($outputs.Count -gt 120){throw 'Unexpectedly large output set.'}
$set=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
$mismatches=@()
foreach($line in Get-Content -LiteralPath (Join-Path $report 'inputs.sha256')){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "QA input changed since test: $relative"}
    if(!$set.Contains($relative) -and (!(Test-Path -LiteralPath (Join-Path $root $relative)) -or (Hash (Join-Path $root $relative)) -ne $expected)){$mismatches+=$relative}
}
if($mismatches.Count){$mismatches | Set-Content -LiteralPath (Join-Path $root 'Library/MobilityPolishUpdate/input-mismatches.txt');throw "Main inputs differ from tested QA: $($mismatches.Count)."}

# Unity YAML uses stable file IDs. Verify that every existing component outside the two
# parent child lists and TownWorldPlaces remains byte-identical after newline normalization.
function Blocks([string]$path){
    $result=@{};$text=[IO.File]::ReadAllText($path).Replace("`r`n","`n")
    foreach($match in [regex]::Matches($text,'(?ms)^--- !u!(?<type>\d+) &(?<id>-?\d+)[^\n]*\n.*?(?=^--- !u!|\z)')){
        $result[$match.Groups['id'].Value]=[PSCustomObject]@{type=$match.Groups['type'].Value;text=$match.Value.TrimEnd()}
    }
    return $result
}
$town='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$oldBlocks=Blocks (Join-Path $before $town);$newBlocks=Blocks (Join-Path $qa $town)
$changedBlocks=@()
foreach($id in $oldBlocks.Keys){
    if(!$newBlocks.ContainsKey($id)){throw "Existing town object removed: $id"}
    if($oldBlocks[$id].text -eq $newBlocks[$id].text){continue}
    $old=$oldBlocks[$id];$new=$newBlocks[$id]
    if($old.type -eq '4'){
        # Parent transforms may only gain child references; their pose, parent and all other fields must stay unchanged.
        $oldPose=[regex]::Replace($old.text,'(?ms)^  m_Children:.*?(?=^  m_Father:)','')
        $newPose=[regex]::Replace($new.text,'(?ms)^  m_Children:.*?(?=^  m_Father:)','')
        if($oldPose -ne $newPose){throw "Existing town transform changed pose: $id"}
        $changedBlocks+=$id;continue
    }
    if($old.type -eq '114' -and $new.text.Contains('  showroomEntrances:')){
        $without=[regex]::Replace($new.text,'(?ms)^  showroomEntrances:.*?(?=^  [A-Za-z_]|\z)','').TrimEnd()
        $oldWithout=[regex]::Replace($old.text,'(?ms)^  showroomEntrances:.*?(?=^  [A-Za-z_]|\z)','').TrimEnd()
        if($without -ne $oldWithout){throw 'Town supplier or layout bindings changed.'}
        $changedBlocks+=$id;continue
    }
    throw "Unexpected existing town component edit: $id (type $($old.type))"
}
if($changedBlocks.Count -ne 3){throw "Expected two parent child lists and one entrance array, found $($changedBlocks.Count)."}
"PASS all existing town YAML objects preserved; only two parent child lists and showroomEntrances changed; no existing geometry, materials, colliders, supplier entries or traffic changed." | Set-Content -LiteralPath (Join-Path $report 'protection.txt')

$changed=@($outputs | Where-Object {!(Test-Path -LiteralPath (Join-Path $root $_)) -or (Hash (Join-Path $root $_)) -ne (Hash (Join-Path $qa $_))})
foreach($relative in $changed){
    $target=Join-Path $root $relative
    if(Test-Path -LiteralPath $target){$snapshot=Join-Path $before $relative;if(!(Test-Path -LiteralPath $snapshot) -or (Hash $snapshot) -ne (Hash $target)){throw "Asset edited after baseline: $relative"}}
}
$backup=Join-Path $root ('Library/MobilityPolishUpdate/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach($relative in $changed){
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output escaped workspace.'}
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    $staged=Join-Path (Split-Path $target -Parent) ('.codex-mobility-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy((Join-Path $qa $relative),$staged)
        if(Test-Path -LiteralPath $target){$copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null;[IO.File]::Replace($staged,$target,$copy)}
        else{[IO.File]::Move($staged,$target)}
    }finally{if(Test-Path -LiteralPath $staged){Remove-Item -LiteralPath $staged}}
    if((Hash $target) -ne (Hash (Join-Path $qa $relative))){throw "Install mismatch: $relative"}
}
$outputs | ForEach-Object {[PSCustomObject]@{path=$_;sha256=Hash (Join-Path $root $_)}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'Library/MobilityPolishUpdate/installed.json') -Encoding utf8
$docs=Join-Path $root 'Docs/MobilityPolish';New-Item -ItemType Directory -Path $docs -Force | Out-Null
Copy-Item -Path (Join-Path $report '*.txt'),(Join-Path $report '*.sha256') -Destination $docs -Force
Copy-Item -Path (Join-Path $qa 'Docs/MobilityPolish/*.png') -Destination $docs -Force
Copy-Item -LiteralPath (Join-Path $qa 'Library/MultiStoreyQA/result.txt') -Destination (Join-Path $docs 'Storeys.txt') -Force
Copy-Item -LiteralPath (Join-Path $qa 'Library/ElevatorModelQA/play.txt') -Destination (Join-Path $docs 'ElevatorCycle.txt') -Force
Write-Output "Installed $($changed.Count) files from $($outputs.Count) verified outputs. Backup: $backup. No save or Git operation."
