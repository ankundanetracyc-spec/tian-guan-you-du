param(
    [ValidateSet('Pack','Import','Check')][string]$Mode = 'Check',
    [ValidateSet(1280,1920)][int]$Width = 1280
)
$artProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$artEngineRoot = Join-Path $artProject 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$artOutput = Join-Path $artProject ('output/talisman-art-20261109/' + $Mode.ToLowerInvariant() + '/' + $Width)
New-Item -ItemType Directory -Path (Join-Path $artOutput 'profile') -Force | Out-Null
$artOldAppData = $env:APPDATA
$artOldDotnet = $env:DOTNET_ROOT
$artOldRollForward = $env:DOTNET_ROLL_FORWARD
try {
    $env:APPDATA = Join-Path $artOutput 'profile'
    $env:DOTNET_ROOT = Join-Path $artEngineRoot 'dotnet'
    $env:DOTNET_ROLL_FORWARD = 'LatestMinor'
    $artArgs = @('--path',$artProject,'--rendering-method','gl_compatibility')
    if ($Mode -eq 'Import') { $artArgs += @('--headless','--editor','--import','--quit') }
    elseif ($Mode -eq 'Pack') { $artArgs += @('--headless','res://tscn/debug/TalismanIconPack.tscn') }
    else { $artArgs += @('res://tscn/debug/TalismanArtNativeSmoke.tscn','--',('--talisman-art-width=' + $Width),('--talisman-art-output=' + $artOutput.Replace('\','/'))) }
    $artStdout = Join-Path $artOutput 'stdout.log'
    $artStderr = Join-Path $artOutput 'stderr.log'
    $artProcess = Start-Process -FilePath (Join-Path $artEngineRoot 'Godot_spine3_8_editor_console.exe') -ArgumentList $artArgs -WorkingDirectory $artProject -WindowStyle Hidden -PassThru -RedirectStandardOutput $artStdout -RedirectStandardError $artStderr
    if (!$artProcess.WaitForExit(60000)) { $artProcess.Kill(); throw '符工美术隔离检查超时，已停止本次检查进程。' }
    Get-Content -LiteralPath $artStdout -Tail 8
    if ($artProcess.ExitCode -ne 0) {
        Get-Content -LiteralPath $artStderr -Tail 18
        throw ('符工美术检查退出码：' + $artProcess.ExitCode)
    }
} finally {
    $env:APPDATA = $artOldAppData
    $env:DOTNET_ROOT = $artOldDotnet
    $env:DOTNET_ROLL_FORWARD = $artOldRollForward
}
