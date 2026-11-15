param(
    [ValidateSet(1280,1920)][int]$Width = 1280,
    [switch]$Headless,
    [switch]$ImportOnly
)
$talismanProject = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$talismanEngineRoot = Join-Path $talismanProject 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$talismanOutput = Join-Path $talismanProject ('output/talisman-native-20261109/' + $Width)
$talismanProfile = Join-Path $talismanOutput 'profile'
New-Item -ItemType Directory -Path $talismanProfile -Force | Out-Null
$talismanOldAppData = $env:APPDATA
$talismanOldDotnet = $env:DOTNET_ROOT
$talismanOldRollForward = $env:DOTNET_ROLL_FORWARD
try {
    $env:APPDATA = $talismanProfile
    $env:DOTNET_ROOT = Join-Path $talismanEngineRoot 'dotnet'
    $env:DOTNET_ROLL_FORWARD = 'LatestMinor'
    $talismanArgs = @('--path', $talismanProject, '--rendering-method', 'gl_compatibility')
    if ($ImportOnly) { $talismanArgs += @('--headless','--editor','--import','--quit') }
    else {
        if ($Headless) { $talismanArgs += '--headless' }
        $talismanArgs += @('res://tscn/debug/TalismanCraftingNativeSmoke.tscn','--', ('--talisman-width=' + $Width), ('--talisman-output=' + $talismanOutput.Replace('\','/')))
    }
    $talismanLog = Join-Path $talismanOutput $(if ($ImportOnly) { 'import.log' } else { 'run.log' })
    $talismanErrorLog = Join-Path $talismanOutput $(if ($ImportOnly) { 'import-errors.log' } else { 'errors.log' })
    # Only the newly created, isolated validation process is bounded/terminated here.
    $talismanProcess = Start-Process -FilePath (Join-Path $talismanEngineRoot 'Godot_spine3_8_editor_console.exe') -ArgumentList $talismanArgs -WorkingDirectory $talismanProject -WindowStyle Hidden -PassThru -RedirectStandardOutput $talismanLog -RedirectStandardError $talismanErrorLog
    if (!$talismanProcess.WaitForExit(60000)) { $talismanProcess.Kill(); throw '符工隔离检查超过一分钟，已停止本次检查进程。' }
    $talismanCode = $talismanProcess.ExitCode
    Get-Content -LiteralPath $talismanLog -Tail 12
    if ((Get-Item -LiteralPath $talismanErrorLog).Length -gt 0) { Get-Content -LiteralPath $talismanErrorLog -Tail 18 }
    if ($talismanCode -ne 0) { throw ('符工检查退出码：' + $talismanCode) }
} finally {
    $env:APPDATA = $talismanOldAppData
    $env:DOTNET_ROOT = $talismanOldDotnet
    $env:DOTNET_ROLL_FORWARD = $talismanOldRollForward
}
