param([switch]$CounterMerge)
$ErrorActionPreference = 'Stop'
$logicRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$logicEngine = Join-Path $logicRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$logicResults = Join-Path $logicRoot '.godot/test-results/formation_logic_ui'
if ($CounterMerge) { $logicResults = Join-Path $logicRoot '.godot/test-results/formation_counter_merge_ui' }
$logicProfile = Join-Path $logicResults 'isolated_appdata'
New-Item -ItemType Directory -Path $logicProfile -Force | Out-Null
$logicOldAppData = $env:APPDATA
try {
    $env:APPDATA = $logicProfile
    $logicOut = Join-Path $logicResults 'native.stdout.log'
    $logicErr = Join-Path $logicResults 'native.stderr.log'
    $logicArgs = @('--path', ('"' + $logicRoot + '"'), '--rendering-method', 'gl_compatibility', '--windowed', '--position', '-30000,-30000', '--resolution', '1280x900', '--scene', 'res://tscn/debug/FormationLogicWorkbenchSmoke.tscn', '--', '--formation-logic-smoke')
    if ($CounterMerge) { $logicArgs += '--formation-counter-merge-smoke' }
    $logicRun = Start-Process -FilePath $logicEngine -ArgumentList $logicArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $logicOut -RedirectStandardError $logicErr
    $null = $logicRun.Handle
    if (!$logicRun.WaitForExit(45000)) { $logicRun.Kill(); $logicRun.WaitForExit(); throw 'Formation logic native UI timed out.' }
    $logicText = (Get-Content -LiteralPath $logicOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $logicErr -Raw -Encoding utf8)
    if ($logicRun.ExitCode -ne 0 -or $logicText -notmatch 'FORMATION_LOGIC_UI_PASS' -or $logicText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') {
        Write-Output $logicText
        throw 'Formation logic native UI failed.'
    }
    ($logicText -split "`n" | Where-Object { $_ -match 'FORMATION_LOGIC_UI_PASS' }) | Write-Output
}
finally {
    if ($null -eq $logicOldAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $logicOldAppData }
}
