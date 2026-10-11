param(
    [string]$ReferenceImage='C:/Users/artem/.codex/generated_images/01a122f0-0110-7302-aaa6-2ce9343d3c58/exec-1fb2b9d7-ba27-4123-8939-a62d5e015ffc.png'
)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskClone=Join-Path $taskRoot 'Library/CityUpdateQA/IsolatedProject'
$taskQa=Join-Path $taskRoot 'Library/ConceptTownKitQA'
$taskSource=Join-Path $taskRoot 'Tools/ConceptTownKit'
$taskDraft=Join-Path $taskRoot 'ArtDrafts/ConceptTownKit'
$taskCloneAssets=Join-Path $taskClone 'Assets/Art/ConceptTownKit'
$taskUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
if(-not (Test-Path -LiteralPath (Join-Path $taskClone 'ProjectSettings/ProjectVersion.txt'))) {throw 'The isolated Unity project is missing.'}
if(-not (Test-Path -LiteralPath $taskUnity)) {throw 'Unity 2022.3.62f3 is unavailable.'}
if(-not $taskDraft.StartsWith($taskRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {throw 'Draft output escaped the project.'}
# Command lines are inspected internally only; Unity Hub can include credentials in them.
$taskRunning=Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Contains($taskClone) -and -not $_.CommandLine.Contains('-adb2')
}
if($taskRunning) {throw 'The isolated Unity editor is already running.'}
[IO.Directory]::CreateDirectory($taskQa) | Out-Null
function Get-ConceptFileHashes([string]$directory,[string]$filter='*') {
    $taskHashes=@{}
    if(Test-Path -LiteralPath $directory) {
        foreach($taskFile in Get-ChildItem -LiteralPath $directory -Filter $filter -Recurse -File) {
            $taskHashes[[IO.Path]::GetRelativePath($directory,$taskFile.FullName)]=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
        }
    }
    return $taskHashes
}
function Assert-ConceptHashes([hashtable]$before,[hashtable]$after,[string]$scope) {
    if($before.Count -ne $after.Count) {throw "$scope file count changed; no drafts staged."}
    foreach($taskPath in $before.Keys) {
        if($before[$taskPath] -ne $after[$taskPath]) {throw "$scope changed: $taskPath; no drafts staged."}
    }
}
$taskScenes=Get-ConceptFileHashes (Join-Path $taskRoot 'Assets') '*.unity'
$taskSourceHashes=Get-ConceptFileHashes $taskSource '*.cs'
$taskTownHashes=Get-ConceptFileHashes (Join-Path $taskRoot 'Assets/Project/Editor/TownKit') '*.cs'
$taskPrevious=Get-ConceptFileHashes $taskDraft
$taskBaseline=@{scenes=$taskScenes;generator=$taskSourceHashes;existingTownKit=$taskTownHashes}
[IO.File]::WriteAllText((Join-Path $taskQa 'baseline.json'),($taskBaseline | ConvertTo-Json -Depth 5))
foreach($taskPair in @(
    @{source=$taskSource;target=(Join-Path $taskClone 'Assets/Project/Editor/ConceptTownKit')},
    @{source=(Join-Path $taskRoot 'Assets/Project/Editor/TownKit');target=(Join-Path $taskClone 'Assets/Project/Editor/TownKit')}
)) {
    [IO.Directory]::CreateDirectory($taskPair.target) | Out-Null
    foreach($taskFile in Get-ChildItem -LiteralPath $taskPair.source -Filter '*.cs' -File) {
        Copy-Item -LiteralPath $taskFile.FullName -Destination (Join-Path $taskPair.target $taskFile.Name) -Force
    }
}
$taskResult=Join-Path $taskClone 'Library/ConceptTownKitQA/result.txt'
if(Test-Path -LiteralPath $taskResult) {Remove-Item -LiteralPath $taskResult}
$taskArgs='-batchmode -projectPath "'+$taskClone+'" -executeMethod RetailEmpireTycoon.Editor.ConceptTownKit.ConceptGeneration.GenerateBatch -logFile "'+(Join-Path $taskQa 'unity.log')+'"'
Write-Output 'Generating twelve draft models and twenty-four Unity previews in the isolated editor...'
$taskProcess=Start-Process -FilePath $taskUnity -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
$taskProcess.WaitForExit()
if($taskProcess.ExitCode -ne 0) {throw "Unity generation failed ($($taskProcess.ExitCode)). See Library/ConceptTownKitQA/unity.log."}
if(-not (Test-Path -LiteralPath $taskResult) -or -not ([IO.File]::ReadAllText($taskResult).StartsWith('CONCEPT KIT PASSED:'))) {throw 'A successful generation report is required.'}
Assert-ConceptHashes $taskScenes (Get-ConceptFileHashes (Join-Path $taskRoot 'Assets') '*.unity') 'Gameplay scenes'
Assert-ConceptHashes $taskSourceHashes (Get-ConceptFileHashes $taskSource '*.cs') 'Concept generator'
Assert-ConceptHashes $taskTownHashes (Get-ConceptFileHashes (Join-Path $taskRoot 'Assets/Project/Editor/TownKit') '*.cs') 'Existing TownKit generator'
Assert-ConceptHashes $taskPrevious (Get-ConceptFileHashes $taskDraft) 'Draft output'
if(Test-Path -LiteralPath $taskDraft) {
    $taskBackup=Join-Path $taskQa ('PreviousDraft_'+[DateTime]::UtcNow.ToString('yyyyMMdd_HHmmssfff'))
    Copy-Item -LiteralPath $taskDraft -Destination $taskBackup -Recurse
}
[IO.Directory]::CreateDirectory($taskDraft) | Out-Null
foreach($taskFile in Get-ChildItem -LiteralPath $taskCloneAssets -File -Recurse) {
    $taskRelative=[IO.Path]::GetRelativePath($taskCloneAssets,$taskFile.FullName)
    $taskDestination=Join-Path $taskDraft $taskRelative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskDestination)) | Out-Null
    Copy-Item -LiteralPath $taskFile.FullName -Destination $taskDestination -Force
}
Copy-Item -LiteralPath (Join-Path $taskClone 'Library/ConceptTownKitQA/ConceptTownKit.unitypackage') -Destination (Join-Path $taskDraft 'ConceptTownKit.unitypackage') -Force
Copy-Item -LiteralPath (Join-Path $taskSource 'README.md') -Destination (Join-Path $taskDraft 'README.md') -Force
if(Test-Path -LiteralPath $ReferenceImage) {Copy-Item -LiteralPath $ReferenceImage -Destination (Join-Path $taskDraft 'ConceptReference.png') -Force}
& (Join-Path $PSScriptRoot 'NewConceptContactSheet.ps1') -DraftDirectory $taskDraft
$taskFinalReport=[IO.File]::ReadAllText($taskResult)+"All recorded gameplay scenes and existing TownKit generator unchanged. Drafts outside Assets. No scene application, save access, commit or push.`n"
[IO.File]::WriteAllText((Join-Path $taskQa 'result.txt'),$taskFinalReport)
Write-Output $taskFinalReport
Write-Output ('Review gallery: '+(Join-Path $taskDraft 'index.html'))
