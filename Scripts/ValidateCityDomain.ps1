$ErrorActionPreference = 'Stop'
$taskProjectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $taskProjectRoot
$taskUnityData = 'C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Data'
$taskQaDir = Join-Path $taskProjectRoot 'Library/CityUpdateQA'
New-Item -ItemType Directory -Force -Path $taskQaDir | Out-Null
$taskRuntimeDir = Join-Path $taskUnityData 'NetCoreRuntime/shared/Microsoft.NETCore.App/6.0.21'
$taskCompilerArgs = @('-nologo','-target:exe','-nostdlib+', ('-out:"' + (Join-Path $taskQaDir 'CityDomainTests.dll') + '"'))
$taskCompilerArgs += @(Get-ChildItem -LiteralPath $taskRuntimeDir -Filter '*.dll' | Where-Object { $_.Name -match '^(System\.|Microsoft\.CSharp|netstandard|mscorlib)' -and $_.Name -notmatch '\.Native\.' } | ForEach-Object { '-r:"' + $_.FullName + '"' })
$taskCompilerArgs += @('Assets/Project/Scripts/Logistics/DeliveryLedger.cs','Assets/Project/Scripts/Logistics/CargoCapacity.cs','Assets/Project/Scripts/Parking/ParkingReservations.cs','Scripts/Tests/CityDomainTests.cs')
$taskRsp = Join-Path $taskQaDir 'CityDomainTests.rsp'
# Generated compiler parameters only; no source or player data is rewritten.
$taskCompilerArgs | Set-Content -LiteralPath $taskRsp -Encoding utf8
& (Join-Path $taskUnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $taskUnityData 'DotNetSdkRoslyn/csc.dll') ('@' + $taskRsp)
if ($LASTEXITCODE -ne 0) { throw 'Domain test compilation failed.' }
Copy-Item -LiteralPath 'Scripts/Tests/CityDomainTests.runtimeconfig.json' -Destination (Join-Path $taskQaDir 'CityDomainTests.runtimeconfig.json') -Force
& (Join-Path $taskUnityData 'NetCoreRuntime/dotnet.exe') (Join-Path $taskQaDir 'CityDomainTests.dll')
if ($LASTEXITCODE -ne 0) { throw 'Domain tests failed.' }
