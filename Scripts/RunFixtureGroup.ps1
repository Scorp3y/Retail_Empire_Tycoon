param([Parameter(Mandatory=$true)][string]$Group,[Parameter(Mandatory=$true)][int]$Iteration)
$projectRoot=Split-Path -Parent $PSScriptRoot
$pythonRuntime='C:/Users/artem/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $pythonRuntime (Join-Path $PSScriptRoot 'Blender/BuildFixtureBundle.py') $Group
if ($LASTEXITCODE -ne 0) { throw 'Fixture bundle failed' }
$scriptPath=Join-Path $projectRoot ('Scripts/Blender/BuildFixtures'+$Group+'.py')
$outputPath=Join-Path $projectRoot ('ArtSource/Fixtures_'+$Group)
$logPath=Join-Path $projectRoot ('Library/Fixtures/'+$Group)
Start-Process 'D:/SteamLibrary/steamapps/common/Blender/blender.exe' -WorkingDirectory $projectRoot -ArgumentList @('--background','--factory-startup','--python',('"'+$scriptPath+'"'),'--','--output',('"'+$outputPath+'"'),'--iteration',$Iteration) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logPath ('{0:00}.log' -f $Iteration)) -RedirectStandardError (Join-Path $logPath ('{0:00}.err' -f $Iteration)) -PassThru | Select-Object Id
