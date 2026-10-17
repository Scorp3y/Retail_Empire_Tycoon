param(
    [string]$Project = 'C:/Users/artem/Retail Empire Tycoon',
    [string]$Blender = 'D:/SteamLibrary/steamapps/common/Blender/blender.exe',
    [string[]]$VehicleIds = @()
)
$ErrorActionPreference = 'Stop'
$approved = Join-Path $Project 'ArtSource/TrafficVehicles/01_Hatchback/01_Hatchback.blend'
$approvedHash = (Get-FileHash -LiteralPath $approved -Algorithm SHA256).Hash
$expectedHash = '2b6738fa1b0119d8366c8f69a30923570c3e9cacafb01540359adcb5e9b7e2be'
if ($approvedHash -ne $expectedHash) { throw 'Approved hatchback changed; stop before proceeding.' }
$ids = @('02_ClassicSedan','03_StationWagon','04_Coupe','05_BoxSUV','06_Pickup',
    '07_Minivan','08_PanelVan','09_DeliveryVan','10_Taxi','11_Minibus','12_FlatbedTruck',
    '13_BoxTruck','14_Crossover','15_ExecutiveSedan','16_Microcar','17_CompactCargoVan',
    '18_DoubleCabPickup','19_SmallCamper','20_Fastback')
if ($VehicleIds.Count -gt 0) {
    foreach ($id in $VehicleIds) { if ($ids -notcontains $id) { throw "Unknown vehicle: $id" } }
    $ids = $VehicleIds
}
try {
    foreach ($id in $ids) {
        $model = Join-Path $Project "ArtSource/TrafficVehicles/$id/$id.blend"
        $review = Join-Path $Project "Docs/VehicleStages/$id"
        Write-Output "Building independent vehicle $id"
        if (-not (Test-Path -LiteralPath $model)) {
            & $Blender --background --factory-startup --python-exit-code 1 --python `
                (Join-Path $Project 'Scripts/Blender/BuildIndividualVehicle.py') -- --project $Project --id $id
            if ($LASTEXITCODE -ne 0) { throw "Build failed: $id" }
        } else {
            Write-Output "Preserving existing model $id; reopening for validation"
        }
        & $Blender --background $model --python-exit-code 1 --python `
            (Join-Path $Project 'Scripts/Blender/VerifyIndividualVehicle.py') -- --project $Project --id $id
        if ($LASTEXITCODE -ne 0) { throw "Reopen validation failed: $id" }
        $report = Get-Content -Raw -LiteralPath (Join-Path $review 'verification.json') | ConvertFrom-Json
        if (-not $report.views_complete -or -not $report.reopened) { throw "Incomplete review: $id" }
        if ((Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash -ne $report.model_sha256) {
            throw "Source hash mismatch: $id"
        }
        Write-Output "COMPLETE $id"
    }
} finally {
    if ((Get-FileHash -LiteralPath $approved -Algorithm SHA256).Hash -ne $approvedHash) {
        throw 'Approved hatchback was unexpectedly changed.'
    }
}
