param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$guardCheckRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$guardCheckEngine = Join-Path $guardCheckRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$guardCheckResults = Join-Path $guardCheckRoot '.godot/test-results/formation_guard_runtime'
$guardCheckProfile = Join-Path $guardCheckResults 'isolated_appdata'
New-Item -ItemType Directory -Path $guardCheckProfile -Force | Out-Null
$guardCheckPreviousAppData = $env:APPDATA
try {
    $env:APPDATA = $guardCheckProfile
    if (!$SkipBuild) {
        & dotnet build (Join-Path $guardCheckRoot 'Stacklands.csproj') --no-restore --nologo -v quiet -m:1 -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw 'Formation guard runtime checks build failed.' }
    }
    if (!(Test-Path -LiteralPath $guardCheckEngine)) { throw 'Configured Godot engine was not found.' }
    $guardCheckOut = Join-Path $guardCheckResults 'native.stdout.log'
    $guardCheckErr = Join-Path $guardCheckResults 'native.stderr.log'
    $guardCheckArgs = @('--path', ('"' + $guardCheckRoot + '"'), '--rendering-method', 'gl_compatibility', '--windowed', '--resolution', '720x480', '--scene', 'res://tscn/debug/FormationGuardRuntimeChecks.tscn', '--', '--formation-guard-runtime-checks')
    $guardCheckProcess = Start-Process -FilePath $guardCheckEngine -ArgumentList $guardCheckArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $guardCheckOut -RedirectStandardError $guardCheckErr
    $null = $guardCheckProcess.Handle
    if (!$guardCheckProcess.WaitForExit(45000)) { $guardCheckProcess.Kill(); $guardCheckProcess.WaitForExit(); throw 'Formation guard runtime checks timed out.' }
    $guardCheckText = (Get-Content -LiteralPath $guardCheckOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $guardCheckErr -Raw -Encoding utf8)
    if ($guardCheckProcess.ExitCode -ne 0 -or $guardCheckText -notmatch 'FORMATION_GUARD_RUNTIME_CHECKS_PASS' -or $guardCheckText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') {
        Write-Output $guardCheckText
        throw 'Formation guard runtime checks failed.'
    }
    ($guardCheckText -split "`n" | Where-Object { $_ -match 'FORMATION_GUARD_RUNTIME_(CHECKS_PASS|GROUP_PASS)' }) | Write-Output
}
finally {
    if ($null -eq $guardCheckPreviousAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $guardCheckPreviousAppData }
}
