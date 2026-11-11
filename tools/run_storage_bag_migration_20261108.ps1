param([switch]$IncludeWindow)
$ErrorActionPreference = 'Stop'
$bagRoot = Split-Path -Parent $PSScriptRoot
$bagRuntime = Join-Path $bagRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$bagEngine = Join-Path $bagRuntime 'Godot_spine3_8_editor_console.exe'
$bagLogs = Join-Path $bagRoot 'output/storage_bag_20261108'
New-Item -ItemType Directory -Force -Path $bagLogs | Out-Null
$env:APPDATA = Join-Path $bagLogs 'isolated_appdata'
New-Item -ItemType Directory -Force -Path $env:APPDATA | Out-Null
$env:DOTNET_ROOT = Join-Path $bagRuntime 'dotnet'
$env:DOTNET_ROLL_FORWARD = 'LatestMinor'
$env:STACKLANDS_GRID_CAPTURE_ROOT = 'res://docs/美术规范/储物袋迁移与四格图标_20261108/实机'
$bagCases = @(
    @{ Name='bag'; Scene='StorageBagInventorySmoke'; Marker='STORAGE_BAG_SMOKE_OK' },
    @{ Name='restart'; Scene='StorageBagInventorySmoke'; Marker='STORAGE_BAG_RESTART_OK'; Args=@('--read-bag-save') },
    @{ Name='spatial'; Scene='SpatialInventoryNativeSmoke'; Marker='SPATIAL_NATIVE_MODEL:' },
    @{ Name='inventory'; Scene='InventoryNativeUiSmoke'; Marker='INVENTORY_NATIVE_UI_SMOKE_OK' },
    @{ Name='trade'; Scene='TradeNativeIntegrationSmoke'; Marker='NATIVE_TRADE_INTEGRATION_OK' },
    @{ Name='claim'; Scene='ItemClaimSpatialNativeSmoke'; Marker='ITEM_CLAIM' }
)
if ($IncludeWindow) { $bagCases += @{ Name='bag_window'; Scene='StorageBagInventorySmoke'; Marker='STORAGE_BAG_SMOKE_OK'; Window=$true } }
foreach ($bagCase in $bagCases) {
    $bagOut = Join-Path $bagLogs ($bagCase.Name + '.stdout.log')
    $bagErr = Join-Path $bagLogs ($bagCase.Name + '.stderr.log')
    $bagLog = Join-Path $bagLogs ($bagCase.Name + '.log')
    $bagArgs = @('--path', ('"' + $bagRoot + '"'), '--rendering-method', 'gl_compatibility', '--log-file', ('"' + $bagLog + '"'), ('res://tools/' + $bagCase.Scene + '.tscn'))
    if ($bagCase.Window) { $bagArgs += @('--rendering-driver', 'opengl3', '--windowed', '--resolution', '1920x1080') }
    else { $bagArgs += '--headless' }
    if ($bagCase.Args) { $bagArgs += '--'; $bagArgs += $bagCase.Args }
    $bagProcess = Start-Process -FilePath $bagEngine -ArgumentList $bagArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $bagOut -RedirectStandardError $bagErr
    $null = $bagProcess.Handle
    if (!$bagProcess.WaitForExit(45000)) { $bagProcess.Kill(); $bagProcess.WaitForExit(); throw ($bagCase.Name + ': timeout') }
    $bagProcess.Refresh()
    $bagText = (Get-Content -LiteralPath $bagOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $bagErr -Raw -Encoding utf8)
    if ($bagProcess.ExitCode -ne 0 -or $bagText -notmatch [regex]::Escape($bagCase.Marker) -or $bagText -match '(?m)^ERROR:|FATAL:') {
        throw ($bagCase.Name + ': failed; exit=' + $bagProcess.ExitCode + '; inspect ' + $bagLog)
    }
    ($bagText -split '\r?\n' | Where-Object { $_ -match '(?:_OK|checks passed|SPATIAL_NATIVE_MODEL:)' }) | Write-Output
    Write-Output ($bagCase.Name + ': PASS')
}
Write-Output ('STORAGE_BAG_MIGRATION_REGRESSION_OK ' + $bagCases.Count + ' scenes')
