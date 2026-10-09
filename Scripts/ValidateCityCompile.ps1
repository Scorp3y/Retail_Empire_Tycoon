$ErrorActionPreference = 'Stop'
$taskProjectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $taskProjectRoot
$taskUnityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Data'
$taskQaDir = Join-Path $taskProjectRoot 'Library/CityUpdateQA'
New-Item -ItemType Directory -Force -Path $taskQaDir | Out-Null
$taskRuntimeRsp = Get-ChildItem -LiteralPath 'Library/Bee/artifacts' -Filter 'Assembly-CSharp.rsp' -Recurse | Select-Object -First 1
$taskEditorRsp = Get-ChildItem -LiteralPath 'Library/Bee/artifacts' -Filter 'Assembly-CSharp-Editor.rsp' -Recurse | Select-Object -First 1
if ($null -eq $taskRuntimeRsp -or $null -eq $taskEditorRsp) { throw 'Unity compiler response files were not found. Import the project first.' }
function Invoke-CityCompile($responseFile, $assemblyName, $sourceRoot) {
    $taskCompilerArgs = @(Get-Content -LiteralPath $responseFile.FullName | Where-Object {
        $_ -notmatch '^(-out:|-refout:)'
    } | ForEach-Object {
        $_.Replace('Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.dll', 'Library/CityUpdateQA/Assembly-CSharp.dll').Replace('Library/Bee/artifacts/1900b0aE.dag/Assembly-CSharp.ref.dll', 'Library/CityUpdateQA/Assembly-CSharp.dll')
    })
    $taskCompilerArgs += '-out:"' + (Join-Path $taskQaDir ($assemblyName + '.dll')) + '"'
    $taskNewSources = @(& rg --files $sourceRoot -g '*.cs' | Where-Object { $assemblyName -eq 'Assembly-CSharp-Editor' -or $_ -notmatch '[/\\]Editor[/\\]' } | ForEach-Object { '"' + $_.Replace('\','/') + '"' })
    $taskCompilerArgs += @($taskNewSources | Where-Object { $taskCompilerArgs -notcontains $_ })
    # Mechanically derived compiler input is a build artifact, not project source.
    $taskDerivedRsp = Join-Path $taskQaDir ($assemblyName + '.rsp')
    $taskCompilerArgs | Set-Content -LiteralPath $taskDerivedRsp -Encoding utf8
    & (Join-Path $taskUnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $taskUnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $taskDerivedRsp)
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $assemblyName" }
}
Invoke-CityCompile $taskRuntimeRsp 'Assembly-CSharp' 'Assets/Project/Scripts'
Invoke-CityCompile $taskEditorRsp 'Assembly-CSharp-Editor' 'Assets/Project/Editor'
Write-Output 'CITY UPDATE COMPILE PASSED'
