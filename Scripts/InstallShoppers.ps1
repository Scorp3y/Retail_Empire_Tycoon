param([string]$Project = (Split-Path $PSScriptRoot -Parent))

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath($Project).TrimEnd('\', '/')
$isolatedRoot = Join-Path $projectRoot 'Library/CityUpdateQA/IsolatedProject'
$evidence = Join-Path $isolatedRoot 'Library/ShopperQA'
$validation = Join-Path $evidence 'validation.txt'
if (!(Test-Path -LiteralPath $validation) -or !(Select-String -LiteralPath $validation -SimpleMatch 'PASS shoppers integrated into real shop purchase/leave flow.')) {
    throw 'The isolated shopper gameplay validation must pass before installation.'
}
$manifestPath = Join-Path $projectRoot 'ArtSource/Shoppers/Manifest.json'
$validatedManifest = Join-Path $evidence 'validated-manifest.sha256'
$testedManifestPath = Join-Path $isolatedRoot 'Assets/Art/Shoppers/Manifest.json'
if (!(Test-Path -LiteralPath $validatedManifest) -or
    (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -ne (Get-Content -LiteralPath $validatedManifest -Raw).Trim() -or
    (Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $testedManifestPath -Algorithm SHA256).Hash) {
    throw 'The current manifest has not passed gameplay validation. An older PASS report cannot authorize new model installation.'
}
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.characters.Count -ne 50) { throw 'Expected exactly 50 source models.' }
if ($manifest.palette_sha256 -and (Get-FileHash -LiteralPath (Join-Path $isolatedRoot 'Assets/Art/Shoppers/ShopperPalette.png') -Algorithm SHA256).Hash -ne $manifest.palette_sha256) {
    throw 'The tested palette does not match the source models. Do not install a palette separately from its UV-mapped models.'
}
foreach ($design in $manifest.characters) {
    $source = Join-Path $projectRoot $design.source
    $model = Join-Path $isolatedRoot ("Assets/Art/Shoppers/Models/" + $design.id + '.fbx')
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $design.source_sha256) { throw "Changed Blender source: $source" }
    if ((Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash -ne $design.fbx_sha256) { throw "Untested FBX: $model" }
}
$prefabs = Get-ChildItem -LiteralPath (Join-Path $isolatedRoot 'Assets/Art/Shoppers/Prefabs') -Filter '*.prefab'
if ($prefabs.Count -ne 50) { throw 'Expected exactly 50 tested prefabs.' }

# Only the new presentation assets are installed. Existing scenes, employees and saves are not touched.
$relativeFiles = @('Assets/Art/Shoppers.meta', 'Assets/Resources/Shoppers.meta')
foreach ($folder in @('Assets/Art/Shoppers', 'Assets/Resources/Shoppers')) {
    $relativeFiles += Get-ChildItem -LiteralPath (Join-Path $isolatedRoot $folder) -Recurse -File |
        ForEach-Object { $_.FullName.Substring($isolatedRoot.Length + 1) }
}
$installRoot = Join-Path $projectRoot ('Library/ShopperInstall/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
$receipt = @()
foreach ($relative in $relativeFiles) {
    $source = Join-Path $isolatedRoot $relative
    $target = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
    if (!$target.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Installation escaped project.' }
    if (Test-Path -LiteralPath $target) {
        $backup = Join-Path $installRoot ('Before/' + $relative)
        New-Item -ItemType Directory -Path (Split-Path $backup -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination $backup
    }
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $target
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash) { throw "Installation verification failed: $relative" }
    $receipt += [PSCustomObject]@{ path = $relative; sha256 = $hash }
}
$receipt | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $installRoot 'receipt.json') -Encoding utf8
Write-Output ("Installed and hash-verified " + $receipt.Count + ' asset files. Backup/receipt: ' + $installRoot)
