param([string]$Project=(Split-Path $PSScriptRoot -Parent),[switch]$ValidateOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath($Project).TrimEnd('\','/')
$qa=Join-Path $root 'Library/CityUpdateQA/IsolatedProject'
$report=Join-Path $qa 'Library/BakeryQA'
$pack='Assets/Resources/StoreAssortment/Expansion.asset'
function Hash([string]$path){(Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
$keys=@('croissant','doughnut','muffin','pretzel','cupcake','cake_slice')
$scripts=@('BakeryExpansionBatch','BakeryExpansionPlay','BakeryExpansionReview','AssortmentUpdateBatch','AssortmentUpdatePlay','ShopUiPreviews') |
    ForEach-Object {'Assets/Project/Editor/'+$_.ToString()+'.cs'}
foreach($name in @('build','play')){
    $path=Join-Path $report ($name+'.txt')
    if(!(Get-Content -LiteralPath $path -Raw).StartsWith('PASS')){throw "Missing passed validation: $name"}
    if((Get-Item -LiteralPath $path).LastWriteTimeUtc -lt (Get-Item (Join-Path $root 'Library/BakeryExpansion/Before')).CreationTimeUtc){throw "Stale report: $name"}
}
$definitions=Get-Content -LiteralPath (Join-Path $qa 'Assets/Art/Bakery/Definitions.json') -Raw | ConvertFrom-Json
if(($definitions.products.key -join ',') -ne ($keys -join ',')){throw 'Unexpected bakery assortment'}
$outputs=@(Get-Content -LiteralPath (Join-Path $report 'outputs.txt') | Sort-Object -Unique)
foreach($relative in $outputs){
    $plain=$relative -replace '\.meta$',''
    $allowed=$relative -match '^Assets/(Art/Bakery|Resources/StoreAssortment/Bakery)(/|\.meta$)'
    $allowed=$allowed -or $plain -eq $pack -or $scripts.Contains($plain)
    $allowed=$allowed -or @($keys | ForEach-Object {'Assets/Art/ShopUi/Items/product_'+$_+'.png'}).Contains($plain)
    $target=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$allowed -or $relative.Contains('..') -or !$target.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Output escaped bakery scope: $relative"}
}
$set=[Collections.Generic.HashSet[string]]::new([string[]]$outputs,[StringComparer]::OrdinalIgnoreCase)
$mismatches=@()
foreach($line in Get-Content -LiteralPath (Join-Path $report 'inputs.sha256')){
    $relative=$line.Substring(65);$expected=$line.Substring(0,64)
    if((Hash (Join-Path $qa $relative)) -ne $expected){throw "QA input changed after verification: $relative"}
    if(!$set.Contains($relative) -and (!(Test-Path -LiteralPath (Join-Path $root $relative)) -or (Hash (Join-Path $root $relative)) -ne $expected)){$mismatches+=$relative}
}
if($mismatches.Count){$mismatches | Set-Content -LiteralPath (Join-Path $root 'Library/BakeryExpansion/input-mismatches.txt');throw "Main differs from verified copy: $($mismatches.Count) inputs"}
$baseline=Get-Content -LiteralPath (Join-Path $root 'Library/BakeryExpansion/baseline.json') -Raw | ConvertFrom-Json
foreach($entry in $baseline){
    if(!$scripts.Contains($entry.path) -and (Hash (Join-Path $root $entry.path)) -ne $entry.sha256){throw "Protected file edited during generation: $($entry.path)"}
}
foreach($relative in $outputs){
    $target=Join-Path $root $relative;if(!(Test-Path -LiteralPath $target)){continue}
    if($relative -like '*.meta'){
        $old=[regex]::Match([IO.File]::ReadAllText($target),'(?m)^guid: (\w+)').Groups[1].Value
        $next=[regex]::Match([IO.File]::ReadAllText((Join-Path $qa $relative)),'(?m)^guid: (\w+)').Groups[1].Value
        if(!$old -or $old -ne $next){throw "Existing GUID changed: $relative"}
    }
    if((Hash $target) -eq (Hash (Join-Path $qa $relative))){continue}
    if($relative -ne $pack){throw "Unexpected replacement: $relative"}
    if((Hash $target) -ne ($baseline | Where-Object path -eq $pack).sha256){throw 'Existing catalogue changed during generation'}
}
Write-Output "Validated $($outputs.Count) outputs; six new bakery models; full 54-product regression; existing GUIDs and scenes preserved."
if($ValidateOnly){return}
$backup=Join-Path $root ('Library/BakeryExpansion/InstalledBefore_'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$changed=0
foreach($relative in $outputs){
    $target=Join-Path $root $relative;$source=Join-Path $qa $relative
    if((Test-Path -LiteralPath $target) -and (Hash $target) -eq (Hash $source)){continue}
    New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
    $stage=Join-Path (Split-Path $target) ('.codex-bakery-'+[Guid]::NewGuid().ToString('N')+'.tmp')
    try{
        [IO.File]::Copy($source,$stage)
        if(Test-Path -LiteralPath $target){
            $copy=Join-Path $backup $relative;New-Item -ItemType Directory -Path (Split-Path $copy) -Force | Out-Null
            [IO.File]::Replace($stage,$target,$copy)
        }else{[IO.File]::Move($stage,$target)}
    }finally{if(Test-Path -LiteralPath $stage){Remove-Item -LiteralPath $stage}}
    if((Hash $target) -ne (Hash $source)){throw "Install verification failed: $relative"}
    $changed++
}
$outputs | ForEach-Object {[PSCustomObject]@{path=$_;sha256=Hash (Join-Path $root $_)}} | ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $root 'Library/BakeryExpansion/installed.json') -Encoding utf8
$docs=Join-Path $root 'Docs/BakeryExpansion';New-Item -ItemType Directory -Path $docs -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $qa 'Docs/BakeryExpansion/Models.png') -Destination $docs -Force
foreach($name in @('build.txt','play.txt','bakery.txt','assortment.txt','inputs.sha256')){Copy-Item -LiteralPath (Join-Path $report $name) -Destination $docs -Force}
Write-Output "Installed $changed files. Six bakery goods appended to the existing catalogue; scenes and player saves unchanged."
