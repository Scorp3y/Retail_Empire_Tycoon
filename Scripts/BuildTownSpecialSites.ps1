param([ValidateSet('Prepare','Before','Generate','After','Play','Apply')][string]$Mode='Prepare')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

$specialProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$specialClone=Join-Path $specialProject 'Library/CityUpdateQA/IsolatedProject'
$specialPipeline=Join-Path $specialProject 'Library/TownSpecialPipeline'
$specialQa=Join-Path $specialClone 'Library/TownSpecialQA'
$specialAssetRoot='Assets/Art/TownSpecialSites'
$specialReviewRoot='Docs/TownSpecialReview'
$specialPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$specialSandbox='Assets/Project/Editor/ShopGameplaySandbox.unity'
$specialUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$specialBaselinePath=Join-Path $specialPipeline 'baseline.json'
$specialOriginalPath=Join-Path $specialQa 'OriginalTown.prefab'

if(-not (Test-Path -LiteralPath (Join-Path $specialClone 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The existing isolated Unity project must be prepared first.'
}
if(-not (Test-Path -LiteralPath $specialUnity)) {throw 'The configured Unity 2022.3.62f3 editor is missing.'}
[IO.Directory]::CreateDirectory($specialPipeline) | Out-Null

# An exclusive file handle prevents two pipeline invocations from racing through the busy check.
$specialLock=[IO.File]::Open((Join-Path $specialPipeline 'pipeline.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)

function Assert-SpecialUnityIdle {
    $running=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($specialClone,[StringComparison]::OrdinalIgnoreCase) }
    if($running) {throw 'The isolated Unity editor is busy. Wait for that run to exit before continuing.'}
}

function Get-SpecialHash([string]$path) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "Required file is missing: $path"}
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

function Write-SpecialJson([string]$path,$value) {
    [IO.File]::WriteAllText($path,($value | ConvertTo-Json -Depth 12))
}

function Read-SpecialJson([string]$path) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "Run the prerequisite phase first; missing receipt: $path"}
    return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable)
}

function Resolve-SpecialRelative([string]$project,[string]$relative) {
    $path=[IO.Path]::GetFullPath((Join-Path $project $relative))
    $canonical=[IO.Path]::GetRelativePath($project,$path).Replace('\','/')
    if($canonical -cne $relative -or $canonical.StartsWith('../',[StringComparison]::Ordinal) -or [IO.Path]::IsPathRooted($relative)) {
        throw "Non-canonical project path: $relative"
    }
    return $path
}

function Test-SpecialOutput([string]$relative) {
    return $relative -ceq $specialPrefab -or $relative -ceq ($specialAssetRoot+'.meta') -or
        $relative.StartsWith($specialAssetRoot+'/',[StringComparison]::Ordinal)
}

function Test-SpecialAuthoring([string]$relative) {
    return $relative -cmatch '^Assets/Project/Editor/TownSpecial[^/]*\.cs(\.meta)?$' -or
        $relative -cmatch '^Assets/Project/Editor/TownKit/TownSpecialModels\.cs(\.meta)?$' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotReview.cs' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotReview.cs.meta'
}

function Test-SpecialNewMetadata([string]$relative) {
    return $relative -cmatch '^Assets/Project/Editor/TownSpecial[^/]*\.cs\.meta$' -or
        $relative -ceq 'Assets/Project/Editor/TownKit/TownSpecialModels.cs.meta'
}

function Get-SpecialSnapshot([string]$project) {
    $snapshot=@{}
    foreach($scope in @('Assets','ProjectSettings','Packages','Docs')) {
        $scopePath=Join-Path $project $scope
        if(-not (Test-Path -LiteralPath $scopePath)) {continue}
        foreach($file in Get-ChildItem -LiteralPath $scopePath -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath($project,$file.FullName).Replace('\','/')
            if($relative -ceq ($specialAssetRoot+'.meta') -or $relative.StartsWith($specialAssetRoot+'/',[StringComparison]::Ordinal) -or
                $relative -ceq ($specialReviewRoot+'.md') -or $relative.StartsWith($specialReviewRoot+'/',[StringComparison]::Ordinal)) {continue}
            $snapshot[$relative]=Get-SpecialHash $file.FullName
        }
    }
    return $snapshot
}

function Assert-SpecialSnapshot([string]$project,$baseline,[bool]$allowTownChange=$false,[bool]$allowAuthoringChanges=$false) {
    $current=Get-SpecialSnapshot $project
    foreach($relative in $baseline.Keys) {
        if($allowTownChange -and $relative -ceq $specialPrefab) {continue}
        if($allowAuthoringChanges -and (Test-SpecialAuthoring $relative)) {
            if(-not $current.ContainsKey($relative)) {throw "Prepare cannot remove a previously prepared authoring source: $relative"}
            continue
        }
        if(-not $current.ContainsKey($relative) -or $current[$relative] -cne $baseline[$relative]) {
            throw "Protected file changed: $relative ($project)"
        }
    }
    foreach($relative in $current.Keys) {
        if(-not $baseline.ContainsKey($relative) -and -not (Test-SpecialNewMetadata $relative) -and
            -not ($allowAuthoringChanges -and (Test-SpecialAuthoring $relative))) {
            throw "Unexpected file outside the Stage 5 allowlist: $relative ($project)"
        }
    }
}

function Assert-SpecialReceipt([string]$phase) {
    $receipt=Read-SpecialJson (Join-Path $specialPipeline ($phase+'.json'))
    if($receipt.exitCode -ne 0 -or $receipt.baselineHash -cne (Get-SpecialHash $specialBaselinePath)) {
        throw "Stage 5 $phase has not validated the current source baseline."
    }
    return $receipt
}

function Set-SpecialReceiptPending([string]$phase) {
    Write-SpecialJson (Join-Path $specialPipeline ($phase+'.json')) @{exitCode=-1}
}

function Assert-SpecialPass([string]$path,[string[]]$prefixes=@('PASS')) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "QA evidence is missing: $path"}
    $value=Get-Content -LiteralPath $path -Raw
    if($value -cmatch '(?m)^[^\r\n]*\bFAILED\b') {throw "QA contains a failure: $path"}
    foreach($prefix in $prefixes) {
        if($value.StartsWith($prefix,[StringComparison]::Ordinal)) {return}
    }
    throw "QA did not pass: $path"
}

function Assert-SpecialGenerationEvidence {
    Assert-SpecialPass (Join-Path $specialQa 'result.txt') @('TOWN SPECIAL SITES PASSED')
    Assert-SpecialPass (Join-Path $specialQa 'templates.txt')
    Assert-SpecialPass (Join-Path $specialQa 'assets.txt')
    Assert-SpecialPass (Join-Path $specialQa 'protection.txt')
}

function Assert-SpecialGameplayEvidence {
    Assert-SpecialPass (Join-Path $specialQa 'gameplay.txt')
    Assert-SpecialPass (Join-Path $specialQa 'runtime.txt')
    Assert-SpecialPass (Join-Path $specialClone 'Library/TownWorldQA/integration.txt') @('TOWN WORLD PASSED')
    $cityEvidence=Get-Content -LiteralPath (Join-Path $specialClone 'Library/CityUpdateQA/integration.txt') -Raw
    # Individual scenarios append PASS before the full trip finishes. Only the exact final terminal
    # plus absence of a FAILED line proves completion; a partial PASS log must never authorise Apply.
    if(-not $cityEvidence.TrimEnd().EndsWith('CITY DELIVERY INTEGRATION PASSED',[StringComparison]::Ordinal) -or
        $cityEvidence -cmatch '(?m)^[^\r\n]*\bFAILED\b') {
        throw 'The original City delivery regression did not finish successfully, or its log contains a FAILED line.'
    }
}

function Assert-SpecialCaptureEvidence([string]$phase) {
    Assert-SpecialPass (Join-Path $specialQa ($phase+'/complete.txt'))
    $directory=Join-Path $specialQa $phase
    $manifest=Read-SpecialJson (Join-Path $directory 'camera-manifest.json')
    $sources=Read-SpecialJson (Join-Path $directory 'source-hashes.json')
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

function Assert-SpecialOriginal($baseline) {
    if((Get-SpecialHash $specialOriginalPath) -cne $baseline.originalPrefabHash -or
        (Get-SpecialHash ($specialOriginalPath+'.meta')) -cne $baseline.originalMetadataHash) {
        throw 'The immutable original town backup changed. It must not be replaced with a generated variant.'
    }
    if((Get-SpecialHash (Join-Path $specialClone ($specialPrefab+'.meta'))) -cne $baseline.originalMetadataHash) {
        throw 'The original town prefab GUID/import metadata changed in the isolated project.'
    }
}

function Copy-SpecialAtomically([string]$source,[string]$target,[bool]$replace=$false) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    $incoming=Join-Path $specialPipeline ([Guid]::NewGuid().ToString()+'.incoming')
    Copy-Item -LiteralPath $source -Destination $incoming
    try {
        if((Get-SpecialHash $incoming) -cne (Get-SpecialHash $source)) {throw "Staging copy mismatch: $source"}
        # Both workspaces and staging are on this project's volume; rename a complete file, never truncate an open asset.
        [IO.File]::Move($incoming,$target,$replace)
    }
    finally {
        if(Test-Path -LiteralPath $incoming) {[IO.File]::Delete($incoming)}
    }
}

function Get-SpecialGeneratedAssets {
    $outputs=@{}
    $outputs[$specialPrefab]=Get-SpecialHash (Join-Path $specialClone $specialPrefab)
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $specialClone $specialAssetRoot) -File -Recurse) {
        $relative=[IO.Path]::GetRelativePath($specialClone,$file.FullName).Replace('\','/')
        $outputs[$relative]=Get-SpecialHash $file.FullName
    }
    $outputs[$specialAssetRoot+'.meta']=Get-SpecialHash (Join-Path $specialClone ($specialAssetRoot+'.meta'))
    return $outputs
}

function Assert-SpecialHashes([string]$project,$expected,[bool]$requireExactAssetSet=$false) {
    foreach($relative in $expected.Keys) {
        $path=Resolve-SpecialRelative $project $relative
        if((Get-SpecialHash $path) -cne $expected[$relative]) {throw "Validated output changed: $relative"}
    }
    if($requireExactAssetSet) {
        $current=Get-SpecialGeneratedAssets
        if($current.Count -ne $expected.Count) {throw 'The set of generated town assets changed after Generate.'}
        foreach($relative in $current.Keys) {
            if(-not $expected.ContainsKey($relative)) {throw "Unvalidated generated asset: $relative"}
        }
    }
}

function Assert-SpecialCaptureInputs([string]$phase,[bool]$allowGeneratedTown=$false) {
    $inputs=Read-SpecialJson (Join-Path $specialQa ($phase+'/source-hashes.json'))
    foreach($inputFile in $inputs.files) {
        $relative=$inputFile.path
        # Packages are mounted virtually by Unity. They remain protected by the complete package-file baseline.
        if(-not $relative.StartsWith('Assets/',[StringComparison]::Ordinal)) {continue}
        if($allowGeneratedTown -and (Test-SpecialOutput $relative)) {continue}
        $path=Resolve-SpecialRelative $specialClone $relative
        if((Get-SpecialHash $path) -cne $inputFile.sha256) {throw "Stale $phase capture input: $relative"}
    }
}

function Get-SpecialCaptureHashes([string]$phase) {
    $outputs=@{}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $specialQa $phase) -File -Recurse) {
        $relative=[IO.Path]::GetRelativePath($specialClone,$file.FullName).Replace('\','/')
        $outputs[$relative]=Get-SpecialHash $file.FullName
    }
    return $outputs
}

function Assert-SpecialBeforeEvidence {
    $receipt=Assert-SpecialReceipt 'Before'
    Assert-SpecialCaptureEvidence 'Before'
    Assert-SpecialHashes $specialClone $receipt.evidence
    $current=Get-SpecialCaptureHashes 'Before'
    if($current.Count -ne $receipt.evidence.Count) {throw 'The immutable Before evidence file set changed.'}
}

function Invoke-SpecialUnity([string]$phase,[string]$entry) {
    Assert-SpecialUnityIdle
    $arguments='-batchmode -projectPath "'+$specialClone+'" -executeMethod '+$entry+' -logFile "'+(Join-Path $specialPipeline ($phase+'.log'))+'"'
    $process=Start-Process -FilePath $specialUnity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if($process.ExitCode -ne 0) {throw "Stage 5 Unity $phase failed with exit code $($process.ExitCode). See its pipeline log."}
}

function Save-SpecialPlayFixture($baseline) {
    $fixture=@{
        directory=(Join-Path $specialPipeline ('PlayFixture_'+[Guid]::NewGuid().ToString('N')))
        baselineHash=(Get-SpecialHash $specialBaselinePath)
        files=@{}
    }
    foreach($relative in @($specialSandbox,($specialSandbox+'.meta'))) {
        if(-not $baseline.main.ContainsKey($relative) -or -not $baseline.isolated.ContainsKey($relative) -or
            $baseline.main[$relative] -cne $baseline.isolated[$relative]) {
            throw "Sandbox fixture must have identical prepared main/isolated bytes: $relative"
        }
        $main=Resolve-SpecialRelative $specialProject $relative
        $source=Resolve-SpecialRelative $specialClone $relative
        $expected=$baseline.isolated[$relative]
        if((Get-SpecialHash $main) -cne $baseline.main[$relative] -or (Get-SpecialHash $source) -cne $expected) {
            throw "The protected sandbox changed before Play; do not adopt it as a temporary fixture: $relative"
        }
        $fixture.files[$relative]=$expected
    }
    # The existing sandbox helper saves its fixed-path scene. Back up only these two known fixture files,
    # outside Assets, rather than weakening the scene SHA guard or rewriting shared test infrastructure.
    [IO.Directory]::CreateDirectory($fixture.directory) | Out-Null
    foreach($relative in $fixture.files.Keys) {
        $saved=Resolve-SpecialRelative $fixture.directory $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
        Copy-Item -LiteralPath (Resolve-SpecialRelative $specialClone $relative) -Destination $saved
        if((Get-SpecialHash $saved) -cne $fixture.files[$relative]) {throw "Sandbox fixture backup mismatch: $relative"}
    }
    Write-SpecialJson (Join-Path $fixture.directory 'receipt.json') $fixture
    Write-SpecialJson (Join-Path $specialPipeline 'active-play-fixture.json') $fixture
    return $fixture
}

function Restore-SpecialPlayFixture($fixture) {
    # Never restore an asset while Unity can still be saving/importing it, even when its run failed.
    Assert-SpecialUnityIdle
    $directory=[IO.Path]::GetFullPath($fixture.directory)
    if([IO.Path]::GetDirectoryName($directory) -cne [IO.Path]::GetFullPath($specialPipeline) -or
        [IO.Path]::GetFileName($directory) -cnotmatch '^PlayFixture_[0-9a-f]{32}$' -or $fixture.files.Count -ne 2) {
        throw 'Invalid scoped sandbox fixture backup.'
    }
    if($fixture.baselineHash -cne (Get-SpecialHash $specialBaselinePath)) {
        throw 'The prepared baseline changed while the scoped sandbox fixture was active.'
    }
    $baseline=Read-SpecialJson $specialBaselinePath
    foreach($relative in @($specialSandbox,($specialSandbox+'.meta'))) {
        if(-not $fixture.files.ContainsKey($relative)) {throw "Sandbox fixture backup omits a protected file: $relative"}
        if(-not $baseline.main.ContainsKey($relative) -or -not $baseline.isolated.ContainsKey($relative) -or
            $fixture.files[$relative] -cne $baseline.main[$relative] -or $fixture.files[$relative] -cne $baseline.isolated[$relative] -or
            (Get-SpecialHash (Resolve-SpecialRelative $specialProject $relative)) -cne $baseline.main[$relative]) {
            throw "Sandbox fixture does not match the protected original main/isolated bytes: $relative"
        }
        $saved=Resolve-SpecialRelative $directory $relative
        if((Get-SpecialHash $saved) -cne $fixture.files[$relative]) {throw "Sandbox fixture original bytes changed: $relative"}
    }
    foreach($relative in @($specialSandbox,($specialSandbox+'.meta'))) {
        $target=Resolve-SpecialRelative $specialClone $relative
        if(-not (Test-Path -LiteralPath $target) -or (Get-SpecialHash $target) -cne $fixture.files[$relative]) {
            Copy-SpecialAtomically (Resolve-SpecialRelative $directory $relative) $target $true
        }
        if((Get-SpecialHash $target) -cne $fixture.files[$relative]) {throw "Sandbox fixture restoration mismatch: $relative"}
    }
    [IO.File]::WriteAllText((Join-Path $fixture.directory 'restored.txt'),
        "PASS: exact isolated sandbox scene/meta bytes restored after Unity exit. Both files remain in the full protected-file SHA baseline; no main scene was written.`n")
}

function Restore-SpecialPendingPlayFixture {
    $active=Join-Path $specialPipeline 'active-play-fixture.json'
    if(-not (Test-Path -LiteralPath $active)) {return}
    $fixture=Read-SpecialJson $active
    $directory=[IO.Path]::GetFullPath($fixture.directory)
    if([IO.Path]::GetDirectoryName($directory) -cne [IO.Path]::GetFullPath($specialPipeline) -or
        [IO.Path]::GetFileName($directory) -cnotmatch '^PlayFixture_[0-9a-f]{32}$') {
        throw 'Invalid pending sandbox fixture path.'
    }
    if(Test-Path -LiteralPath (Join-Path $directory 'restored.txt')) {return}
    # A PowerShell crash can bypass finally. Resume only this recorded fixture, whose backup/main bytes
    # must still match the same baseline; a changed source/scene can never be silently adopted.
    Restore-SpecialPlayFixture $fixture
    Write-Output 'Recovered the pending isolated sandbox fixture after an interrupted Play invocation.'
}

function Invoke-SpecialPlay($baseline) {
    $fixture=Save-SpecialPlayFixture $baseline
    $playFailure=$null
    $restorationFailure=$null
    try {
        Invoke-SpecialUnity 'Play' 'TownSpecialBatch.ValidatePlayMode'
    }
    catch {$playFailure=$_}
    finally {
        try {Restore-SpecialPlayFixture $fixture}
        catch {$restorationFailure=$_}
    }
    if($playFailure -and $restorationFailure) {
        throw "Play failed: $($playFailure.Exception.Message). Sandbox restoration also failed: $($restorationFailure.Exception.Message). Original fixture: $($fixture.directory)"
    }
    if($restorationFailure) {throw $restorationFailure}
    if($playFailure) {throw $playFailure}
}

try {
    Assert-SpecialUnityIdle
    Restore-SpecialPendingPlayFixture
    if($Mode -eq 'Prepare') {
        $refresh=Test-Path -LiteralPath $specialBaselinePath
        if($refresh) {
            $previous=Read-SpecialJson $specialBaselinePath
            Assert-SpecialSnapshot $specialProject $previous.main $false $true
            Assert-SpecialSnapshot $specialClone $previous.isolated $true $true
            Assert-SpecialOriginal $previous
            $hasBefore=Test-Path -LiteralPath (Join-Path $specialQa 'Before/complete.txt')
            if($hasBefore) {
                Assert-SpecialBeforeEvidence
                Assert-SpecialCaptureInputs 'Before' $true
            }
            # A retry can refresh only the Stage 5 authoring code; all saved scenes/assets/Before images remain immutable.
            foreach($file in Get-ChildItem -LiteralPath (Join-Path $specialProject 'Assets/Project/Editor') -File -Recurse) {
                $relative=[IO.Path]::GetRelativePath($specialProject,$file.FullName).Replace('\','/')
                if(Test-SpecialAuthoring $relative) {
                    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $specialClone $relative) -Force
                }
            }
            Copy-SpecialAtomically $specialOriginalPath (Join-Path $specialClone $specialPrefab) $true
            $baseline=@{
                main=(Get-SpecialSnapshot $specialProject)
                isolated=(Get-SpecialSnapshot $specialClone)
                originalPrefabHash=$previous.originalPrefabHash
                originalMetadataHash=$previous.originalMetadataHash
            }
            Write-SpecialJson $specialBaselinePath $baseline
            if($hasBefore) {
                Write-SpecialJson (Join-Path $specialPipeline 'Before.json') @{
                    exitCode=0;baselineHash=(Get-SpecialHash $specialBaselinePath);evidence=(Get-SpecialCaptureHashes 'Before')
                }
            }
            foreach($phase in @('Generate','After','Play')) {Set-SpecialReceiptPending $phase}
            Write-Output 'Stage 5 authoring refreshed. Immutable Before/original town preserved; Generate, After and Play must be repeated.'
            exit 0
        }

        if(Test-Path -LiteralPath (Join-Path $specialQa 'Before/complete.txt')) {
            throw 'Before evidence exists without its pipeline baseline. Preserve that evidence; do not prepare over it.'
        }
        if((Test-Path -LiteralPath (Join-Path $specialProject $specialAssetRoot)) -or
            (Test-Path -LiteralPath (Join-Path $specialProject ($specialAssetRoot+'.meta')))) {
            throw 'Stage 5 generated output already exists in the main project. A new delivery needs a separately approved baseline.'
        }
        foreach($scope in @('Assets','ProjectSettings','Packages','Docs')) {
            $scopePath=Join-Path $specialProject $scope
            if(-not (Test-Path -LiteralPath $scopePath)) {continue}
            foreach($file in Get-ChildItem -LiteralPath $scopePath -File -Recurse) {
                $relative=[IO.Path]::GetRelativePath($specialProject,$file.FullName).Replace('\','/')
                if($relative -ceq ($specialReviewRoot+'.md') -or $relative.StartsWith($specialReviewRoot+'/',[StringComparison]::Ordinal)) {continue}
                $target=Resolve-SpecialRelative $specialClone $relative
                if((Test-Path -LiteralPath $target -PathType Leaf) -and (Get-SpecialHash $target) -ceq (Get-SpecialHash $file.FullName)) {continue}
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            }
        }
        [IO.Directory]::CreateDirectory($specialQa) | Out-Null
        foreach($backup in @(@{source=(Join-Path $specialProject $specialPrefab);target=$specialOriginalPath},
            @{source=(Join-Path $specialProject ($specialPrefab+'.meta'));target=($specialOriginalPath+'.meta')})) {
            if(Test-Path -LiteralPath $backup.target) {
                if((Get-SpecialHash $backup.target) -cne (Get-SpecialHash $backup.source)) {throw 'An existing original-town backup differs; refusing to replace it.'}
            }
            else {Copy-Item -LiteralPath $backup.source -Destination $backup.target}
        }
        $baseline=@{
            main=(Get-SpecialSnapshot $specialProject)
            isolated=(Get-SpecialSnapshot $specialClone)
            originalPrefabHash=(Get-SpecialHash $specialOriginalPath)
            originalMetadataHash=(Get-SpecialHash ($specialOriginalPath+'.meta'))
        }
        Write-SpecialJson $specialBaselinePath $baseline
        Write-Output 'Stage 5 prepared: all original assets/settings/packages and Stage 1/2/3/4 evidence protected; original town saved outside Assets.'
        exit 0
    }

    $specialBaseline=Read-SpecialJson $specialBaselinePath
    Assert-SpecialSnapshot $specialProject $specialBaseline.main
    Assert-SpecialSnapshot $specialClone $specialBaseline.isolated ($Mode -ne 'Before')
    Assert-SpecialOriginal $specialBaseline

    if($Mode -eq 'Before') {
        if(Test-Path -LiteralPath (Join-Path $specialQa 'Before/complete.txt')) {throw 'Before is immutable. Continue with Generate, or Prepare to refresh only Stage 5 authoring code.'}
        Set-SpecialReceiptPending 'Before'
        Invoke-SpecialUnity 'Before' 'TownPilotReview.CaptureSpecialBefore'
        Assert-SpecialCaptureEvidence 'Before'
        Assert-SpecialSnapshot $specialProject $specialBaseline.main
        Assert-SpecialSnapshot $specialClone $specialBaseline.isolated
        Assert-SpecialCaptureInputs 'Before'
        Write-SpecialJson (Join-Path $specialPipeline 'Before.json') @{
            exitCode=0;baselineHash=(Get-SpecialHash $specialBaselinePath);evidence=(Get-SpecialCaptureHashes 'Before')
        }
        Write-Output 'Stage 5 Before captured with protected sources and immutable evidence.'
        exit 0
    }

    Assert-SpecialBeforeEvidence
    Assert-SpecialCaptureInputs 'Before' $true
    if($Mode -eq 'Generate') {
        foreach($phase in @('Generate','After','Play')) {Set-SpecialReceiptPending $phase}
        # Every attempt starts from the same original prefab, not from the partially edited output of a failed attempt.
        Copy-SpecialAtomically $specialOriginalPath (Join-Path $specialClone $specialPrefab) $true
        foreach($name in @('result.txt','templates.txt','assets.txt','protection.txt','gameplay.txt','runtime.txt')) {[IO.File]::WriteAllText((Join-Path $specialQa $name),'PENDING: new generation has not passed.')}
        Invoke-SpecialUnity 'Generate' 'TownSpecialBatch.Generate'
        Assert-SpecialGenerationEvidence
        Assert-SpecialSnapshot $specialProject $specialBaseline.main
        Assert-SpecialSnapshot $specialClone $specialBaseline.isolated $true
        Assert-SpecialBeforeEvidence
        Write-SpecialJson (Join-Path $specialPipeline 'Generate.json') @{
            exitCode=0;baselineHash=(Get-SpecialHash $specialBaselinePath);assets=(Get-SpecialGeneratedAssets)
        }
        Write-Output 'Stage 5 generated: only the isolated town prefab/new special assets may change; all other original files unchanged.'
        exit 0
    }

    $specialGenerated=Assert-SpecialReceipt 'Generate'
    Assert-SpecialHashes $specialClone $specialGenerated.assets $true
    if($Mode -eq 'After') {
        Set-SpecialReceiptPending 'After'
        Set-SpecialReceiptPending 'Play'
        [IO.Directory]::CreateDirectory((Join-Path $specialQa 'After')) | Out-Null
        [IO.File]::WriteAllText((Join-Path $specialQa 'After/complete.txt'),'PENDING: the current After capture has not passed.')
        Invoke-SpecialUnity 'After' 'TownPilotReview.CaptureSpecialAfter'
        Assert-SpecialCaptureEvidence 'After'
        if((Get-SpecialHash (Join-Path $specialQa 'Before/camera-manifest.json')) -cne
            (Get-SpecialHash (Join-Path $specialQa 'After/camera-manifest.json'))) {
            throw 'Before/After camera definitions differ; the pictures are not comparable.'
        }
        Assert-SpecialCaptureInputs 'After'
        Assert-SpecialHashes $specialClone $specialGenerated.assets $true
        Assert-SpecialSnapshot $specialProject $specialBaseline.main
        Assert-SpecialSnapshot $specialClone $specialBaseline.isolated $true
        Assert-SpecialBeforeEvidence
        $evidence=Get-SpecialCaptureHashes 'After'
        Write-SpecialJson (Join-Path $specialPipeline 'After.json') @{
            exitCode=0;baselineHash=(Get-SpecialHash $specialBaselinePath);evidence=$evidence
        }
        Write-Output 'Stage 5 After captured against the same cameras; generated assets and protected files unchanged by capture.'
        exit 0
    }

    $specialAfter=Assert-SpecialReceipt 'After'
    Assert-SpecialCaptureEvidence 'After'
    Assert-SpecialHashes $specialClone $specialAfter.evidence
    if((Get-SpecialCaptureHashes 'After').Count -ne $specialAfter.evidence.Count) {throw 'The validated After evidence file set changed.'}
    Assert-SpecialCaptureInputs 'After'
    if($Mode -eq 'Play') {
        Set-SpecialReceiptPending 'Play'
        foreach($name in @('gameplay.txt','runtime.txt')) {[IO.File]::WriteAllText((Join-Path $specialQa $name),'PENDING: Play Mode has not passed.')}
        foreach($relative in @('Library/TownWorldQA/integration.txt','Library/CityUpdateQA/integration.txt')) {
            $path=Resolve-SpecialRelative $specialClone $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path)) | Out-Null
            [IO.File]::WriteAllText($path,'PENDING: the current Stage 5 Play regression has not passed.')
        }
        Invoke-SpecialPlay $specialBaseline
        Assert-SpecialGameplayEvidence
        Assert-SpecialGenerationEvidence
        Assert-SpecialSnapshot $specialProject $specialBaseline.main
        Assert-SpecialSnapshot $specialClone $specialBaseline.isolated $true
        Assert-SpecialHashes $specialClone $specialGenerated.assets $true
        Assert-SpecialHashes $specialClone $specialAfter.evidence
        Assert-SpecialBeforeEvidence
        $outputs=Get-SpecialGeneratedAssets
        foreach($file in Get-ChildItem -LiteralPath $specialQa -File -Recurse) {
            $localRelative=[IO.Path]::GetRelativePath($specialQa,$file.FullName).Replace('\','/')
            if($localRelative -ceq 'OriginalTown.prefab' -or $localRelative -ceq 'OriginalTown.prefab.meta') {continue}
            $outputs['Library/TownSpecialQA/'+$localRelative]=Get-SpecialHash $file.FullName
        }
        Write-SpecialJson (Join-Path $specialPipeline 'outputs.json') $outputs
        Write-SpecialJson (Join-Path $specialPipeline 'Play.json') @{
            exitCode=0;baselineHash=(Get-SpecialHash $specialBaselinePath);outputHash=(Get-SpecialHash (Join-Path $specialPipeline 'outputs.json'))
        }
        Write-Output 'Stage 5 Play Mode and output hashes passed. Main project still unchanged; Apply is now eligible.'
        exit 0
    }

    if($Mode -eq 'Apply') {
        $play=Assert-SpecialReceipt 'Play'
        $outputsPath=Join-Path $specialPipeline 'outputs.json'
        if($play.outputHash -cne (Get-SpecialHash $outputsPath)) {throw 'The validated output manifest changed after Play Mode.'}
        Assert-SpecialGenerationEvidence
        Assert-SpecialGameplayEvidence
        $outputs=Read-SpecialJson $outputsPath
        Assert-SpecialHashes $specialClone $outputs
        $copies=[Collections.Generic.List[object]]::new()
        foreach($relative in $outputs.Keys) {
            $source=Resolve-SpecialRelative $specialClone $relative
            if(Test-SpecialOutput $relative) {$targetRelative=$relative}
            elseif($relative.StartsWith('Library/TownSpecialQA/',[StringComparison]::Ordinal)) {
                $targetRelative='Docs/TownSpecialReview/'+$relative.Substring('Library/TownSpecialQA/'.Length)
            }
            else {throw "Output is not in the Stage 5 transfer allowlist: $relative"}
            $target=Resolve-SpecialRelative $specialProject $targetRelative
            if(Test-Path -LiteralPath $target) {
                $targetHash=Get-SpecialHash $target
                if($targetRelative -ceq $specialPrefab) {
                    if($targetHash -cne $specialBaseline.main[$specialPrefab]) {throw 'Concurrent main-town edit: the prepared prefab is no longer current.'}
                }
                elseif($targetHash -cne $outputs[$relative]) {throw "Refusing to overwrite an independently edited new asset/review: $targetRelative"}
            }
            $copies.Add(@{source=$source;relative=$targetRelative;hash=$outputs[$relative]})
        }

        $backup=Join-Path $specialPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8))
        [IO.Directory]::CreateDirectory($backup) | Out-Null
        foreach($relative in @($specialPrefab,($specialPrefab+'.meta'))) {
            $source=Resolve-SpecialRelative $specialProject $relative
            $saved=Resolve-SpecialRelative $backup $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
            Copy-Item -LiteralPath $source -Destination $saved
            if((Get-SpecialHash $saved) -cne (Get-SpecialHash $source)) {throw "Recoverable backup mismatch: $relative"}
        }
        Write-SpecialJson (Join-Path $backup 'receipt.json') @{
            baselineHash=(Get-SpecialHash $specialBaselinePath);outputHash=$play.outputHash;originalTownHash=$specialBaseline.originalPrefabHash
        }
        $changedTown=$false
        try {
            # GUID metadata first, then native assets/evidence; the prefab referencing them is replaced last.
            $ordered=$copies | Sort-Object @{Expression={if($_.relative -ceq $specialPrefab){3}elseif($_.relative.EndsWith('.meta',[StringComparison]::Ordinal)){0}elseif($_.relative.StartsWith('Assets/',[StringComparison]::Ordinal)){1}else{2}}}, @{Expression={$_.relative}}
            foreach($copy in $ordered) {
                $target=Resolve-SpecialRelative $specialProject $copy.relative
                if(Test-Path -LiteralPath $target) {
                    if((Get-SpecialHash $target) -ceq $copy.hash) {continue}
                    if($copy.relative -cne $specialPrefab -or (Get-SpecialHash $target) -cne $specialBaseline.originalPrefabHash) {
                        throw "Concurrent edit during transfer: $($copy.relative)"
                    }
                }
                Copy-SpecialAtomically $copy.source $target ($copy.relative -ceq $specialPrefab)
                if($copy.relative -ceq $specialPrefab) {$changedTown=$true}
                if((Get-SpecialHash $target) -cne $copy.hash) {throw "Transferred file mismatch: $($copy.relative)"}
            }
            Assert-SpecialSnapshot $specialProject $specialBaseline.main $true
            foreach($copy in $copies) {
                if((Get-SpecialHash (Resolve-SpecialRelative $specialProject $copy.relative)) -cne $copy.hash) {throw "Transferred output changed: $($copy.relative)"}
            }
        }
        catch {
            $failure=$_
            $mainTown=Join-Path $specialProject $specialPrefab
            if($changedTown -and (Get-SpecialHash $mainTown) -ceq $specialGenerated.assets[$specialPrefab]) {
                Copy-SpecialAtomically (Join-Path $backup $specialPrefab) $mainTown $true
                Write-Warning 'Transfer failed; the original town prefab was restored. Successfully copied new, unreferenced assets/evidence remain recoverable.'
            }
            throw "Stage 5 transfer failed. Original prefab backup: $backup. $($failure.Exception.Message)"
        }
        [IO.File]::WriteAllText((Join-Path $specialPipeline 'applied.txt'),"PASS: $($copies.Count) verified outputs delivered. Only the town prefab was replaced; all other original assets/settings/packages and Stage 1/2/3/4 evidence unchanged.`nRecoverable original backup: $backup`n")
        Write-Output "Stage 5 applied ($($copies.Count) files). Recoverable original prefab: $backup"
    }
}
finally {
    $specialLock.Dispose()
}


