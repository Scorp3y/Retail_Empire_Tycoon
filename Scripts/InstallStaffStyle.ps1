param([string]$Project=(Split-Path $PSScriptRoot -Parent),[ValidateSet('StaffStyle','StaffSoftening','StaffHands')][string]$Revision='StaffStyle')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/StaffStyleQA'
$tests=Join-Path $qa 'Library/StaffQA'
$before=Join-Path $root ('Library/'+$Revision+'/Before')
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
foreach($path in @((Join-Path $report 'build.txt'))+@('play','duties','journey','suppliers' | ForEach-Object {Join-Path $tests ($_+'.txt')})){
    if(!(Get-Content -LiteralPath $path -Raw).StartsWith('PASS') -or (Get-Item $path).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh validation: $path"}
}
$outputs=@(Get-Content -LiteralPath (Join-Path $report 'outputs.txt') | Sort-Object -Unique)
foreach($relative in $outputs){
    $role='(Cashier|Guard|Stocker|Cleaner|Driver)'
    $allowed=$relative -match ('^Assets/Art/Staff/(Models/'+$role+'\.fbx|Prefabs/'+$role+'\.prefab|'+$role+'\.controller|Staff\.mat|ShopperPalette\.png)(\.meta)?$')
    $allowed=$allowed -or $relative -match ('^Assets/Art/ShopUi/Items/Staff_'+$role+'\.png(\.meta)?$')
    $allowed=$allowed -or $relative -eq 'Assets/Resources/ShopUi/Theme.asset'
    if(!$allowed){throw "Unexpected appearance output: $relative"}
}
if($outputs.Count -ne 45){throw "Expected 45 appearance outputs, got $($outputs.Count)."}
$set=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
$mismatches=@()
foreach($line in Get-Content -LiteralPath (Join-Path $tests 'inputs.sha256')){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "QA input changed after tests: $relative"}
    if(!$set.Contains($relative) -and (!(Test-Path -LiteralPath (Join-Path $root $relative)) -or (Hash (Join-Path $root $relative)) -ne $expected)){$mismatches+=$relative}
}
if($mismatches.Count){$mismatches | Set-Content -LiteralPath (Join-Path $root ('Library/'+$Revision+'/input-mismatches.txt'));throw "Main differs from the verified copy: $($mismatches.Count) inputs."}
# Existing scene references must retain their asset identities when replacing the models.
foreach($relative in $outputs | Where-Object {$_ -like '*.meta'}){
    $old=[regex]::Match([IO.File]::ReadAllText((Join-Path $root $relative)),'(?m)^guid: (\w+)').Groups[1].Value
    $next=[regex]::Match([IO.File]::ReadAllText((Join-Path $qa $relative)),'(?m)^guid: (\w+)').Groups[1].Value
    if(!$old -or $old -ne $next){throw "Appearance GUID changed: $relative"}
}
$changed=@($outputs | Where-Object {(Hash (Join-Path $root $_)) -ne (Hash (Join-Path $qa $_))})
foreach($relative in $changed){if((Hash (Join-Path $root $relative)) -ne (Hash (Join-Path $before $relative))){throw "Asset edited since baseline: $relative"}}
$backup=Join-Path $root ('Library/'+$Revision+'/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach($relative in $changed){
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output escaped workspace'}
    $stage=Join-Path (Split-Path $target) ('.codex-staff-style-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    $copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy) -Force | Out-Null
    try{[IO.File]::Copy((Join-Path $qa $relative),$stage);[IO.File]::Replace($stage,$target,$copy)}finally{if(Test-Path -LiteralPath $stage){Remove-Item -LiteralPath $stage}}
    if((Hash $target) -ne (Hash (Join-Path $qa $relative))){throw "Install mismatch: $relative"}
}
$outputs | ForEach-Object {[PSCustomObject]@{path=$_;sha256=Hash (Join-Path $root $_)}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root ('Library/'+$Revision+'/installed.json')) -Encoding utf8
$docs=Join-Path $root ('Docs/'+$Revision);New-Item -ItemType Directory -Path $docs -Force | Out-Null
foreach($name in @('play','duties','journey','suppliers')){Copy-Item -LiteralPath (Join-Path $tests ($name+'.txt')) -Destination $docs -Force}
Copy-Item -LiteralPath (Join-Path $report 'build.txt'),(Join-Path $tests 'inputs.sha256') -Destination $docs -Force
Copy-Item -Path (Join-Path $qa 'Docs/StaffUpdate/Staff*.png') -Destination $docs -Force
Write-Output "Installed $($changed.Count) appearance files; all existing GUIDs retained. Scene and runtime job scripts unchanged. Backup: $backup"
