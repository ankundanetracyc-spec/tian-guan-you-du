param()
$ErrorActionPreference = 'Stop'
$guardRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$guardEngine = Join-Path $guardRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$guardResults = Join-Path $guardRoot '.godot/test-results/formation_guard_ui'
$guardProfile = Join-Path $guardResults 'isolated_appdata'
New-Item -ItemType Directory -Path $guardProfile -Force | Out-Null
$guardOldAppData = $env:APPDATA
try {
    $env:APPDATA = $guardProfile
    $guardOut = Join-Path $guardResults 'native.stdout.log'
    $guardErr = Join-Path $guardResults 'native.stderr.log'
    $guardArgs = @('--path', ('"' + $guardRoot + '"'), '--rendering-method', 'gl_compatibility', '--windowed', '--position', '-30000,-30000', '--resolution', '1280x900', '--scene', 'res://tscn/debug/FormationGuardTrial.tscn', '--', '--formation-guard-smoke')
    $guardRun = Start-Process -FilePath $guardEngine -ArgumentList $guardArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $guardOut -RedirectStandardError $guardErr
    $null = $guardRun.Handle
    if (!$guardRun.WaitForExit(45000)) { $guardRun.Kill(); $guardRun.WaitForExit(); throw 'Guard native UI timed out.' }
    $guardText = (Get-Content -LiteralPath $guardOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $guardErr -Raw -Encoding utf8)
    if ($guardRun.ExitCode -ne 0 -or $guardText -notmatch 'FORMATION_GUARD_UI_PASS' -or $guardText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') { Write-Output $guardText; throw 'Guard native UI failed.' }
    ($guardText -split "`n" | Where-Object { $_ -match 'FORMATION_GUARD_UI_PASS' }) | Write-Output
}
finally {
    if ($null -eq $guardOldAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $guardOldAppData }
}
