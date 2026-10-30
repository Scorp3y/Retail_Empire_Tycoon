param([string]$Project=(Split-Path $PSScriptRoot -Parent),[switch]$ValidateOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/AssortmentQA'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
foreach($name in @('build','play')){
    if(!(Get-Content -LiteralPath (Join-Path $report ($name+'.txt')) -Raw).StartsWith('PASS')){throw "Validation did not pass: $name"}
}
$definitions=Get-Content -LiteralPath (Join-Path $qa 'Assets/Art/Assortment/Definitions.json') -Raw | ConvertFrom-Json
if($definitions.products.Count -ne 48 -or $definitions.fixtures.Count -ne 4){throw 'Incomplete approved assortment'}
$scripts=@('Assets/Project/Scripts/Core/StoreAssortmentPack.cs','Assets/Project/Scripts/ProductSystem/ProductPlacementTypes.cs',
    'Assets/Project/Scripts/ProductSystem/ProductCatalog.cs','Assets/Project/Scripts/BuildSystem/BuildItemCatalog.cs',
    'Assets/Project/Scripts/UI/Shop/ShopWindow.cs','Assets/Project/Scripts/UI/Shop/ShopText.cs',
    'Assets/Project/Editor/AssortmentUpdateBatch.cs','Assets/Project/Editor/AssortmentUpdatePlay.cs','Assets/Project/Editor/AssortmentUpdateReview.cs')
$allowedIcons=@($definitions.products | ForEach-Object {'Assets/Art/ShopUi/Items/product_'+$_.key+'.png'})+
    @($definitions.fixtures | ForEach-Object {'Assets/Art/ShopUi/Items/shelf_'+$_.key+'_01.png'})
$outputs=@(Get-Content -LiteralPath (Join-Path $report 'outputs.txt') | Sort-Object -Unique)
foreach($relative in $outputs){
    $plain=$relative -replace '\.meta$',''
    $allowed=$relative -match '^Assets/(Art/Assortment|Resources/StoreAssortment)(/|\.meta$)'
    $allowed=$allowed -or $scripts.Contains($plain) -or $allowedIcons.Contains($plain)
    if(!$allowed -or $relative.Contains('..') -or $relative -like '*.unity'){throw "Unexpected output: $relative"}
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Output escaped project'}
}
$set=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
$mismatches=@()
foreach($line in Get-Content -LiteralPath (Join-Path $report 'inputs.sha256')){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "QA changed after checks: $relative"}
    if(!$set.Contains($relative) -and (!(Test-Path -LiteralPath (Join-Path $root $relative)) -or (Hash (Join-Path $root $relative)) -ne $expected)){$mismatches+=$relative}
}
if($mismatches.Count){$mismatches | Set-Content -LiteralPath (Join-Path $root 'Library/AssortmentUpdate/input-mismatches.txt');throw "Main differs from verified QA: $($mismatches.Count) inputs"}
$baseline=Get-Content -LiteralPath (Join-Path $root 'Library/AssortmentUpdate/baseline.json') -Raw | ConvertFrom-Json
foreach($entry in $baseline){
    if(!$set.Contains($entry.path) -and (Hash (Join-Path $root $entry.path)) -ne $entry.sha256){throw "Project edited during generation: $($entry.path)"}
}
foreach($relative in $outputs){
    $target=Join-Path $root $relative
    if(!(Test-Path -LiteralPath $target)){continue}
    if($relative -like '*.meta'){
        $old=[regex]::Match([IO.File]::ReadAllText($target),'(?m)^guid: (\w+)').Groups[1].Value
        $next=[regex]::Match([IO.File]::ReadAllText((Join-Path $qa $relative)),'(?m)^guid: (\w+)').Groups[1].Value
        if(!$old -or $old -ne $next){throw "Existing asset GUID changed: $relative"}
    }
    if((Hash $target) -eq (Hash (Join-Path $qa $relative))){continue}
    if(!$scripts.Contains(($relative -replace '\.meta$',''))){throw "New assortment would overwrite an existing asset: $relative"}
    # Source scripts were edited in the main workspace; they must exactly match the tested copy.
    throw "Source changed after QA sync: $relative"
}
Write-Output "Validated $($outputs.Count) outputs, 48 products, four fixtures, original GUIDs and unchanged scenes."
if($ValidateOnly){return}
$backup=Join-Path $root ('Library/AssortmentUpdate/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$installed=0
foreach($relative in $outputs){
    $target=Join-Path $root $relative;$source=Join-Path $qa $relative
    if((Test-Path -LiteralPath $target) -and (Hash $target) -eq (Hash $source)){continue}
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    $stage=Join-Path (Split-Path $target) ('.codex-assortment-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy($source,$stage)
        if(Test-Path -LiteralPath $target){
            $copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy) -Force | Out-Null
            [IO.File]::Replace($stage,$target,$copy)
        }else{[IO.File]::Move($stage,$target)}
    }finally{if(Test-Path -LiteralPath $stage){Remove-Item -LiteralPath $stage}}
    if((Hash $target) -ne (Hash $source)){throw "Installed hash mismatch: $relative"}
    $installed++
}
$outputs | ForEach-Object {[PSCustomObject]@{path=$_;sha256=Hash (Join-Path $root $_)}} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $root 'Library/AssortmentUpdate/installed.json') -Encoding utf8
$docs=Join-Path $root 'Docs/AssortmentUpdate';New-Item -ItemType Directory -Path $docs -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $qa 'Docs/AssortmentUpdate/Models.png') -Destination $docs -Force
foreach($name in @('build.txt','play.txt','details.txt','inputs.sha256')){Copy-Item -LiteralPath (Join-Path $report $name) -Destination $docs -Force}
Write-Output "Installed $installed files. Scenes, starter stock, existing catalogue IDs and user saves unchanged."
