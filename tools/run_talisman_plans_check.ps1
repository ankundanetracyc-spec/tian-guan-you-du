param([ValidateSet(1280,1920)][int]$Width = 1280)
$plansProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$plansEngineRoot = Join-Path $plansProject 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$plansOutput = Join-Path $plansProject ('output/talisman-plans-20261109/' + $Width)
New-Item -ItemType Directory -Path (Join-Path $plansOutput 'profile') -Force | Out-Null
$plansOldAppData = $env:APPDATA
$plansOldDotnet = $env:DOTNET_ROOT
$plansOldRollForward = $env:DOTNET_ROLL_FORWARD
try {
    $env:APPDATA = Join-Path $plansOutput 'profile'
    $env:DOTNET_ROOT = Join-Path $plansEngineRoot 'dotnet'
    $env:DOTNET_ROLL_FORWARD = 'LatestMinor'
    $plansArgs = @('--path',$plansProject,'--rendering-method','gl_compatibility','res://tscn/debug/TalismanPlansNativeSmoke.tscn','--',('--talisman-plans-width=' + $Width),('--talisman-plans-output=' + $plansOutput.Replace('\','/')))
    $plansStdout = Join-Path $plansOutput 'stdout.log'
    $plansStderr = Join-Path $plansOutput 'stderr.log'
    $plansProcess = Start-Process -FilePath (Join-Path $plansEngineRoot 'Godot_spine3_8_editor_console.exe') -ArgumentList $plansArgs -WorkingDirectory $plansProject -WindowStyle Hidden -PassThru -RedirectStandardOutput $plansStdout -RedirectStandardError $plansStderr
    if (!$plansProcess.WaitForExit(60000)) { $plansProcess.Kill(); throw '符工配伍册隔离检查超时，已停止本次检查进程。' }
    Get-Content -LiteralPath $plansStdout -Tail 12
    if ($plansProcess.ExitCode -ne 0) {
        Get-Content -LiteralPath $plansStderr -Tail 20
        throw ('符工配伍册检查退出码：' + $plansProcess.ExitCode)
    }
} finally {
    $env:APPDATA = $plansOldAppData
    $env:DOTNET_ROOT = $plansOldDotnet
    $env:DOTNET_ROLL_FORWARD = $plansOldRollForward
}
