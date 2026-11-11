param()
$ErrorActionPreference = 'Stop'
$artRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$artEngine = Join-Path $artRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$artResults = Join-Path $artRoot '.godot/test-results/formation_guard_art'
$artProfile = Join-Path $artResults 'isolated_appdata'
New-Item -ItemType Directory -Path $artProfile -Force | Out-Null
$artOldAppData = $env:APPDATA
try {
    $env:APPDATA = $artProfile
    $artOut = Join-Path $artResults 'native.stdout.log'
    $artErr = Join-Path $artResults 'native.stderr.log'
    $artArgs = @('--path', ('"' + $artRoot + '"'), '--rendering-method', 'gl_compatibility', '--windowed', '--position', '-30000,-30000', '--resolution', '1280x900', '--scene', 'res://tscn/debug/FormationGuardMaterialsPreview.tscn', '--', '--formation-guard-art-smoke')
    $artRun = Start-Process -FilePath $artEngine -ArgumentList $artArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $artOut -RedirectStandardError $artErr
    $null = $artRun.Handle
    if (!$artRun.WaitForExit(45000)) { $artRun.Kill(); $artRun.WaitForExit(); throw 'Copper formation native UI timed out.' }
    $artText = (Get-Content -LiteralPath $artOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $artErr -Raw -Encoding utf8)
    if ($artRun.ExitCode -ne 0 -or $artText -notmatch 'FORMATION_GUARD_ART_PASS' -or $artText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') { Write-Output $artText; throw 'Copper formation native UI failed.' }
    ($artText -split "`n" | Where-Object { $_ -match 'FORMATION_GUARD_ART_PASS' }) | Write-Output
}
finally {
    if ($null -eq $artOldAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $artOldAppData }
}
