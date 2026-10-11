param([ValidateSet('Prepare','Models','Survey','ReviewBefore','PreviewA','ReviewAfter','Launch','LaunchPilot','Apply')][string]$Mode='Prepare',[string]$ResumeBackup='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskClone=Join-Path $taskRoot 'Library/CityUpdateQA/IsolatedProject'
$taskReport=Join-Path $taskRoot 'Library/TownWorldQA'
$taskScopes=@('Assets/Project','Assets/Prefabs','Assets/Resources/ShopUi','Assets/Art/TownKit','Assets/Shaders','Assets/Settings','ProjectSettings/QualitySettings.asset','ProjectSettings/GraphicsSettings.asset')
if(Test-Path -LiteralPath (Join-Path $taskRoot 'Assets/Art/TownDetails')) {$taskScopes+='Assets/Art/TownDetails'}
if(-not (Test-Path -LiteralPath (Join-Path $taskClone 'ProjectSettings/ProjectVersion.txt'))) {throw 'The isolated Unity project is missing.'}
$taskRunning=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -and $_.CommandLine.Contains($taskClone) -and $_.CommandLine.Contains('-executeMethod') }
if($taskRunning) {throw 'The isolated Unity editor is busy.'}
[IO.Directory]::CreateDirectory($taskReport) | Out-Null
if($Mode -eq 'Prepare') {
    $taskBaseline=@{}
    foreach($taskScope in $taskScopes) {
        foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskRoot $taskScope) -File -Recurse) {
            $taskRelative=[IO.Path]::GetRelativePath($taskRoot,$taskFile.FullName)
            $taskBaseline[$taskRelative]=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
            $taskTarget=Join-Path $taskClone $taskRelative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskTarget)) | Out-Null
            Copy-Item -LiteralPath $taskFile.FullName -Destination $taskTarget -Force
        }
    }
    # Approved models remain outside main Assets; bake them into the shared world in the isolated editor.
    foreach($taskPart in @('Prefabs','Meshes','Materials')) {
        $taskDraft=Join-Path $taskRoot ('ArtDrafts/ConceptTownKit/'+$taskPart)
        foreach($taskFile in Get-ChildItem -LiteralPath $taskDraft -File -Recurse) {
            $taskRelative=[IO.Path]::GetRelativePath($taskRoot,$taskFile.FullName)
            $taskBaseline[$taskRelative]=(Get-FileHash -LiteralPath $taskFile.FullName).Hash
            $taskTarget=Join-Path $taskClone ('Assets/Art/ConceptTownKit/'+$taskPart+'/'+[IO.Path]::GetRelativePath($taskDraft,$taskFile.FullName))
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskTarget)) | Out-Null
            Copy-Item -LiteralPath $taskFile.FullName -Destination $taskTarget -Force
        }
    }
    foreach($taskName in @('TownOverview','TownStreet','TownIndustrial','TownShop','TownStoreFrontage','TownFuelStation','TownFacades','TownFarmParking','TownFarmJunction','TownSupplierRow','TownCargo','TownFarm','TownPark','TownDrive','TownRailway','TownBridge','TownCivicSquare','TownPlayground','TownSportsCourt','TownRiverfront','TownMixedStreets','TownCornerHousing','TownLocalShops','TownStationSquare','TownQuietCourt')) {
        $taskRelative='Docs\Images\'+$taskName+'.png'
        $taskFile=Join-Path $taskRoot $taskRelative
        if(Test-Path -LiteralPath $taskFile) {$taskBaseline[$taskRelative]=(Get-FileHash -LiteralPath $taskFile).Hash}
    }
    [IO.File]::WriteAllText((Join-Path $taskReport 'baseline.json'),($taskBaseline | ConvertTo-Json -Depth 4))
    Write-Output 'World QA prepared; main project remains unchanged.'
}
if($Mode -in @('Models','Survey','ReviewBefore','PreviewA','ReviewAfter','Launch','LaunchPilot')) {
    if($Mode -in @('Launch','LaunchPilot')) {
        [IO.File]::WriteAllText((Join-Path $taskReport 'launch.json'),'{"exitCode":-1}')
    }
    $taskEntry=switch ($Mode) {
        'Models' {'RetailEmpireTycoon.Editor.TownKit.TownDetailKitGeneration.GenerateBatch'}
        'Survey' {'TownWorldSurvey.Run'}
        'ReviewBefore' {'TownPilotReview.CaptureBefore'}
        'PreviewA' {'TownPilotVariantBatch.RunA'}
        'ReviewAfter' {'TownPilotReview.CaptureAfter'}
        'LaunchPilot' {'TownWorldBatch.RunPilot'}
        default {'TownWorldBatch.Run'}
    }
    $taskArgs='-batchmode -projectPath "'+$taskClone+'" -executeMethod '+$taskEntry+' -logFile "'+(Join-Path $taskReport ($Mode+'.log'))+'"'
    $taskProcess=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
    if($Mode -in @('Launch','LaunchPilot')) {
        $taskReceipt=@{exitCode=$taskProcess.ExitCode;baselineHash=(Get-FileHash -LiteralPath (Join-Path $taskReport 'baseline.json')).Hash;pilot=($Mode -eq 'LaunchPilot')}
        [IO.File]::WriteAllText((Join-Path $taskReport 'launch.json'),($taskReceipt | ConvertTo-Json))
    }
    Write-Output ('World Unity exit '+$taskProcess.ExitCode)
    exit $taskProcess.ExitCode
}
if($Mode -eq 'Apply') {
    $taskResume=$ResumeBackup.Length -gt 0
    if($taskResume) {
        $taskBackup=[IO.Path]::GetFullPath($ResumeBackup)
        if([IO.Path]::GetDirectoryName($taskBackup) -ne [IO.Path]::GetFullPath($taskReport) -or
            [IO.Path]::GetFileName($taskBackup) -notmatch '^BeforeApply_\d{8}_\d{6}$' -or -not (Test-Path -LiteralPath $taskBackup -PathType Container)) {
            throw 'Resume requires an existing BeforeApply directory inside this project report folder.'
        }
    }
    $taskReceipt=Get-Content -LiteralPath (Join-Path $taskReport 'launch.json') -Raw | ConvertFrom-Json
    if($taskReceipt.exitCode -ne 0) {throw 'The latest isolated Unity run did not complete successfully.'}
    $taskBaselinePath=Join-Path $taskReport 'baseline.json'
    if($taskReceipt.baselineHash -ne (Get-FileHash -LiteralPath $taskBaselinePath).Hash) {throw 'QA preparation changed after the validated run.'}
    $taskBaseline=Get-Content -LiteralPath $taskBaselinePath -Raw | ConvertFrom-Json -AsHashtable
    $taskQa=Join-Path $taskClone 'Library/TownWorldQA'
    if(-not (Get-Content -LiteralPath (Join-Path $taskQa 'integration.txt') -Raw).StartsWith('TOWN WORLD PASSED')) {throw 'Town integration validation failed.'}
    if(-not (Get-Content -LiteralPath (Join-Path $taskQa 'supplier-preservation.txt') -Raw).StartsWith('PASS')) {throw 'Protected supply points changed.'}
    if(-not (Get-Content -LiteralPath (Join-Path $taskQa 'farm-preservation.txt') -Raw).StartsWith('PASS')) {throw 'Protected farm changed.'}
    if($taskReceipt.pilot) {
        if(-not (Get-Content -LiteralPath (Join-Path $taskQa 'pilot-scope-preservation.txt') -Raw).StartsWith('PASS')) {throw 'World outside the pilot changed.'}
        if(-not (Get-Content -LiteralPath (Join-Path $taskQa 'pilot-gameplay.txt') -Raw).StartsWith('PASS')) {throw 'Pilot physics/driving validation failed.'}
        foreach($taskPhase in @('Before','VariantA','After')) {
            $taskReview=Join-Path $taskClone ('Library/TownPilotReview/'+$taskPhase)
            if(-not (Get-Content -LiteralPath (Join-Path $taskReview 'complete.txt') -Raw).StartsWith('PASS')) {throw "Pilot comparison capture failed: $taskPhase"}
        }
        $taskCaptureInputs=Get-Content -LiteralPath (Join-Path $taskClone 'Library/TownPilotReview/After/source-hashes.json') -Raw | ConvertFrom-Json
        # Unity exposes Packages/... as virtual mounted paths while its editor is running.
        # Only Assets dependencies belong to the generated scene transfer; package files are not copied.
        foreach($taskInput in $taskCaptureInputs.files | Where-Object { $_.path.StartsWith('Assets/',[StringComparison]::Ordinal) }) {
            $taskInputPath=[IO.Path]::GetFullPath((Join-Path $taskClone $taskInput.path))
            if(-not $taskInputPath.StartsWith(([IO.Path]::GetFullPath($taskClone)+[IO.Path]::DirectorySeparatorChar),[StringComparison]::OrdinalIgnoreCase)) {throw 'Pilot capture input is outside the isolated project.'}
            if((Get-FileHash -LiteralPath $taskInputPath).Hash -ne $taskInput.sha256) {throw "Pilot screenshots are stale: $($taskInput.path)"}
        }
    }
    if(-not (Get-Content -LiteralPath (Join-Path $taskQa 'detail-coverage.txt') -Raw).StartsWith('PASS')) {throw 'Public pedestrian routes failed final geometry validation.'}
    if(-not (Get-Content -LiteralPath (Join-Path $taskClone 'Library/TownDetailsQA/result.txt') -Raw).StartsWith('TOWN DETAILS PASSED')) {throw 'Model generation failed.'}
    if(-not (Get-Content -LiteralPath (Join-Path $taskClone 'Library/CityUpdateQA/integration.txt') -Raw).Contains('CITY DELIVERY INTEGRATION PASSED')) {throw 'Existing delivery/parking regression failed.'}
    # Scene references are meaningful only while the prepared code and source palette/models have not changed.
    foreach($taskRelative in $taskBaseline.Keys) {
        if($taskRelative.EndsWith('.cs') -or $taskRelative.StartsWith('Assets\Art\TownKit\') -or $taskRelative.StartsWith('ArtDrafts\ConceptTownKit\')) {
            $taskCurrent=Join-Path $taskRoot $taskRelative
            if(-not (Test-Path -LiteralPath $taskCurrent) -or (Get-FileHash -LiteralPath $taskCurrent).Hash -ne $taskBaseline[$taskRelative]) {throw "Source changed during QA: $taskRelative"}
        }
    }
    $taskCopies=[Collections.Generic.List[object]]::new()
    foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskClone 'Assets/Art/TownDetails') -File -Recurse) {
        $taskCopies.Add(@{source=$taskFile.FullName;relative=[IO.Path]::GetRelativePath($taskClone,$taskFile.FullName)})
    }
    $taskCopies.Add(@{source=(Join-Path $taskClone 'Assets/Art/TownDetails.meta');relative='Assets\Art\TownDetails.meta'})
    foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskClone 'Assets/Prefabs/TownWorld') -File -Recurse) {
        $taskCopies.Add(@{source=$taskFile.FullName;relative=[IO.Path]::GetRelativePath($taskClone,$taskFile.FullName)})
    }
    foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskClone 'Assets/Prefabs/StoreUpgrade') -File -Recurse) {
        $taskCopies.Add(@{source=$taskFile.FullName;relative=[IO.Path]::GetRelativePath($taskClone,$taskFile.FullName)})
    }
    foreach($taskRelative in @('Assets/Prefabs/Structures/Wall.asset','Assets/Prefabs/Structures/Wall_Corner.asset','Assets/Prefabs/Structures/Door.asset','Assets/Prefabs/Structures/Door.prefab','Assets/Prefabs/City/Pickup.prefab','Assets/Prefabs/City/parking_01.asset','Assets/Prefabs/City/ParkingBay.prefab','Assets/Prefabs/City/asphalt_01.asset')) {
        $taskCopies.Add(@{source=(Join-Path $taskClone $taskRelative);relative=$taskRelative.Replace('/','\')})
    }
    foreach($taskRelative in @('Assets/Prefabs/TownWorld.meta','Assets/Prefabs/StarterStore/StarterStore.asset','Assets/Project/Scenes/Game.unity','Assets/Project/Scenes/City.unity','ProjectSettings/QualitySettings.asset','ProjectSettings/GraphicsSettings.asset')) {
        $taskCopies.Add(@{source=(Join-Path $taskClone $taskRelative);relative=$taskRelative.Replace('/','\')})
    }
    # Transfer Unity GUIDs for the newly authored scripts; never import the QA sandbox or unrelated project changes.
    foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Assets/Project') -Filter '*.cs' -File -Recurse) {
        $taskRelative=[IO.Path]::GetRelativePath($taskRoot,$taskFile.FullName)+'.meta'
        $taskMeta=Join-Path $taskClone $taskRelative
        if(Test-Path -LiteralPath $taskMeta) {$taskCopies.Add(@{source=$taskMeta;relative=$taskRelative})}
    }
    foreach($taskName in @('TownOverview','TownStreet','TownIndustrial','TownShop','TownStoreFrontage','TownFuelStation','TownFacades','TownFarmParking','TownFarmJunction','TownSupplierRow','TownCargo','TownFarm','TownPark','TownDrive','TownRailway','TownBridge','TownCivicSquare','TownPlayground','TownSportsCourt','TownRiverfront','TownMixedStreets','TownCornerHousing','TownLocalShops','TownStationSquare','TownQuietCourt')) {
        $taskCopies.Add(@{source=(Join-Path $taskQa ($taskName+'.png'));relative=('Docs\Images\'+$taskName+'.png')})
    }
    if($taskReceipt.pilot) {
        foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskClone 'Library/TownPilotReview') -File -Recurse) {
            $taskCopies.Add(@{source=$taskFile.FullName;relative=('Docs\CityPilotReview\'+[IO.Path]::GetRelativePath((Join-Path $taskClone 'Library/TownPilotReview'),$taskFile.FullName))})
        }
        $taskCopies.Add(@{source=(Join-Path $taskQa 'townhouse-pilot-layout.txt');relative='Docs\CityPilotReview\After\layout.txt'})
        $taskCopies.Add(@{source=(Join-Path $taskQa 'pilot-scope-preservation.txt');relative='Docs\CityPilotReview\After\protection.txt'})
        $taskCopies.Add(@{source=(Join-Path $taskQa 'pilot-gameplay.txt');relative='Docs\CityPilotReview\After\gameplay.txt'})
        $taskCopies.Add(@{source=(Join-Path $taskQa 'PilotDrivingCamera.png');relative='Docs\CityPilotReview\After\PilotDrivingCamera.png'})
    }
    foreach($taskCopy in $taskCopies) {
        if(-not (Test-Path -LiteralPath $taskCopy.source)) {throw "Missing validated output: $($taskCopy.source)"}
        $taskCopy.hash=(Get-FileHash -LiteralPath $taskCopy.source).Hash
        $taskTarget=Join-Path $taskRoot $taskCopy.relative
        if(Test-Path -LiteralPath $taskTarget) {
            $taskHash=(Get-FileHash -LiteralPath $taskTarget).Hash
            if($taskBaseline.ContainsKey($taskCopy.relative)) {
                if($taskHash -ne $taskBaseline[$taskCopy.relative]) {
                    $taskOriginal=if($taskResume) {Join-Path $taskBackup $taskCopy.relative} else {$null}
                    if(-not $taskResume -or $taskHash -ne $taskCopy.hash -or -not (Test-Path -LiteralPath $taskOriginal) -or
                        (Get-FileHash -LiteralPath $taskOriginal).Hash -ne $taskBaseline[$taskCopy.relative]) {
                        throw "Concurrent project edit: $($taskCopy.relative)"
                    }
                }
            } elseif($taskHash -ne (Get-FileHash -LiteralPath $taskCopy.source).Hash) {throw "Refusing to overwrite an unprepared file: $($taskCopy.relative)"}
        }
    }
    if(-not $taskResume) {$taskBackup=Join-Path $taskReport ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))}
    $taskStaging=Join-Path $taskReport 'TransferStaging'
    [IO.Directory]::CreateDirectory($taskStaging) | Out-Null
    foreach($taskCopy in $taskCopies) {
        $taskTarget=Join-Path $taskRoot $taskCopy.relative
        if(Test-Path -LiteralPath $taskTarget) {
            if((Get-FileHash -LiteralPath $taskTarget).Hash -eq $taskCopy.hash) {continue}
            $taskSaved=Join-Path $taskBackup $taskCopy.relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskSaved)) | Out-Null
            if(-not (Test-Path -LiteralPath $taskSaved)) {Copy-Item -LiteralPath $taskTarget -Destination $taskSaved}
        }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskTarget)) | Out-Null
        # Renaming a complete sibling-volume file avoids truncating meshes memory-mapped by the open editor.
        # A failed transfer remains resumable only when both the prepared hash and original backup still match.
        $taskIncoming=Join-Path $taskStaging ([Guid]::NewGuid().ToString()+'.tmp')
        Copy-Item -LiteralPath $taskCopy.source -Destination $taskIncoming
        [IO.File]::Move($taskIncoming,$taskTarget,$true)
        if((Get-FileHash -LiteralPath $taskTarget).Hash -ne $taskCopy.hash) {throw "Transferred file hash mismatch: $($taskCopy.relative)"}
    }
    foreach($taskName in @('assets.txt','integration.txt','farm-preservation.txt','supplier-preservation.txt','detail-coverage.txt')) {Copy-Item -LiteralPath (Join-Path $taskQa $taskName) -Destination (Join-Path $taskReport $taskName) -Force}
    if($taskReceipt.pilot) {Copy-Item -LiteralPath (Join-Path $taskQa 'pilot-scope-preservation.txt') -Destination (Join-Path $taskReport 'pilot-scope-preservation.txt') -Force}
    Copy-Item -LiteralPath (Join-Path $taskClone 'Library/TownDetailsQA/result.txt') -Destination (Join-Path $taskReport 'models.txt') -Force
    Copy-Item -LiteralPath (Join-Path $taskClone 'Library/CityUpdateQA/integration.txt') -Destination (Join-Path $taskReport 'parking-regression.txt') -Force
    Write-Output "Validated town applied ($($taskCopies.Count) files). Recoverable scene backup: $taskBackup"
}
exit 0
