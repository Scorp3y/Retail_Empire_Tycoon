$ErrorActionPreference = 'Stop'
$taskProjectRoot = Split-Path -Parent $PSScriptRoot
$taskCloneRoot = Join-Path $taskProjectRoot 'Library/CityUpdateQA/IsolatedProject'
$taskReport = Join-Path $taskCloneRoot 'Library/CityUpdateQA/integration.txt'
if (!(Test-Path -LiteralPath $taskReport) -or !(Get-Content -LiteralPath $taskReport -Raw).Contains('CITY DELIVERY INTEGRATION PASSED')) {
    throw 'Full city integration must pass before applying generated assets.'
}
Set-Location -LiteralPath $taskProjectRoot
& git diff --quiet -- Assets/Project/Scenes/Game.unity ProjectSettings/EditorBuildSettings.asset
if ($LASTEXITCODE -ne 0) { throw 'Game scene or Build Settings changed locally; merge generated assets explicitly instead of replacing them.' }
$taskAssets = @()
foreach ($taskSubfolder in @('Assets/Prefabs','Assets/Art/ShopUi/Items')) {
    $taskAssets += Get-ChildItem -LiteralPath (Join-Path $taskCloneRoot $taskSubfolder) -File -Recurse |
        Where-Object { $_.Extension -in @('.asset','.prefab','.meta','.mat','.physicMaterial','.png') }
}
foreach ($taskRelative in @('Assets/Prefabs/City.meta','Assets/Prefabs/Product/Bakery.meta','Assets/Project/Scenes/Game.unity','Assets/Project/Scenes/City.unity','Assets/Project/Scenes/City.unity.meta','ProjectSettings/EditorBuildSettings.asset')) {
    $taskAssets += Get-Item -LiteralPath (Join-Path $taskCloneRoot $taskRelative)
}
# New code identities must match the component references in generated scenes/prefabs.
foreach ($taskSource in Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/Project') -File -Recurse -Filter '*.cs') {
    $taskRelative = $taskSource.FullName.Substring($taskProjectRoot.Length + 1)
    & git ls-files --error-unmatch -- $taskRelative 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0) {
        $taskMeta = Join-Path $taskCloneRoot ($taskRelative + '.meta')
        if (Test-Path -LiteralPath $taskMeta) { $taskAssets += Get-Item -LiteralPath $taskMeta }
    }
}
foreach ($taskFolder in @('Assets/Project/Scripts/City.meta','Assets/Project/Scripts/Logistics.meta','Assets/Project/Scripts/Parking.meta')) {
    $taskAssets += Get-Item -LiteralPath (Join-Path $taskCloneRoot $taskFolder)
}
$taskApplied = 0
foreach ($taskAsset in $taskAssets | Sort-Object FullName -Unique) {
    $taskRelative = $taskAsset.FullName.Substring($taskCloneRoot.Length + 1)
    $taskTarget = [IO.Path]::GetFullPath((Join-Path $taskProjectRoot $taskRelative))
    if (!$taskTarget.StartsWith($taskProjectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid generated asset target.' }
    if ((Test-Path -LiteralPath $taskTarget) -and (Get-FileHash -LiteralPath $taskTarget).Hash -eq (Get-FileHash -LiteralPath $taskAsset.FullName).Hash) { continue }
    New-Item -ItemType Directory -Path (Split-Path -Parent $taskTarget) -Force | Out-Null
    Copy-Item -LiteralPath $taskAsset.FullName -Destination $taskTarget -Force
    $taskApplied++
}
New-Item -ItemType Directory -Path (Join-Path $taskProjectRoot 'Docs/Images') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $taskCloneRoot 'Library/CityUpdateQA/city.png') -Destination (Join-Path $taskProjectRoot 'Docs/Images/CityDelivery.png') -Force
Write-Output ('Verified generated assets applied: ' + $taskApplied)
