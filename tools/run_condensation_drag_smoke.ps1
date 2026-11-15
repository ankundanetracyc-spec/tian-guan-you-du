param([string]$GodotPath = "", [switch]$SkipBuild, [switch]$HeadlessOnly)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $GodotPath = Join-Path $repoRoot "runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor_console.exe"
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
$resultDirectory = Join-Path $repoRoot ".godot\test-results\condensation_drag"
$appDataPath = Join-Path $resultDirectory "isolated_appdata"
New-Item -ItemType Directory -Path $appDataPath -Force | Out-Null
$previousAppData = $env:APPDATA

function Invoke-DragCheck([string]$Name, [string[]]$Arguments) {
    $stdout = Join-Path $resultDirectory "$Name.stdout.log"
    $stderr = Join-Path $resultDirectory "$Name.stderr.log"
    $engineArguments = @('--path', ('"' + $repoRoot + '"'), '--rendering-method', 'gl_compatibility', '--scene', 'res://tools/CondensationDragSmoke.tscn') + $Arguments
    $run = Start-Process -FilePath $GodotPath -ArgumentList $engineArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
    $null = $run.Handle
    if (!$run.WaitForExit(60000)) { $run.Kill(); $run.WaitForExit(); throw "Condensation drag check timed out: $Name" }
    $runExit = $run.ExitCode
    $output = (Get-Content -LiteralPath $stdout -Raw -Encoding utf8) + (Get-Content -LiteralPath $stderr -Raw -Encoding utf8)
    if ($runExit -ne 0 -or $output -notmatch 'CONDENSATION_DRAG_SMOKE_PASS' -or $output -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') {
        Write-Output $output
        throw "Condensation drag check failed: $Name; exit=$runExit"
    }
    ($output -split "`n" | Where-Object { $_ -match 'CONDENSATION_DRAG_SMOKE_PASS' }) | Write-Output
}

Push-Location $repoRoot
try {
    $env:APPDATA = $appDataPath
    if (!$SkipBuild) {
        & dotnet build Stacklands.csproj --no-restore --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "Project build failed." }
    }
    Invoke-DragCheck "headless" @("--headless")
    if (!$HeadlessOnly) {
        Invoke-DragCheck "window_1920" @("--windowed", "--resolution", "1920x1080")
        Invoke-DragCheck "window_1280" @("--windowed", "--resolution", "1280x720", "--", "--small-window")
    }
}
finally {
    if ($null -eq $previousAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $previousAppData }
    Pop-Location
}
