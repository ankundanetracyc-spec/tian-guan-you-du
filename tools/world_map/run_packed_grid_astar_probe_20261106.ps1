param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunName = 'packed_grid')
$ErrorActionPreference = 'Stop'
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$reportRoot = Join-Path $projectPath 'output\packed_grid_astar_20261106'
$casePath = Join-Path $reportRoot $RunName
$isolatedProject = Join-Path $casePath 'isolated_project'
$scriptFolder = Join-Path $isolatedProject 'gdscripts\world'
$probeFolder = Join-Path $isolatedProject 'tools\world_map'
$isolatedAppData = Join-Path $casePath 'appdata'
New-Item -ItemType Directory -Force -Path $scriptFolder, $probeFolder, $isolatedAppData | Out-Null
Copy-Item -LiteralPath (Join-Path $projectPath 'gdscripts\world\PackedGridAStar.gd') -Destination $scriptFolder
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verify_packed_grid_astar_20261106.gd') -Destination $probeFolder
@'
config_version=5

[application]
config/name="Packed Grid Isolated Probe"

[rendering]
renderer/rendering_method="gl_compatibility"
'@ | Set-Content -LiteralPath (Join-Path $isolatedProject 'project.godot') -Encoding utf8
$godotPath = Join-Path $projectPath 'runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor_console.exe'
if (-not (Test-Path -LiteralPath $godotPath)) { throw 'The project Godot console executable was not found.' }
$reportPath = Join-Path $casePath 'report.json'
$outPath = Join-Path $casePath 'stdout.log'
$errPath = Join-Path $casePath 'stderr.log'
$logPath = Join-Path $casePath 'godot.log'
$oldAppData = $env:APPDATA
try {
    $env:APPDATA = $isolatedAppData
    $arguments = @('--headless', '--path', ('"' + $isolatedProject + '"'), '--script', 'res://tools/world_map/verify_packed_grid_astar_20261106.gd', '--log-file', ('"' + $logPath + '"'), '--', ('"--expected-appdata=' + $isolatedAppData + '"'), ('"--report=' + $reportPath + '"'))
    $process = Start-Process -FilePath $godotPath -ArgumentList $arguments -WorkingDirectory $isolatedProject -WindowStyle Hidden -RedirectStandardOutput $outPath -RedirectStandardError $errPath -PassThru
    Write-Output ('PROBE_PID=' + $process.Id)
    Write-Output ('PROBE_STDOUT=' + $outPath)
    Write-Output ('PROBE_STDERR=' + $errPath)
    Write-Output ('PROBE_REPORT=' + $reportPath)
} finally { $env:APPDATA = $oldAppData }
