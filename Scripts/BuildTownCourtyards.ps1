param([ValidateSet('Prepare','Before','Generate','After','Play','Apply')][string]$Mode='Prepare')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

$courtyardProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$courtyardClone=Join-Path $courtyardProject 'Library/CityUpdateQA/IsolatedProject'
$courtyardPipeline=Join-Path $courtyardProject 'Library/TownCourtyardPipeline'
$courtyardQa=Join-Path $courtyardClone 'Library/TownCourtyardQA'
$courtyardAssetRoot='Assets/Art/TownCourtyards'
$courtyardReviewRoot='Docs/TownCourtyardReview'
$courtyardPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$courtyardSandbox='Assets/Project/Editor/ShopGameplaySandbox.unity'
$courtyardUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$courtyardBaselinePath=Join-Path $courtyardPipeline 'baseline.json'
$courtyardOriginalPath=Join-Path $courtyardQa 'OriginalTown.prefab'

if(-not (Test-Path -LiteralPath (Join-Path $courtyardClone 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The existing isolated Unity project must be prepared first.'
}
if(-not (Test-Path -LiteralPath $courtyardUnity)) {throw 'The configured Unity 2022.3.62f3 editor is missing.'}
[IO.Directory]::CreateDirectory($courtyardPipeline) | Out-Null

# An exclusive file handle prevents two pipeline invocations from racing through the busy check.
$courtyardLock=[IO.File]::Open((Join-Path $courtyardPipeline 'pipeline.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)

function Assert-CourtyardUnityIdle {
    $running=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($courtyardClone,[StringComparison]::OrdinalIgnoreCase) }
    if($running) {throw 'The isolated Unity editor is busy. Wait for that run to exit before continuing.'}
}

function Get-CourtyardHash([string]$path) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "Required file is missing: $path"}
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

function Write-CourtyardJson([string]$path,$value) {
    [IO.File]::WriteAllText($path,($value | ConvertTo-Json -Depth 12))
}

function Read-CourtyardJson([string]$path) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "Run the prerequisite phase first; missing receipt: $path"}
    return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable)
}

function Resolve-CourtyardRelative([string]$project,[string]$relative) {
    $path=[IO.Path]::GetFullPath((Join-Path $project $relative))
    $canonical=[IO.Path]::GetRelativePath($project,$path).Replace('\','/')
    if($canonical -cne $relative -or $canonical.StartsWith('../',[StringComparison]::Ordinal) -or [IO.Path]::IsPathRooted($relative)) {
        throw "Non-canonical project path: $relative"
    }
    return $path
}

function Test-CourtyardOutput([string]$relative) {
    return $relative -ceq $courtyardPrefab -or $relative -ceq ($courtyardAssetRoot+'.meta') -or
        $relative.StartsWith($courtyardAssetRoot+'/',[StringComparison]::Ordinal)
}

function Test-CourtyardAuthoring([string]$relative) {
    return $relative -cmatch '^Assets/Project/Editor/TownCourtyard[^/]*\.cs(\.meta)?$' -or
        $relative -cmatch '^Assets/Project/Editor/TownKit/TownCourtyardModels\.cs(\.meta)?$' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotReview.cs' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotReview.cs.meta'
}

function Test-CourtyardNewMetadata([string]$relative) {
    return $relative -cmatch '^Assets/Project/Editor/TownCourtyard[^/]*\.cs\.meta$' -or
        $relative -ceq 'Assets/Project/Editor/TownKit/TownCourtyardModels.cs.meta'
}

function Get-CourtyardSnapshot([string]$project) {
    $snapshot=@{}
    foreach($scope in @('Assets','ProjectSettings','Packages','Docs')) {
        $scopePath=Join-Path $project $scope
        if(-not (Test-Path -LiteralPath $scopePath)) {continue}
        foreach($file in Get-ChildItem -LiteralPath $scopePath -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath($project,$file.FullName).Replace('\','/')
            if($relative -ceq ($courtyardAssetRoot+'.meta') -or $relative.StartsWith($courtyardAssetRoot+'/',[StringComparison]::Ordinal) -or
                $relative -ceq ($courtyardReviewRoot+'.md') -or $relative.StartsWith($courtyardReviewRoot+'/',[StringComparison]::Ordinal)) {continue}
            $snapshot[$relative]=Get-CourtyardHash $file.FullName
        }
    }
    return $snapshot
}

function Assert-CourtyardSnapshot([string]$project,$baseline,[bool]$allowTownChange=$false,[bool]$allowAuthoringChanges=$false) {
    $current=Get-CourtyardSnapshot $project
    foreach($relative in $baseline.Keys) {
        if($allowTownChange -and $relative -ceq $courtyardPrefab) {continue}
        if($allowAuthoringChanges -and (Test-CourtyardAuthoring $relative)) {
            if(-not $current.ContainsKey($relative)) {throw "Prepare cannot remove a previously prepared authoring source: $relative"}
            continue
        }
        if(-not $current.ContainsKey($relative) -or $current[$relative] -cne $baseline[$relative]) {
            throw "Protected file changed: $relative ($project)"
        }
    }
    foreach($relative in $current.Keys) {
        if(-not $baseline.ContainsKey($relative) -and -not (Test-CourtyardNewMetadata $relative) -and
            -not ($allowAuthoringChanges -and (Test-CourtyardAuthoring $relative))) {
            throw "Unexpected file outside the Stage 4 allowlist: $relative ($project)"
        }
    }
}

function Assert-CourtyardReceipt([string]$phase) {
    $receipt=Read-CourtyardJson (Join-Path $courtyardPipeline ($phase+'.json'))
    if($receipt.exitCode -ne 0 -or $receipt.baselineHash -cne (Get-CourtyardHash $courtyardBaselinePath)) {
        throw "Stage 4 $phase has not validated the current source baseline."
    }
    return $receipt
}

function Set-CourtyardReceiptPending([string]$phase) {
    Write-CourtyardJson (Join-Path $courtyardPipeline ($phase+'.json')) @{exitCode=-1}
}

function Assert-CourtyardPass([string]$path,[string[]]$prefixes=@('PASS')) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "QA evidence is missing: $path"}
    $value=Get-Content -LiteralPath $path -Raw
    if($value -cmatch '(?m)^[^\r\n]*\bFAILED\b') {throw "QA contains a failure: $path"}
    foreach($prefix in $prefixes) {
        if($value.StartsWith($prefix,[StringComparison]::Ordinal)) {return}
    }
    throw "QA did not pass: $path"
}

function Assert-CourtyardGenerationEvidence {
    Assert-CourtyardPass (Join-Path $courtyardQa 'result.txt') @('TOWN COURTYARDS PASSED')
    Assert-CourtyardPass (Join-Path $courtyardQa 'templates.txt')
    Assert-CourtyardPass (Join-Path $courtyardQa 'assets.txt')
    Assert-CourtyardPass (Join-Path $courtyardQa 'protection.txt')
}

function Assert-CourtyardGameplayEvidence {
    Assert-CourtyardPass (Join-Path $courtyardQa 'gameplay.txt')
    Assert-CourtyardPass (Join-Path $courtyardQa 'runtime.txt')
    Assert-CourtyardPass (Join-Path $courtyardClone 'Library/TownWorldQA/integration.txt') @('TOWN WORLD PASSED')
    $cityEvidence=Get-Content -LiteralPath (Join-Path $courtyardClone 'Library/CityUpdateQA/integration.txt') -Raw
    # Individual scenarios append PASS before the full trip finishes. Only the exact final terminal
    # plus absence of a FAILED line proves completion; a partial PASS log must never authorise Apply.
    if(-not $cityEvidence.TrimEnd().EndsWith('CITY DELIVERY INTEGRATION PASSED',[StringComparison]::Ordinal) -or
        $cityEvidence -cmatch '(?m)^[^\r\n]*\bFAILED\b') {
        throw 'The original City delivery regression did not finish successfully, or its log contains a FAILED line.'
    }
}

function Assert-CourtyardCaptureEvidence([string]$phase) {
    Assert-CourtyardPass (Join-Path $courtyardQa ($phase+'/complete.txt'))
    $directory=Join-Path $courtyardQa $phase
    $manifest=Read-CourtyardJson (Join-Path $directory 'camera-manifest.json')
    $sources=Read-CourtyardJson (Join-Path $directory 'source-hashes.json')
    if($manifest.schemaVersion -ne 1 -or $manifest.cameras.Count -eq 0 -or $sources.files.Count -eq 0) {
        throw "Incomplete native $phase capture manifest."
    }
    $names=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($camera in $manifest.cameras) {
        if($camera.name -cnotmatch '^[A-Za-z][A-Za-z0-9_-]*$' -or -not $names.Add($camera.name) -or
            $camera.width -lt 1 -or $camera.height -lt 1) {
            throw "Invalid or duplicate $phase camera definition."
        }
        $path=Join-Path $directory ($camera.name+'.png')
        if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "Missing native $phase screenshot: $($camera.name)"}
        $stream=[IO.File]::OpenRead($path)
        try {
            $header=[byte[]]::new(24)
            if($stream.Read($header,0,$header.Length) -ne $header.Length -or
                [BitConverter]::ToString($header,0,8) -cne '89-50-4E-47-0D-0A-1A-0A' -or
                [Text.Encoding]::ASCII.GetString($header,12,4) -cne 'IHDR') {
                throw "Invalid native PNG evidence: $path"
            }
            $width=[uint32]$header[16]*16777216+[uint32]$header[17]*65536+[uint32]$header[18]*256+$header[19]
            $height=[uint32]$header[20]*16777216+[uint32]$header[21]*65536+[uint32]$header[22]*256+$header[23]
            if($width -ne $camera.width -or $height -ne $camera.height) {throw "Native capture dimensions differ from the camera manifest: $path"}
        }
        finally {$stream.Dispose()}
    }
    $screenshots=@(Get-ChildItem -LiteralPath $directory -Filter '*.png' -File -Recurse)
    if($screenshots.Count -ne $names.Count) {throw "The $phase native screenshot count differs from its camera manifest."}
}

function Assert-CourtyardOriginal($baseline) {
    if((Get-CourtyardHash $courtyardOriginalPath) -cne $baseline.originalPrefabHash -or
        (Get-CourtyardHash ($courtyardOriginalPath+'.meta')) -cne $baseline.originalMetadataHash) {
        throw 'The immutable original town backup changed. It must not be replaced with a generated variant.'
    }
    if((Get-CourtyardHash (Join-Path $courtyardClone ($courtyardPrefab+'.meta'))) -cne $baseline.originalMetadataHash) {
        throw 'The original town prefab GUID/import metadata changed in the isolated project.'
    }
}

function Copy-CourtyardAtomically([string]$source,[string]$target,[bool]$replace=$false) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    $incoming=Join-Path $courtyardPipeline ([Guid]::NewGuid().ToString()+'.incoming')
    Copy-Item -LiteralPath $source -Destination $incoming
    try {
        if((Get-CourtyardHash $incoming) -cne (Get-CourtyardHash $source)) {throw "Staging copy mismatch: $source"}
        # Both workspaces and staging are on this project's volume; rename a complete file, never truncate an open asset.
        [IO.File]::Move($incoming,$target,$replace)
    }
    finally {
        if(Test-Path -LiteralPath $incoming) {[IO.File]::Delete($incoming)}
    }
}

function Get-CourtyardGeneratedAssets {
    $outputs=@{}
    $outputs[$courtyardPrefab]=Get-CourtyardHash (Join-Path $courtyardClone $courtyardPrefab)
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $courtyardClone $courtyardAssetRoot) -File -Recurse) {
        $relative=[IO.Path]::GetRelativePath($courtyardClone,$file.FullName).Replace('\','/')
        $outputs[$relative]=Get-CourtyardHash $file.FullName
    }
    $outputs[$courtyardAssetRoot+'.meta']=Get-CourtyardHash (Join-Path $courtyardClone ($courtyardAssetRoot+'.meta'))
    return $outputs
}

function Assert-CourtyardHashes([string]$project,$expected,[bool]$requireExactAssetSet=$false) {
    foreach($relative in $expected.Keys) {
        $path=Resolve-CourtyardRelative $project $relative
        if((Get-CourtyardHash $path) -cne $expected[$relative]) {throw "Validated output changed: $relative"}
    }
    if($requireExactAssetSet) {
        $current=Get-CourtyardGeneratedAssets
        if($current.Count -ne $expected.Count) {throw 'The set of generated town assets changed after Generate.'}
        foreach($relative in $current.Keys) {
            if(-not $expected.ContainsKey($relative)) {throw "Unvalidated generated asset: $relative"}
        }
    }
}

function Assert-CourtyardCaptureInputs([string]$phase,[bool]$allowGeneratedTown=$false) {
    $inputs=Read-CourtyardJson (Join-Path $courtyardQa ($phase+'/source-hashes.json'))
    foreach($inputFile in $inputs.files) {
        $relative=$inputFile.path
        # Packages are mounted virtually by Unity. They remain protected by the complete package-file baseline.
        if(-not $relative.StartsWith('Assets/',[StringComparison]::Ordinal)) {continue}
        if($allowGeneratedTown -and (Test-CourtyardOutput $relative)) {continue}
        $path=Resolve-CourtyardRelative $courtyardClone $relative
        if((Get-CourtyardHash $path) -cne $inputFile.sha256) {throw "Stale $phase capture input: $relative"}
    }
}

function Get-CourtyardCaptureHashes([string]$phase) {
    $outputs=@{}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $courtyardQa $phase) -File -Recurse) {
        $relative=[IO.Path]::GetRelativePath($courtyardClone,$file.FullName).Replace('\','/')
        $outputs[$relative]=Get-CourtyardHash $file.FullName
    }
    return $outputs
}

function Assert-CourtyardBeforeEvidence {
    $receipt=Assert-CourtyardReceipt 'Before'
    Assert-CourtyardCaptureEvidence 'Before'
    Assert-CourtyardHashes $courtyardClone $receipt.evidence
    $current=Get-CourtyardCaptureHashes 'Before'
    if($current.Count -ne $receipt.evidence.Count) {throw 'The immutable Before evidence file set changed.'}
}

function Invoke-CourtyardUnity([string]$phase,[string]$entry) {
    Assert-CourtyardUnityIdle
    $arguments='-batchmode -projectPath "'+$courtyardClone+'" -executeMethod '+$entry+' -logFile "'+(Join-Path $courtyardPipeline ($phase+'.log'))+'"'
    $process=Start-Process -FilePath $courtyardUnity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if($process.ExitCode -ne 0) {throw "Stage 4 Unity $phase failed with exit code $($process.ExitCode). See its pipeline log."}
}

function Save-CourtyardPlayFixture($baseline) {
    $fixture=@{
        directory=(Join-Path $courtyardPipeline ('PlayFixture_'+[Guid]::NewGuid().ToString('N')))
        baselineHash=(Get-CourtyardHash $courtyardBaselinePath)
        files=@{}
    }
    foreach($relative in @($courtyardSandbox,($courtyardSandbox+'.meta'))) {
        if(-not $baseline.main.ContainsKey($relative) -or -not $baseline.isolated.ContainsKey($relative) -or
            $baseline.main[$relative] -cne $baseline.isolated[$relative]) {
            throw "Sandbox fixture must have identical prepared main/isolated bytes: $relative"
        }
        $main=Resolve-CourtyardRelative $courtyardProject $relative
        $source=Resolve-CourtyardRelative $courtyardClone $relative
        $expected=$baseline.isolated[$relative]
        if((Get-CourtyardHash $main) -cne $baseline.main[$relative] -or (Get-CourtyardHash $source) -cne $expected) {
            throw "The protected sandbox changed before Play; do not adopt it as a temporary fixture: $relative"
        }
        $fixture.files[$relative]=$expected
    }
    # The existing sandbox helper saves its fixed-path scene. Back up only these two known fixture files,
    # outside Assets, rather than weakening the scene SHA guard or rewriting shared test infrastructure.
    [IO.Directory]::CreateDirectory($fixture.directory) | Out-Null
    foreach($relative in $fixture.files.Keys) {
        $saved=Resolve-CourtyardRelative $fixture.directory $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
        Copy-Item -LiteralPath (Resolve-CourtyardRelative $courtyardClone $relative) -Destination $saved
        if((Get-CourtyardHash $saved) -cne $fixture.files[$relative]) {throw "Sandbox fixture backup mismatch: $relative"}
    }
    Write-CourtyardJson (Join-Path $fixture.directory 'receipt.json') $fixture
    Write-CourtyardJson (Join-Path $courtyardPipeline 'active-play-fixture.json') $fixture
    return $fixture
}

function Restore-CourtyardPlayFixture($fixture) {
    # Never restore an asset while Unity can still be saving/importing it, even when its run failed.
    Assert-CourtyardUnityIdle
    $directory=[IO.Path]::GetFullPath($fixture.directory)
    if([IO.Path]::GetDirectoryName($directory) -cne [IO.Path]::GetFullPath($courtyardPipeline) -or
        [IO.Path]::GetFileName($directory) -cnotmatch '^PlayFixture_[0-9a-f]{32}$' -or $fixture.files.Count -ne 2) {
        throw 'Invalid scoped sandbox fixture backup.'
    }
    if($fixture.baselineHash -cne (Get-CourtyardHash $courtyardBaselinePath)) {
        throw 'The prepared baseline changed while the scoped sandbox fixture was active.'
    }
    $baseline=Read-CourtyardJson $courtyardBaselinePath
    foreach($relative in @($courtyardSandbox,($courtyardSandbox+'.meta'))) {
        if(-not $fixture.files.ContainsKey($relative)) {throw "Sandbox fixture backup omits a protected file: $relative"}
        if(-not $baseline.main.ContainsKey($relative) -or -not $baseline.isolated.ContainsKey($relative) -or
            $fixture.files[$relative] -cne $baseline.main[$relative] -or $fixture.files[$relative] -cne $baseline.isolated[$relative] -or
            (Get-CourtyardHash (Resolve-CourtyardRelative $courtyardProject $relative)) -cne $baseline.main[$relative]) {
            throw "Sandbox fixture does not match the protected original main/isolated bytes: $relative"
        }
        $saved=Resolve-CourtyardRelative $directory $relative
        if((Get-CourtyardHash $saved) -cne $fixture.files[$relative]) {throw "Sandbox fixture original bytes changed: $relative"}
    }
    foreach($relative in @($courtyardSandbox,($courtyardSandbox+'.meta'))) {
        $target=Resolve-CourtyardRelative $courtyardClone $relative
        if(-not (Test-Path -LiteralPath $target) -or (Get-CourtyardHash $target) -cne $fixture.files[$relative]) {
            Copy-CourtyardAtomically (Resolve-CourtyardRelative $directory $relative) $target $true
        }
        if((Get-CourtyardHash $target) -cne $fixture.files[$relative]) {throw "Sandbox fixture restoration mismatch: $relative"}
    }
    [IO.File]::WriteAllText((Join-Path $fixture.directory 'restored.txt'),
        "PASS: exact isolated sandbox scene/meta bytes restored after Unity exit. Both files remain in the full protected-file SHA baseline; no main scene was written.`n")
}

function Restore-CourtyardPendingPlayFixture {
    $active=Join-Path $courtyardPipeline 'active-play-fixture.json'
    if(-not (Test-Path -LiteralPath $active)) {return}
    $fixture=Read-CourtyardJson $active
    $directory=[IO.Path]::GetFullPath($fixture.directory)
    if([IO.Path]::GetDirectoryName($directory) -cne [IO.Path]::GetFullPath($courtyardPipeline) -or
        [IO.Path]::GetFileName($directory) -cnotmatch '^PlayFixture_[0-9a-f]{32}$') {
        throw 'Invalid pending sandbox fixture path.'
    }
    if(Test-Path -LiteralPath (Join-Path $directory 'restored.txt')) {return}
    # A PowerShell crash can bypass finally. Resume only this recorded fixture, whose backup/main bytes
    # must still match the same baseline; a changed source/scene can never be silently adopted.
    Restore-CourtyardPlayFixture $fixture
    Write-Output 'Recovered the pending isolated sandbox fixture after an interrupted Play invocation.'
}

function Invoke-CourtyardPlay($baseline) {
    $fixture=Save-CourtyardPlayFixture $baseline
    $playFailure=$null
    $restorationFailure=$null
    try {
        Invoke-CourtyardUnity 'Play' 'TownCourtyardBatch.ValidatePlayMode'
    }
    catch {$playFailure=$_}
    finally {
        try {Restore-CourtyardPlayFixture $fixture}
        catch {$restorationFailure=$_}
    }
    if($playFailure -and $restorationFailure) {
        throw "Play failed: $($playFailure.Exception.Message). Sandbox restoration also failed: $($restorationFailure.Exception.Message). Original fixture: $($fixture.directory)"
    }
    if($restorationFailure) {throw $restorationFailure}
    if($playFailure) {throw $playFailure}
}

try {
    Assert-CourtyardUnityIdle
    Restore-CourtyardPendingPlayFixture
    if($Mode -eq 'Prepare') {
        $refresh=Test-Path -LiteralPath $courtyardBaselinePath
        if($refresh) {
            $previous=Read-CourtyardJson $courtyardBaselinePath
            Assert-CourtyardSnapshot $courtyardProject $previous.main $false $true
            Assert-CourtyardSnapshot $courtyardClone $previous.isolated $true $true
            Assert-CourtyardOriginal $previous
            $hasBefore=Test-Path -LiteralPath (Join-Path $courtyardQa 'Before/complete.txt')
            if($hasBefore) {
                Assert-CourtyardBeforeEvidence
                Assert-CourtyardCaptureInputs 'Before' $true
            }
            # A retry can refresh only the Stage 4 authoring code; all saved scenes/assets/Before images remain immutable.
            foreach($file in Get-ChildItem -LiteralPath (Join-Path $courtyardProject 'Assets/Project/Editor') -File -Recurse) {
                $relative=[IO.Path]::GetRelativePath($courtyardProject,$file.FullName).Replace('\','/')
                if(Test-CourtyardAuthoring $relative) {
                    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $courtyardClone $relative) -Force
                }
            }
            Copy-CourtyardAtomically $courtyardOriginalPath (Join-Path $courtyardClone $courtyardPrefab) $true
            $baseline=@{
                main=(Get-CourtyardSnapshot $courtyardProject)
                isolated=(Get-CourtyardSnapshot $courtyardClone)
                originalPrefabHash=$previous.originalPrefabHash
                originalMetadataHash=$previous.originalMetadataHash
            }
            Write-CourtyardJson $courtyardBaselinePath $baseline
            if($hasBefore) {
                Write-CourtyardJson (Join-Path $courtyardPipeline 'Before.json') @{
                    exitCode=0;baselineHash=(Get-CourtyardHash $courtyardBaselinePath);evidence=(Get-CourtyardCaptureHashes 'Before')
                }
            }
            foreach($phase in @('Generate','After','Play')) {Set-CourtyardReceiptPending $phase}
            Write-Output 'Stage 4 authoring refreshed. Immutable Before/original town preserved; Generate, After and Play must be repeated.'
            exit 0
        }

        if(Test-Path -LiteralPath (Join-Path $courtyardQa 'Before/complete.txt')) {
            throw 'Before evidence exists without its pipeline baseline. Preserve that evidence; do not prepare over it.'
        }
        if((Test-Path -LiteralPath (Join-Path $courtyardProject $courtyardAssetRoot)) -or
            (Test-Path -LiteralPath (Join-Path $courtyardProject ($courtyardAssetRoot+'.meta')))) {
            throw 'Stage 4 generated output already exists in the main project. A new delivery needs a separately approved baseline.'
        }
        foreach($scope in @('Assets','ProjectSettings','Packages','Docs')) {
            $scopePath=Join-Path $courtyardProject $scope
            if(-not (Test-Path -LiteralPath $scopePath)) {continue}
            foreach($file in Get-ChildItem -LiteralPath $scopePath -File -Recurse) {
                $relative=[IO.Path]::GetRelativePath($courtyardProject,$file.FullName).Replace('\','/')
                if($relative -ceq ($courtyardReviewRoot+'.md') -or $relative.StartsWith($courtyardReviewRoot+'/',[StringComparison]::Ordinal)) {continue}
                $target=Resolve-CourtyardRelative $courtyardClone $relative
                if((Test-Path -LiteralPath $target -PathType Leaf) -and (Get-CourtyardHash $target) -ceq (Get-CourtyardHash $file.FullName)) {continue}
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            }
        }
        [IO.Directory]::CreateDirectory($courtyardQa) | Out-Null
        foreach($backup in @(@{source=(Join-Path $courtyardProject $courtyardPrefab);target=$courtyardOriginalPath},
            @{source=(Join-Path $courtyardProject ($courtyardPrefab+'.meta'));target=($courtyardOriginalPath+'.meta')})) {
            if(Test-Path -LiteralPath $backup.target) {
                if((Get-CourtyardHash $backup.target) -cne (Get-CourtyardHash $backup.source)) {throw 'An existing original-town backup differs; refusing to replace it.'}
            }
            else {Copy-Item -LiteralPath $backup.source -Destination $backup.target}
        }
        $baseline=@{
            main=(Get-CourtyardSnapshot $courtyardProject)
            isolated=(Get-CourtyardSnapshot $courtyardClone)
            originalPrefabHash=(Get-CourtyardHash $courtyardOriginalPath)
            originalMetadataHash=(Get-CourtyardHash ($courtyardOriginalPath+'.meta'))
        }
        Write-CourtyardJson $courtyardBaselinePath $baseline
        Write-Output 'Stage 4 prepared: all original assets/settings/packages and Stage 1/2/3 evidence protected; original town saved outside Assets.'
        exit 0
    }

    $courtyardBaseline=Read-CourtyardJson $courtyardBaselinePath
    Assert-CourtyardSnapshot $courtyardProject $courtyardBaseline.main
    Assert-CourtyardSnapshot $courtyardClone $courtyardBaseline.isolated ($Mode -ne 'Before')
    Assert-CourtyardOriginal $courtyardBaseline

    if($Mode -eq 'Before') {
        if(Test-Path -LiteralPath (Join-Path $courtyardQa 'Before/complete.txt')) {throw 'Before is immutable. Continue with Generate, or Prepare to refresh only Stage 4 authoring code.'}
        Set-CourtyardReceiptPending 'Before'
        Invoke-CourtyardUnity 'Before' 'TownPilotReview.CaptureCourtyardsBefore'
        Assert-CourtyardCaptureEvidence 'Before'
        Assert-CourtyardSnapshot $courtyardProject $courtyardBaseline.main
        Assert-CourtyardSnapshot $courtyardClone $courtyardBaseline.isolated
        Assert-CourtyardCaptureInputs 'Before'
        Write-CourtyardJson (Join-Path $courtyardPipeline 'Before.json') @{
            exitCode=0;baselineHash=(Get-CourtyardHash $courtyardBaselinePath);evidence=(Get-CourtyardCaptureHashes 'Before')
        }
        Write-Output 'Stage 4 Before captured with protected sources and immutable evidence.'
        exit 0
    }

    Assert-CourtyardBeforeEvidence
    Assert-CourtyardCaptureInputs 'Before' $true
    if($Mode -eq 'Generate') {
        foreach($phase in @('Generate','After','Play')) {Set-CourtyardReceiptPending $phase}
        # Every attempt starts from the same original prefab, not from the partially edited output of a failed attempt.
        Copy-CourtyardAtomically $courtyardOriginalPath (Join-Path $courtyardClone $courtyardPrefab) $true
        foreach($name in @('result.txt','templates.txt','assets.txt','protection.txt','gameplay.txt','runtime.txt')) {[IO.File]::WriteAllText((Join-Path $courtyardQa $name),'PENDING: new generation has not passed.')}
        Invoke-CourtyardUnity 'Generate' 'TownCourtyardBatch.Generate'
        Assert-CourtyardGenerationEvidence
        Assert-CourtyardSnapshot $courtyardProject $courtyardBaseline.main
        Assert-CourtyardSnapshot $courtyardClone $courtyardBaseline.isolated $true
        Assert-CourtyardBeforeEvidence
        Write-CourtyardJson (Join-Path $courtyardPipeline 'Generate.json') @{
            exitCode=0;baselineHash=(Get-CourtyardHash $courtyardBaselinePath);assets=(Get-CourtyardGeneratedAssets)
        }
        Write-Output 'Stage 4 generated: only the isolated town prefab/new courtyard assets may change; all other original files unchanged.'
        exit 0
    }

    $courtyardGenerated=Assert-CourtyardReceipt 'Generate'
    Assert-CourtyardHashes $courtyardClone $courtyardGenerated.assets $true
    if($Mode -eq 'After') {
        Set-CourtyardReceiptPending 'After'
        Set-CourtyardReceiptPending 'Play'
        [IO.Directory]::CreateDirectory((Join-Path $courtyardQa 'After')) | Out-Null
        [IO.File]::WriteAllText((Join-Path $courtyardQa 'After/complete.txt'),'PENDING: the current After capture has not passed.')
        Invoke-CourtyardUnity 'After' 'TownPilotReview.CaptureCourtyardsAfter'
        Assert-CourtyardCaptureEvidence 'After'
        if((Get-CourtyardHash (Join-Path $courtyardQa 'Before/camera-manifest.json')) -cne
            (Get-CourtyardHash (Join-Path $courtyardQa 'After/camera-manifest.json'))) {
            throw 'Before/After camera definitions differ; the pictures are not comparable.'
        }
        Assert-CourtyardCaptureInputs 'After'
        Assert-CourtyardHashes $courtyardClone $courtyardGenerated.assets $true
        Assert-CourtyardSnapshot $courtyardProject $courtyardBaseline.main
        Assert-CourtyardSnapshot $courtyardClone $courtyardBaseline.isolated $true
        Assert-CourtyardBeforeEvidence
        $evidence=Get-CourtyardCaptureHashes 'After'
        Write-CourtyardJson (Join-Path $courtyardPipeline 'After.json') @{
            exitCode=0;baselineHash=(Get-CourtyardHash $courtyardBaselinePath);evidence=$evidence
        }
        Write-Output 'Stage 4 After captured against the same cameras; generated assets and protected files unchanged by capture.'
        exit 0
    }

    $courtyardAfter=Assert-CourtyardReceipt 'After'
    Assert-CourtyardCaptureEvidence 'After'
    Assert-CourtyardHashes $courtyardClone $courtyardAfter.evidence
    if((Get-CourtyardCaptureHashes 'After').Count -ne $courtyardAfter.evidence.Count) {throw 'The validated After evidence file set changed.'}
    Assert-CourtyardCaptureInputs 'After'
    if($Mode -eq 'Play') {
        Set-CourtyardReceiptPending 'Play'
        foreach($name in @('gameplay.txt','runtime.txt')) {[IO.File]::WriteAllText((Join-Path $courtyardQa $name),'PENDING: Play Mode has not passed.')}
        foreach($relative in @('Library/TownWorldQA/integration.txt','Library/CityUpdateQA/integration.txt')) {
            $path=Resolve-CourtyardRelative $courtyardClone $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
            [IO.File]::WriteAllText($path,'PENDING: the current Stage 4 Play regression has not passed.')
        }
        Invoke-CourtyardPlay $courtyardBaseline
        Assert-CourtyardGameplayEvidence
        Assert-CourtyardGenerationEvidence
        Assert-CourtyardSnapshot $courtyardProject $courtyardBaseline.main
        Assert-CourtyardSnapshot $courtyardClone $courtyardBaseline.isolated $true
        Assert-CourtyardHashes $courtyardClone $courtyardGenerated.assets $true
        Assert-CourtyardHashes $courtyardClone $courtyardAfter.evidence
        Assert-CourtyardBeforeEvidence
        $outputs=Get-CourtyardGeneratedAssets
        foreach($file in Get-ChildItem -LiteralPath $courtyardQa -File -Recurse) {
            $localRelative=[IO.Path]::GetRelativePath($courtyardQa,$file.FullName).Replace('\','/')
            if($localRelative -ceq 'OriginalTown.prefab' -or $localRelative -ceq 'OriginalTown.prefab.meta') {continue}
            $outputs['Library/TownCourtyardQA/'+$localRelative]=Get-CourtyardHash $file.FullName
        }
        Write-CourtyardJson (Join-Path $courtyardPipeline 'outputs.json') $outputs
        Write-CourtyardJson (Join-Path $courtyardPipeline 'Play.json') @{
            exitCode=0;baselineHash=(Get-CourtyardHash $courtyardBaselinePath);outputHash=(Get-CourtyardHash (Join-Path $courtyardPipeline 'outputs.json'))
        }
        Write-Output 'Stage 4 Play Mode and output hashes passed. Main project still unchanged; Apply is now eligible.'
        exit 0
    }

    if($Mode -eq 'Apply') {
        $play=Assert-CourtyardReceipt 'Play'
        $outputsPath=Join-Path $courtyardPipeline 'outputs.json'
        if($play.outputHash -cne (Get-CourtyardHash $outputsPath)) {throw 'The validated output manifest changed after Play Mode.'}
        Assert-CourtyardGenerationEvidence
        Assert-CourtyardGameplayEvidence
        $outputs=Read-CourtyardJson $outputsPath
        Assert-CourtyardHashes $courtyardClone $outputs
        $copies=[Collections.Generic.List[object]]::new()
        foreach($relative in $outputs.Keys) {
            $source=Resolve-CourtyardRelative $courtyardClone $relative
            if(Test-CourtyardOutput $relative) {$targetRelative=$relative}
            elseif($relative.StartsWith('Library/TownCourtyardQA/',[StringComparison]::Ordinal)) {
                $targetRelative='Docs/TownCourtyardReview/'+$relative.Substring('Library/TownCourtyardQA/'.Length)
            }
            else {throw "Output is not in the Stage 4 transfer allowlist: $relative"}
            $target=Resolve-CourtyardRelative $courtyardProject $targetRelative
            if(Test-Path -LiteralPath $target) {
                $targetHash=Get-CourtyardHash $target
                if($targetRelative -ceq $courtyardPrefab) {
                    if($targetHash -cne $courtyardBaseline.main[$courtyardPrefab]) {throw 'Concurrent main-town edit: the prepared prefab is no longer current.'}
                }
                elseif($targetHash -cne $outputs[$relative]) {throw "Refusing to overwrite an independently edited new asset/review: $targetRelative"}
            }
            $copies.Add(@{source=$source;relative=$targetRelative;hash=$outputs[$relative]})
        }

        $backup=Join-Path $courtyardPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8))
        [IO.Directory]::CreateDirectory($backup) | Out-Null
        foreach($relative in @($courtyardPrefab,($courtyardPrefab+'.meta'))) {
            $source=Resolve-CourtyardRelative $courtyardProject $relative
            $saved=Resolve-CourtyardRelative $backup $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
            Copy-Item -LiteralPath $source -Destination $saved
            if((Get-CourtyardHash $saved) -cne (Get-CourtyardHash $source)) {throw "Recoverable backup mismatch: $relative"}
        }
        Write-CourtyardJson (Join-Path $backup 'receipt.json') @{
            baselineHash=(Get-CourtyardHash $courtyardBaselinePath);outputHash=$play.outputHash;originalTownHash=$courtyardBaseline.originalPrefabHash
        }
        $changedTown=$false
        try {
            # GUID metadata first, then native assets/evidence; the prefab referencing them is replaced last.
            $ordered=$copies | Sort-Object @{Expression={if($_.relative -ceq $courtyardPrefab){3}elseif($_.relative.EndsWith('.meta',[StringComparison]::Ordinal)){0}elseif($_.relative.StartsWith('Assets/',[StringComparison]::Ordinal)){1}else{2}}}, @{Expression={$_.relative}}
            foreach($copy in $ordered) {
                $target=Resolve-CourtyardRelative $courtyardProject $copy.relative
                if(Test-Path -LiteralPath $target) {
                    if((Get-CourtyardHash $target) -ceq $copy.hash) {continue}
                    if($copy.relative -cne $courtyardPrefab -or (Get-CourtyardHash $target) -cne $courtyardBaseline.originalPrefabHash) {
                        throw "Concurrent edit during transfer: $($copy.relative)"
                    }
                }
                Copy-CourtyardAtomically $copy.source $target ($copy.relative -ceq $courtyardPrefab)
                if($copy.relative -ceq $courtyardPrefab) {$changedTown=$true}
                if((Get-CourtyardHash $target) -cne $copy.hash) {throw "Transferred file mismatch: $($copy.relative)"}
            }
            Assert-CourtyardSnapshot $courtyardProject $courtyardBaseline.main $true
            foreach($copy in $copies) {
                if((Get-CourtyardHash (Resolve-CourtyardRelative $courtyardProject $copy.relative)) -cne $copy.hash) {throw "Transferred output changed: $($copy.relative)"}
            }
        }
        catch {
            $failure=$_
            $mainTown=Join-Path $courtyardProject $courtyardPrefab
            if($changedTown -and (Get-CourtyardHash $mainTown) -ceq $courtyardGenerated.assets[$courtyardPrefab]) {
                Copy-CourtyardAtomically (Join-Path $backup $courtyardPrefab) $mainTown $true
                Write-Warning 'Transfer failed; the original town prefab was restored. Successfully copied new, unreferenced assets/evidence remain recoverable.'
            }
            throw "Stage 4 transfer failed. Original prefab backup: $backup. $($failure.Exception.Message)"
        }
        [IO.File]::WriteAllText((Join-Path $courtyardPipeline 'applied.txt'),"PASS: $($copies.Count) verified outputs delivered. Only the town prefab was replaced; all other original assets/settings/packages and Stage 1/2/3 evidence unchanged.`nRecoverable original backup: $backup`n")
        Write-Output "Stage 4 applied ($($copies.Count) files). Recoverable original prefab: $backup"
    }
}
finally {
    $courtyardLock.Dispose()
}
