param(
    [Parameter(Mandatory=$true)][string]$Name,
    [Parameter(Mandatory=$true)][string]$Folder,
    [Parameter(Mandatory=$true)][int]$Iteration
)
$projectRoot = Split-Path -Parent $PSScriptRoot
$pythonRuntime = 'C:/Users/artem/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
& $pythonRuntime (Join-Path $PSScriptRoot 'Blender/BuildExpansionBundle.py') $Name $Folder
if ($LASTEXITCODE -ne 0) { throw 'Could not bundle expansion category.' }
$scriptPath = Join-Path $PSScriptRoot ('Blender/Build' + $Name + '.py')
$outputPath = Join-Path $projectRoot ('ArtSource/' + $Folder)
$logPath = Join-Path $projectRoot ('Library/Expansion/' + $Name)
Start-Process 'D:/SteamLibrary/steamapps/common/Blender/blender.exe' -WorkingDirectory $projectRoot -ArgumentList @('--background','--factory-startup','--python',('"'+$scriptPath+'"'),'--','--output',('"'+$outputPath+'"'),'--iteration',$Iteration) -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logPath ('{0:00}.log' -f $Iteration)) -RedirectStandardError (Join-Path $logPath ('{0:00}.err' -f $Iteration)) -PassThru | Select-Object Id
