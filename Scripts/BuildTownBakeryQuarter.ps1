param([ValidateSet('Inspect','Generate','Review','Play','Apply')][string]$Mode='Inspect')
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$quarterProject=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$quarterClone=Join-Path $quarterProject 'Library/CityUpdateQA/IsolatedProject'
$quarterPipeline=Join-Path $quarterProject 'Library/TownBakeryQuarterPipeline'
$quarterPrefab='Assets/Prefabs/TownWorld/TownEnvironment.prefab'
$quarterUnity='C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe'
$quarterAssets='Assets/Art/TownBakeryQuarter'
$quarterEvidence='Docs/TownBakeryQuarterReview'
New-Item -ItemType Directory -Path $quarterPipeline -Force | Out-Null
function Get-QuarterHash([string]$path){return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash}
function Copy-QuarterFile([string]$source,[string]$target){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))|Out-Null;Copy-Item -LiteralPath $source -Destination $target -Force}
function Get-QuarterInputs {
    $inputs=@{}
    foreach($scope in @('Assets','Packages','ProjectSettings')){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $quarterProject $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($quarterProject,$file.FullName).Replace('\','/')
            if($relative -eq ($quarterAssets+'.meta') -or $relative.StartsWith($quarterAssets+'/')){continue}
            $inputs[$relative]=Get-QuarterHash $file.FullName
        }
    }
    return $inputs
}
if(Test-Path -LiteralPath (Join-Path $quarterClone 'Temp/UnityLockfile')){throw 'The isolated Unity editor is busy; do not interrupt it.'}
if($Mode -in @('Inspect','Generate')){
    $quarterBaseline=Get-QuarterInputs
    [IO.File]::WriteAllText((Join-Path $quarterPipeline 'baseline.json'),($quarterBaseline|ConvertTo-Json -Depth 5))
    foreach($relative in $quarterBaseline.Keys){
        $source=Join-Path $quarterProject $relative;$target=Join-Path $quarterClone $relative
        if(-not(Test-Path -LiteralPath $target) -or (Get-QuarterHash $target) -ne $quarterBaseline[$relative]){Copy-QuarterFile $source $target}
    }
    if(Test-Path -LiteralPath (Join-Path $quarterProject $quarterAssets)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $quarterProject $quarterAssets) -File -Recurse){Copy-QuarterFile $file.FullName (Join-Path $quarterClone ([IO.Path]::GetRelativePath($quarterProject,$file.FullName)))}
    }
    Copy-QuarterFile (Join-Path $quarterProject $quarterPrefab) (Join-Path $quarterPipeline 'OriginalTown.prefab')
    $quarterMethod='TownBakeryQuarterBatch.'+$Mode
} elseif($Mode -in @('Play','Review')){
    $quarterMethod=if($Mode -eq 'Play'){'TownBakeryPlayValidation.RunQuarter'}else{'TownBakeryQuarterBatch.Review'}
} else {
    $quarterQa=Join-Path $quarterClone 'Library/TownBakeryQuarterQA'
    foreach($receipt in @('result.txt','play.txt','review.txt')){
        if(-not(Get-Content -LiteralPath (Join-Path $quarterQa $receipt) -Raw).StartsWith('PASS:')){throw "Quarter acceptance failed: $receipt"}
    }
    $quarterHash=Get-QuarterHash (Join-Path $quarterClone $quarterPrefab)
    foreach($receipt in @('play-input.sha256','review-input.sha256')){
        if((Get-Content -LiteralPath (Join-Path $quarterQa $receipt) -Raw).Trim() -ne $quarterHash){throw "Stale quarter acceptance: $receipt"}
    }
    $quarterBaseline=Get-Content -LiteralPath (Join-Path $quarterPipeline 'baseline.json') -Raw | ConvertFrom-Json -AsHashtable
    $quarterCurrent=Get-QuarterInputs
    foreach($relative in $quarterBaseline.Keys){if(-not $quarterCurrent.ContainsKey($relative) -or $quarterCurrent[$relative] -ne $quarterBaseline[$relative]){throw "Main input changed: $relative"}}
    foreach($relative in $quarterCurrent.Keys){if(-not $quarterBaseline.ContainsKey($relative) -and -not $relative.EndsWith('.meta')){throw "Unexpected main input: $relative"}}
    $quarterBackup=Join-Path $quarterPipeline ('BeforeApply_'+(Get-Date -Format 'yyyyMMdd_HHmmss'))
    Copy-QuarterFile (Join-Path $quarterProject $quarterPrefab) (Join-Path $quarterBackup $quarterPrefab)
    foreach($scope in @($quarterAssets,$quarterEvidence)){
        foreach($file in Get-ChildItem -LiteralPath (Join-Path $quarterClone $scope) -File -Recurse){
            $relative=[IO.Path]::GetRelativePath($quarterClone,$file.FullName);$target=Join-Path $quarterProject $relative
            if(Test-Path -LiteralPath $target){Copy-QuarterFile $target (Join-Path $quarterBackup $relative)}
            Copy-QuarterFile $file.FullName $target
        }
    }
    Copy-QuarterFile (Join-Path $quarterClone ($quarterAssets+'.meta')) (Join-Path $quarterProject ($quarterAssets+'.meta'))
    foreach($relative in @('Assets/Project/Editor/TownBakeryQuarterBatch.cs.meta','Assets/Project/Editor/TownBakeryQuarterPlacement.cs.meta','Assets/Project/Editor/TownKit/TownBakeryQuarterModels.cs.meta')){Copy-QuarterFile (Join-Path $quarterClone $relative) (Join-Path $quarterProject $relative)}
    Copy-QuarterFile (Join-Path $quarterClone $quarterPrefab) (Join-Path $quarterProject $quarterPrefab)
    foreach($receipt in @('result.txt','play.txt','review.txt','placement.txt','protection.txt','access.txt')){Copy-QuarterFile (Join-Path $quarterQa $receipt) (Join-Path $quarterProject ($quarterEvidence+'/'+$receipt))}
    [IO.File]::WriteAllText((Join-Path $quarterPipeline 'applied.json'),(@{backup=$quarterBackup;prefab=$quarterHash;utc=[DateTime]::UtcNow.ToString('O')}|ConvertTo-Json))
    Write-Output "BAKERY QUARTER APPLIED. Recoverable backup: $quarterBackup";return
}
$quarterLog=Join-Path $quarterPipeline ($Mode+'.log')
$quarterProcess=Start-Process -FilePath $quarterUnity -ArgumentList @('-batchmode','-projectPath',('"'+$quarterClone+'"'),'-executeMethod',$quarterMethod,'-logFile',('"'+$quarterLog+'"')) -WindowStyle Hidden -PassThru
Write-Output "Bakery quarter $Mode launched. PID $($quarterProcess.Id); log $quarterLog"
