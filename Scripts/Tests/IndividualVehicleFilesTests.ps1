param([string]$Project = 'C:/Users/artem/Retail Empire Tycoon')
$ErrorActionPreference = 'Stop'
$review = Join-Path $Project 'Docs/VehicleStages'
$models = Join-Path $Project 'ArtSource/TrafficVehicles'
$reports = @(Get-ChildItem -LiteralPath $review -Directory |
    Where-Object { $_.Name -match '^(0[2-9]|1[0-9]|20)_' } | ForEach-Object {
        $report = Get-Content -Raw -LiteralPath (Join-Path $_.FullName 'verification.json') | ConvertFrom-Json
        $model = Join-Path $models ($report.identity+'/'+$report.identity+'.blend')
        if ((Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash -ne $report.model_sha256) {
            throw ('Stale report: '+$report.identity)
        }
        if (-not $report.views_complete -or -not $report.reopened -or
            -not $report.opaque_windows -or $report.zero_area_triangles -ne 0 -or
            $report.packed_surface_maps -ne 4 -or $report.independent_wheels -ne 4) {
            throw ('Failed saved-model check: '+$report.identity)
        }
        if ($report.family -in @('delivery','flatbed','box_truck','camper') -and
            (-not $report.cab_step_clearance -or -not $report.mounted_rear_lights)) {
            throw ('Cargo detail check missing: '+$report.identity)
        }
        foreach ($view in @('Front.png','Rear.png','Side.png','Reference.png')) {
            if (-not (Test-Path -LiteralPath (Join-Path $_.FullName $view) -PathType Leaf)) {
                throw ('Missing view: '+$report.identity+'/'+$view)
            }
        }
        $report
    })
if ($reports.Count -ne 19) { throw ('Expected 19 new models, found '+$reports.Count) }
$files = @(Get-ChildItem -LiteralPath $models -Filter '*.blend' -Recurse)
if ($files.Count -ne 20) { throw ('Expected 20 individual files, found '+$files.Count) }
if (@($reports.model_sha256 | Select-Object -Unique).Count -ne 19) { throw 'Duplicate individual model files.' }
$hash = (Get-FileHash -LiteralPath (Join-Path $models '01_Hatchback/01_Hatchback.blend') -Algorithm SHA256).Hash
if ($hash -ne '2b6738fa1b0119d8366c8f69a30923570c3e9cacafb01540359adcb5e9b7e2be') {
    throw 'Approved hatchback changed'
}
$reports | Sort-Object identity | Select-Object identity,triangles,family | Format-Table -AutoSize
[PSCustomObject]@{
    NewModels = $reports.Count
    IndividualBlendFiles = $files.Count
    ReopenedModels = $reports.Count
    VerifiedViews = $reports.Count*3
    MinTriangles = ($reports.triangles | Measure-Object -Minimum).Minimum
    MaxTriangles = ($reports.triangles | Measure-Object -Maximum).Maximum
    Megabytes = [math]::Round(($files | Measure-Object -Property Length -Sum).Sum/1MB,1)
    ApprovedHatchbackUnchanged = $true
} | Format-List
