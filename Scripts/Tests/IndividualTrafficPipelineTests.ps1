param(
    [string]$Project = 'C:/Users/artem/Retail Empire Tycoon',
    [switch]$Installed
)
$ErrorActionPreference = 'Stop'
$exports = Join-Path $Project 'Library/IndividualTrafficImport/Exports'
$fleet = Get-Content -Raw -LiteralPath (Join-Path $exports 'fleet.json') | ConvertFrom-Json
if ($fleet.vehicles.Count -ne 20) { throw 'Exactly twenty individual exports are required.' }
foreach ($vehicle in $fleet.vehicles) {
    $source = Join-Path $Project $vehicle.source
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $vehicle.source_sha256) {
        throw "Editable source changed: $($vehicle.id)"
    }
    $fbx = Join-Path $exports ($vehicle.id+'.fbx')
    if ((Get-FileHash -LiteralPath $fbx -Algorithm SHA256).Hash -ne $vehicle.fbx_sha256) {
        throw "Stale export: $($vehicle.id)"
    }
    if ($vehicle.wheels.Count -ne 4 -or @($vehicle.wheels | Where-Object front).Count -ne 2) {
        throw "Incomplete wheel bindings: $($vehicle.id)"
    }
    if ($vehicle.lodTriangles.Count -ne 3 -or $vehicle.lodTriangles[1] -ge $vehicle.lodTriangles[0]*.5 -or
        $vehicle.lodTriangles[2] -ge $vehicle.lodTriangles[0]*.2) { throw "LOD budget: $($vehicle.id)" }
    foreach ($wheel in $vehicle.wheels) {
        if ([math]::Abs($wheel.position[1]-$vehicle.radius) -gt .001) { throw 'Incorrect ground contact.' }
    }
}
Add-Type -AssemblyName System.Drawing
$base = [Drawing.Bitmap]::FromFile((Join-Path $exports 'FleetBase.png'))
try {
    if ($base.Width -ne 2048 -or $base.Height -ne 2048) { throw 'Unexpected atlas dimensions.' }
    $glass = $base.GetPixel(128, 2048-(5*256+128)-1)
    if ($glass.A -ne 255 -or $glass.R -gt 100 -or $glass.G -gt 100 -or $glass.B -gt 110) {
        throw 'Glass atlas tile must be dark and completely opaque.'
    }
} finally { $base.Dispose() }
if ($Installed) {
    $evidence = Join-Path $Project 'Docs/IndividualTrafficReview'
    foreach ($relative in @('IndividualTrafficQA/build.txt','IndividualTrafficQA/review.txt','TrafficFleetQA/play.txt')) {
        if (-not (Get-Content -Raw -LiteralPath (Join-Path $evidence $relative)).StartsWith('PASS:')) {
            throw "Missing native validation: $relative"
        }
    }
    foreach ($stage in @('individual-review','play')) {
        foreach ($line in Get-Content -LiteralPath (Join-Path $evidence ("TrafficFleetQA/$stage-assets.sha256"))) {
            $expected,$relative = $line -split ' ',2
            if ((Get-FileHash -LiteralPath (Join-Path $Project $relative) -Algorithm SHA256).Hash -ne $expected) {
                throw "Installed asset differs from tested Unity output: $relative"
            }
        }
    }
}
Write-Output "PASS: 20 unchanged Blender sources, 20 exact FBX exports, four wheels, three LODs, opaque atlas. Installed=$Installed"
