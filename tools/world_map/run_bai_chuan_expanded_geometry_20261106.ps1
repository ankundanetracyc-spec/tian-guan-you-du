param([ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunName = 'geometry')
$ErrorActionPreference = 'Stop'
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$outputRoot = Join-Path $projectPath 'output\bai_chuan_expanded3x_20261106'
$casePath = Join-Path $outputRoot $RunName
$isolatedProject = Join-Path $casePath 'isolated_project'
$worldScripts = Join-Path $isolatedProject 'gdscripts\world'
$csharpScripts = Join-Path $isolatedProject 'csharp\world'
$probeFolder = Join-Path $isolatedProject 'tools\world_map'
$assemblyFolder = Join-Path $isolatedProject '.godot\mono\temp\bin\Debug'
$isolatedAppData = Join-Path $casePath 'appdata'
New-Item -ItemType Directory -Force -Path $worldScripts, $csharpScripts, $probeFolder, $assemblyFolder, $isolatedAppData | Out-Null
foreach ($folder in @('resources', 'textures')) {
    $link = Join-Path $isolatedProject $folder
    if (-not (Test-Path -LiteralPath $link)) {
        New-Item -ItemType Junction -Path $link -Target (Join-Path $projectPath $folder) | Out-Null
    }
}
foreach ($file in @('BaiChuanWorldScale.gd', 'WorldMapSourceRegistry.gd', 'BaiChuanFreeNavigation.gd', 'BaiChuanTerrainTravel.gd', 'BaiChuanNodeGraph.gd', 'BaiChuanNpcNodeGraph.gd')) {
    Copy-Item -LiteralPath (Join-Path $projectPath ('gdscripts\world\' + $file)) -Destination $worldScripts
}
Copy-Item -LiteralPath (Join-Path $projectPath 'csharp\world\PackedWorldNavigation.cs') -Destination $csharpScripts
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'verify_bai_chuan_expanded_world_20261106.gd') -Destination $probeFolder
foreach ($file in @('stacklands.dll', 'stacklands.deps.json')) {
    Copy-Item -LiteralPath (Join-Path $projectPath ('.godot\mono\temp\bin\Debug\' + $file)) -Destination $assemblyFolder
}
@'
config_version=5

[application]
config/name="Expanded World Independent Geometry QA"
config/features=PackedStringArray("4.7", "C#", "GL Compatibility")

[dotnet]
project/assembly_name="stacklands"

[rendering]
renderer/rendering_method="gl_compatibility"
'@ | Set-Content -LiteralPath (Join-Path $isolatedProject 'project.godot') -Encoding utf8
$godotPath = Join-Path $projectPath 'runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor_console.exe'
$reportPrefix = Join-Path $outputRoot 'geometry_runtime_report'
$pythonReport = Join-Path $outputRoot 'geometry_runtime_report_python.json'
$outPath = Join-Path $casePath 'stdout.log'
$errPath = Join-Path $casePath 'stderr.log'
$logPath = Join-Path $casePath 'godot.log'
$oldAppData = $env:APPDATA
try {
    $env:APPDATA = $isolatedAppData
    $arguments = @('--headless', '--path', ('"' + $isolatedProject + '"'), '--script', 'res://tools/world_map/verify_bai_chuan_expanded_world_20261106.gd', '--log-file', ('"' + $logPath + '"'), '--', ('"--expected-appdata=' + $isolatedAppData + '"'), ('"--report-prefix=' + $reportPrefix + '"'), ('"--python-report=' + $pythonReport + '"'))
    $process = Start-Process -FilePath $godotPath -ArgumentList $arguments -WorkingDirectory $isolatedProject -WindowStyle Hidden -RedirectStandardOutput $outPath -RedirectStandardError $errPath -PassThru
    Write-Output ('GEOMETRY_PID=' + $process.Id)
    Write-Output ('GEOMETRY_STDOUT=' + $outPath)
    Write-Output ('GEOMETRY_STDERR=' + $errPath)
    Write-Output ('GEOMETRY_REPORT=' + $reportPrefix + '.json')
} finally { $env:APPDATA = $oldAppData }
