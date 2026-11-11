param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$logicCheckRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$logicCheckEngine = Join-Path $logicCheckRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$logicCheckResults = Join-Path $logicCheckRoot '.godot/test-results/formation_logic_runtime'
$logicCheckProfile = Join-Path $logicCheckResults 'isolated_appdata'
New-Item -ItemType Directory -Path $logicCheckProfile -Force | Out-Null
$logicCheckPreviousAppData = $env:APPDATA
try {
    $env:APPDATA = $logicCheckProfile
    if (!$SkipBuild) {
        & dotnet build (Join-Path $logicCheckRoot 'Stacklands.csproj') --no-restore --nologo -v quiet -m:1 -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw 'Formation logic runtime checks build failed.' }
    }
    if (!(Test-Path -LiteralPath $logicCheckEngine)) { throw 'Configured Godot engine was not found.' }
    $logicCheckOut = Join-Path $logicCheckResults 'native.stdout.log'
    $logicCheckErr = Join-Path $logicCheckResults 'native.stderr.log'
    $logicCheckArgs = @('--path', ('"' + $logicCheckRoot + '"'), '--rendering-method', 'gl_compatibility', '--windowed', '--resolution', '720x480', '--scene', 'res://tscn/debug/FormationLogicRuntimeChecks.tscn', '--', '--formation-logic-runtime-checks')
    $logicCheckProcess = Start-Process -FilePath $logicCheckEngine -ArgumentList $logicCheckArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $logicCheckOut -RedirectStandardError $logicCheckErr
    $null = $logicCheckProcess.Handle
    if (!$logicCheckProcess.WaitForExit(45000)) { $logicCheckProcess.Kill(); $logicCheckProcess.WaitForExit(); throw 'Formation logic runtime checks timed out.' }
    $logicCheckText = (Get-Content -LiteralPath $logicCheckOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $logicCheckErr -Raw -Encoding utf8)
    if ($logicCheckProcess.ExitCode -ne 0 -or $logicCheckText -notmatch 'FORMATION_LOGIC_RUNTIME_CHECKS_PASS' -or $logicCheckText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') {
        Write-Output $logicCheckText
        throw 'Formation logic runtime checks failed.'
    }
    ($logicCheckText -split "`n" | Where-Object { $_ -match 'FORMATION_LOGIC_RUNTIME_(CHECKS_PASS|GROUP_PASS)' }) | Write-Output
}
finally {
    if ($null -eq $logicCheckPreviousAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $logicCheckPreviousAppData }
}
