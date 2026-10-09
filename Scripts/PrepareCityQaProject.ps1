$ErrorActionPreference = 'Stop'
$taskProjectRoot = Split-Path -Parent $PSScriptRoot
$taskCloneRoot = Join-Path $taskProjectRoot 'Library/CityUpdateQA/IsolatedProject'
if (Test-Path -LiteralPath $taskCloneRoot) { throw 'A previous isolated QA project exists; preserve it and choose whether to reuse it explicitly.' }
New-Item -ItemType Directory -Path $taskCloneRoot | Out-Null
foreach ($taskFolderName in @('Assets','Packages','ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $taskProjectRoot $taskFolderName) -Destination $taskCloneRoot -Recurse
}
# Keep cached packages local: QA neither installs packages nor needs a network connection.
$taskManifestPath = Join-Path $taskCloneRoot 'Packages/manifest.json'
$taskManifest = Get-Content -LiteralPath $taskManifestPath -Raw | ConvertFrom-Json -AsHashtable
foreach ($taskCachedPackage in Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Library/PackageCache') -Directory) {
    $taskPackageManifest = Join-Path $taskCachedPackage.FullName 'package.json'
    if (!(Test-Path -LiteralPath $taskPackageManifest)) { continue }
    $taskPackageName = (Get-Content -LiteralPath $taskPackageManifest -Raw | ConvertFrom-Json).name
    $taskManifest.dependencies[$taskPackageName] = 'file:' + $taskCachedPackage.FullName.Replace('\','/')
}
# Mechanical path rewrite applies only to the disposable clone's package manifest.
$taskManifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $taskManifestPath -Encoding utf8
Write-Output ('QA clone ready: ' + $taskCloneRoot)
