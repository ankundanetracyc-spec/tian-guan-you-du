param([switch]$Headless)
$ErrorActionPreference = 'Stop'
$spatialRoot = Split-Path -Parent $PSScriptRoot
$spatialRuntime = Join-Path $spatialRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$spatialEngine = Join-Path $spatialRuntime 'Godot_spine3_8_editor_console.exe'
$spatialLogs = Join-Path $spatialRoot 'tmp/item_spatial_hover_20261108'
New-Item -ItemType Directory -Force -Path $spatialLogs | Out-Null
$env:DOTNET_ROOT = Join-Path $spatialRuntime 'dotnet'
$env:DOTNET_ROLL_FORWARD = 'LatestMinor'
foreach ($spatialSize in @(@{ Width=1280; Height=720 }, @{ Width=1920; Height=1080 })) {
    $spatialName = [string]$spatialSize.Width + 'x' + [string]$spatialSize.Height
    $spatialOut = Join-Path $spatialLogs ($spatialName + '.stdout.log')
    $spatialErr = Join-Path $spatialLogs ($spatialName + '.stderr.log')
    $spatialLog = Join-Path $spatialLogs ($spatialName + '.log')
    $spatialArgs = @('--path', ('"' + $spatialRoot + '"'), '--rendering-method', 'gl_compatibility', '--log-file', ('"' + $spatialLog + '"'), 'res://tools/ItemSpatialHoverSmoke.tscn')
    if ($Headless) { $spatialArgs += '--headless' }
    else { $spatialArgs += @('--rendering-driver', 'opengl3', '--windowed', '--resolution', $spatialName) }
    $spatialArgs += @('--', ('--qa-width=' + $spatialSize.Width), ('--qa-height=' + $spatialSize.Height))
    $spatialProcess = Start-Process -FilePath $spatialEngine -ArgumentList $spatialArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $spatialOut -RedirectStandardError $spatialErr
    if (!$spatialProcess.WaitForExit(45000)) {
        $spatialProcess.Kill(); $spatialProcess.WaitForExit()
        throw ('Item spatial hover timeout: ' + $spatialName)
    }
    $spatialText = (Get-Content -LiteralPath $spatialOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $spatialErr -Raw -Encoding utf8)
    if ($spatialProcess.ExitCode -ne 0 -or $spatialText -notmatch 'ITEM_SPATIAL_HOVER_OK:' -or $spatialText -match '(?m)^ERROR:|FATAL:') {
        throw ('Item spatial hover failed: ' + $spatialName + '; inspect ' + $spatialOut + ' and ' + $spatialErr)
    }
    ($spatialText -split '\r?\n' | Where-Object { $_ -match 'ITEM_SPATIAL_HOVER_OK:' }) | Write-Output
}
