param([ValidateSet('Prepare','Launch','Apply')][string]$Mode='Prepare', [switch]$CatalogueCorrectionsOnly, [switch]$StarterStore)
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskClone=Join-Path $taskRoot 'Library/CityUpdateQA/IsolatedProject'
$taskReport=Join-Path $taskRoot 'Library/StoreUpgradeQA'
$taskScopes=@('Assets/Prefabs','Assets/Art/ShopUi','Assets/Resources/ShopUi','Assets/Project/Scenes','Assets/Project/Scripts','Assets/Project/Editor')
if(-not (Test-Path -LiteralPath (Join-Path $taskClone 'ProjectSettings/ProjectVersion.txt'))) {throw 'The isolated Unity project is missing.'}
if($Mode -eq 'Prepare') {
    [IO.Directory]::CreateDirectory($taskReport) | Out-Null
    $taskBaseline=@{}
    foreach($taskScope in $taskScopes) {
        foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskRoot $taskScope) -File -Recurse) {
            $taskRelative=[IO.Path]::GetRelativePath($taskRoot,$taskFile.FullName)
            $taskBaseline[$taskRelative]=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
            $taskDestination=Join-Path $taskClone $taskRelative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskDestination)) | Out-Null
            Copy-Item -LiteralPath $taskFile.FullName -Destination $taskDestination -Force
        }
    }
    [IO.File]::WriteAllText((Join-Path $taskReport 'baseline.json'),($taskBaseline | ConvertTo-Json -Depth 3))
    Write-Output 'Prepared isolated project and recorded original hashes. Player save is not accessed.'
}
if($Mode -eq 'Launch') {
    $taskLog=Join-Path $taskReport 'unity.log'
    $taskEntry=if($StarterStore) {'StoreUpgradeBatch.RunStarter'} elseif($CatalogueCorrectionsOnly) {'StoreUpgradeBatch.RunCorrections'} else {'StoreUpgradeBatch.Run'}
    $taskArgs='-batchmode -projectPath "'+$taskClone+'" -executeMethod '+$taskEntry+' -logFile "'+$taskLog+'"'
    $taskUnity=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
    $taskUnity.WaitForExit()
    Write-Output ('Unity verification exit '+$taskUnity.ExitCode)
    exit $taskUnity.ExitCode
}
if($Mode -eq 'Apply') {
    if($StarterStore -and -not ([IO.File]::ReadAllText((Join-Path $taskClone 'Library/StoreUpgradeQA/starter.txt')).Contains('STARTER STORE PASSED'))) {throw 'Starter store and editor tests did not pass.'}
    $taskResult=Join-Path $taskClone 'Library/StoreUpgradeQA/integration.txt'
    if(-not (Test-Path -LiteralPath $taskResult) -or -not ([IO.File]::ReadAllText($taskResult).Contains('STORE UPGRADE PASSED'))) {throw 'The store-upgrade integration tests did not pass.'}
    $taskCity=Join-Path $taskClone 'Library/CityUpdateQA/integration.txt'
    if(-not ([IO.File]::ReadAllText($taskCity).Contains('CITY DELIVERY INTEGRATION PASSED'))) {throw 'The city/delivery regression tests did not pass.'}
    $taskBaseline=Get-Content -LiteralPath (Join-Path $taskReport 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $taskChanges=@()
    foreach($taskScope in $taskScopes) {
        foreach($taskFile in Get-ChildItem -LiteralPath (Join-Path $taskClone $taskScope) -File -Recurse) {
            $taskRelative=[IO.Path]::GetRelativePath($taskClone,$taskFile.FullName)
            if($taskRelative -match 'ShopGameplaySandbox\.unity') {continue}
            $taskHash=(Get-FileHash -LiteralPath $taskFile.FullName -Algorithm SHA256).Hash
            if($taskBaseline.ContainsKey($taskRelative) -and $taskHash -eq $taskBaseline[$taskRelative]) {continue}
            if($taskRelative -match '\.cs$') {throw "The clone unexpectedly changed source code: $taskRelative"}
            $taskTarget=Join-Path $taskRoot $taskRelative
            if(Test-Path -LiteralPath $taskTarget) {
                $taskCurrent=(Get-FileHash -LiteralPath $taskTarget -Algorithm SHA256).Hash
                if($taskCurrent -eq $taskHash) {continue}
                if(-not $taskBaseline.ContainsKey($taskRelative) -or $taskCurrent -ne $taskBaseline[$taskRelative]) {throw "Main project was modified during QA: $taskRelative"}
            }
            $taskChanges+=@{Source=$taskFile.FullName;Target=$taskTarget;Hash=$taskHash}
        }
    }
    # Check every target before copying any file; never overwrite concurrent user edits.
    foreach($taskChange in $taskChanges) {
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($taskChange.Target)) | Out-Null
        Copy-Item -LiteralPath $taskChange.Source -Destination $taskChange.Target -Force
        if((Get-FileHash -LiteralPath $taskChange.Target -Algorithm SHA256).Hash -ne $taskChange.Hash) {throw 'Copy verification failed.'}
    }
    Write-Output ("Applied $($taskChanges.Count) verified generated files with hash checks. No save, commit or push.")
}
