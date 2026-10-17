$ErrorActionPreference='Stop'
$performanceRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$script=Get-Content -LiteralPath (Join-Path $performanceRoot 'Scripts/MeasureTownPerformance.ps1') -Raw
$tokens=$null; $parseErrors=$null
[void][System.Management.Automation.Language.Parser]::ParseInput($script,[ref]$tokens,[ref]$parseErrors)
if($parseErrors.Count -ne 0) {throw ($parseErrors | Out-String)}
$checks=@(
    @($script.Contains('[IO.FileShare]::None'),'exclusive pipeline lock'),
    @($script.Contains('-WindowStyle Hidden'),'hidden helper only'),
    @($script.Contains('$process.Kill($true)') -and -not $script.Contains('Stop-Process'),'only owned timeout process tree'),
    @($script.Contains('Assert-ProfileSnapshot $profileProject $mainBefore'),'main source protection'),
    @($script.Contains('Assert-ProfileSnapshot $profileClone $cloneBefore'),'clone protection'),
    @($script.Contains('[IO.File]::WriteAllBytes($settingsPath,$settingsBytes)'),'exact player settings restoration'),
    @($script.Contains('[IO.File]::WriteAllBytes($path,$backup[$path])'),'exact gameplay sandbox restoration'),
    @($script.Contains('evidence must never be silently overwritten'),'immutable run evidence'),
    @($script.Contains('CITY DELIVERY INTEGRATION PASSED'),'terminal delivery result required'),
    @($script.Contains('$draw[0].mean -le 0'),'actual native rendering required'),
    @($script.Contains('-force-d3d11') -and -not $script.Contains('-nographics'),'native GPU path'),
    @($script.Contains('$receipt.sources') -and $script.Contains('$receipt.exeHash'),'built source and binary receipts'),
    @($script.Contains('$view.renderCoverage -lt .95'),'render cadence, not just update cadence')
)
foreach($check in $checks) {if(-not $check[0]) {throw ('Pipeline contract failed: '+$check[1])}}
Write-Output ('TOWN PERFORMANCE PIPELINE TESTS PASSED: '+$checks.Count+' source/fixture/evidence safety contracts (not native performance tests).')
