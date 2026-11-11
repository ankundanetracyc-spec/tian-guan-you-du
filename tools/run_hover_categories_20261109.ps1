$ErrorActionPreference = 'Stop'
$categoryRoot = Split-Path -Parent $PSScriptRoot
$categoryRuntime = Join-Path $categoryRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$categoryEngine = Join-Path $categoryRuntime 'Godot_spine3_8_editor_console.exe'
$categoryLogs = Join-Path $categoryRoot 'tmp/hover_category_20261109'
New-Item -ItemType Directory -Force -Path $categoryLogs | Out-Null
$categoryOldAppData = $env:APPDATA
$categoryFailures = @()
try {
    $env:DOTNET_ROOT = Join-Path $categoryRuntime 'dotnet'
    $env:DOTNET_ROLL_FORWARD = 'LatestMinor'
    foreach ($categorySize in @('1280x720','1920x1080')) {
        $categoryProfile = Join-Path $categoryLogs ('profile_' + $categorySize)
        New-Item -ItemType Directory -Force -Path $categoryProfile | Out-Null
        $env:APPDATA = $categoryProfile
        $categoryOut = Join-Path $categoryLogs ($categorySize + '.stdout.log')
        $categoryErr = Join-Path $categoryLogs ($categorySize + '.stderr.log')
        $categoryArgs = @('--path', ('"' + $categoryRoot + '"'), '--rendering-method', 'gl_compatibility', '--rendering-driver', 'opengl3',
            '--windowed', '--resolution', $categorySize, 'res://tools/HoverCategoryNativeSmoke.tscn', '--')
        if ($categorySize -eq '1920x1080') { $categoryArgs += '--large' }
        $categoryProcess = Start-Process -FilePath $categoryEngine -ArgumentList $categoryArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $categoryOut -RedirectStandardError $categoryErr
        $categoryDeadline = [DateTime]::UtcNow.AddSeconds(150)
        while (!$categoryProcess.WaitForExit(1000)) {
            if ([DateTime]::UtcNow -gt $categoryDeadline) { $categoryProcess.Kill(); throw ('Category hover timed out: ' + $categorySize) }
        }
        Get-Content -LiteralPath $categoryOut -Tail 4
        $categoryErrorText = Get-Content -LiteralPath $categoryErr -Raw -Encoding utf8
        if ($categoryProcess.ExitCode -ne 0 -or $categoryErrorText -match '(?m)^ERROR:|FATAL:') {
            Get-Content -LiteralPath $categoryErr -Tail 12; $categoryFailures += $categorySize
        }
    }
} finally { $env:APPDATA = $categoryOldAppData }
if ($categoryFailures.Count -gt 0) { throw ('Category hover has failures: ' + ($categoryFailures -join ', ')) }
