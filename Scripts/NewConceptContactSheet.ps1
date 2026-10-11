param([string]$DraftDirectory)
$ErrorActionPreference='Stop'
$taskProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(-not $DraftDirectory) {$DraftDirectory=Join-Path $taskProject 'ArtDrafts/ConceptTownKit'}
$taskDraft=[IO.Path]::GetFullPath($DraftDirectory)
if(-not $taskDraft.StartsWith($taskProject+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {throw 'Preview directory must stay inside this project.'}
$taskManifest=Get-Content -LiteralPath (Join-Path $taskDraft 'manifest.json') -Raw | ConvertFrom-Json
if($taskManifest.assets.Count -ne 12) {throw 'Expected the twelve approved models.'}
Add-Type -AssemblyName System.Drawing
$taskBitmap=[Drawing.Bitmap]::new(2660,2340)
$taskGraphics=[Drawing.Graphics]::FromImage($taskBitmap)
$taskTitle=[Drawing.Font]::new('Segoe UI',34,[Drawing.FontStyle]::Bold)
$taskLabel=[Drawing.Font]::new('Segoe UI',24,[Drawing.FontStyle]::Bold)
$taskDetail=[Drawing.Font]::new('Segoe UI',17)
$taskInk=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(42,68,65))
$taskMuted=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(93,112,105))
$taskRule=[Drawing.Pen]::new([Drawing.Color]::FromArgb(217,216,204),1)
try {
    $taskGraphics.Clear([Drawing.Color]::FromArgb(244,242,235))
    $taskGraphics.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $taskGraphics.DrawString('RETAIL EMPIRE TYCOON · 12 ОБЪЕКТОВ',$taskTitle,$taskInk,32,20)
    $taskGraphics.DrawString('Реальные 3D-модели · рендеры Unity · на игровую карту не добавлены',$taskDetail,$taskMuted,34,86)
    for($taskIndex=0;$taskIndex -lt 12;$taskIndex++) {
        $taskEntry=$taskManifest.assets[$taskIndex]
        $taskLeft=20+($taskIndex%4)*660
        $taskTop=140+[Math]::Floor($taskIndex/4)*730
        $taskImage=[Drawing.Image]::FromFile((Join-Path $taskDraft $taskEntry.preview))
        try {$taskGraphics.DrawImage($taskImage,[Drawing.Rectangle]::new($taskLeft,$taskTop,640,640))}
        finally {$taskImage.Dispose()}
        $taskName=$taskEntry.id.Substring(0,2)+'  '+$taskEntry.name
        $taskGraphics.DrawString($taskName,$taskLabel,$taskInk,$taskLeft+12,$taskTop+640)
        $taskDescription=('{0:N0} треугольников' -f $taskEntry.triangles)
        $taskGraphics.DrawString($taskDescription,$taskDetail,$taskMuted,$taskLeft+12,$taskTop+683)
        $taskGraphics.DrawRectangle($taskRule,[Drawing.Rectangle]::new($taskLeft,$taskTop,640,714))
    }
    $taskBitmap.Save((Join-Path $taskDraft 'Previews/AllModelsLabeled.png'),[Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $taskRule.Dispose();$taskMuted.Dispose();$taskInk.Dispose();$taskDetail.Dispose();$taskLabel.Dispose();$taskTitle.Dispose()
    $taskGraphics.Dispose();$taskBitmap.Dispose()
}
