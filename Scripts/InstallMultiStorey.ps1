param([switch]$Install)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = Join-Path $projectRoot 'Library/CityUpdateQA/IsolatedProject'
$qaRoot = Join-Path $sourceRoot 'Library/MultiStoreyQA'

foreach ($receipt in @('result.txt', 'game-result.txt')) {
    $result = Get-Content -LiteralPath (Join-Path $qaRoot $receipt) -Raw
    if ($result -match 'FAIL' -or $result -notmatch 'PASS all construction integration checks') {
        throw "Native Unity checks have not passed: $receipt"
    }
}

# This allow-list intentionally excludes all scenes, town meshes, saves and unrelated assets.
$relativeFiles = [Collections.Generic.List[string]]::new()
foreach ($directory in @('Assets/Prefabs/MultiStorey', 'Assets/Resources/MultiStorey')) {
    $relativeFiles.Add($directory + '.meta')
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $sourceRoot $directory) -File -Recurse) {
        $relativeFiles.Add($file.FullName.Substring($sourceRoot.Length + 1))
    }
}
foreach ($file in @('Assets/Prefabs/City/ParkingBay.prefab', 'Assets/Prefabs/City/parking_01.asset')) {
    $relativeFiles.Add($file)
    $relativeFiles.Add($file + '.meta')
}
foreach ($id in @('stairs_01', 'elevator_01', 'escalator_01', 'parking_01')) {
    $relativeFiles.Add("Assets/Art/ShopUi/Items/$id.png")
    $relativeFiles.Add("Assets/Art/ShopUi/Items/$id.png.meta")
}

foreach ($relative in $relativeFiles) {
    $source = [IO.Path]::GetFullPath((Join-Path $sourceRoot $relative))
    $target = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
    if (-not $source.StartsWith($sourceRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not $target.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Asset path escaped the intended project: $relative"
    }
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing source asset: $relative" }
    if ($relative.EndsWith('.meta') -and (Test-Path -LiteralPath $target)) {
        $sourceGuid = (Select-String -LiteralPath $source -Pattern '^guid: ').Line
        $targetGuid = (Select-String -LiteralPath $target -Pattern '^guid: ').Line
        if ($sourceGuid -ne $targetGuid) { throw "Existing Unity GUID must not change: $relative" }
    }
}

if (-not $Install) {
    Write-Output "Validated installation scope: $($relativeFiles.Count) files. Use -Install to copy them."
    return
}

$backupRoot = Join-Path $projectRoot ('Library/MultiStoreyInstall/Before_' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
foreach ($relative in $relativeFiles) {
    $source = Join-Path $sourceRoot $relative
    $target = Join-Path $projectRoot $relative
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        $backup = Join-Path $backupRoot $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $backup) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash) {
        throw "Installed bytes differ from the tested asset: $relative"
    }
}
Write-Output "Installed and hash-verified $($relativeFiles.Count) files. Previous files preserved at $backupRoot"
