param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunName = 'packed_dotnet')
$ErrorActionPreference = 'Stop'
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$casePath = Join-Path $projectPath ('output\packed_world_navigation_20261106\' + $RunName)
$isolatedProject = Join-Path $casePath 'isolated_project'
$scriptFolder = Join-Path $isolatedProject 'csharp\world'
$probeFolder = Join-Path $isolatedProject 'tools\world_map'
$assemblyFolder = Join-Path $isolatedProject '.godot\mono\temp\bin\Debug'
$isolatedAppData = Join-Path $casePath 'appdata'
New-Item -ItemType Directory -Force -Path $scriptFolder, $probeFolder, $assemblyFolder, $isolatedAppData | Out-Null
Copy-Item -LiteralPath (Join-Path $projectPath 'csharp\world\PackedWorldNavigation.cs') -Destination $scriptFolder
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verify_packed_world_navigation_20261106.gd') -Destination $probeFolder
foreach ($name in @('stacklands.dll', 'stacklands.deps.json')) {
    Copy-Item -LiteralPath (Join-Path $projectPath ('.godot\mono\temp\bin\Debug\' + $name)) -Destination $assemblyFolder
}
@'
config_version=5

[application]
config/name="Packed Navigation .NET Isolated Probe"
config/features=PackedStringArray("4.7", "C#", "GL Compatibility")

[dotnet]
project/assembly_name="stacklands"

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
    $arguments = @('--headless', '--path', ('"' + $isolatedProject + '"'), '--script', 'res://tools/world_map/verify_packed_world_navigation_20261106.gd', '--log-file', ('"' + $logPath + '"'), '--', ('"--expected-appdata=' + $isolatedAppData + '"'), ('"--report=' + $reportPath + '"'))
    $process = Start-Process -FilePath $godotPath -ArgumentList $arguments -WorkingDirectory $isolatedProject -WindowStyle Hidden -RedirectStandardOutput $outPath -RedirectStandardError $errPath -PassThru
    Write-Output ('PROBE_PID=' + $process.Id)
    Write-Output ('PROBE_STDOUT=' + $outPath)
    Write-Output ('PROBE_STDERR=' + $errPath)
    Write-Output ('PROBE_REPORT=' + $reportPath)
} finally { $env:APPDATA = $oldAppData }
