param(
    [string]$GodotPath = "",
    [switch]$HeadlessOnly,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $GodotPath = Join-Path $repoRoot "runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor_console.exe"
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
$resultDirectory = Join-Path $repoRoot ".godot\test-results\condensation_balance"
$appDataPath = Join-Path $resultDirectory "isolated_appdata"
New-Item -ItemType Directory -Path $appDataPath -Force | Out-Null
$previousAppData = $env:APPDATA

function Invoke-CondensationCheck([string]$Name, [string[]]$Arguments, [string]$SuccessMarker) {
    $log = Join-Path $resultDirectory "$Name.log"
    $stdout = Join-Path $resultDirectory "$Name.stdout.log"
    $stderr = Join-Path $resultDirectory "$Name.stderr.log"
    $engineArguments = @('--path', ('"' + $repoRoot + '"'), '--rendering-method', 'gl_compatibility', '--scene', 'res://tools/CondensationBalanceSmoke.tscn') + $Arguments
    $run = Start-Process -FilePath $GodotPath -ArgumentList $engineArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    if (!$run.WaitForExit(60000)) { $run.Kill(); $run.WaitForExit(); throw "Condensation check timed out: $Name" }
    $runExit = $run.ExitCode
    $output = (Get-Content -LiteralPath $stdout -Raw -Encoding utf8) + (Get-Content -LiteralPath $stderr -Raw -Encoding utf8)
    Set-Content -LiteralPath $log -Value $output -Encoding utf8
    $success = Select-String -LiteralPath $log -SimpleMatch $SuccessMarker
    if ($runExit -ne 0 -or !$success -or $output -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') {
        Get-Content -LiteralPath $log -Tail 35 | Write-Output
        throw "Condensation check failed: $Name; exit=$runExit; log=$log"
    }
    $success.Line | Write-Output
}

Push-Location $repoRoot
try {
    $env:APPDATA = $appDataPath
    if (!$SkipBuild) {
        & dotnet build Stacklands.csproj --no-restore --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "Project build failed." }
    }
    & dotnet run --project tools/condensation_balance_audit/CondensationBalanceAudit.csproj -- --checks
    if ($LASTEXITCODE -ne 0) { throw "Condensation rule checks failed." }
    Invoke-CondensationCheck "headless" @("--headless") "CONDENSATION_BALANCE_SMOKE_PASS"
    if (!$HeadlessOnly) {
        Invoke-CondensationCheck "window_1920" @("--windowed", "--resolution", "1920x1080") "CONDENSATION_BALANCE_SMOKE_PASS"
        Invoke-CondensationCheck "window_1280" @("--windowed", "--resolution", "1280x720", "--", "--small-window") "CONDENSATION_BALANCE_SMOKE_PASS"
    }
    Invoke-CondensationCheck "read_saved" @("--headless", "--", "--read-saved") "CONDENSATION_BALANCE_SAVE_READ_PASS"
}
finally {
    if ($null -eq $previousAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $previousAppData }
    Pop-Location
}
