param([string]$ProjectRoot='C:/Users/artem/Retail Empire Tycoon')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$names=@('01_Hatchback','02_ClassicSedan','03_StationWagon','04_Coupe','05_BoxSUV','06_Pickup','07_Minivan','08_PanelVan','09_DeliveryVan','10_Taxi','11_Minibus','12_FlatbedTruck','13_BoxTruck','14_Crossover','15_ExecutiveSedan','16_Microcar','17_CompactCargoVan','18_DoubleCabPickup','19_SmallCamper','20_Fastback')
$sheet=[Drawing.Bitmap]::FromFile((Join-Path $ProjectRoot 'Docs/Art/TrafficFleetConcept-v2.png'))
try {
    $left=@(8,314,624,932,1237)
    $top=@(74,296,528,765)
    $height=@(180,190,198,200)
    for($index=1;$index -lt 20;$index++) {
        $row=[int][Math]::Floor($index/5)
        $column=$index%5
        $rectangle=[Drawing.Rectangle]::new($left[$column],$top[$row],285,$height[$row])
        $crop=$sheet.Clone($rectangle,[Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $folder=Join-Path $ProjectRoot ('Docs/VehicleStages/'+$names[$index])
        New-Item -ItemType Directory -Path $folder -Force | Out-Null
        try { $crop.Save((Join-Path $folder 'Reference.png'),[Drawing.Imaging.ImageFormat]::Png) }
        finally { $crop.Dispose() }
    }
}
finally { $sheet.Dispose() }
