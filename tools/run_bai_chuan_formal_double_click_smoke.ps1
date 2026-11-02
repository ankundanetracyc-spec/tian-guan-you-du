param(
    [string]$GodotPath = ""
)

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $GodotPath = Join-Path $repoRoot "runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor_console.exe"
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
$runId = Get-Date -Format "yyyyMMdd_HHmmss_fff"
$runFolder = Join-Path $repoRoot "output\bai_chuan_formal_double_click_smoke_$runId"
$appDataPath = Join-Path $runFolder "isolated_appdata"
$logPath = Join-Path $runFolder "run.log"

New-Item -ItemType Directory -Path $appDataPath -Force | Out-Null
$previousAppData = $env:APPDATA
try {
    $env:APPDATA = $appDataPath
    & $GodotPath --path $repoRoot --windowed --resolution 1920x1080 --scene "res://tools/BaiChuanFormalDoubleClickSmoke.tscn" -- "--expected-appdata=$appDataPath" 2>&1 | Tee-Object -FilePath $logPath
    $exitCode = $LASTEXITCODE
}
finally {
    if ($null -eq $previousAppData) {
        Remove-Item Env:APPDATA -ErrorAction SilentlyContinue
    }
    else {
        $env:APPDATA = $previousAppData
    }
}

Write-Host "Smoke log: $logPath"
Write-Host "Isolated user data: $appDataPath"
if ($exitCode -ne 0) {
    throw "Godot double-click smoke failed with exit code $exitCode. See $logPath"
}
