param([switch]$IncludeWindow)
$ErrorActionPreference = 'Stop'
$inventoryRoot = Split-Path -Parent $PSScriptRoot
$inventoryRuntime = Join-Path $inventoryRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$inventoryEngine = Join-Path $inventoryRuntime 'Godot_spine3_8_editor_console.exe'
$inventoryLogs = Join-Path $inventoryRoot 'outputs/inventory_scale_20261104'
New-Item -ItemType Directory -Force -Path $inventoryLogs | Out-Null
$env:DOTNET_ROOT = Join-Path $inventoryRuntime 'dotnet'
$env:DOTNET_ROLL_FORWARD = 'LatestMinor'
$inventoryCases = @(
    @{ Name='ring_final'; Scene='StorageBagInventorySmoke'; Marker='STORAGE_BAG_SMOKE_OK' },
    @{ Name='ring_restart'; Scene='StorageBagInventorySmoke'; Marker='STORAGE_BAG_RESTART_OK'; Args=@('--read-bag-save') },
    @{ Name='spatial_model'; Scene='SpatialInventoryNativeSmoke'; Marker='SPATIAL_NATIVE_MODEL:' },
    @{ Name='inventory_ui'; Scene='InventoryNativeUiSmoke'; Marker='INVENTORY_NATIVE_UI_SMOKE_OK' },
    @{ Name='trade'; Scene='TradeNativeIntegrationSmoke'; Marker='NATIVE_TRADE_INTEGRATION_OK' },
    @{ Name='claim'; Scene='ItemClaimSpatialNativeSmoke'; Marker='ITEM_CLAIM' }
)
if ($IncludeWindow) { $inventoryCases += @{ Name='ring_window_final'; Scene='StorageBagInventorySmoke'; Marker='STORAGE_BAG_SMOKE_OK'; Window=$true } }
foreach ($inventoryCase in $inventoryCases) {
    $inventoryStdout = Join-Path $inventoryLogs ($inventoryCase.Name + '.stdout.log')
    $inventoryStderr = Join-Path $inventoryLogs ($inventoryCase.Name + '.stderr.log')
    $inventoryLog = Join-Path $inventoryLogs ($inventoryCase.Name + '.log')
    $inventoryArgs = @('--path', ('"' + $inventoryRoot + '"'), '--rendering-method', 'gl_compatibility', '--log-file', ('"' + $inventoryLog + '"'), ('res://tools/' + $inventoryCase.Scene + '.tscn'))
    if ($inventoryCase.Window) { $inventoryArgs += @('--rendering-driver','opengl3','--windowed','--resolution','1920x1080') }
    else { $inventoryArgs += '--headless' }
    if ($inventoryCase.Args) { $inventoryArgs += '--'; $inventoryArgs += $inventoryCase.Args }
    $inventoryProcess = Start-Process -FilePath $inventoryEngine -ArgumentList $inventoryArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $inventoryStdout -RedirectStandardError $inventoryStderr
    if (!$inventoryProcess.WaitForExit(45000)) {
        $inventoryProcess.Kill(); $inventoryProcess.WaitForExit(); throw ($inventoryCase.Name + ': timeout; process stopped')
    }
    $inventoryText = (Get-Content -LiteralPath $inventoryStdout -Raw -Encoding utf8) + (Get-Content -LiteralPath $inventoryStderr -Raw -Encoding utf8)
    if ($inventoryProcess.ExitCode -ne 0 -or $inventoryText -notmatch [regex]::Escape($inventoryCase.Marker) -or $inventoryText -match '(?m)^ERROR:|FATAL:') {
        throw ($inventoryCase.Name + ' failed; inspect ' + $inventoryLog)
    }
    ($inventoryText -split '\r?\n' | Where-Object { $_ -match '(?:_OK|checks passed)' }) | Write-Output
    Write-Output ($inventoryCase.Name + ': PASS, warnings=' + [regex]::Matches($inventoryText, '(?m)^WARNING:').Count)
}
Write-Output ('INVENTORY_SCALE_REGRESSION_OK ' + $inventoryCases.Count + ' scenes')
