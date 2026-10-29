param([string]$Project=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/ElevatorModelQA'
$before=Join-Path $root 'Library/ElevatorModelUpdate/Before'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
if((Hash (Join-Path $root 'Assets/Project/Scripts/BuildSystem/LiftLandingDoors.cs.meta')) -ne (Hash (Join-Path $qa 'Assets/Project/Scripts/BuildSystem/LiftLandingDoors.cs.meta'))){throw 'Landing-door script GUID differs from the tested prefab.'}
function InstallFile([string]$source,[string]$target,[string]$backupPath){
    $staged=Join-Path (Split-Path $target -Parent) ('.codex-install-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy($source,$staged)
        if(Test-Path -LiteralPath $target){
            New-Item -ItemType Directory -Path (Split-Path $backupPath -Parent) -Force | Out-Null
            [IO.File]::Replace($staged,$target,$backupPath)
        }else{[IO.File]::Move($staged,$target)}
    }finally{if(Test-Path -LiteralPath $staged){Remove-Item -LiteralPath $staged}}
}
foreach($name in @('build','play','stock','city','placement','cycle','workspace','language','upstairs','doors')){
    $result=Join-Path $report "$name.txt"
    if(!(Get-Content -LiteralPath $result -Raw).StartsWith('PASS ') -or (Get-Item $result).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh $name validation."}
}
foreach($relative in @('Library/MultiStoreyQA/result.txt')){
    $result=Join-Path $qa $relative
    if(!(Get-Content -LiteralPath $result -Raw).StartsWith('PASS') -or (Get-Item $result).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh regression: $relative"}
}
$receipts=Get-Content -LiteralPath (Join-Path $report 'play-inputs.sha256')
$outputs=@(Get-Content (Join-Path $report 'outputs.txt'))
$outputs+='Assets/Art/ElevatorModel.meta'
$outputs+=Get-ChildItem (Join-Path $qa 'Assets/Art/ElevatorModel') -File -Recurse | ForEach-Object {[IO.Path]::GetRelativePath($qa,$_.FullName).Replace('\','/')}
$outputs=@($outputs | Sort-Object -Unique)
if($outputs.Count -gt 32 -or @($outputs | Where-Object {$_ -notmatch '^Assets/Art/ElevatorModel(\.meta|/)' -and $_ -ne 'Assets/Resources/MultiStorey/elevator_01.asset' -and $_ -ne 'Assets/Art/ShopUi/Items/elevator_01.png'}).Count -gt 0){throw "Unexpected output scope: $($outputs.Count)"}
$set=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
foreach($line in $receipts){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "Tested input changed: $relative"}
    if(!$set.Contains($relative) -and (Hash (Join-Path $root $relative)) -ne $expected){throw "Main dependency/source differs: $relative"}
}
$changed=@($outputs | Where-Object {!(Test-Path -LiteralPath (Join-Path $root $_)) -or (Hash (Join-Path $root $_)) -ne (Hash (Join-Path $qa $_))})
foreach($relative in $changed){
    $target=Join-Path $root $relative
    if(Test-Path -LiteralPath $target){
        $snapshot=Join-Path $before $relative
        if(!(Test-Path -LiteralPath $snapshot) -or (Hash $snapshot) -ne (Hash $target)){throw "User-edited asset since snapshot: $relative"}
    }elseif(!$relative.StartsWith('Assets/Art/ElevatorModel')){throw "Unexpected new output: $relative"}
}
$backup=Join-Path $root ('Library/ElevatorModelUpdate/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
$installed=@()
foreach($relative in $changed){
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output escaped workspace.'}
    if(Test-Path -LiteralPath $target){
        $copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $copy
    }
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    InstallFile (Join-Path $qa $relative) $target (Join-Path $backup ('Atomic/'+$relative))
    $hash=Hash (Join-Path $qa $relative);if((Hash $target) -ne $hash){throw "Install hash mismatch: $relative"}
}
$installed=@($outputs | ForEach-Object {
    $hash=Hash (Join-Path $qa $_)
    if((Hash (Join-Path $root $_)) -ne $hash){throw "Final output mismatch: $_"}
    [PSCustomObject]@{path=$_;sha256=$hash}
})
$installed | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'Library/ElevatorModelUpdate/installed.json') -Encoding utf8
$docs=Join-Path $root 'Docs/ElevatorModel';New-Item -ItemType Directory -Path $docs -Force | Out-Null
foreach($name in @('build.txt','play.txt','stock.txt','city.txt','placement.txt','cycle.txt','workspace.txt','language.txt','upstairs.txt','doors.txt','play-inputs.sha256')){
    Copy-Item -LiteralPath (Join-Path $report $name) -Destination $docs -Force
}
foreach($name in @('Model.png','Front.png','Overview.png','Workspace_Floor1.png','Workspace_Floor2.png','CityStore.png','Gate.png','StoreAndParking.png')){
    Copy-Item -LiteralPath (Join-Path $qa ('Docs/ElevatorModel/'+$name)) -Destination $docs -Force
}
Copy-Item -LiteralPath (Join-Path $qa 'Library/MultiStoreyQA/result.txt') -Destination (Join-Path $docs 'Storeys.txt') -Force
Write-Output "Verified $($outputs.Count) scoped asset files; installed $($changed.Count) in this pass. Backup: $backup. No player save or Git operation."



