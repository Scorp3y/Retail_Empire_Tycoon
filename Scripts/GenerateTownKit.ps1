param([ValidateSet('Prepare','Launch','Apply')][string]$Mode='Prepare')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskClone=Join-Path $taskRoot 'Library/CityUpdateQA/IsolatedProject'
$taskReport=Join-Path $taskRoot 'Library/TownKitQA'
$taskModelsRelative='Assets/Art/TownKit'
$taskEditorRelative='Assets/Project/Editor/TownKit'
if(-not (Test-Path -LiteralPath (Join-Path $taskClone 'ProjectSettings/ProjectVersion.txt'))) {throw 'Isolated Unity project is missing.'}

function Assert-TownCloneIdle {
    $taskRunning=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
        $_.CommandLine -and $_.CommandLine.Contains($taskClone) -and $_.CommandLine.Contains('-executeMethod')
    }
    if($taskRunning) {throw 'The isolated Unity project is busy. Wait for the previous process to finish.'}
}
function Get-TownSceneHashes {
    $taskHashes=@{}
    foreach($taskScene in Get-ChildItem -LiteralPath (Join-Path $taskRoot 'Assets') -Filter '*.unity' -Recurse -File) {
        $taskHashes[[IO.Path]::GetRelativePath($taskRoot,$taskScene.FullName)]=(Get-FileHash -LiteralPath $taskScene.FullName -Algorithm SHA256).Hash
    }
    return $taskHashes
}
if($Mode -eq 'Prepare') {
    Assert-TownCloneIdle
    [IO.Directory]::CreateDirectory($taskReport) | Out-Null
    $taskAssetHashes=@{}
    foreach($taskScope in @($taskModelsRelative,$taskEditorRelative)) {
        $taskSource=Join-Path $taskRoot $taskScope
        if(-not (Test-Path -LiteralPath $taskSource)) {continue}
        foreach($taskFile in Get-ChildItem -LiteralPath $taskSource -File -Recurse) {
            $taskRelative=[IO.Path]::GetRelativePath($taskRoot,$taskFile.FullName)
            $taskAssetHashes[$taskRelative]=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
            $taskDestination=Join-Path $taskClone $taskRelative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskDestination)) | Out-Null
            Copy-Item -LiteralPath $taskFile.FullName -Destination $taskDestination -Force
        }
    }
    $taskSnapshot=@{assets=$taskAssetHashes;scenes=(Get-TownSceneHashes)}
    [IO.File]::WriteAllText((Join-Path $taskReport 'baseline.json'),($taskSnapshot | ConvertTo-Json -Depth 4))
    Write-Output 'TownKit editor prepared. Gameplay scenes recorded, not opened or modified.'
}
if($Mode -eq 'Launch') {
    Assert-TownCloneIdle
    # A previous successful report must not survive a crash or failed compilation.
    $taskPreviousResult=[IO.Path]::GetFullPath((Join-Path $taskClone 'Library/TownKitQA/result.txt'))
    if(-not $taskPreviousResult.StartsWith($taskClone+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {throw 'QA report escaped the isolated project.'}
    if(Test-Path -LiteralPath $taskPreviousResult) {Remove-Item -LiteralPath $taskPreviousResult}
    $taskArgs='-batchmode -projectPath "'+$taskClone+'" -executeMethod RetailEmpireTycoon.Editor.TownKit.TownKitGeneration.GenerateBatch -logFile "'+(Join-Path $taskReport 'unity.log')+'"'
    $taskProcess=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
    $taskProcess.WaitForExit()
    Write-Output ('TownKit Unity exit '+$taskProcess.ExitCode)
    exit $taskProcess.ExitCode
}
if($Mode -eq 'Apply') {
    Assert-TownCloneIdle
    $taskResult=Join-Path $taskClone 'Library/TownKitQA/result.txt'
    if(-not (Test-Path -LiteralPath $taskResult) -or -not ([IO.File]::ReadAllText($taskResult).StartsWith('TOWN KIT PASSED:'))) {throw 'TownKit asset verification did not pass.'}
    $taskBaseline=Get-Content -LiteralPath (Join-Path $taskReport 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    foreach($taskSource in $taskBaseline.assets.Keys | Where-Object {$_ -like 'Assets*Project*Editor*TownKit*'}) {
        $taskSourcePath=Join-Path $taskRoot $taskSource
        if(-not (Test-Path -LiteralPath $taskSourcePath) -or (Get-FileHash -LiteralPath $taskSourcePath -Algorithm SHA256).Hash -ne $taskBaseline.assets[$taskSource]) {throw "Generator changed during generation; prepare and run again: $taskSource"}
    }
    $taskScenes=Get-TownSceneHashes
    foreach($taskScene in $taskBaseline.scenes.Keys) {
        if($taskScenes[$taskScene] -ne $taskBaseline.scenes[$taskScene]) {throw "Scene changed during generation; no files applied: $taskScene"}
    }
    $taskGenerated=Get-ChildItem -LiteralPath (Join-Path $taskClone $taskModelsRelative) -File -Recurse
    $taskTransfers=@()
    foreach($taskFile in $taskGenerated) {
        $taskRelative=[IO.Path]::GetRelativePath($taskClone,$taskFile.FullName)
        $taskTarget=Join-Path $taskRoot $taskRelative
        $taskCurrentHash=if(Test-Path -LiteralPath $taskTarget) {(Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256).Hash} else {$null}
        $taskIncomingHash=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
        if($taskCurrentHash -eq $taskIncomingHash) {continue}
        if($taskCurrentHash -and $taskCurrentHash -ne $taskBaseline.assets[$taskRelative]) {throw "Asset changed during generation; no files applied: $taskRelative"}
        $taskTransfers+=@{source=$taskFile.FullName;target=$taskTarget}
    }
    foreach($taskTransfer in $taskTransfers) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskTransfer.target)) | Out-Null
        Copy-Item -LiteralPath $taskTransfer.source -Destination $taskTransfer.target -Force
    }
    [IO.File]::WriteAllText((Join-Path $taskReport 'result.txt'),[IO.File]::ReadAllText($taskResult)+"All recorded gameplay scenes unchanged. Model assets only applied. No save/commit/push.`n")
    Write-Output ("Applied {0} TownKit asset files. All recorded gameplay scenes unchanged." -f $taskTransfers.Count)
}
exit 0
