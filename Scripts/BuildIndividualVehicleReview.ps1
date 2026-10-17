param(
    [string]$Project = 'C:/Users/artem/Retail Empire Tycoon',
    [string[]]$VehicleIds = @(),
    [string]$Name = 'Fleet',
    [switch]$Catalogue
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Join-Path $Project 'Docs/VehicleStages'
if ($VehicleIds.Count -eq 0) {
    $VehicleIds = @(Get-ChildItem -LiteralPath (Join-Path $Project 'ArtSource/TrafficVehicles') -Directory |
        Sort-Object Name | ForEach-Object { $_.Name })
}
$columns = if ($Catalogue) { 5 } else { 4 }
$rows = if ($Catalogue) { [int][Math]::Ceiling($VehicleIds.Count / 5.0) } else { $VehicleIds.Count }
$cellWidth = 450
$cellHeight = 326
$canvas = [Drawing.Bitmap]::new($columns * $cellWidth, $rows * $cellHeight)
$graphics = [Drawing.Graphics]::FromImage($canvas)
$font = [Drawing.Font]::new('Segoe UI', 12, [Drawing.FontStyle]::Bold)
$ink = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(35, 39, 42))
try {
    $graphics.Clear([Drawing.Color]::FromArgb(236, 234, 230))
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    for ($i = 0; $i -lt $VehicleIds.Count; $i++) {
        $id = $VehicleIds[$i]
        [string[]]$views = if ($Catalogue) { @('Front') } else { @('Reference','Front','Rear','Side') }
        for ($v = 0; $v -lt $views.Count; $v++) {
            $view = $views[$v]
            $path = Join-Path $root "$id/$view.png"
            if ($id -eq '01_Hatchback') {
                $files = @{Front='01-Hatchback-reopened.png';Rear='02-Rear-quarter.png';
                    Side='03-Side-profile.png';Reference='Reference-closeup.png'}
                $path = Join-Path $root "$id/$($files[$view])"
            }
            if (-not (Test-Path -LiteralPath $path)) { throw "Missing review image: $path" }
            $column = if ($Catalogue) { $i % 5 } else { $v }
            $row = if ($Catalogue) { [int][Math]::Floor($i / 5.0) } else { $i }
            $x = $column * $cellWidth
            $y = $row * $cellHeight
            $source = [Drawing.Image]::FromFile($path)
            try {
                $scale = [Math]::Min($cellWidth / $source.Width, 296.0 / $source.Height)
                $width = [int]($source.Width * $scale)
                $height = [int]($source.Height * $scale)
                $rect = [Drawing.Rectangle]::new([int]($x+($cellWidth-$width)/2), [int]($y+(296-$height)/2), $width, $height)
                $graphics.DrawImage($source, $rect)
            } finally { $source.Dispose() }
            $label = if ($Catalogue) { $id.Replace('_',' ') } else { "$id / $view" }
            $graphics.DrawString($label, $font, $ink, $x+10, $y+299)
        }
    }
    $destination = Join-Path $root ($Name+'.png')
    $canvas.Save($destination, [Drawing.Imaging.ImageFormat]::Png)
    Write-Output $destination
} finally {
    $graphics.Dispose(); $canvas.Dispose(); $font.Dispose(); $ink.Dispose()
}
