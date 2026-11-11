param([switch]$IncludeWindow, [string]$FromCase = '')
$ErrorActionPreference = 'Stop'
$gridRoot = Split-Path -Parent $PSScriptRoot
$gridRuntime = Join-Path $gridRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$gridEngine = Join-Path $gridRuntime 'Godot_spine3_8_editor_console.exe'
$gridLogs = Join-Path $gridRoot 'output/inventory_capacity200_20261107'
New-Item -ItemType Directory -Force -Path $gridLogs | Out-Null
$gridSaveRoot = Join-Path $gridLogs 'manual_library_appdata'
New-Item -ItemType Directory -Force -Path $gridSaveRoot | Out-Null
$env:APPDATA = $gridSaveRoot
$env:DOTNET_ROOT = Join-Path $gridRuntime 'dotnet'
$env:DOTNET_ROLL_FORWARD = 'LatestMinor'
$env:STACKLANDS_GRID_CAPTURE_ROOT = 'res://docs/美术规范/行囊原面积200格扩容_20261107/实机'
$gridCases = @(
    @{ Name='model'; Scene='SpatialInventoryNativeSmoke'; Marker='SPATIAL_NATIVE_MODEL:' },
    @{ Name='inventory'; Scene='InventoryNativeUiSmoke'; Marker='INVENTORY_NATIVE_UI_SMOKE_OK' },
    @{ Name='bag'; Scene='StorageBagInventorySmoke'; Marker='STORAGE_BAG_SMOKE_OK' },
    @{ Name='claim'; Scene='ItemClaimSpatialNativeSmoke'; Marker='ITEM_CLAIM' },
    @{ Name='trade'; Scene='TradeNativeIntegrationSmoke'; Marker='NATIVE_TRADE_INTEGRATION_OK' },
    @{ Name='loot'; Scene='WorldLootCapacitySmoke'; Marker='WORLD_LOOT_CAPACITY_SMOKE_OK' },
    @{ Name='exchange'; Scene='YuxuExchangePurchaseSmoke'; Marker='YUXU_EXCHANGE_PURCHASE_OK' },
    @{ Name='manual'; Scene='YuxuManualLibrarySmoke'; Marker='YUXU_MANUAL_LIBRARY_OK' },
    @{ Name='journey'; Scene='InventoryJourneyEntrySmoke'; Marker='InventoryJourneyEntrySmoke:' }
)
if ($IncludeWindow) {
    $gridCases += @{ Name='inventory_window'; Scene='InventoryNativeUiSmoke'; Marker='INVENTORY_NATIVE_UI_SMOKE_OK'; Window=$true }
    $gridCases += @{ Name='ring_window'; Scene='StorageBagInventorySmoke'; Marker='STORAGE_BAG_SMOKE_OK'; Window=$true }
    $gridCases += @{ Name='claim_window'; Scene='ItemClaimSpatialNativeSmoke'; Marker='ITEM_CLAIM'; Window=$true }
}
if ($FromCase.Length -gt 0) {
    $gridStarted = $false
    $gridCases = @($gridCases | Where-Object { if ($_.Name -eq $FromCase) { $gridStarted = $true }; $gridStarted })
    if ($gridCases.Count -eq 0) { throw ('Unknown case: ' + $FromCase) }
}
foreach ($gridCase in $gridCases) {
    $gridOut = Join-Path $gridLogs ($gridCase.Name + '.stdout.log')
    $gridErr = Join-Path $gridLogs ($gridCase.Name + '.stderr.log')
    $gridLog = Join-Path $gridLogs ($gridCase.Name + '.log')
    $gridArgs = @('--path', ('"' + $gridRoot + '"'), '--rendering-method','gl_compatibility', '--log-file', ('"' + $gridLog + '"'), ('res://tools/' + $gridCase.Scene + '.tscn'))
    if ($gridCase.Window) { $gridArgs += @('--rendering-driver','opengl3','--windowed','--resolution','1920x1080') }
    else { $gridArgs += '--headless' }
    $gridProcess = Start-Process -FilePath $gridEngine -ArgumentList $gridArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $gridOut -RedirectStandardError $gridErr
    $null = $gridProcess.Handle
    if (!$gridProcess.WaitForExit(45000)) { $gridProcess.Kill(); $gridProcess.WaitForExit(); throw ($gridCase.Name + ': timeout') }
    $gridProcess.Refresh()
    $gridText = (Get-Content -LiteralPath $gridOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $gridErr -Raw -Encoding utf8)
    if ($gridProcess.ExitCode -ne 0 -or $gridText -notmatch [regex]::Escape($gridCase.Marker) -or $gridText -match '(?m)^ERROR:|FATAL:') {
        throw ($gridCase.Name + ': failed; exit=' + $gridProcess.ExitCode + '; inspect ' + $gridLog)
    }
    ($gridText -split '\r?\n' | Where-Object { $_ -match '(?:_OK|checks passed|SPATIAL_NATIVE_MODEL:)' }) | Write-Output
    Write-Output ($gridCase.Name + ': PASS')
}
Write-Output ('INVENTORY_CAPACITY200_REGRESSION_OK ' + $gridCases.Count + ' scenes')
