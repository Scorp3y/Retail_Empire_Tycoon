param([ValidateSet('Prepare','Survey','Launch','Apply')][string]$Mode='Prepare')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskClone=Join-Path $taskRoot 'Library/CityUpdateQA/IsolatedProject'
$taskReport=Join-Path $taskRoot 'Library/TownWorldQA'
$taskScopes=@('Assets/Project','Assets/Prefabs','Assets/Resources/ShopUi','Assets/Art/TownKit','Assets/Shaders','Assets/Settings','ProjectSettings/QualitySettings.asset','ProjectSettings/GraphicsSettings.asset')
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
    foreach($taskName in @('TownOverview','TownStreet','TownIndustrial','TownShop','TownFarm','TownPark','TownDrive')) {
        $taskRelative='Docs\Images\'+$taskName+'.png'
        $taskFile=Join-Path $taskRoot $taskRelative
        if(Test-Path -LiteralPath $taskFile) {$taskBaseline[$taskRelative]=(Get-FileHash -LiteralPath $taskFile).Hash}
    }
    [IO.File]::WriteAllText((Join-Path $taskReport 'baseline.json'),($taskBaseline | ConvertTo-Json -Depth 4))
    Write-Output 'World QA prepared; main project remains unchanged.'
}
if($Mode -in @('Survey','Launch')) {
    if($Mode -eq 'Launch') {
        [IO.File]::WriteAllText((Join-Path $taskReport 'launch.json'),'{"exitCode":-1}')
    }
    $taskEntry=if($Mode -eq 'Survey') {'TownWorldSurvey.Run'} else {'TownWorldBatch.Run'}
    $taskArgs='-batchmode -projectPath "'+$taskClone+'" -executeMethod '+$taskEntry+' -logFile "'+(Join-Path $taskReport ($Mode+'.log'))+'"'
    $taskProcess=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
    if($Mode -eq 'Launch') {
        $taskReceipt=@{exitCode=$taskProcess.ExitCode;baselineHash=(Get-FileHash -LiteralPath (Join-Path $taskReport 'baseline.json')).Hash}
        [IO.File]::WriteAllText((Join-Path $taskReport 'launch.json'),($taskReceipt | ConvertTo-Json))
    }
    Write-Output ('World Unity exit '+$taskProcess.ExitCode)
    exit $taskProcess.ExitCode
}
if($Mode -eq 'Apply') {
    $taskReceipt=Get-Content -LiteralPath (Join-Path $taskReport 'launch.json') -Raw | ConvertFrom-Json
    if($taskReceipt.exitCode -ne 0) {throw 'The latest isolated Unity run did not complete successfully.'}
    $taskBaselinePath=Join-Path $taskReport 'baseline.json'
    if($taskReceipt.baselineHash -ne (Get-FileHash -LiteralPath $taskBaselinePath).Hash) {throw 'QA preparation changed after the validated run.'}
    $taskBaseline=Get-Content -LiteralPath $taskBaselinePath -Raw | ConvertFrom-Json -AsHashtable
    $taskQa=Join-Path $taskClone 'Library/TownWorldQA'
    if(-not (Get-Content -LiteralPath (Join-Path $taskQa 'integration.txt') -Raw).StartsWith('TOWN WORLD PASSED')) {throw 'Town integration validation failed.'}
    if(-not (Get-Content -LiteralPath (Join-Path $taskClone 'Library/CityUpdateQA/integration.txt') -Raw).Contains('CITY DELIVERY INTEGRATION PASSED')) {throw 'Existing delivery/parking regression failed.'}
    # Scene references are meaningful only while the prepared code and source palette/models have not changed.
    foreach($taskRelative in $taskBaseline.Keys) {
        if($taskRelative.EndsWith('.cs') -or $taskRelative.StartsWith('Assets\Art\TownKit\')) {
            $taskCurrent=Join-Path $taskRoot $taskRelative
            if(-not (Test-Path -LiteralPath $taskCurrent) -or (Get-FileHash -LiteralPath $taskCurrent).Hash -ne $taskBaseline[$taskRelative]) {throw "Source changed during QA: $taskRelative"}
        }
    }
    $taskCopies=[Collections.Generic.List[object]]::new()
    foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskClone 'Assets/Prefabs/TownWorld') -File -Recurse) {
        $taskCopies.Add(@{source=$taskFile.FullName;relative=[IO.Path]::GetRelativePath($taskClone,$taskFile.FullName)})
    }
    foreach($taskRelative in @('Assets/Prefabs/TownWorld.meta','Assets/Project/Scenes/Game.unity','Assets/Project/Scenes/City.unity','ProjectSettings/QualitySettings.asset','ProjectSettings/GraphicsSettings.asset')) {
        $taskCopies.Add(@{source=(Join-Path $taskClone $taskRelative);relative=$taskRelative.Replace('/','\')})
    }
    # Transfer Unity GUIDs for the newly authored scripts; never import the QA sandbox or unrelated project changes.
    foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Assets/Project') -Filter '*.cs' -File -Recurse) {
        $taskRelative=[IO.Path]::GetRelativePath($taskRoot,$taskFile.FullName)+'.meta'
        $taskMeta=Join-Path $taskClone $taskRelative
        if(Test-Path -LiteralPath $taskMeta) {$taskCopies.Add(@{source=$taskMeta;relative=$taskRelative})}
    }
    foreach($taskName in @('TownOverview','TownStreet','TownIndustrial','TownShop','TownFarm','TownPark','TownDrive')) {
        $taskCopies.Add(@{source=(Join-Path $taskQa ($taskName+'.png'));relative=('Docs\Images\'+$taskName+'.png')})
    }
    foreach($taskCopy in $taskCopies) {
        if(-not (Test-Path -LiteralPath $taskCopy.source)) {throw "Missing validated output: $($taskCopy.source)"}
        $taskTarget=Join-Path $taskRoot $taskCopy.relative
        if(Test-Path -LiteralPath $taskTarget) {
            $taskHash=(Get-FileHash -LiteralPath $taskTarget).Hash
            if($taskBaseline.ContainsKey($taskCopy.relative)) {
                if($taskHash -ne $taskBaseline[$taskCopy.relative]) {throw "Concurrent project edit: $($taskCopy.relative)"}
            } elseif($taskHash -ne (Get-FileHash -LiteralPath $taskCopy.source).Hash) {throw "Refusing to overwrite an unprepared file: $($taskCopy.relative)"}
        }
    }
    $taskBackup=Join-Path $taskReport ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    foreach($taskCopy in $taskCopies) {
        $taskTarget=Join-Path $taskRoot $taskCopy.relative
        if(Test-Path -LiteralPath $taskTarget) {
            $taskSaved=Join-Path $taskBackup $taskCopy.relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskSaved)) | Out-Null
            Copy-Item -LiteralPath $taskTarget -Destination $taskSaved
        }
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskTarget)) | Out-Null
        Copy-Item -LiteralPath $taskCopy.source -Destination $taskTarget -Force
    }
    foreach($taskName in @('assets.txt','integration.txt')) {Copy-Item -LiteralPath (Join-Path $taskQa $taskName) -Destination (Join-Path $taskReport $taskName) -Force}
    Copy-Item -LiteralPath (Join-Path $taskClone 'Library/CityUpdateQA/integration.txt') -Destination (Join-Path $taskReport 'parking-regression.txt') -Force
    Write-Output "Validated town applied ($($taskCopies.Count) files). Recoverable scene backup: $taskBackup"
}
exit 0
