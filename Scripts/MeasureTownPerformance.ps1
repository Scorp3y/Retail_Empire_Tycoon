param(
    [ValidateSet('Build','Profile','EditorProfile','Acceptance')][string]$Mode='Build',
    [ValidatePattern('^(Before|After)[1-3]$')][string]$RunId='Before1'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$profileProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$profileClone=Join-Path $profileProject 'Library/CityUpdateQA/IsolatedProject'
$profileRoot=Join-Path $profileProject 'Library/TownPerformancePipeline'
$profileQa=Join-Path $profileClone 'Library/TownPerformanceQA'
$profilePlayer=Join-Path $profileClone 'Builds/TownPerformancePlayer'
$profileUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
[IO.Directory]::CreateDirectory($profileRoot) | Out-Null
$profileLock=[IO.File]::Open((Join-Path $profileRoot 'pipeline.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)

function Get-ProfileSnapshot([string]$root) {
    $files=@{}
    foreach($scope in @('Assets','ProjectSettings','Packages','Docs')) {
        $folder=Join-Path $root $scope
        if(-not (Test-Path -LiteralPath $folder)) {continue}
        foreach($file in Get-ChildItem -LiteralPath $folder -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath($root,$file.FullName).Replace('\','/')
            if($relative -eq 'Docs/TownPerformanceReview.md' -or $relative.StartsWith('Docs/TownPerformanceReview/',[StringComparison]::Ordinal)) {continue}
            if($relative -eq 'Assets/Art/TownPerformanceFixture.meta' -or $relative.StartsWith('Assets/Art/TownPerformanceFixture/',[StringComparison]::Ordinal)) {continue}
            $files[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
    return $files
}
function Assert-ProfileSnapshot([string]$root,$expected) {
    $actual=Get-ProfileSnapshot $root
    if($actual.Count -ne $expected.Count) {throw "Protected file count changed in $root : $($expected.Count) -> $($actual.Count)"}
    foreach($relative in $expected.Keys) {
        if(-not $actual.ContainsKey($relative) -or $actual[$relative] -cne $expected[$relative]) {throw "Protected file changed: $relative ($root)"}
    }
}
function Invoke-ProfileProcess([string]$exe,[string[]]$arguments,[int]$minutes) {
    $process=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $profileClone -WindowStyle Hidden -PassThru
    if(-not $process.WaitForExit($minutes*60000)) {
        # Only the helper launched by this invocation can be terminated; never a user's editor.
        $process.Kill($true); $process.WaitForExit(); throw "Disposable helper timed out after $minutes minutes."
    }
    $process.Refresh()
    if($process.ExitCode -ne 0) {throw "Disposable helper failed with exit code $($process.ExitCode). Check its native log."}
}
function Publish-ProfileEvidence([string]$evidence,[string]$name,$context) {
    $terminal=Get-Content -LiteralPath (Join-Path $evidence 'complete.txt') -Raw
    if(-not $terminal.StartsWith('PASS',[StringComparison]::Ordinal)) {throw 'Native profiling did not complete.'}
    $data=Get-Content -LiteralPath (Join-Path $evidence 'summary.json') -Raw | ConvertFrom-Json
    if($data.views.Count -ne 7) {throw 'Not all seven views were measured.'}
    foreach($view in $data.views) {
        $draw=@($view.counters | Where-Object {$_.name -eq 'Draw Calls Count'})
        if($view.frames -lt 30 -or $view.meanFrameMs -le 0 -or $draw.Count -ne 1 -or $draw[0].validSamples -lt 30 -or $draw[0].mean -le 0) {throw "Missing real rendering/counter evidence: $($view.name)"}
        if($view.PSObject.Properties.Name -contains 'renderCoverage' -and $view.renderCoverage -lt .95) {throw "Camera rendered less than 95% of update frames: $($view.name)"}
    }
    $context.endUtc=[DateTime]::UtcNow.ToString('O')
    [IO.File]::WriteAllText((Join-Path $evidence 'source-receipt.json'),($context | ConvertTo-Json))
    $destination=Join-Path $profileProject ('Docs/TownPerformanceReview/'+$name)
    if(Test-Path -LiteralPath $destination) {throw 'Published evidence already exists.'}
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    foreach($file in Get-ChildItem -LiteralPath $evidence -File) {Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $destination $file.Name)}
    Write-Output "TOWN PERFORMANCE $name PASSED; seven views, actual shop departure/return."
    $data.views | Select-Object name,frames,meanFrameMs,p95Ms,gpuMs,mainMs,loopFps | Format-Table -AutoSize
}
try {
    if(-not (Test-Path -LiteralPath (Join-Path $profileClone 'ProjectSettings/ProjectVersion.txt'))) {throw 'The existing isolated Unity project is missing.'}
    $busy=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {$_.CommandLine -and $_.CommandLine.Contains($profileClone,[StringComparison]::OrdinalIgnoreCase)}
    if($busy) {throw 'The isolated Unity editor is busy.'}
    $mainBefore=Get-ProfileSnapshot $profileProject
    if($Mode -eq 'Build' -or $Mode -eq 'EditorProfile') {
        foreach($relative in $mainBefore.Keys) {
            $target=Join-Path $profileClone $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            if(-not (Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -cne $mainBefore[$relative]) {
                Copy-Item -LiteralPath (Join-Path $profileProject $relative) -Destination $target -Force
            }
        }
        [IO.Directory]::CreateDirectory($profileQa) | Out-Null
        $cloneBefore=Get-ProfileSnapshot $profileClone
        $settingsPath=Join-Path $profileClone 'ProjectSettings/ProjectSettings.asset'
        $settingsBytes=[IO.File]::ReadAllBytes($settingsPath)
        [IO.File]::WriteAllText((Join-Path $profileQa 'build.txt'),'PENDING')
        $editorEvidence=Join-Path $profileQa ('EditorEvidence/'+$RunId)
        if($Mode -eq 'EditorProfile' -and (Test-Path -LiteralPath $editorEvidence)) {throw 'This editor run already exists; evidence must never be silently overwritten.'}
        try {
            if($Mode -eq 'Build') {
                Invoke-ProfileProcess $profileUnity @('-batchmode','-projectPath',('"'+$profileClone+'"'),'-executeMethod','TownPerformanceBatch.Build','-logFile',('"'+(Join-Path $profileRoot 'build.log')+'"')) 8
            }
            else {
                Invoke-ProfileProcess $profileUnity @('-batchmode','-force-d3d11','-projectPath',('"'+$profileClone+'"'),'-executeMethod','TownPerformanceBatch.MeasureEditor','-town-profile-out',('"'+$editorEvidence+'"'),'-logFile',('"'+(Join-Path $profileRoot ('Editor'+$RunId+'.log'))+'"')) 6
            }
        }
        finally {[IO.File]::WriteAllBytes($settingsPath,$settingsBytes)}
        Assert-ProfileSnapshot $profileClone $cloneBefore
        Assert-ProfileSnapshot $profileProject $mainBefore
        if($Mode -eq 'EditorProfile') {
            $sourceReceipt=Join-Path $profileRoot ('Editor'+$RunId+'.sources.json')
            [IO.File]::WriteAllText($sourceReceipt,($mainBefore | ConvertTo-Json -Depth 5))
            $context=@{run=$RunId; mode='Unity Editor Play Mode, not Windows player'; startSourceHash=(Get-FileHash -LiteralPath $sourceReceipt -Algorithm SHA256).Hash; townHash=$mainBefore['Assets/Prefabs/TownWorld/TownEnvironment.prefab']; otherUnityEditors=@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count}
            Publish-ProfileEvidence $editorEvidence ('Editor'+$RunId) $context
            return
        }
        $buildText=Get-Content -LiteralPath (Join-Path $profileQa 'build.txt') -Raw
        if(-not $buildText.StartsWith('PASS',[StringComparison]::Ordinal)) {throw 'Native player build did not pass.'}
        $receipt=@{sources=$mainBefore; cloneSources=$cloneBefore; exeHash=(Get-FileHash -LiteralPath (Join-Path $profilePlayer 'TownBenchmark.exe') -Algorithm SHA256).Hash; buildUtc=[DateTime]::UtcNow.ToString('O')}
        [IO.File]::WriteAllText((Join-Path $profileRoot 'build.json'),($receipt | ConvertTo-Json -Depth 6))
        Write-Output 'TOWN PERFORMANCE BUILD PASSED; protected source/scene/assets unchanged.'
    }
    elseif($Mode -eq 'Profile') {
        $receipt=Get-Content -LiteralPath (Join-Path $profileRoot 'build.json') -Raw | ConvertFrom-Json -AsHashtable
        Assert-ProfileSnapshot $profileProject $receipt.sources
        Assert-ProfileSnapshot $profileClone $receipt.cloneSources
        $exe=Join-Path $profilePlayer 'TownBenchmark.exe'
        if((Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash -cne $receipt.exeHash) {throw 'Player binary differs from its build receipt.'}
        $evidence=Join-Path $profilePlayer ('Evidence/'+$RunId)
        if(Test-Path -LiteralPath $evidence) {throw 'This run already exists; evidence must never be silently overwritten.'}
        $context=@{run=$RunId; startUtc=[DateTime]::UtcNow.ToString('O'); exeHash=$receipt.exeHash; unityEditors=@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count; townHash=$receipt.sources['Assets/Prefabs/TownWorld/TownEnvironment.prefab']}
        Invoke-ProfileProcess $exe @('-batchmode','-force-d3d11','-screen-width','1920','-screen-height','1080','-screen-fullscreen','0','-town-profile-out',('"'+$evidence+'"'),'-logFile',('"'+(Join-Path $profileRoot ($RunId+'.log'))+'"')) 5
        Assert-ProfileSnapshot $profileProject $mainBefore
        Assert-ProfileSnapshot $profileClone $receipt.cloneSources
        Publish-ProfileEvidence $evidence $RunId $context
    }
    else {
        $cloneBefore=Get-ProfileSnapshot $profileClone
        $sandbox=Join-Path $profileClone 'Assets/Project/Editor/ShopGameplaySandbox.unity'
        $backup=@{}
        foreach($path in @($sandbox,($sandbox+'.meta'))) {
            $backup[$path]=if(Test-Path -LiteralPath $path) {[IO.File]::ReadAllBytes($path)} else {$null}
        }
        try {
            Invoke-ProfileProcess $profileUnity @('-batchmode','-projectPath',('"'+$profileClone+'"'),'-executeMethod','TownSeamBatch.ValidatePlayMode','-logFile',('"'+(Join-Path $profileRoot 'acceptance.log')+'"')) 15
        }
        finally {
            foreach($path in $backup.Keys) {
                if($null -ne $backup[$path]) {[IO.File]::WriteAllBytes($path,$backup[$path])}
                elseif(Test-Path -LiteralPath $path) {Remove-Item -LiteralPath $path}
            }
        }
        Assert-ProfileSnapshot $profileClone $cloneBefore
        $logs=@{
            'Library/TownSeamQA/gameplay.txt'='gameplay.txt';
            'Library/TownSeamQA/runtime.txt'='seams-runtime.txt';
            'Library/TownWorldQA/integration.txt'='town-integration.txt';
            'Library/CityUpdateQA/integration.txt'='city-integration.txt';
            'Library/TownCourtyardQA/runtime.txt'='courtyards-runtime.txt';
            'Library/TownResidentialQA/runtime.txt'='residential-runtime.txt';
            'Library/TownSpecialQA/runtime.txt'='industrial-runtime.txt'
        }
        $gameplay=Get-Content -LiteralPath (Join-Path $profileClone 'Library/TownSeamQA/gameplay.txt') -Raw
        $delivery=Get-Content -LiteralPath (Join-Path $profileClone 'Library/CityUpdateQA/integration.txt') -Raw
        if(-not $gameplay.StartsWith('PASS',[StringComparison]::Ordinal) -or -not $delivery.TrimEnd().EndsWith('CITY DELIVERY INTEGRATION PASSED',[StringComparison]::Ordinal)) {throw 'Native full acceptance did not complete.'}
        $destination=Join-Path $profileProject 'Docs/TownPerformanceReview/Acceptance'
        [IO.Directory]::CreateDirectory($destination) | Out-Null
        foreach($relative in $logs.Keys) {
            $text=Get-Content -LiteralPath (Join-Path $profileClone $relative) -Raw
            if($text -cmatch '(?m)^[^\r\n]*\bFAILED\b') {throw "Acceptance failure: $relative"}
            Copy-Item -LiteralPath (Join-Path $profileClone $relative) -Destination (Join-Path $destination $logs[$relative]) -Force
        }
        Write-Output 'TOWN PERFORMANCE ACCEPTANCE PASSED; original gameplay and all implemented city-stage regressions.'
    }
    Assert-ProfileSnapshot $profileProject $mainBefore
}
finally {$profileLock.Dispose()}
