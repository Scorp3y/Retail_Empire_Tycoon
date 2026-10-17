param([ValidateSet('Prepare','Before','Generate','After','Play','Apply')][string]$Mode='Prepare')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

$residentialProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$residentialClone=Join-Path $residentialProject 'Library/CityUpdateQA/IsolatedProject'
$residentialPipeline=Join-Path $residentialProject 'Library/TownResidentialPipeline'
$residentialQa=Join-Path $residentialClone 'Library/TownResidentialQA'
$residentialAssetRoot='Assets/Art/TownResidentialLayout'
$residentialPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$residentialSandbox='Assets/Project/Editor/ShopGameplaySandbox.unity'
$residentialUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$residentialBaselinePath=Join-Path $residentialPipeline 'baseline.json'
$residentialOriginalPath=Join-Path $residentialQa 'OriginalTown.prefab'

if(-not (Test-Path -LiteralPath (Join-Path $residentialClone 'ProjectSettings/ProjectVersion.txt'))) {
    throw 'The existing isolated Unity project must be prepared first.'
}
if(-not (Test-Path -LiteralPath $residentialUnity)) {throw 'The configured Unity 2022.3.62f3 editor is missing.'}
[IO.Directory]::CreateDirectory($residentialPipeline) | Out-Null

# An exclusive file handle prevents two pipeline invocations from racing through the busy check.
$residentialLock=[IO.File]::Open((Join-Path $residentialPipeline 'pipeline.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)

function Assert-ResidentialUnityIdle {
    $running=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($residentialClone,[StringComparison]::OrdinalIgnoreCase) }
    if($running) {throw 'The isolated Unity editor is busy. Wait for that run to exit before continuing.'}
}

function Get-ResidentialHash([string]$path) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "Required file is missing: $path"}
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
}

function Write-ResidentialJson([string]$path,$value) {
    [IO.File]::WriteAllText($path,($value | ConvertTo-Json -Depth 12))
}

function Read-ResidentialJson([string]$path) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "Run the prerequisite phase first; missing receipt: $path"}
    return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json -AsHashtable)
}

function Resolve-ResidentialRelative([string]$project,[string]$relative) {
    $path=[IO.Path]::GetFullPath((Join-Path $project $relative))
    $canonical=[IO.Path]::GetRelativePath($project,$path).Replace('\','/')
    if($canonical -cne $relative -or $canonical.StartsWith('../',[StringComparison]::Ordinal) -or [IO.Path]::IsPathRooted($relative)) {
        throw "Non-canonical project path: $relative"
    }
    return $path
}

function Test-ResidentialOutput([string]$relative) {
    return $relative -ceq $residentialPrefab -or $relative -ceq ($residentialAssetRoot+'.meta') -or
        $relative.StartsWith($residentialAssetRoot+'/',[StringComparison]::Ordinal)
}

function Test-ResidentialAuthoring([string]$relative) {
    return $relative -cmatch '^Assets/Project/Editor/TownResidential[^/]*\.cs(\.meta)?$' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotReview.cs' -or
        $relative -ceq 'Assets/Project/Editor/TownPilotReview.cs.meta'
}

function Test-ResidentialNewMetadata([string]$relative) {
    return $relative -cmatch '^Assets/Project/Editor/TownResidential[^/]*\.cs\.meta$'
}

function Get-ResidentialSnapshot([string]$project) {
    $snapshot=@{}
    foreach($scope in @('Assets','ProjectSettings','Packages','Docs/CityPilotReview','Docs/TownArchitectureReview')) {
        $scopePath=Join-Path $project $scope
        if(-not (Test-Path -LiteralPath $scopePath)) {continue}
        foreach($file in Get-ChildItem -LiteralPath $scopePath -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath($project,$file.FullName).Replace('\','/')
            if($relative -ceq ($residentialAssetRoot+'.meta') -or $relative.StartsWith($residentialAssetRoot+'/',[StringComparison]::Ordinal)) {continue}
            $snapshot[$relative]=Get-ResidentialHash $file.FullName
        }
    }
    return $snapshot
}

function Assert-ResidentialSnapshot([string]$project,$baseline,[bool]$allowTownChange=$false,[bool]$allowAuthoringChanges=$false) {
    $current=Get-ResidentialSnapshot $project
    foreach($relative in $baseline.Keys) {
        if($allowTownChange -and $relative -ceq $residentialPrefab) {continue}
        if($allowAuthoringChanges -and (Test-ResidentialAuthoring $relative)) {
            if(-not $current.ContainsKey($relative)) {throw "Prepare cannot remove a previously prepared authoring source: $relative"}
            continue
        }
        if(-not $current.ContainsKey($relative) -or $current[$relative] -cne $baseline[$relative]) {
            throw "Protected file changed: $relative ($project)"
        }
    }
    foreach($relative in $current.Keys) {
        if(-not $baseline.ContainsKey($relative) -and -not (Test-ResidentialNewMetadata $relative) -and
            -not ($allowAuthoringChanges -and (Test-ResidentialAuthoring $relative))) {
            throw "Unexpected file outside the Stage 3 allowlist: $relative ($project)"
        }
    }
}

function Assert-ResidentialReceipt([string]$phase) {
    $receipt=Read-ResidentialJson (Join-Path $residentialPipeline ($phase+'.json'))
    if($receipt.exitCode -ne 0 -or $receipt.baselineHash -cne (Get-ResidentialHash $residentialBaselinePath)) {
        throw "Stage 3 $phase has not validated the current source baseline."
    }
    return $receipt
}

function Set-ResidentialReceiptPending([string]$phase) {
    Write-ResidentialJson (Join-Path $residentialPipeline ($phase+'.json')) @{exitCode=-1}
}

function Assert-ResidentialPass([string]$path,[string[]]$prefixes=@('PASS')) {
    if(-not (Test-Path -LiteralPath $path -PathType Leaf)) {throw "QA evidence is missing: $path"}
    $value=Get-Content -LiteralPath $path -Raw
    foreach($prefix in $prefixes) {
        if($value.StartsWith($prefix,[StringComparison]::Ordinal)) {return}
    }
    throw "QA did not pass: $path"
}

function Assert-ResidentialGameplayEvidence {
    Assert-ResidentialPass (Join-Path $residentialQa 'gameplay.txt')
    Assert-ResidentialPass (Join-Path $residentialQa 'runtime.txt')
    Assert-ResidentialPass (Join-Path $residentialClone 'Library/TownWorldQA/integration.txt') @('TOWN WORLD PASSED')
    $cityEvidence=Get-Content -LiteralPath (Join-Path $residentialClone 'Library/CityUpdateQA/integration.txt') -Raw
    # Individual scenarios append PASS before the full trip finishes. Only the exact final terminal
    # plus absence of a FAILED line proves completion; a partial PASS log must never authorise Apply.
    if(-not $cityEvidence.TrimEnd().EndsWith('CITY DELIVERY INTEGRATION PASSED',[StringComparison]::Ordinal) -or
        $cityEvidence -cmatch '(?m)^[^\r\n]*\bFAILED\b') {
        throw 'The original City delivery regression did not finish successfully, or its log contains a FAILED line.'
    }
}

function Assert-ResidentialOriginal($baseline) {
    if((Get-ResidentialHash $residentialOriginalPath) -cne $baseline.originalPrefabHash -or
        (Get-ResidentialHash ($residentialOriginalPath+'.meta')) -cne $baseline.originalMetadataHash) {
        throw 'The immutable original town backup changed. It must not be replaced with a generated variant.'
    }
    if((Get-ResidentialHash (Join-Path $residentialClone ($residentialPrefab+'.meta'))) -cne $baseline.originalMetadataHash) {
        throw 'The original town prefab GUID/import metadata changed in the isolated project.'
    }
}

function Copy-ResidentialAtomically([string]$source,[string]$target,[bool]$replace=$false) {
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    $incoming=Join-Path $residentialPipeline ([Guid]::NewGuid().ToString()+'.incoming')
    Copy-Item -LiteralPath $source -Destination $incoming
    try {
        if((Get-ResidentialHash $incoming) -cne (Get-ResidentialHash $source)) {throw "Staging copy mismatch: $source"}
        # Both workspaces and staging are on this project's volume; rename a complete file, never truncate an open asset.
        [IO.File]::Move($incoming,$target,$replace)
    }
    finally {
        if(Test-Path -LiteralPath $incoming) {[IO.File]::Delete($incoming)}
    }
}

function Get-ResidentialGeneratedAssets {
    $outputs=@{}
    $outputs[$residentialPrefab]=Get-ResidentialHash (Join-Path $residentialClone $residentialPrefab)
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $residentialClone $residentialAssetRoot) -File -Recurse) {
        $relative=[IO.Path]::GetRelativePath($residentialClone,$file.FullName).Replace('\','/')
        $outputs[$relative]=Get-ResidentialHash $file.FullName
    }
    $outputs[$residentialAssetRoot+'.meta']=Get-ResidentialHash (Join-Path $residentialClone ($residentialAssetRoot+'.meta'))
    return $outputs
}

function Assert-ResidentialHashes([string]$project,$expected,[bool]$requireExactAssetSet=$false) {
    foreach($relative in $expected.Keys) {
        $path=Resolve-ResidentialRelative $project $relative
        if((Get-ResidentialHash $path) -cne $expected[$relative]) {throw "Validated output changed: $relative"}
    }
    if($requireExactAssetSet) {
        $current=Get-ResidentialGeneratedAssets
        if($current.Count -ne $expected.Count) {throw 'The set of generated town assets changed after Generate.'}
        foreach($relative in $current.Keys) {
            if(-not $expected.ContainsKey($relative)) {throw "Unvalidated generated asset: $relative"}
        }
    }
}

function Assert-ResidentialCaptureInputs([string]$phase,[bool]$allowGeneratedTown=$false) {
    $inputs=Read-ResidentialJson (Join-Path $residentialQa ($phase+'/source-hashes.json'))
    foreach($inputFile in $inputs.files) {
        $relative=$inputFile.path
        # Packages are mounted virtually by Unity. They remain protected by the complete package-file baseline.
        if(-not $relative.StartsWith('Assets/',[StringComparison]::Ordinal)) {continue}
        if($allowGeneratedTown -and (Test-ResidentialOutput $relative)) {continue}
        $path=Resolve-ResidentialRelative $residentialClone $relative
        if((Get-ResidentialHash $path) -cne $inputFile.sha256) {throw "Stale $phase capture input: $relative"}
    }
}

function Get-ResidentialBeforeHashes {
    $outputs=@{}
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $residentialQa 'Before') -File -Recurse) {
        $relative=[IO.Path]::GetRelativePath($residentialClone,$file.FullName).Replace('\','/')
        $outputs[$relative]=Get-ResidentialHash $file.FullName
    }
    return $outputs
}

function Assert-ResidentialBeforeEvidence {
    $receipt=Assert-ResidentialReceipt 'Before'
    Assert-ResidentialPass (Join-Path $residentialQa 'Before/complete.txt')
    Assert-ResidentialHashes $residentialClone $receipt.evidence
    $current=Get-ResidentialBeforeHashes
    if($current.Count -ne $receipt.evidence.Count) {throw 'The immutable Before evidence file set changed.'}
}

function Invoke-ResidentialUnity([string]$phase,[string]$entry) {
    Assert-ResidentialUnityIdle
    $arguments='-batchmode -projectPath "'+$residentialClone+'" -executeMethod '+$entry+' -logFile "'+(Join-Path $residentialPipeline ($phase+'.log'))+'"'
    $process=Start-Process -FilePath $residentialUnity -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    if($process.ExitCode -ne 0) {throw "Stage 3 Unity $phase failed with exit code $($process.ExitCode). See its pipeline log."}
}

function Save-ResidentialPlayFixture($baseline) {
    $fixture=@{
        directory=(Join-Path $residentialPipeline ('PlayFixture_'+[Guid]::NewGuid().ToString('N')))
        baselineHash=(Get-ResidentialHash $residentialBaselinePath)
        files=@{}
    }
    foreach($relative in @($residentialSandbox,($residentialSandbox+'.meta'))) {
        if(-not $baseline.main.ContainsKey($relative) -or -not $baseline.isolated.ContainsKey($relative) -or
            $baseline.main[$relative] -cne $baseline.isolated[$relative]) {
            throw "Sandbox fixture must have identical prepared main/isolated bytes: $relative"
        }
        $main=Resolve-ResidentialRelative $residentialProject $relative
        $source=Resolve-ResidentialRelative $residentialClone $relative
        $expected=$baseline.isolated[$relative]
        if((Get-ResidentialHash $main) -cne $baseline.main[$relative] -or (Get-ResidentialHash $source) -cne $expected) {
            throw "The protected sandbox changed before Play; do not adopt it as a temporary fixture: $relative"
        }
        $fixture.files[$relative]=$expected
    }
    # The existing sandbox helper saves its fixed-path scene. Back up only these two known fixture files,
    # outside Assets, rather than weakening the scene SHA guard or rewriting shared test infrastructure.
    [IO.Directory]::CreateDirectory($fixture.directory) | Out-Null
    foreach($relative in $fixture.files.Keys) {
        $saved=Resolve-ResidentialRelative $fixture.directory $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
        Copy-Item -LiteralPath (Resolve-ResidentialRelative $residentialClone $relative) -Destination $saved
        if((Get-ResidentialHash $saved) -cne $fixture.files[$relative]) {throw "Sandbox fixture backup mismatch: $relative"}
    }
    Write-ResidentialJson (Join-Path $fixture.directory 'receipt.json') $fixture
    Write-ResidentialJson (Join-Path $residentialPipeline 'active-play-fixture.json') $fixture
    return $fixture
}

function Restore-ResidentialPlayFixture($fixture) {
    # Never restore an asset while Unity can still be saving/importing it, even when its run failed.
    Assert-ResidentialUnityIdle
    $directory=[IO.Path]::GetFullPath($fixture.directory)
    if([IO.Path]::GetDirectoryName($directory) -cne [IO.Path]::GetFullPath($residentialPipeline) -or
        [IO.Path]::GetFileName($directory) -cnotmatch '^PlayFixture_[0-9a-f]{32}$' -or $fixture.files.Count -ne 2) {
        throw 'Invalid scoped sandbox fixture backup.'
    }
    if($fixture.baselineHash -cne (Get-ResidentialHash $residentialBaselinePath)) {
        throw 'The prepared baseline changed while the scoped sandbox fixture was active.'
    }
    $baseline=Read-ResidentialJson $residentialBaselinePath
    foreach($relative in @($residentialSandbox,($residentialSandbox+'.meta'))) {
        if(-not $fixture.files.ContainsKey($relative)) {throw "Sandbox fixture backup omits a protected file: $relative"}
        if(-not $baseline.main.ContainsKey($relative) -or -not $baseline.isolated.ContainsKey($relative) -or
            $fixture.files[$relative] -cne $baseline.main[$relative] -or $fixture.files[$relative] -cne $baseline.isolated[$relative] -or
            (Get-ResidentialHash (Resolve-ResidentialRelative $residentialProject $relative)) -cne $baseline.main[$relative]) {
            throw "Sandbox fixture does not match the protected original main/isolated bytes: $relative"
        }
        $saved=Resolve-ResidentialRelative $directory $relative
        if((Get-ResidentialHash $saved) -cne $fixture.files[$relative]) {throw "Sandbox fixture original bytes changed: $relative"}
    }
    foreach($relative in @($residentialSandbox,($residentialSandbox+'.meta'))) {
        $target=Resolve-ResidentialRelative $residentialClone $relative
        if(-not (Test-Path -LiteralPath $target) -or (Get-ResidentialHash $target) -cne $fixture.files[$relative]) {
            Copy-ResidentialAtomically (Resolve-ResidentialRelative $directory $relative) $target $true
        }
        if((Get-ResidentialHash $target) -cne $fixture.files[$relative]) {throw "Sandbox fixture restoration mismatch: $relative"}
    }
    [IO.File]::WriteAllText((Join-Path $fixture.directory 'restored.txt'),
        "PASS: exact isolated sandbox scene/meta bytes restored after Unity exit. Both files remain in the full protected-file SHA baseline; no main scene was written.`n")
}

function Restore-ResidentialPendingPlayFixture {
    $active=Join-Path $residentialPipeline 'active-play-fixture.json'
    if(-not (Test-Path -LiteralPath $active)) {return}
    $fixture=Read-ResidentialJson $active
    $directory=[IO.Path]::GetFullPath($fixture.directory)
    if([IO.Path]::GetDirectoryName($directory) -cne [IO.Path]::GetFullPath($residentialPipeline) -or
        [IO.Path]::GetFileName($directory) -cnotmatch '^PlayFixture_[0-9a-f]{32}$') {
        throw 'Invalid pending sandbox fixture path.'
    }
    if(Test-Path -LiteralPath (Join-Path $directory 'restored.txt')) {return}
    # A PowerShell crash can bypass finally. Resume only this recorded fixture, whose backup/main bytes
    # must still match the same baseline; a changed source/scene can never be silently adopted.
    Restore-ResidentialPlayFixture $fixture
    Write-Output 'Recovered the pending isolated sandbox fixture after an interrupted Play invocation.'
}

function Invoke-ResidentialPlay($baseline) {
    $fixture=Save-ResidentialPlayFixture $baseline
    $playFailure=$null
    $restorationFailure=$null
    try {
        Invoke-ResidentialUnity 'Play' 'TownResidentialBatch.ValidatePlayMode'
    }
    catch {$playFailure=$_}
    finally {
        try {Restore-ResidentialPlayFixture $fixture}
        catch {$restorationFailure=$_}
    }
    if($playFailure -and $restorationFailure) {
        throw "Play failed: $($playFailure.Exception.Message). Sandbox restoration also failed: $($restorationFailure.Exception.Message). Original fixture: $($fixture.directory)"
    }
    if($restorationFailure) {throw $restorationFailure}
    if($playFailure) {throw $playFailure}
}

try {
    Assert-ResidentialUnityIdle
    if($Mode -eq 'Prepare') {
        $refresh=Test-Path -LiteralPath $residentialBaselinePath
        if($refresh) {
            $previous=Read-ResidentialJson $residentialBaselinePath
            Assert-ResidentialSnapshot $residentialProject $previous.main $false $true
            Assert-ResidentialSnapshot $residentialClone $previous.isolated $true $true
            Assert-ResidentialOriginal $previous
            $hasBefore=Test-Path -LiteralPath (Join-Path $residentialQa 'Before/complete.txt')
            if($hasBefore) {
                Assert-ResidentialBeforeEvidence
                Assert-ResidentialCaptureInputs 'Before' $true
            }
            # A retry can refresh only the Stage 3 authoring code; all saved scenes/assets/Before images remain immutable.
            foreach($file in Get-ChildItem -LiteralPath (Join-Path $residentialProject 'Assets/Project/Editor') -File) {
                $relative=[IO.Path]::GetRelativePath($residentialProject,$file.FullName).Replace('\','/')
                if(Test-ResidentialAuthoring $relative) {
                    Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $residentialClone $relative) -Force
                }
            }
            Copy-ResidentialAtomically $residentialOriginalPath (Join-Path $residentialClone $residentialPrefab) $true
            $baseline=@{
                main=(Get-ResidentialSnapshot $residentialProject)
                isolated=(Get-ResidentialSnapshot $residentialClone)
                originalPrefabHash=$previous.originalPrefabHash
                originalMetadataHash=$previous.originalMetadataHash
            }
            Write-ResidentialJson $residentialBaselinePath $baseline
            if($hasBefore) {
                Write-ResidentialJson (Join-Path $residentialPipeline 'Before.json') @{
                    exitCode=0;baselineHash=(Get-ResidentialHash $residentialBaselinePath);evidence=(Get-ResidentialBeforeHashes)
                }
            }
            foreach($phase in @('Generate','After','Play')) {Set-ResidentialReceiptPending $phase}
            Write-Output 'Stage 3 authoring refreshed. Immutable Before/original town preserved; Generate, After and Play must be repeated.'
            exit 0
        }

        if(Test-Path -LiteralPath (Join-Path $residentialQa 'Before/complete.txt')) {
            throw 'Before evidence exists without its pipeline baseline. Preserve that evidence; do not prepare over it.'
        }
        if(Test-Path -LiteralPath (Join-Path $residentialProject $residentialAssetRoot)) {
            throw 'Stage 3 generated output already exists in the main project. A new delivery needs a separately approved baseline.'
        }
        foreach($scope in @('Assets/Project','Assets/Prefabs','Assets/Resources/ShopUi','Assets/Art/TownKit','Assets/Art/TownDetails','Assets/Art/TownArchitecture','Assets/Shaders','Assets/Settings','ProjectSettings/QualitySettings.asset','ProjectSettings/GraphicsSettings.asset')) {
            $scopePath=Join-Path $residentialProject $scope
            if(-not (Test-Path -LiteralPath $scopePath)) {continue}
            foreach($file in Get-ChildItem -LiteralPath $scopePath -File -Recurse) {
                $relative=[IO.Path]::GetRelativePath($residentialProject,$file.FullName).Replace('\','/')
                $target=Resolve-ResidentialRelative $residentialClone $relative
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
                Copy-Item -LiteralPath $file.FullName -Destination $target -Force
            }
        }
        # The catalogue root metadata is outside its directory enumeration but is still a Unity GUID dependency.
        foreach($relative in @('Assets/Art/TownArchitecture.meta')) {
            $source=Join-Path $residentialProject $relative
            if(Test-Path -LiteralPath $source) {Copy-Item -LiteralPath $source -Destination (Join-Path $residentialClone $relative) -Force}
        }
        [IO.Directory]::CreateDirectory($residentialQa) | Out-Null
        foreach($backup in @(@{source=(Join-Path $residentialProject $residentialPrefab);target=$residentialOriginalPath},
            @{source=(Join-Path $residentialProject ($residentialPrefab+'.meta'));target=($residentialOriginalPath+'.meta')})) {
            if(Test-Path -LiteralPath $backup.target) {
                if((Get-ResidentialHash $backup.target) -cne (Get-ResidentialHash $backup.source)) {throw 'An existing original-town backup differs; refusing to replace it.'}
            }
            else {Copy-Item -LiteralPath $backup.source -Destination $backup.target}
        }
        $baseline=@{
            main=(Get-ResidentialSnapshot $residentialProject)
            isolated=(Get-ResidentialSnapshot $residentialClone)
            originalPrefabHash=(Get-ResidentialHash $residentialOriginalPath)
            originalMetadataHash=(Get-ResidentialHash ($residentialOriginalPath+'.meta'))
        }
        Write-ResidentialJson $residentialBaselinePath $baseline
        Write-Output 'Stage 3 prepared: all original assets/settings/packages and Stage 1/2 evidence protected; original town saved outside Assets.'
        exit 0
    }

    $residentialBaseline=Read-ResidentialJson $residentialBaselinePath
    if($Mode -eq 'Play') {Restore-ResidentialPendingPlayFixture}
    Assert-ResidentialSnapshot $residentialProject $residentialBaseline.main
    Assert-ResidentialSnapshot $residentialClone $residentialBaseline.isolated ($Mode -ne 'Before')
    Assert-ResidentialOriginal $residentialBaseline

    if($Mode -eq 'Before') {
        if(Test-Path -LiteralPath (Join-Path $residentialQa 'Before/complete.txt')) {throw 'Before is immutable. Continue with Generate, or Prepare to refresh only Stage 3 authoring code.'}
        Set-ResidentialReceiptPending 'Before'
        Invoke-ResidentialUnity 'Before' 'TownPilotReview.CaptureResidentialBefore'
        Assert-ResidentialPass (Join-Path $residentialQa 'Before/complete.txt')
        Assert-ResidentialSnapshot $residentialProject $residentialBaseline.main
        Assert-ResidentialSnapshot $residentialClone $residentialBaseline.isolated
        Assert-ResidentialCaptureInputs 'Before'
        Write-ResidentialJson (Join-Path $residentialPipeline 'Before.json') @{
            exitCode=0;baselineHash=(Get-ResidentialHash $residentialBaselinePath);evidence=(Get-ResidentialBeforeHashes)
        }
        Write-Output 'Stage 3 Before captured with protected sources and immutable evidence.'
        exit 0
    }

    Assert-ResidentialBeforeEvidence
    Assert-ResidentialCaptureInputs 'Before' $true
    if($Mode -eq 'Generate') {
        foreach($phase in @('Generate','After','Play')) {Set-ResidentialReceiptPending $phase}
        # Every attempt starts from the same original prefab, not from the partially edited output of a failed attempt.
        Copy-ResidentialAtomically $residentialOriginalPath (Join-Path $residentialClone $residentialPrefab) $true
        foreach($name in @('result.txt','protection.txt','gameplay.txt')) {[IO.File]::WriteAllText((Join-Path $residentialQa $name),'PENDING: new generation has not passed.')}
        Invoke-ResidentialUnity 'Generate' 'TownResidentialBatch.Generate'
        Assert-ResidentialPass (Join-Path $residentialQa 'result.txt') @('PASS','TOWN RESIDENTIAL PASSED')
        Assert-ResidentialPass (Join-Path $residentialQa 'protection.txt')
        Assert-ResidentialSnapshot $residentialProject $residentialBaseline.main
        Assert-ResidentialSnapshot $residentialClone $residentialBaseline.isolated $true
        Assert-ResidentialBeforeEvidence
        Write-ResidentialJson (Join-Path $residentialPipeline 'Generate.json') @{
            exitCode=0;baselineHash=(Get-ResidentialHash $residentialBaselinePath);assets=(Get-ResidentialGeneratedAssets)
        }
        Write-Output 'Stage 3 generated: only the isolated town prefab/new residential meshes may change; all other original files unchanged.'
        exit 0
    }

    $residentialGenerated=Assert-ResidentialReceipt 'Generate'
    Assert-ResidentialHashes $residentialClone $residentialGenerated.assets $true
    if($Mode -eq 'After') {
        Set-ResidentialReceiptPending 'After'
        Set-ResidentialReceiptPending 'Play'
        Invoke-ResidentialUnity 'After' 'TownPilotReview.CaptureResidentialAfter'
        Assert-ResidentialPass (Join-Path $residentialQa 'After/complete.txt')
        if((Get-ResidentialHash (Join-Path $residentialQa 'Before/camera-manifest.json')) -cne
            (Get-ResidentialHash (Join-Path $residentialQa 'After/camera-manifest.json'))) {
            throw 'Before/After camera definitions differ; the pictures are not comparable.'
        }
        Assert-ResidentialCaptureInputs 'After'
        Assert-ResidentialHashes $residentialClone $residentialGenerated.assets $true
        Assert-ResidentialSnapshot $residentialProject $residentialBaseline.main
        Assert-ResidentialSnapshot $residentialClone $residentialBaseline.isolated $true
        Assert-ResidentialBeforeEvidence
        $evidence=@{}
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $residentialQa 'After') -File -Recurse) {
            $relative=[IO.Path]::GetRelativePath($residentialClone,$file.FullName).Replace('\','/')
            $evidence[$relative]=Get-ResidentialHash $file.FullName
        }
        Write-ResidentialJson (Join-Path $residentialPipeline 'After.json') @{
            exitCode=0;baselineHash=(Get-ResidentialHash $residentialBaselinePath);evidence=$evidence
        }
        Write-Output 'Stage 3 After captured against the same cameras; generated assets and protected files unchanged by capture.'
        exit 0
    }

    $residentialAfter=Assert-ResidentialReceipt 'After'
    Assert-ResidentialHashes $residentialClone $residentialAfter.evidence
    Assert-ResidentialCaptureInputs 'After'
    if($Mode -eq 'Play') {
        Set-ResidentialReceiptPending 'Play'
        [IO.File]::WriteAllText((Join-Path $residentialQa 'gameplay.txt'),'PENDING: Play Mode has not passed.')
        Invoke-ResidentialPlay $residentialBaseline
        Assert-ResidentialGameplayEvidence
        Assert-ResidentialPass (Join-Path $residentialQa 'result.txt') @('PASS','TOWN RESIDENTIAL PASSED')
        Assert-ResidentialPass (Join-Path $residentialQa 'protection.txt')
        Assert-ResidentialSnapshot $residentialProject $residentialBaseline.main
        Assert-ResidentialSnapshot $residentialClone $residentialBaseline.isolated $true
        Assert-ResidentialHashes $residentialClone $residentialGenerated.assets $true
        Assert-ResidentialHashes $residentialClone $residentialAfter.evidence
        Assert-ResidentialBeforeEvidence
        $outputs=Get-ResidentialGeneratedAssets
        foreach($file in Get-ChildItem -LiteralPath $residentialQa -File -Recurse) {
            $localRelative=[IO.Path]::GetRelativePath($residentialQa,$file.FullName).Replace('\','/')
            if($localRelative -ceq 'OriginalTown.prefab' -or $localRelative -ceq 'OriginalTown.prefab.meta') {continue}
            $outputs['Library/TownResidentialQA/'+$localRelative]=Get-ResidentialHash $file.FullName
        }
        Write-ResidentialJson (Join-Path $residentialPipeline 'outputs.json') $outputs
        Write-ResidentialJson (Join-Path $residentialPipeline 'Play.json') @{
            exitCode=0;baselineHash=(Get-ResidentialHash $residentialBaselinePath);outputHash=(Get-ResidentialHash (Join-Path $residentialPipeline 'outputs.json'))
        }
        Write-Output 'Stage 3 Play Mode and output hashes passed. Main project still unchanged; Apply is now eligible.'
        exit 0
    }

    if($Mode -eq 'Apply') {
        $play=Assert-ResidentialReceipt 'Play'
        $outputsPath=Join-Path $residentialPipeline 'outputs.json'
        if($play.outputHash -cne (Get-ResidentialHash $outputsPath)) {throw 'The validated output manifest changed after Play Mode.'}
        Assert-ResidentialPass (Join-Path $residentialQa 'result.txt') @('PASS','TOWN RESIDENTIAL PASSED')
        Assert-ResidentialPass (Join-Path $residentialQa 'protection.txt')
        Assert-ResidentialGameplayEvidence
        $outputs=Read-ResidentialJson $outputsPath
        Assert-ResidentialHashes $residentialClone $outputs
        $copies=[Collections.Generic.List[object]]::new()
        foreach($relative in $outputs.Keys) {
            $source=Resolve-ResidentialRelative $residentialClone $relative
            if(Test-ResidentialOutput $relative) {$targetRelative=$relative}
            elseif($relative.StartsWith('Library/TownResidentialQA/',[StringComparison]::Ordinal)) {
                $targetRelative='Docs/TownResidentialReview/'+$relative.Substring('Library/TownResidentialQA/'.Length)
            }
            else {throw "Output is not in the Stage 3 transfer allowlist: $relative"}
            $target=Resolve-ResidentialRelative $residentialProject $targetRelative
            if(Test-Path -LiteralPath $target) {
                $targetHash=Get-ResidentialHash $target
                if($targetRelative -ceq $residentialPrefab) {
                    if($targetHash -cne $residentialBaseline.main[$residentialPrefab]) {throw 'Concurrent main-town edit: the prepared prefab is no longer current.'}
                }
                elseif($targetHash -cne $outputs[$relative]) {throw "Refusing to overwrite an independently edited new asset/review: $targetRelative"}
            }
            $copies.Add(@{source=$source;relative=$targetRelative;hash=$outputs[$relative]})
        }

        $backup=Join-Path $residentialPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8))
        [IO.Directory]::CreateDirectory($backup) | Out-Null
        foreach($relative in @($residentialPrefab,($residentialPrefab+'.meta'))) {
            $source=Resolve-ResidentialRelative $residentialProject $relative
            $saved=Resolve-ResidentialRelative $backup $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($saved)) | Out-Null
            Copy-Item -LiteralPath $source -Destination $saved
            if((Get-ResidentialHash $saved) -cne (Get-ResidentialHash $source)) {throw "Recoverable backup mismatch: $relative"}
        }
        Write-ResidentialJson (Join-Path $backup 'receipt.json') @{
            baselineHash=(Get-ResidentialHash $residentialBaselinePath);outputHash=$play.outputHash;originalTownHash=$residentialBaseline.originalPrefabHash
        }
        $changedTown=$false
        try {
            # GUID metadata first, then native assets/evidence; the prefab referencing them is replaced last.
            $ordered=$copies | Sort-Object @{Expression={if($_.relative -ceq $residentialPrefab){3}elseif($_.relative.EndsWith('.meta',[StringComparison]::Ordinal)){0}elseif($_.relative.StartsWith('Assets/',[StringComparison]::Ordinal)){1}else{2}}}, @{Expression={$_.relative}}
            foreach($copy in $ordered) {
                $target=Resolve-ResidentialRelative $residentialProject $copy.relative
                if(Test-Path -LiteralPath $target) {
                    if((Get-ResidentialHash $target) -ceq $copy.hash) {continue}
                    if($copy.relative -cne $residentialPrefab -or (Get-ResidentialHash $target) -cne $residentialBaseline.originalPrefabHash) {
                        throw "Concurrent edit during transfer: $($copy.relative)"
                    }
                }
                Copy-ResidentialAtomically $copy.source $target ($copy.relative -ceq $residentialPrefab)
                if($copy.relative -ceq $residentialPrefab) {$changedTown=$true}
                if((Get-ResidentialHash $target) -cne $copy.hash) {throw "Transferred file mismatch: $($copy.relative)"}
            }
            Assert-ResidentialSnapshot $residentialProject $residentialBaseline.main $true
            foreach($copy in $copies) {
                if((Get-ResidentialHash (Resolve-ResidentialRelative $residentialProject $copy.relative)) -cne $copy.hash) {throw "Transferred output changed: $($copy.relative)"}
            }
        }
        catch {
            $failure=$_
            $mainTown=Join-Path $residentialProject $residentialPrefab
            if($changedTown -and (Get-ResidentialHash $mainTown) -ceq $residentialGenerated.assets[$residentialPrefab]) {
                Copy-ResidentialAtomically (Join-Path $backup $residentialPrefab) $mainTown $true
                Write-Warning 'Transfer failed; the original town prefab was restored. Successfully copied new, unreferenced assets/evidence remain recoverable.'
            }
            throw "Stage 3 transfer failed. Original prefab backup: $backup. $($failure.Exception.Message)"
        }
        [IO.File]::WriteAllText((Join-Path $residentialPipeline 'applied.txt'),"PASS: $($copies.Count) verified outputs delivered. Only the town prefab was replaced; all other original assets/settings/packages and Stage 1/2 evidence unchanged.`nRecoverable original backup: $backup`n")
        Write-Output "Stage 3 applied ($($copies.Count) files). Recoverable original prefab: $backup"
    }
}
finally {
    $residentialLock.Dispose()
}
