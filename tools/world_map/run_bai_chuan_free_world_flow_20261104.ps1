param(
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$RunName = "flow",
    [switch]$Headless,
    [switch]$CompatibilityOnly,
    [switch]$MovementOnly,
    [switch]$EventOnly,
    [switch]$TerrainOnly
)
$ErrorActionPreference = 'Stop'
$selectedGroups = @($CompatibilityOnly, $MovementOnly, $EventOnly, $TerrainOnly) | Where-Object { $_.IsPresent }
if (@($selectedGroups).Count -gt 1) { throw 'Choose only one acceptance group per isolated process.' }
$projectPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$qaPath = Join-Path $projectPath 'output\bai_chuan_terrain_speed_20261105\qa'
New-Item -ItemType Directory -Force -Path $qaPath | Out-Null
$isolatedAppData = Join-Path $qaPath ($RunName + '_appdata')
New-Item -ItemType Directory -Force -Path $isolatedAppData | Out-Null
$godotPath = Join-Path $projectPath 'runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor_console.exe'
$logPath = Join-Path $qaPath ($RunName + '_godot.log')
$outPath = Join-Path $qaPath ($RunName + '_stdout.log')
$errPath = Join-Path $qaPath ($RunName + '_stderr.log')
$oldAppData = $env:APPDATA
try {
    $env:APPDATA = $isolatedAppData
    $arguments = @('--path', ('"' + $projectPath + '"'), '--scene', 'res://tools/BaiChuanFreeWorldFlowSmoke.tscn', '--log-file', ('"' + $logPath + '"'), '--', ('"--expected-appdata=' + $isolatedAppData + '"'), ('--run-name=' + $RunName))
    if ($Headless) { $arguments = @('--headless') + $arguments }
    if ($CompatibilityOnly) { $arguments += '--compatibility-only' }
    if ($MovementOnly) { $arguments += '--movement-only' }
    if ($EventOnly) { $arguments += '--event-only' }
    if ($TerrainOnly) { $arguments += '--terrain-only' }
    $process = Start-Process -FilePath $godotPath -ArgumentList $arguments -WorkingDirectory $projectPath -WindowStyle Hidden -RedirectStandardOutput $outPath -RedirectStandardError $errPath -PassThru
    Write-Output ("QA_PID=" + $process.Id)
    Write-Output ("QA_LOG=" + $logPath)
    Write-Output ("QA_APPDATA=" + $isolatedAppData)
    Write-Output ("QA_REPORT=" + (Join-Path $qaPath ($RunName + '_report.txt')))
} finally { $env:APPDATA = $oldAppData }
