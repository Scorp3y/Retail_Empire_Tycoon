param([ValidateSet('Prepare','Audit','Before','Generate','After','Play','Apply')][string]$Mode='Prepare')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

$seamProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$seamClone=Join-Path $seamProject 'Library/CityUpdateQA/IsolatedProject'
$seamPipeline=Join-Path $seamProject 'Library/TownSeamPipeline'
$seamQa=Join-Path $seamClone 'Library/TownSeamQA'
$seamAssetRoot='Assets/Art/TownSeamRepairs'
$seamReviewRoot='Docs/TownSeamReview'
$seamPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$seamSandbox='Assets/Project/Editor/ShopGameplaySandbox.unity'
$seamUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$seamBaselinePath=Join-Path $seamPipeline 'baseline.json'
$seamOriginalPath=Join-Path $seamQa 'OriginalTown.prefab'

if(-not (Test-Path -LiteralPath (Join-Path $seamClone 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The existing isolated Unity project must be prepared first.'
}
if(-not (Test-Path -LiteralPath $seamUnity)) {throw 'The configured Unity 2022.3.62f3 editor is missing.'}
[IO.Directory]::CreateDirectory($seamPipeline) | Out-Null

# An exclusive file handle prevents two pipeline invocations from racing through the busy check.
$seamLock=[IO.File]::Open((Join-Path $seamPipeline 'pipeline.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)

function Assert-SeamUnityIdle {
    $running=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($seamClone,[StringComparison]::OrdinalIgnoreCase) }
    if($running) {throw 'The isolated Unity editor is busy. Wait for that run to exit before continuing.'}
}

function Get-SeamHash([string]$path) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "Required file is missing: $path"}
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

function Write-SeamJson([string]$path,$value) {
    [IO.File]::WriteAllText($path,($value | ConvertTo-Json -Depth 12))
}

function Read-SeamJson([string]$path) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "Run the prerequisite phase first; missing receipt: $path"}
    return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable)
}

function Resolve-SeamRelative([string]$project,[string]$relative) {
    $path=[IO.Path]::GetFullPath((Join-Path $project $relative))
    $canonical=[IO.Path]::GetRelativePath($project,$path).Replace('\','/')
    if($canonical -cne $relative -or $canonical.StartsWith('../',[StringComparison]::Ordinal) -or [IO.Path]::IsPathRooted($relative)) {
        throw "Non-canonical project path: $relative"
    }
    return $path
}

function Test-SeamOutput([string]$relative) {
    return $relative -ceq $seamPrefab -or $relative -ceq ($seamAssetRoot+'.meta') -or
        $relative.StartsWith($seamAssetRoot+'/',[StringComparison]::Ordinal)
}

function Test-SeamAuthoring([string]$relative) {
    return $relative -cmatch '^Assets/Project/Editor/TownSeam[^/]*\.cs(\.meta)?$' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotReview.cs' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotReview.cs.meta' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotScopePreservation.cs' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotScopePreservation.cs.meta'
}

function Test-SeamNewMetadata([string]$relative) {
    return $relative -cmatch '^Assets/Project/Editor/TownSeam[^/]*\.cs\.meta$'
}

function Get-SeamSnapshot([string]$project) {
    $snapshot=@{}
    foreach($scope in @('Assets','ProjectSettings','Packages','Docs')) {
        $scopePath=Join-Path $project $scope
        if(-not (Test-Path -LiteralPath $scopePath)) {continue}
        foreach($file in Get-ChildItem -LiteralPath $scopePath -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath($project,$file.FullName).Replace('\','/')
            if($relative -ceq ($seamAssetRoot+'.meta') -or $relative.StartsWith($seamAssetRoot+'/',[StringComparison]::Ordinal) -or
                $relative -ceq ($seamReviewRoot+'.md') -or $relative.StartsWith($seamReviewRoot+'/',[StringComparison]::Ordinal)) {continue}
            $snapshot[$relative]=Get-SeamHash $file.FullName
        }
    }
    return $snapshot
}

function Assert-SeamSnapshot([string]$project,$baseline,[bool]$allowTownChange=$false,[bool]$allowAuthoringChanges=$false) {
    $current=Get-SeamSnapshot $project
    foreach($relative in $baseline.Keys) {
        if($allowTownChange -and $relative -ceq $seamPrefab) {continue}
        if($allowAuthoringChanges -and (Test-SeamAuthoring $relative)) {
            if(-not $current.ContainsKey($relative)) {throw "Prepare cannot remove a previously prepared authoring source: $relative"}
            continue
        }
        if(-not $current.ContainsKey($relative) -or $current[$relative] -cne $baseline[$relative]) {
            throw "Protected file changed: $relative ($project)"
        }
    }
    foreach($relative in $current.Keys) {
        if(-not $baseline.ContainsKey($relative) -and -not (Test-SeamNewMetadata $relative) -and
            -not ($allowAuthoringChanges -and (Test-SeamAuthoring $relative))) {
            throw "Unexpected file outside the Stage 6 allowlist: $relative ($project)"
        }
    }
}

function Assert-SeamReceipt([string]$phase) {
    $receipt=Read-SeamJson (Join-Path $seamPipeline ($phase+'.json'))
    if($receipt.exitCode -ne 0 -or $receipt.baselineHash -cne (Get-SeamHash $seamBaselinePath)) {
        throw "Stage 6 $phase has not validated the current source baseline."
    }
    return $receipt
}

function Set-SeamReceiptPending([string]$phase) {
    Write-SeamJson (Join-Path $seamPipeline ($phase+'.json')) @{exitCode=-1}
}

function Assert-SeamPass([string]$path,[string[]]$prefixes=@('PASS')) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "QA evidence is missing: $path"}
    $value=Get-Content -LiteralPath $path -Raw
    if($value -cmatch '(?m)^[^\r\n]*\bFAILED\b') {throw "QA contains a failure: $path"}
    foreach($prefix in $prefixes) {
        if($value.StartsWith($prefix,[StringComparison]::Ordinal)) {return}
    }
    throw "QA did not pass: $path"
}

function Assert-SeamGenerationEvidence {
    Assert-SeamPass (Join-Path $seamQa 'result.txt') @('TOWN SEAM REPAIRS PASSED')
    Assert-SeamPass (Join-Path $seamQa 'templates.txt')
    Assert-SeamPass (Join-Path $seamQa 'assets.txt')
    Assert-SeamPass (Join-Path $seamQa 'protection.txt')
}

function Assert-SeamGameplayEvidence {
    Assert-SeamPass (Join-Path $seamQa 'gameplay.txt')
    Assert-SeamPass (Join-Path $seamQa 'runtime.txt')
    Assert-SeamPass (Join-Path $seamClone 'Library/TownWorldQA/integration.txt') @('TOWN WORLD PASSED')
    $cityEvidence=Get-Content -LiteralPath (Join-Path $seamClone 'Library/CityUpdateQA/integration.txt') -Raw
    # Individual scenarios append PASS before the full trip finishes. Only the exact final terminal
    # plus absence of a FAILED line proves completion; a partial PASS log must never authorise Apply.
    if(-not $cityEvidence.TrimEnd().EndsWith('CITY DELIVERY INTEGRATION PASSED',[StringComparison]::Ordinal) -or
        $cityEvidence -cmatch '(?m)^[^\r\n]*\bFAILED\b') {
        throw 'The original City delivery regression did not finish successfully, or its log contains a FAILED line.'
    }
}

function Assert-SeamCaptureEvidence([string]$phase) {
    Assert-SeamPass (Join-Path $seamQa ($phase+'/complete.txt'))
    $directory=Join-Path $seamQa $phase
    $manifest=Read-SeamJson (Join-Path $directory 'camera-manifest.json')
    $sources=Read-SeamJson (Join-Path $directory 'source-hashes.json')
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

function Assert-SeamOriginal($baseline) {
    if((Get-SeamHash $seamOriginalPath) -cne $baseline.originalPrefabHash -or
        (Get-SeamHash ($seamOriginalPath+'.meta')) -cne $baseline.originalMetadataHash) {
        throw 'The immutable original town backup changed. It must not be replaced with a generated variant.'
    }
    if((Get-SeamHash (Join-Path $seamClone ($seamPrefab+'.meta'))) -cne $baseline.originalMetadataHash) {
        throw 'The original town prefab GUID/import metadata changed in the isolated project.'
    }
}

function Copy-SeamAtomically([string]$source,[string]$target,[bool]$replace=$false) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    $incoming=Join-Path $seamPipeline ([Guid]::NewGuid().ToString()+'.incoming')
    Copy-Item -LiteralPath $source -Destination $incoming
    try {
        if((Get-SeamHash $incoming) -cne (Get-SeamHash $source)) {throw "Staging copy mismatch: $source"}
        # Both workspaces and staging are on this project's volume; rename a complete file, never truncate an open asset.
        [IO.File]::Move($incoming,$target,$replace)
    }
    finally {
        if(Test-Path -LiteralPath $incoming) {[IO.File]::Delete($incoming)}
    }
}

function Get-SeamGeneratedAssets {
    $outputs=@{}
    $outputs[$seamPrefab]=Get-SeamHash (Join-Path $seamClone $seamPrefab)
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $seamClone $seamAssetRoot) -File -Recurse) {
        $relative=[IO.Path]::GetRelativePath($seamClone,$file.FullName).Replace('\','/')
        $outputs[$relative]=Get-SeamHash $file.FullName
    }
    $outputs[$seamAssetRoot+'.meta']=Get-SeamHash (Join-Path $seamClone ($seamAssetRoot+'.meta'))
    return $outputs
}

function Assert-SeamHashes([string]$project,$expected,[bool]$requireExactAssetSet=$false) {
    foreach($relative in $expected.Keys) {
        $path=Resolve-SeamRelative $project $relative
        if((Get-SeamHash $path) -cne $expected[$relative]) {throw "Validated output changed: $relative"}
    }
    if($requireExactAssetSet) {
        $current=Get-SeamGeneratedAssets
        if($current.Count -ne $expected.Count) {throw 'The set of generated town assets changed after Generate.'}
        foreach($relative in $current.Keys) {
            if(-not $expected.ContainsKey($relative)) {throw "Unvalidated generated asset: $relative"}
        }
    }
}

function Assert-SeamCaptureInputs([string]$phase,[bool]$allowGeneratedTown=$false) {
    $inputs=Read-SeamJson (Join-Path $seamQa ($phase+'/source-hashes.json'))
    foreach($inputFile in $inputs.files) {
        $relative=$inputFile.path
        # Packages are mounted virtually by Unity. They remain protected by the complete package-file baseline.
        if(-not $relative.StartsWith('Assets/',[StringComparison]::Ordinal)) {continue}
        if($allowGeneratedTown -and (Test-SeamOutput $relative)) {continue}
        $path=Resolve-SeamRelative $seamClone $relative
        if((Get-SeamHash $path) -cne $inputFile.sha256) {throw "Stale $phase capture input: $relative"}
    }
}

function Get-SeamCaptureHashes([string]$phase) {
    $outputs=@{}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $seamQa $phase) -File -Recurse) {
        $relative=[IO.Path]::GetRelativePath($seamClone,$file.FullName).Replace('\','/')
        $outputs[$relative]=Get-SeamHash $file.FullName
    }
    return $outputs
}

function Assert-SeamBeforeEvidence {
    $receipt=Assert-SeamReceipt 'Before'
    Assert-SeamCaptureEvidence 'Before'
    Assert-SeamHashes $seamClone $receipt.evidence
    $current=Get-SeamCaptureHashes 'Before'
    if($current.Count -ne $receipt.evidence.Count) {throw 'The immutable Before evidence file set changed.'}
}

function Invoke-SeamUnity([string]$phase,[string]$entry) {
    Assert-SeamUnityIdle
    $arguments='-batchmode -projectPath "'+$seamClone+'" -executeMethod '+$entry+' -logFile "'+(Join-Path $seamPipeline ($phase+'.log'))+'"'
    $process=Start-Process -FilePath $seamUnity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if($process.ExitCode -ne 0) {throw "Stage 6 Unity $phase failed with exit code $($process.ExitCode). See its pipeline log."}
}

function Save-SeamPlayFixture($baseline) {
    $fixture=@{
        directory=(Join-Path $seamPipeline ('PlayFixture_'+[Guid]::NewGuid().ToString('N')))
        baselineHash=(Get-SeamHash $seamBaselinePath)
        files=@{}
    }
    foreach($relative in @($seamSandbox,($seamSandbox+'.meta'))) {
        if(-not $baseline.main.ContainsKey($relative) -or -not $baseline.isolated.ContainsKey($relative) -or
            $baseline.main[$relative] -cne $baseline.isolated[$relative]) {
            throw "Sandbox fixture must have identical prepared main/isolated bytes: $relative"
        }
        $main=Resolve-SeamRelative $seamProject $relative
        $source=Resolve-SeamRelative $seamClone $relative
        $expected=$baseline.isolated[$relative]
        if((Get-SeamHash $main) -cne $baseline.main[$relative] -or (Get-SeamHash $source) -cne $expected) {
            throw "The protected sandbox changed before Play; do not adopt it as a temporary fixture: $relative"
        }
        $fixture.files[$relative]=$expected
    }
    # The existing sandbox helper saves its fixed-path scene. Back up only these two known fixture files,
    # outside Assets, rather than weakening the scene SHA guard or rewriting shared test infrastructure.
    [IO.Directory]::CreateDirectory($fixture.directory) | Out-Null
    foreach($relative in $fixture.files.Keys) {
        $saved=Resolve-SeamRelative $fixture.directory $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
        Copy-Item -LiteralPath (Resolve-SeamRelative $seamClone $relative) -Destination $saved
        if((Get-SeamHash $saved) -cne $fixture.files[$relative]) {throw "Sandbox fixture backup mismatch: $relative"}
    }
    Write-SeamJson (Join-Path $fixture.directory 'receipt.json') $fixture
    Write-SeamJson (Join-Path $seamPipeline 'active-play-fixture.json') $fixture
    return $fixture
}

function Restore-SeamPlayFixture($fixture) {
    # Never restore an asset while Unity can still be saving/importing it, even when its run failed.
    Assert-SeamUnityIdle
    $directory=[IO.Path]::GetFullPath($fixture.directory)
    if([IO.Path]::GetDirectoryName($directory) -cne [IO.Path]::GetFullPath($seamPipeline) -or
        [IO.Path]::GetFileName($directory) -cnotmatch '^PlayFixture_[0-9a-f]{32}$' -or $fixture.files.Count -ne 2) {
        throw 'Invalid scoped sandbox fixture backup.'
    }
    if($fixture.baselineHash -cne (Get-SeamHash $seamBaselinePath)) {
        throw 'The prepared baseline changed while the scoped sandbox fixture was active.'
    }
    $baseline=Read-SeamJson $seamBaselinePath
    foreach($relative in @($seamSandbox,($seamSandbox+'.meta'))) {
        if(-not $fixture.files.ContainsKey($relative)) {throw "Sandbox fixture backup omits a protected file: $relative"}
        if(-not $baseline.main.ContainsKey($relative) -or -not $baseline.isolated.ContainsKey($relative) -or
            $fixture.files[$relative] -cne $baseline.main[$relative] -or $fixture.files[$relative] -cne $baseline.isolated[$relative] -or
            (Get-SeamHash (Resolve-SeamRelative $seamProject $relative)) -cne $baseline.main[$relative]) {
            throw "Sandbox fixture does not match the protected original main/isolated bytes: $relative"
        }
        $saved=Resolve-SeamRelative $directory $relative
        if((Get-SeamHash $saved) -cne $fixture.files[$relative]) {throw "Sandbox fixture original bytes changed: $relative"}
    }
    foreach($relative in @($seamSandbox,($seamSandbox+'.meta'))) {
        $target=Resolve-SeamRelative $seamClone $relative
        if(-not (Test-Path -LiteralPath $target) -or (Get-SeamHash $target) -cne $fixture.files[$relative]) {
            Copy-SeamAtomically (Resolve-SeamRelative $directory $relative) $target $true
        }
        if((Get-SeamHash $target) -cne $fixture.files[$relative]) {throw "Sandbox fixture restoration mismatch: $relative"}
    }
    [IO.File]::WriteAllText((Join-Path $fixture.directory 'restored.txt'),
        "PASS: exact isolated sandbox scene/meta bytes restored after Unity exit. Both files remain in the full protected-file SHA baseline; no main scene was written.`n")
}

function Restore-SeamPendingPlayFixture {
    $active=Join-Path $seamPipeline 'active-play-fixture.json'
    if(-not (Test-Path -LiteralPath $active)) {return}
    $fixture=Read-SeamJson $active
    $directory=[IO.Path]::GetFullPath($fixture.directory)
    if([IO.Path]::GetDirectoryName($directory) -cne [IO.Path]::GetFullPath($seamPipeline) -or
        [IO.Path]::GetFileName($directory) -cnotmatch '^PlayFixture_[0-9a-f]{32}$') {
        throw 'Invalid pending sandbox fixture path.'
    }
    if(Test-Path -LiteralPath (Join-Path $directory 'restored.txt')) {return}
    # A PowerShell crash can bypass finally. Resume only this recorded fixture, whose backup/main bytes
    # must still match the same baseline; a changed source/scene can never be silently adopted.
    Restore-SeamPlayFixture $fixture
    Write-Output 'Recovered the pending isolated sandbox fixture after an interrupted Play invocation.'
}

function Invoke-SeamPlay($baseline) {
    $fixture=Save-SeamPlayFixture $baseline
    $playFailure=$null
    $restorationFailure=$null
    try {
        Invoke-SeamUnity 'Play' 'TownSeamBatch.ValidatePlayMode'
    }
    catch {$playFailure=$_}
    finally {
        try {Restore-SeamPlayFixture $fixture}
        catch {$restorationFailure=$_}
    }
    if($playFailure -and $restorationFailure) {
        throw "Play failed: $($playFailure.Exception.Message). Sandbox restoration also failed: $($restorationFailure.Exception.Message). Original fixture: $($fixture.directory)"
    }
    if($restorationFailure) {throw $restorationFailure}
    if($playFailure) {throw $playFailure}
}

try {
    Assert-SeamUnityIdle
    Restore-SeamPendingPlayFixture
    if($Mode -eq 'Prepare') {
        $refresh=Test-Path -LiteralPath $seamBaselinePath
        if($refresh) {
            $previous=Read-SeamJson $seamBaselinePath
            Assert-SeamSnapshot $seamProject $previous.main $false $true
            Assert-SeamSnapshot $seamClone $previous.isolated $true $true
            Assert-SeamOriginal $previous
            $hasBefore=Test-Path -LiteralPath (Join-Path $seamQa 'Before/complete.txt')
            if($hasBefore) {
                Assert-SeamBeforeEvidence
                Assert-SeamCaptureInputs 'Before' $true
            }
            # A retry can refresh only the Stage 6 authoring code; all saved scenes/assets/Before images remain immutable.
            foreach($file in Get-ChildItem -LiteralPath (Join-Path $seamProject 'Assets/Project/Editor') -File -Recurse) {
                $relative=[IO.Path]::GetRelativePath($seamProject,$file.FullName).Replace('\','/')
                if(Test-SeamAuthoring $relative) {
                    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $seamClone $relative) -Force
                }
            }
            Copy-SeamAtomically $seamOriginalPath (Join-Path $seamClone $seamPrefab) $true
            $baseline=@{
                main=(Get-SeamSnapshot $seamProject)
                isolated=(Get-SeamSnapshot $seamClone)
                originalPrefabHash=$previous.originalPrefabHash
                originalMetadataHash=$previous.originalMetadataHash
            }
            Write-SeamJson $seamBaselinePath $baseline
            if($hasBefore) {
                Write-SeamJson (Join-Path $seamPipeline 'Before.json') @{
                    exitCode=0;baselineHash=(Get-SeamHash $seamBaselinePath);evidence=(Get-SeamCaptureHashes 'Before')
                }
            }
            foreach($phase in @('Generate','After','Play')) {Set-SeamReceiptPending $phase}
            Write-Output 'Stage 6 authoring refreshed. Immutable Before/original town preserved; Generate, After and Play must be repeated.'
            exit 0
        }

        if(Test-Path -LiteralPath (Join-Path $seamQa 'Before/complete.txt')) {
            throw 'Before evidence exists without its pipeline baseline. Preserve that evidence; do not prepare over it.'
        }
        if((Test-Path -LiteralPath (Join-Path $seamProject $seamAssetRoot)) -or
            (Test-Path -LiteralPath (Join-Path $seamProject ($seamAssetRoot+'.meta')))) {
            throw 'Stage 6 generated output already exists in the main project. A new delivery needs a separately approved baseline.'
        }
        foreach($scope in @('Assets','ProjectSettings','Packages','Docs')) {
            $scopePath=Join-Path $seamProject $scope
            if(-not (Test-Path -LiteralPath $scopePath)) {continue}
            foreach($file in Get-ChildItem -LiteralPath $scopePath -File -Recurse) {
                $relative=[IO.Path]::GetRelativePath($seamProject,$file.FullName).Replace('\','/')
                if($relative -ceq ($seamReviewRoot+'.md') -or $relative.StartsWith($seamReviewRoot+'/',[StringComparison]::Ordinal)) {continue}
                $target=Resolve-SeamRelative $seamClone $relative
                if((Test-Path -LiteralPath $target -PathType Leaf) -and (Get-SeamHash $target) -ceq (Get-SeamHash $file.FullName)) {continue}
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            }
        }
        [IO.Directory]::CreateDirectory($seamQa) | Out-Null
        foreach($backup in @(@{source=(Join-Path $seamProject $seamPrefab);target=$seamOriginalPath},
            @{source=(Join-Path $seamProject ($seamPrefab+'.meta'));target=($seamOriginalPath+'.meta')})) {
            if(Test-Path -LiteralPath $backup.target) {
                if((Get-SeamHash $backup.target) -cne (Get-SeamHash $backup.source)) {throw 'An existing original-town backup differs; refusing to replace it.'}
            }
            else {Copy-Item -LiteralPath $backup.source -Destination $backup.target}
        }
        $baseline=@{
            main=(Get-SeamSnapshot $seamProject)
            isolated=(Get-SeamSnapshot $seamClone)
            originalPrefabHash=(Get-SeamHash $seamOriginalPath)
            originalMetadataHash=(Get-SeamHash ($seamOriginalPath+'.meta'))
        }
        Write-SeamJson $seamBaselinePath $baseline
        Write-Output 'Stage 6 prepared: all original assets/settings/packages and Stage 1/2/3/4/5 evidence protected; original town saved outside Assets.'
        exit 0
    }

    $seamBaseline=Read-SeamJson $seamBaselinePath
    Assert-SeamSnapshot $seamProject $seamBaseline.main
    Assert-SeamSnapshot $seamClone $seamBaseline.isolated ($Mode -ne 'Before')
    Assert-SeamOriginal $seamBaseline

    if($Mode -eq 'Audit') {
        Invoke-SeamUnity 'Audit' 'TownSeamBatch.Audit'
        Assert-SeamPass (Join-Path $seamQa 'audit.txt')
        Assert-SeamSnapshot $seamProject $seamBaseline.main
        Assert-SeamSnapshot $seamClone $seamBaseline.isolated
        Write-Output 'Stage 6 read-only native audit passed.'
        exit 0
    }

    if($Mode -eq 'Before') {
        if(Test-Path -LiteralPath (Join-Path $seamQa 'Before/complete.txt')) {throw 'Before is immutable. Continue with Generate, or Prepare to refresh only Stage 6 authoring code.'}
        Set-SeamReceiptPending 'Before'
        Invoke-SeamUnity 'Before' 'TownPilotReview.CaptureSeamBefore'
        Assert-SeamCaptureEvidence 'Before'
        Assert-SeamSnapshot $seamProject $seamBaseline.main
        Assert-SeamSnapshot $seamClone $seamBaseline.isolated
        Assert-SeamCaptureInputs 'Before'
        Write-SeamJson (Join-Path $seamPipeline 'Before.json') @{
            exitCode=0;baselineHash=(Get-SeamHash $seamBaselinePath);evidence=(Get-SeamCaptureHashes 'Before')
        }
        Write-Output 'Stage 6 Before captured with protected sources and immutable evidence.'
        exit 0
    }

    Assert-SeamBeforeEvidence
    Assert-SeamCaptureInputs 'Before' $true
    if($Mode -eq 'Generate') {
        foreach($phase in @('Generate','After','Play')) {Set-SeamReceiptPending $phase}
        # Every attempt starts from the same original prefab, not from the partially edited output of a failed attempt.
        Copy-SeamAtomically $seamOriginalPath (Join-Path $seamClone $seamPrefab) $true
        foreach($name in @('result.txt','templates.txt','assets.txt','protection.txt','gameplay.txt','runtime.txt')) {[IO.File]::WriteAllText((Join-Path $seamQa $name),'PENDING: new generation has not passed.')}
        Invoke-SeamUnity 'Generate' 'TownSeamBatch.Generate'
        Assert-SeamGenerationEvidence
        Assert-SeamSnapshot $seamProject $seamBaseline.main
        Assert-SeamSnapshot $seamClone $seamBaseline.isolated $true
        Assert-SeamBeforeEvidence
        Write-SeamJson (Join-Path $seamPipeline 'Generate.json') @{
            exitCode=0;baselineHash=(Get-SeamHash $seamBaselinePath);assets=(Get-SeamGeneratedAssets)
        }
        Write-Output 'Stage 6 generated: only the isolated town prefab/new seam assets may change; all other original files unchanged.'
        exit 0
    }

    $seamGenerated=Assert-SeamReceipt 'Generate'
    Assert-SeamHashes $seamClone $seamGenerated.assets $true
    if($Mode -eq 'After') {
        Set-SeamReceiptPending 'After'
        Set-SeamReceiptPending 'Play'
        [IO.Directory]::CreateDirectory((Join-Path $seamQa 'After')) | Out-Null
        [IO.File]::WriteAllText((Join-Path $seamQa 'After/complete.txt'),'PENDING: the current After capture has not passed.')
        Invoke-SeamUnity 'After' 'TownPilotReview.CaptureSeamAfter'
        Assert-SeamCaptureEvidence 'After'
        if((Get-SeamHash (Join-Path $seamQa 'Before/camera-manifest.json')) -cne
            (Get-SeamHash (Join-Path $seamQa 'After/camera-manifest.json'))) {
            throw 'Before/After camera definitions differ; the pictures are not comparable.'
        }
        Assert-SeamCaptureInputs 'After'
        Assert-SeamHashes $seamClone $seamGenerated.assets $true
        Assert-SeamSnapshot $seamProject $seamBaseline.main
        Assert-SeamSnapshot $seamClone $seamBaseline.isolated $true
        Assert-SeamBeforeEvidence
        $evidence=Get-SeamCaptureHashes 'After'
        Write-SeamJson (Join-Path $seamPipeline 'After.json') @{
            exitCode=0;baselineHash=(Get-SeamHash $seamBaselinePath);evidence=$evidence
        }
        Write-Output 'Stage 6 After captured against the same cameras; generated assets and protected files unchanged by capture.'
        exit 0
    }

    $seamAfter=Assert-SeamReceipt 'After'
    Assert-SeamCaptureEvidence 'After'
    Assert-SeamHashes $seamClone $seamAfter.evidence
    if((Get-SeamCaptureHashes 'After').Count -ne $seamAfter.evidence.Count) {throw 'The validated After evidence file set changed.'}
    Assert-SeamCaptureInputs 'After'
    if($Mode -eq 'Play') {
        Set-SeamReceiptPending 'Play'
        foreach($name in @('gameplay.txt','runtime.txt')) {[IO.File]::WriteAllText((Join-Path $seamQa $name),'PENDING: Play Mode has not passed.')}
        foreach($relative in @('Library/TownWorldQA/integration.txt','Library/CityUpdateQA/integration.txt')) {
            $path=Resolve-SeamRelative $seamClone $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
            [IO.File]::WriteAllText($path,'PENDING: the current Stage 6 Play regression has not passed.')
        }
        Invoke-SeamPlay $seamBaseline
        Assert-SeamGameplayEvidence
        Assert-SeamGenerationEvidence
        Assert-SeamSnapshot $seamProject $seamBaseline.main
        Assert-SeamSnapshot $seamClone $seamBaseline.isolated $true
        Assert-SeamHashes $seamClone $seamGenerated.assets $true
        Assert-SeamHashes $seamClone $seamAfter.evidence
        Assert-SeamBeforeEvidence
        $outputs=Get-SeamGeneratedAssets
        foreach($file in Get-ChildItem -LiteralPath $seamQa -File -Recurse) {
            $localRelative=[IO.Path]::GetRelativePath($seamQa,$file.FullName).Replace('\','/')
            if($localRelative -ceq 'OriginalTown.prefab' -or $localRelative -ceq 'OriginalTown.prefab.meta') {continue}
            $outputs['Library/TownSeamQA/'+$localRelative]=Get-SeamHash $file.FullName
        }
        Write-SeamJson (Join-Path $seamPipeline 'outputs.json') $outputs
        Write-SeamJson (Join-Path $seamPipeline 'Play.json') @{
            exitCode=0;baselineHash=(Get-SeamHash $seamBaselinePath);outputHash=(Get-SeamHash (Join-Path $seamPipeline 'outputs.json'))
        }
        Write-Output 'Stage 6 Play Mode and output hashes passed. Main project still unchanged; Apply is now eligible.'
        exit 0
    }

    if($Mode -eq 'Apply') {
        $play=Assert-SeamReceipt 'Play'
        $outputsPath=Join-Path $seamPipeline 'outputs.json'
        if($play.outputHash -cne (Get-SeamHash $outputsPath)) {throw 'The validated output manifest changed after Play Mode.'}
        Assert-SeamGenerationEvidence
        Assert-SeamGameplayEvidence
        $outputs=Read-SeamJson $outputsPath
        Assert-SeamHashes $seamClone $outputs
        $copies=[Collections.Generic.List[object]]::new()
        foreach($relative in $outputs.Keys) {
            $source=Resolve-SeamRelative $seamClone $relative
            if(Test-SeamOutput $relative) {$targetRelative=$relative}
            elseif($relative.StartsWith('Library/TownSeamQA/',[StringComparison]::Ordinal)) {
                $targetRelative='Docs/TownSeamReview/'+$relative.Substring('Library/TownSeamQA/'.Length)
            }
            else {throw "Output is not in the Stage 6 transfer allowlist: $relative"}
            $target=Resolve-SeamRelative $seamProject $targetRelative
            if(Test-Path -LiteralPath $target) {
                $targetHash=Get-SeamHash $target
                if($targetRelative -ceq $seamPrefab) {
                    if($targetHash -cne $seamBaseline.main[$seamPrefab]) {throw 'Concurrent main-town edit: the prepared prefab is no longer current.'}
                }
                elseif($targetHash -cne $outputs[$relative]) {throw "Refusing to overwrite an independently edited new asset/review: $targetRelative"}
            }
            $copies.Add(@{source=$source;relative=$targetRelative;hash=$outputs[$relative]})
        }

        $backup=Join-Path $seamPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8))
        [IO.Directory]::CreateDirectory($backup) | Out-Null
        foreach($relative in @($seamPrefab,($seamPrefab+'.meta'))) {
            $source=Resolve-SeamRelative $seamProject $relative
            $saved=Resolve-SeamRelative $backup $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
            Copy-Item -LiteralPath $source -Destination $saved
            if((Get-SeamHash $saved) -cne (Get-SeamHash $source)) {throw "Recoverable backup mismatch: $relative"}
        }
        Write-SeamJson (Join-Path $backup 'receipt.json') @{
            baselineHash=(Get-SeamHash $seamBaselinePath);outputHash=$play.outputHash;originalTownHash=$seamBaseline.originalPrefabHash
        }
        $changedTown=$false
        try {
            # GUID metadata first, then native assets/evidence; the prefab referencing them is replaced last.
            $ordered=$copies | Sort-Object @{Expression={if($_.relative -ceq $seamPrefab){3}elseif($_.relative.EndsWith('.meta',[StringComparison]::Ordinal)){0}elseif($_.relative.StartsWith('Assets/',[StringComparison]::Ordinal)){1}else{2}}}, @{Expression={$_.relative}}
            foreach($copy in $ordered) {
                $target=Resolve-SeamRelative $seamProject $copy.relative
                if(Test-Path -LiteralPath $target) {
                    if((Get-SeamHash $target) -ceq $copy.hash) {continue}
                    if($copy.relative -cne $seamPrefab -or (Get-SeamHash $target) -cne $seamBaseline.originalPrefabHash) {
                        throw "Concurrent edit during transfer: $($copy.relative)"
                    }
                }
                Copy-SeamAtomically $copy.source $target ($copy.relative -ceq $seamPrefab)
                if($copy.relative -ceq $seamPrefab) {$changedTown=$true}
                if((Get-SeamHash $target) -cne $copy.hash) {throw "Transferred file mismatch: $($copy.relative)"}
            }
            Assert-SeamSnapshot $seamProject $seamBaseline.main $true
            foreach($copy in $copies) {
                if((Get-SeamHash (Resolve-SeamRelative $seamProject $copy.relative)) -cne $copy.hash) {throw "Transferred output changed: $($copy.relative)"}
            }
        }
        catch {
            $failure=$_
            $mainTown=Join-Path $seamProject $seamPrefab
            if($changedTown -and (Get-SeamHash $mainTown) -ceq $seamGenerated.assets[$seamPrefab]) {
                Copy-SeamAtomically (Join-Path $backup $seamPrefab) $mainTown $true
                Write-Warning 'Transfer failed; the original town prefab was restored. Successfully copied new, unreferenced assets/evidence remain recoverable.'
            }
            throw "Stage 6 transfer failed. Original prefab backup: $backup. $($failure.Exception.Message)"
        }
        [IO.File]::WriteAllText((Join-Path $seamPipeline 'applied.txt'),"PASS: $($copies.Count) verified outputs delivered. Only the town prefab was replaced; all other original assets/settings/packages and Stage 1/2/3/4/5 evidence unchanged.`nRecoverable original backup: $backup`n")
        Write-Output "Stage 6 applied ($($copies.Count) files). Recoverable original prefab: $backup"
    }
}
finally {
    $seamLock.Dispose()
}
