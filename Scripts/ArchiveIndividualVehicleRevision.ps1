param(
    [Parameter(Mandatory)][string[]]$VehicleIds,
    [Parameter(Mandatory)][string]$Reason,
    [string]$Project = 'C:/Users/artem/Retail Empire Tycoon'
)
$ErrorActionPreference = 'Stop'
if ($Reason -notmatch '^[a-z0-9-]+$') { throw 'Use a short safe revision label.' }
$root = [IO.Path]::GetFullPath((Join-Path $Project 'ArtSource/TrafficVehicles'))
$archive = Join-Path $Project ('Library/IndividualVehiclePipeline/Revisions/' +
    (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + $Reason)
New-Item -ItemType Directory -Path $archive | Out-Null
foreach ($id in $VehicleIds) {
    if ($id -notmatch '^(0[2-9]|1[0-9]|20)_[A-Za-z]+$') { throw "Protected or invalid model: $id" }
    $source = [IO.Path]::GetFullPath((Join-Path $root "$id/$id.blend"))
    if (-not $source.StartsWith($root+[IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Revision source outside the individual-vehicle folder.'
    }
    $reportPath = Join-Path $Project "Docs/VehicleStages/$id/verification.json"
    if (Test-Path -LiteralPath $reportPath) {
        $report = Get-Content -Raw -LiteralPath $reportPath | ConvertFrom-Json
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $report.model_sha256) {
            throw "Possible artist changes detected; preserve and inspect manually: $id"
        }
    }
    Move-Item -LiteralPath $source -Destination (Join-Path $archive "$id.blend")
    Write-Output "Preserved $id in $archive"
}
