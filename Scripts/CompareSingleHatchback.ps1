param(
    [string]$ProjectRoot = 'C:/Users/artem/Retail Empire Tycoon'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$reviewRoot = Join-Path $ProjectRoot 'Docs/VehicleStages/01_Hatchback'
$reference = [Drawing.Bitmap]::FromFile((Join-Path $reviewRoot 'Reference-closeup.png'))
$render = [Drawing.Bitmap]::FromFile((Join-Path $reviewRoot '01-Hatchback-reopened.png'))
$canvas = [Drawing.Bitmap]::new(1920, 700)
$graphics = [Drawing.Graphics]::FromImage($canvas)
$font = [Drawing.Font]::new('Segoe UI', 19, [Drawing.FontStyle]::Bold)
$captionFont = [Drawing.Font]::new('Segoe UI', 12)
$ink = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(38, 42, 46))
try {
    $graphics.Clear([Drawing.Color]::FromArgb(241, 239, 236))
    $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $graphics.DrawString('РЕФЕРЕНС · машина 01', $font, $ink, 20, 8)
    $graphics.DrawString('МОДЕЛЬ В BLENDER · машина 01', $font, $ink, 980, 8)
    $graphics.DrawImage($reference, [Drawing.Rectangle]::new(0, 52, 960, 606))
    $graphics.DrawImage($render, [Drawing.Rectangle]::new(960, 52, 960, 606))
    $graphics.DrawString('Хэтчбек №01: сглаженные кромки, новые фары, колёса и фактуры. Справа — рендер сохранённого .blend.', $captionFont, $ink, 20, 666)
    $canvas.Save((Join-Path $reviewRoot 'Reference-vs-Blender.png'), [Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $ink.Dispose()
    $font.Dispose()
    $captionFont.Dispose()
    $graphics.Dispose()
    $canvas.Dispose()
    $reference.Dispose()
    $render.Dispose()
}
