param([ValidateSet('Prepare','Before','RefreshSources','Generate','After','Apply')][string]$Mode='Prepare')
$ErrorActionPreference='Stop'
$architectureProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$architectureClone=Join-Path $architectureProject 'Library/CityUpdateQA/IsolatedProject'
$architecturePipeline=Join-Path $architectureProject 'Library/TownArchitecturePipeline'
$architectureQa=Join-Path $architectureClone 'Library/TownArchitectureQA'
$architectureAssetRoot='Assets/Art/TownArchitecture'
$architectureScripts=@('TownArchitectureModels','TownArchitectureParts','TownArchitecturePalette','TownArchitectureValidation','TownArchitectureGeneration','TownArchitecturePreviews')
if(-not (Test-Path -LiteralPath (Join-Path $architectureClone 'ProjectSettings/ProjectVersion.txt'))) {throw 'Prepare the existing isolated Unity project first.'}
$architectureRunning=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($architectureClone) }
if($architectureRunning) {throw 'The isolated Unity editor is busy.'}
[IO.Directory]::CreateDirectory($architecturePipeline) | Out-Null

function Get-ArchitectureSnapshot([string]$project) {
    $snapshot=@{}
    foreach($scope in @('Assets','ProjectSettings','Packages','Docs/CityPilotReview')) {
        $scopePath=Join-Path $project $scope
        if(-not (Test-Path -LiteralPath $scopePath)) {continue}
        foreach($file in Get-ChildItem -LiteralPath $scopePath -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath($project,$file.FullName).Replace('\','/')
            if($relative.StartsWith($architectureAssetRoot+'/') -or $relative -eq ($architectureAssetRoot+'.meta')) {continue}
            $snapshot[$relative]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
        }
    }
    return $snapshot
}

function Test-ArchitectureMetadata([string]$relative) {
    foreach($name in $architectureScripts) {
        if($relative -eq ('Assets/Project/Editor/TownKit/'+$name+'.cs.meta')) {return $true}
    }
    return $false
}

function Test-ArchitectureSource([string]$relative) {
    foreach($name in $architectureScripts) {
        if($relative -eq ('Assets/Project/Editor/TownKit/'+$name+'.cs') -or $relative -eq ('Assets/Project/Editor/TownKit/'+$name+'.cs.meta')) {return $true}
    }
    return $false
}

function Assert-ArchitectureSnapshot([string]$project,$baseline,[bool]$allowAuthoringChanges=$false) {
    $current=Get-ArchitectureSnapshot $project
    foreach($relative in $baseline.Keys) {
        if($allowAuthoringChanges -and (Test-ArchitectureSource $relative)) {continue}
        if(-not $current.ContainsKey($relative) -or $current[$relative] -ne $baseline[$relative]) {throw "Protected file changed: $relative ($project)"}
    }
    foreach($relative in $current.Keys) {
        if(-not $baseline.ContainsKey($relative) -and -not (Test-ArchitectureMetadata $relative)) {throw "Unexpected file outside architecture kit: $relative ($project)"}
    }
}

function Read-ArchitectureBaseline {
    $path=Join-Path $architecturePipeline 'baseline.json'
    if(-not (Test-Path -LiteralPath $path)) {throw 'Run Prepare before architecture QA.'}
    return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable)
}

function Assert-ArchitectureReceipt([string]$phase) {
    $receipt=Get-Content -LiteralPath (Join-Path $architecturePipeline ($phase+'.json')) -Raw | ConvertFrom-Json
    if($receipt.exitCode -ne 0 -or $receipt.baselineHash -ne (Get-FileHash -LiteralPath (Join-Path $architecturePipeline 'baseline.json')).Hash) {throw "Stage 2 $phase did not validate the current baseline."}
}

if($Mode -eq 'Prepare') {
    if(Test-Path -LiteralPath (Join-Path $architectureQa 'Before/complete.txt')) {throw 'This Stage 2 Before already exists. Preserve that QA directory before preparing a different baseline.'}
    # Synchronise only known source scopes. No scene generator or legacy model generator is invoked.
    foreach($scope in @('Assets/Project','Assets/Prefabs','Assets/Resources/ShopUi','Assets/Art/TownKit','Assets/Art/TownDetails','Assets/Shaders','Assets/Settings','ProjectSettings/QualitySettings.asset','ProjectSettings/GraphicsSettings.asset')) {
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $architectureProject $scope) -File -Recurse) {
            $target=Join-Path $architectureClone ([IO.Path]::GetRelativePath($architectureProject,$file.FullName))
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        }
    }
    $baseline=@{main=(Get-ArchitectureSnapshot $architectureProject);isolated=(Get-ArchitectureSnapshot $architectureClone)}
    [IO.File]::WriteAllText((Join-Path $architecturePipeline 'baseline.json'),($baseline | ConvertTo-Json -Depth 5))
    Write-Output 'Stage 2 prepared: legacy scenes, shaders, models, palettes and Stage 1 evidence are protected by raw SHA256.'
    exit 0
}

$architectureBaseline=Read-ArchitectureBaseline
if($Mode -eq 'RefreshSources') {
    # Iteration may change only the six new kit authoring files, never a scene/camera/legacy generator.
    Assert-ArchitectureReceipt 'Before'
    Assert-ArchitectureSnapshot $architectureProject $architectureBaseline.main $true
    Assert-ArchitectureSnapshot $architectureClone $architectureBaseline.isolated $true
    $inputs=Get-Content -LiteralPath (Join-Path $architectureQa 'Before/source-hashes.json') -Raw | ConvertFrom-Json
    foreach($inputFile in $inputs.files | Where-Object { $_.path.StartsWith('Assets/',[StringComparison]::Ordinal) }) {
        if((Get-FileHash -LiteralPath (Join-Path $architectureClone $inputFile.path)).Hash -ne $inputFile.sha256) {throw "Immutable Before scene input changed: $($inputFile.path)"}
    }
    foreach($name in $architectureScripts) {
        foreach($extension in @('.cs','.cs.meta')) {
            $relative='Assets/Project/Editor/TownKit/'+$name+$extension
            $source=Join-Path $architectureProject $relative
            if(Test-Path -LiteralPath $source) {Copy-Item -LiteralPath $source -Destination (Join-Path $architectureClone $relative) -Force}
        }
    }
    $updated=@{main=(Get-ArchitectureSnapshot $architectureProject);isolated=(Get-ArchitectureSnapshot $architectureClone)}
    [IO.File]::WriteAllText((Join-Path $architecturePipeline 'baseline.json'),($updated | ConvertTo-Json -Depth 5))
    # Only code that cannot affect the immutable City baseline changed. All old generation receipts become stale.
    [IO.File]::WriteAllText((Join-Path $architecturePipeline 'Before.json'),(@{exitCode=0;baselineHash=(Get-FileHash -LiteralPath (Join-Path $architecturePipeline 'baseline.json')).Hash} | ConvertTo-Json))
    Write-Output 'New-kit authoring sources refreshed. Original Before scene/cameras preserved; regenerate before acceptance.'
    exit 0
}
Assert-ArchitectureSnapshot $architectureProject $architectureBaseline.main
Assert-ArchitectureSnapshot $architectureClone $architectureBaseline.isolated
if($Mode -in @('Before','Generate','After')) {
    if($Mode -eq 'Generate') {Assert-ArchitectureReceipt 'Before'}
    if($Mode -eq 'After') {Assert-ArchitectureReceipt 'Generate'}
    $entry=switch($Mode) {
        'Before' {'TownPilotReview.CaptureArchitectureBefore'}
        'Generate' {'RetailEmpireTycoon.Editor.TownKit.TownArchitectureGeneration.GenerateBatch'}
        'After' {'TownPilotReview.CaptureArchitectureAfter'}
    }
    $receiptPath=Join-Path $architecturePipeline ($Mode+'.json')
    [IO.File]::WriteAllText($receiptPath,'{"exitCode":-1}')
    $arguments='-batchmode -projectPath "'+$architectureClone+'" -executeMethod '+$entry+' -logFile "'+(Join-Path $architecturePipeline ($Mode+'.log'))+'"'
    $process=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if($process.ExitCode -ne 0) {throw "Architecture Unity $Mode failed: $($process.ExitCode)"}
    Assert-ArchitectureSnapshot $architectureProject $architectureBaseline.main
    Assert-ArchitectureSnapshot $architectureClone $architectureBaseline.isolated
    $receipt=@{exitCode=0;baselineHash=(Get-FileHash -LiteralPath (Join-Path $architecturePipeline 'baseline.json')).Hash}
    [IO.File]::WriteAllText($receiptPath,($receipt | ConvertTo-Json))
    if($Mode -eq 'After') {
        foreach($phase in @('Before','After')) {
            if(-not (Get-Content -LiteralPath (Join-Path $architectureQa ($phase+'/complete.txt')) -Raw).StartsWith('PASS')) {throw "Architecture scene capture failed: $phase"}
        }
        $before=Join-Path $architectureQa 'Before'
        $after=Join-Path $architectureQa 'After'
        foreach($name in @('camera-manifest.json','source-hashes.json')) {
            if((Get-FileHash -LiteralPath (Join-Path $before $name)).Hash -ne (Get-FileHash -LiteralPath (Join-Path $after $name)).Hash) {throw "Asset-only stage changed scene/cameras: $name"}
        }
        $beforeMetrics=Get-Content -LiteralPath (Join-Path $before 'metrics.json') -Raw | ConvertFrom-Json
        $afterMetrics=Get-Content -LiteralPath (Join-Path $after 'metrics.json') -Raw | ConvertFrom-Json
        foreach($field in @('allSceneRenderers','allSceneTriangles','allTownRenderers','allAmbientWalkers','activeAmbientWalkers','activeBackgroundSpawners','plots')) {
            if(($beforeMetrics.$field | ConvertTo-Json -Depth 12 -Compress) -ne ($afterMetrics.$field | ConvertTo-Json -Depth 12 -Compress)) {throw "Asset-only stage changed measurements: $field"}
        }
        $outputs=@{}
        foreach($scope in @((Join-Path $architectureClone $architectureAssetRoot),$architectureQa)) {
            foreach($file in Get-ChildItem -LiteralPath $scope -File -Recurse) {$outputs[[IO.Path]::GetRelativePath($architectureClone,$file.FullName).Replace('\','/')]=(Get-FileHash -LiteralPath $file.FullName).Hash}
        }
        $outputs[$architectureAssetRoot+'.meta']=(Get-FileHash -LiteralPath (Join-Path $architectureClone ($architectureAssetRoot+'.meta'))).Hash
        [IO.File]::WriteAllText((Join-Path $architecturePipeline 'outputs.json'),($outputs | ConvertTo-Json -Depth 3))
        [IO.File]::WriteAllText((Join-Path $architecturePipeline 'protection.txt'),"PASS: every existing asset, shader, material, scene, layout and project setting unchanged. Before/After camera manifests, scene dependencies and plot measurements identical. Only native architecture assets were generated.`n")
    }
    Write-Output "Architecture $Mode passed; protected project unchanged."
    exit 0
}

if($Mode -eq 'Apply') {
    foreach($phase in @('Before','Generate','After')) {Assert-ArchitectureReceipt $phase}
    if(-not (Get-Content -LiteralPath (Join-Path $architectureQa 'result.txt') -Raw).StartsWith('TOWN ARCHITECTURE PASSED')) {throw 'Architecture model QA did not pass.'}
    if(-not (Get-Content -LiteralPath (Join-Path $architecturePipeline 'protection.txt') -Raw).StartsWith('PASS')) {throw 'Protected file validation did not pass.'}
    $outputs=Get-Content -LiteralPath (Join-Path $architecturePipeline 'outputs.json') -Raw | ConvertFrom-Json -AsHashtable
    $copies=[Collections.Generic.List[object]]::new()
    foreach($relative in $outputs.Keys) {
        $source=Join-Path $architectureClone $relative
        $canonicalRelative=[IO.Path]::GetRelativePath($architectureClone,[IO.Path]::GetFullPath($source)).Replace('\','/')
        if($canonicalRelative -ne $relative) {throw "Output path is not canonical: $relative"}
        if((Get-FileHash -LiteralPath $source).Hash -ne $outputs[$relative]) {throw "Validated output changed: $relative"}
        if($relative.StartsWith($architectureAssetRoot+'/') -or $relative -eq ($architectureAssetRoot+'.meta')) {$targetRelative=$relative}
        elseif($relative.StartsWith('Library/TownArchitectureQA/')) {$targetRelative='Docs/TownArchitectureReview/'+$relative.Substring('Library/TownArchitectureQA/'.Length)}
        else {throw "Output is not in the Stage 2 allowlist: $relative"}
        $copies.Add(@{source=$source;relative=$targetRelative;hash=$outputs[$relative]})
    }
    foreach($name in $architectureScripts) {
        $relative='Assets/Project/Editor/TownKit/'+$name+'.cs.meta'
        $source=Join-Path $architectureClone $relative
        if(Test-Path -LiteralPath $source) {$copies.Add(@{source=$source;relative=$relative;hash=(Get-FileHash -LiteralPath $source).Hash})}
    }
    $copies.Add(@{source=(Join-Path $architecturePipeline 'protection.txt');relative='Docs/TownArchitectureReview/protection.txt';hash=(Get-FileHash -LiteralPath (Join-Path $architecturePipeline 'protection.txt')).Hash})
    # This first asset-only delivery must never replace an independently edited existing file.
    foreach($copy in $copies) {
        $target=Join-Path $architectureProject $copy.relative
        if((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -ne $copy.hash) {throw "Refusing to overwrite an existing Stage 2 target: $($copy.relative)"}
    }
    $staging=Join-Path $architecturePipeline 'TransferStaging'
    [IO.Directory]::CreateDirectory($staging) | Out-Null
    foreach($copy in $copies) {
        $target=Join-Path $architectureProject $copy.relative
        if(Test-Path -LiteralPath $target) {continue}
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        $incoming=Join-Path $staging ([Guid]::NewGuid().ToString()+'.tmp')
        Copy-Item -LiteralPath $copy.source -Destination $incoming
        [IO.File]::Move($incoming,$target)
        if((Get-FileHash -LiteralPath $target).Hash -ne $copy.hash) {throw "Transferred file mismatch: $($copy.relative)"}
    }
    Assert-ArchitectureSnapshot $architectureProject $architectureBaseline.main
    [IO.File]::WriteAllText((Join-Path $architecturePipeline 'applied.txt'),"PASS: $($copies.Count) verified native asset/review files delivered. No scene or existing model changed.`n")
    Write-Output "Stage 2 native assets applied ($($copies.Count) files); City composition and all existing assets unchanged."
}
