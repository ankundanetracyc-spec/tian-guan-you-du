param(
    [string]$GodotPath = "",
    [string]$RunDirectory = "",
    [ValidateSet("seed", "warmup", "to_yuanxi", "resume", "finish", "verify")]
    [string]$FromStage = "seed",
    [switch]$Windowed,
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if ([string]::IsNullOrWhiteSpace($GodotPath)) {
    $GodotPath = Join-Path $repoRoot "runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor_console.exe"
}
$GodotPath = (Resolve-Path -LiteralPath $GodotPath).Path
if ([string]::IsNullOrWhiteSpace($RunDirectory)) {
    $RunDirectory = Join-Path $repoRoot (".godot\test-results\cloud_seven_formal\run_" + (Get-Date -Format "yyyyMMdd_HHmmss_fff"))
}
$RunDirectory = [IO.Path]::GetFullPath($RunDirectory)
New-Item -ItemType Directory -Path $RunDirectory -Force | Out-Null
$appDataPath = Join-Path $RunDirectory "isolated_appdata"
New-Item -ItemType Directory -Path $appDataPath -Force | Out-Null
$previousAppData = $env:APPDATA
Push-Location $repoRoot
try {
    $env:APPDATA = $appDataPath
    Write-Output "Run directory: $RunDirectory"
    if (!$SkipBuild) {
        & dotnet build Stacklands.csproj --no-restore --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "Project build failed." }
    }
    $stages = @("seed", "warmup", "to_yuanxi", "resume", "finish", "verify")
    $startIndex = [Array]::IndexOf($stages, $FromStage)
    for ($stageIndex = $startIndex; $stageIndex -lt $stages.Count; $stageIndex++) {
        $stage = $stages[$stageIndex]
        $log = Join-Path $RunDirectory "$stage.log"
        $stdout = Join-Path $RunDirectory "$stage.stdout.log"
        $stderr = Join-Path $RunDirectory "$stage.stderr.log"
        $presentation = if ($Windowed) { @("--windowed", "--resolution", "1920x1080") } else { @("--headless") }
        $engineArguments = @("--path", ('"' + $repoRoot + '"'), "--rendering-method", "gl_compatibility",
            "--scene", "res://tools/CloudSevenFormalJourneySmoke.tscn") + $presentation + @("--", "--stage=$stage")
        if ($Windowed) { $engineArguments += "--windowed-run" }
        $stageProcess = Start-Process -FilePath $GodotPath -ArgumentList $engineArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $stdout -RedirectStandardError $stderr
        if (!$stageProcess.WaitForExit(60000)) {
            $stageProcess.Kill(); $stageProcess.WaitForExit()
            throw "Formal journey timed out at $stage."
        }
        $runExit = $stageProcess.ExitCode
        $consoleText = (Get-Content -LiteralPath $stdout -Raw -Encoding utf8) + (Get-Content -LiteralPath $stderr -Raw -Encoding utf8)
        Set-Content -LiteralPath $log -Value $consoleText -Encoding utf8
        $markers = Select-String -LiteralPath $log -Pattern "CLOUD_SEVEN_"
        $markers.Line | Write-Output
        $runtimeFailures = Select-String -LiteralPath $log -Pattern '(^|\s)ERROR:|SCRIPT ERROR:|were leaked|still in use at exit'
        if ($runExit -ne 0 -or $runtimeFailures -or !(Select-String -LiteralPath $log -SimpleMatch "CLOUD_SEVEN_STAGE_PASS: $stage;")) {
            Get-Content -LiteralPath $log -Tail 18 | Write-Output
            throw "Formal journey failed at $stage; exit=$runExit; log=$log"
        }
    }
}
finally {
    if ($null -eq $previousAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $previousAppData }
    Pop-Location
}
