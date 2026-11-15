param([string]$OutputDirectory = '', [switch]$WindowedLifecycle)
$ErrorActionPreference = 'Stop'
$forgingRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $forgingRoot 'tmp/forging_battle_fix_20261104/final' }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$forgingRuntime = Join-Path $forgingRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$forgingEngine = Join-Path $forgingRuntime 'Godot_spine3_8_editor_console.exe'
$env:DOTNET_ROOT = Join-Path $forgingRuntime 'dotnet'
$env:DOTNET_ROLL_FORWARD = 'LatestMinor'
Push-Location $forgingRoot
try {
    & dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
    $forgingCases = @(
        @{ Name='flow'; Scene='ForgingBattleFlowSmoke'; Marker='FORGING_BATTLE_FLOW_OK'; Args=@() },
        @{ Name='restart'; Scene='ForgingBattleFlowSmoke'; Marker='FORGING_BATTLE_FLOW_RESTART_OK'; Args=@('--read-forged-flow') },
        @{ Name='crafting'; Scene='CraftingIntegrationSmoke'; Marker='CRAFTING_INTEGRATION_OK'; Args=@() },
        @{ Name='adapter'; Scene='ForgedSwordAdapterSmoke'; Marker='FORGED_SWORD_ADAPTER_OK'; Args=@() },
        @{ Name='material'; Scene='ForgingMaterialDiscoverySmoke'; Marker='FORGING_MATERIAL_DISCOVERY_OK'; Args=@() },
        @{ Name='bonds'; Scene='ArtifactBondSmoke'; Marker='ARTIFACT_BOND_OK'; Args=@() },
        @{ Name='runtime'; Scene='ArtifactBattleRuntimeSmoke'; Marker='ARTIFACT_BATTLE_RUNTIME_SMOKE:'; Args=@() },
        @{ Name='natural_gc'; Scene='ForgingResourceLifetimeSmoke'; Marker='FORGING_RESOURCE_LIFETIME_OK'; Args=@('--natural-gc') },
        @{ Name='forced_gc'; Scene='ForgingResourceLifetimeSmoke'; Marker='FORGING_RESOURCE_LIFETIME_OK'; Args=@() },
        @{ Name='forge_exit'; Scene='SceneResourceLifecycleSmoke'; Marker='SCENE_RESOURCE_LIFECYCLE_OK forge'; Args=@('--mode=forge') },
        @{ Name='battle_exit'; Scene='SceneResourceLifecycleSmoke'; Marker='SCENE_RESOURCE_LIFECYCLE_OK battle'; Args=@('--mode=battle') },
        @{ Name='mixed_exit'; Scene='SceneResourceLifecycleSmoke'; Marker='SCENE_RESOURCE_LIFECYCLE_OK mixed'; Args=@('--mode=mixed') }
    )
    if ($WindowedLifecycle) {
        $forgingCases += @{ Name='window_exit'; Scene='SceneResourceLifecycleSmoke'; Marker='SCENE_RESOURCE_LIFECYCLE_OK mixed'; Args=@('--mode=mixed','--require-renderer'); Window=$true }
    }
    foreach ($forgingCase in $forgingCases) {
        $forgingStdout = Join-Path $OutputDirectory ($forgingCase.Name + '.stdout.log')
        $forgingStderr = Join-Path $OutputDirectory ($forgingCase.Name + '.stderr.log')
        $forgingLog = Join-Path $OutputDirectory ($forgingCase.Name + '.log')
        $forgingArgs = @('--path',('"' + $forgingRoot + '"'),'--rendering-method','gl_compatibility',
            '--log-file',('"' + $forgingLog + '"'),('res://tools/' + $forgingCase.Scene + '.tscn'))
        if ($forgingCase.Window) { $forgingArgs += @('--rendering-driver','opengl3','--windowed','--resolution','1280x720') }
        else { $forgingArgs += '--headless' }
        if ($forgingCase.Args.Count) { $forgingArgs += '--'; $forgingArgs += $forgingCase.Args }
        $forgingProcess = Start-Process -FilePath $forgingEngine -ArgumentList $forgingArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $forgingStdout -RedirectStandardError $forgingStderr
        if (!$forgingProcess.WaitForExit(45000)) {
            $forgingProcess.Kill(); $forgingProcess.WaitForExit(); throw ($forgingCase.Name + ' 超时；进程已停止。')
        }
        $forgingText = (Get-Content -LiteralPath $forgingStdout -Raw -Encoding utf8) + (Get-Content -LiteralPath $forgingStderr -Raw -Encoding utf8)
        if ($forgingProcess.ExitCode -ne 0 -or $forgingText -notmatch [regex]::Escape($forgingCase.Marker) -or $forgingText -match '(?m)^ERROR:|FATAL:|(?:RIDs|ObjectDB instances).*leaked|System\.(?:Exception|InvalidOperationException|ArgumentException):') {
            throw ($forgingCase.Name + ' 失败，请查看 ' + $forgingLog)
        }
        $forgingMarker = ($forgingText -split '\r?\n' | Where-Object { $_.StartsWith($forgingCase.Marker) }) -join '; '
        Write-Output $forgingMarker
        # 纹理、字体和对象退出泄漏均使回归失败；其他警告继续原样报告。
        $forgingWarnings = [regex]::Matches($forgingText, '(?m)^(?:ERROR|WARNING):').Count
        if ($forgingWarnings) { Write-Output ($forgingCase.Name + ': 保留 ' + $forgingWarnings + ' 条资源或退出告警，详见日志。') }
    }
    Write-Output ('FORGING_BATTLE_REGRESSION_OK ' + $forgingCases.Count + ' scenes')
}
finally { Pop-Location }
