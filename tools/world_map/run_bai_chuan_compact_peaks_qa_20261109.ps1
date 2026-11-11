param(
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunName = 'migration',
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputRoot = Join-Path $projectPath 'output\bai_chuan_compact_peaks_20261109'
$casePath = Join-Path $outputRoot $RunName
$isolatedProject = Join-Path $casePath 'isolated_project'
$probeFolder = Join-Path $isolatedProject 'tools\world_map'
$assemblyFolder = Join-Path $isolatedProject '.godot\mono\temp\bin\Debug'
$isolatedAppData = Join-Path $casePath 'appdata'
New-Item -ItemType Directory -Force -Path $probeFolder, $assemblyFolder, $isolatedAppData | Out-Null
$importLink = Join-Path $isolatedProject '.godot\imported'
if (-not (Test-Path -LiteralPath $importLink)) {
    New-Item -ItemType Junction -Path $importLink -Target (Join-Path $projectPath '.godot\imported') | Out-Null
}

# Independent project config and output, production code/data available read-only.
# No game main scene, save manager, user runtime or editor is started or stopped.
foreach ($folder in @('gdscripts', 'scripts', 'csharp', 'resources', 'textures', 'shaders', 'docs')) {
    $link = Join-Path $isolatedProject $folder
    if (-not (Test-Path -LiteralPath $link)) {
        New-Item -ItemType Junction -Path $link -Target (Join-Path $projectPath $folder) | Out-Null
    }
}
$rigLink = Join-Path $isolatedProject 'tools\action_battle_test'
if (-not (Test-Path -LiteralPath $rigLink)) {
    New-Item -ItemType Junction -Path $rigLink -Target (Join-Path $projectPath 'tools\action_battle_test') | Out-Null
}
Copy-Item -LiteralPath (Join-Path $projectPath '.godot\global_script_class_cache.cfg') -Destination (Join-Path $isolatedProject '.godot\global_script_class_cache.cfg')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verify_bai_chuan_compact_peaks_migration_20261109.gd') -Destination $probeFolder
foreach ($file in @('stacklands.dll', 'stacklands.deps.json')) {
    Copy-Item -LiteralPath (Join-Path $projectPath ('.godot\mono\temp\bin\Debug\' + $file)) -Destination $assemblyFolder
}
@'
config_version=5

[application]
config/name="Compact Peaks Independent Migration QA"
config/features=PackedStringArray("4.7", "C#", "GL Compatibility")
run/flush_stdout_on_print=true

[autoload]
GameTimeManager="*res://csharp/autoload/GameTimeMgr.cs"
NpcWorldManager="*res://csharp/world/NpcWorldManager.cs"

[dotnet]
project/assembly_name="stacklands"

[rendering]
renderer/rendering_method="gl_compatibility"
'@ | Set-Content -LiteralPath (Join-Path $isolatedProject 'project.godot') -Encoding utf8
$godotPath = Join-Path $projectPath 'runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor_console.exe'
$reportPath = Join-Path $outputRoot 'production_migration_report.json'
$pythonReport = Join-Path $outputRoot 'geometry_independent_report.json'
$outPath = Join-Path $casePath 'stdout.log'
$errPath = Join-Path $casePath 'stderr.log'
$logPath = Join-Path $casePath 'godot.log'
$savedAppData = $env:APPDATA
try {
    $env:APPDATA = $isolatedAppData
    $arguments = @('--headless', '--path', ('"' + $isolatedProject + '"'), '--script', 'res://tools/world_map/verify_bai_chuan_compact_peaks_migration_20261109.gd', '--log-file', ('"' + $logPath + '"'))
    if ($CheckOnly) { $arguments += '--check-only' }
    $arguments += @('--', ('"--expected-appdata=' + $isolatedAppData + '"'), ('"--report-path=' + $reportPath + '"'), ('"--python-report=' + $pythonReport + '"'))
    $process = Start-Process -FilePath $godotPath -ArgumentList $arguments -WorkingDirectory $isolatedProject -WindowStyle Hidden -RedirectStandardOutput $outPath -RedirectStandardError $errPath -PassThru
    Write-Output ('QA_PID=' + $process.Id)
    Write-Output ('QA_STDOUT=' + $outPath)
    Write-Output ('QA_STDERR=' + $errPath)
    Write-Output ('QA_REPORT=' + $reportPath)
} finally { $env:APPDATA = $savedAppData }
