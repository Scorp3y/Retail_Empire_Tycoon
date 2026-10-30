param([string]$Project=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/StaffQA'
$before=Join-Path $root 'Library/StaffUpdate/Before'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
foreach($name in @('build','play','routes','duties','journey','suppliers')){
    $path=Join-Path $report ($name+'.txt')
    if(!(Get-Content -LiteralPath $path -Raw).StartsWith('PASS') -or (Get-Item $path).LastWriteTimeUtc -lt (Get-Item $before).CreationTimeUtc){throw "Missing fresh $name validation."}
}
$supplierReport=Get-Content -LiteralPath (Join-Path $report 'suppliers.txt') -Raw
foreach($name in @('Wholesale','Bakery','Farmers','Equipment','Construction','Decoration')){if(!$supplierReport.Contains('PASS '+$name)){throw "Supplier round trip not verified: $name"}}
$outputs=@(Get-Content -LiteralPath (Join-Path $report 'outputs.txt') | Sort-Object -Unique)
foreach($relative in $outputs){
    $allowed=$relative -match '^Assets/Art/Staff(\.meta|/)'
    $allowed=$allowed -or $relative -match '^Assets/Art/ShopUi/Items/Staff_(Cashier|Guard|Stocker|Cleaner|Driver)\.png(\.meta)?$'
    $allowed=$allowed -or $relative -match '^Assets/Project/(Editor/Staff[^/]+|Scripts/Logistics/Driver[^/]+|Scripts/StoreOperations/StaffAppearance)\.cs\.meta$'
    $allowed=$allowed -or $relative -in @('Assets/Project/Scenes/Game.unity','Assets/Resources/ShopUi/Theme.asset')
    if(!$allowed){throw "Unexpected generated output: $relative"}
}
if($outputs.Count -gt 180){throw 'Unexpectedly large output set.'}
$set=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
$mismatches=@()
foreach($line in Get-Content -LiteralPath (Join-Path $report 'inputs.sha256')){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "QA input changed since test: $relative"}
    if(!$set.Contains($relative) -and (!(Test-Path -LiteralPath (Join-Path $root $relative)) -or (Hash (Join-Path $root $relative)) -ne $expected)){$mismatches+=$relative}
}
if($mismatches.Count){$mismatches | Set-Content -LiteralPath (Join-Path $root 'Library/StaffUpdate/input-mismatches.txt');throw "Main inputs differ from tested QA: $($mismatches.Count)."}
$changed=@($outputs | Where-Object {!(Test-Path -LiteralPath (Join-Path $root $_)) -or (Hash (Join-Path $root $_)) -ne (Hash (Join-Path $qa $_))})
foreach($relative in $changed){
    $target=Join-Path $root $relative
    if(Test-Path -LiteralPath $target){$snapshot=Join-Path $before $relative;if(!(Test-Path -LiteralPath $snapshot) -or (Hash $snapshot) -ne (Hash $target)){throw "Asset changed since baseline: $relative"}}
}
$backup=Join-Path $root ('Library/StaffUpdate/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
foreach($relative in $changed){
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output escaped workspace.'}
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    if($relative.EndsWith('.meta') -and (Test-Path -LiteralPath (Join-Path $qa $relative.Substring(0,$relative.Length-5)) -PathType Container)){
        New-Item -ItemType Directory -Path $target.Substring(0,$target.Length-5) -Force | Out-Null
    }
    $stage=Join-Path (Split-Path $target -Parent) ('.codex-staff-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy((Join-Path $qa $relative),$stage)
        if(Test-Path -LiteralPath $target){$copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy -Parent) -Force | Out-Null;[IO.File]::Replace($stage,$target,$copy)}
        else{[IO.File]::Move($stage,$target)}
    }finally{if(Test-Path -LiteralPath $stage){Remove-Item -LiteralPath $stage}}
    if((Hash $target) -ne (Hash (Join-Path $qa $relative))){throw "Install hash mismatch: $relative"}
}
$outputs | ForEach-Object {[PSCustomObject]@{path=$_;sha256=Hash (Join-Path $root $_)}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'Library/StaffUpdate/installed.json') -Encoding utf8
$docs=Join-Path $root 'Docs/StaffUpdate';New-Item -ItemType Directory -Path $docs -Force | Out-Null
foreach($name in @('build','play','routes','duties','journey','suppliers','outputs')){Copy-Item -LiteralPath (Join-Path $report ($name+'.txt')) -Destination $docs -Force}
Copy-Item -LiteralPath (Join-Path $report 'inputs.sha256') -Destination $docs -Force
Copy-Item -Path (Join-Path $qa 'Docs/StaffUpdate/Staff*.png'),(Join-Path $qa 'Docs/StaffUpdate/Driver*.png') -Destination $docs -Force
Write-Output "Installed $($changed.Count) files from $($outputs.Count) verified outputs. Backup: $backup. Player save and Git history untouched."
