param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$formationRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$formationEngine = Join-Path $formationRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$formationResults = Join-Path $formationRoot '.godot/test-results/formation_grid'
$formationProfile = Join-Path $formationResults 'isolated_appdata'
New-Item -ItemType Directory -Path $formationProfile -Force | Out-Null
$formationPreviousAppData = $env:APPDATA
try {
    $env:APPDATA = $formationProfile
    if (!$SkipBuild) {
        & dotnet build (Join-Path $formationRoot 'Stacklands.csproj') --no-restore --nologo -v quiet -m:1 -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw 'Formation workbench build failed.' }
    }
    $formationOut = Join-Path $formationResults 'native.stdout.log'
    $formationErr = Join-Path $formationResults 'native.stderr.log'
    $formationArgs = @('--path', ('"' + $formationRoot + '"'), '--rendering-method', 'gl_compatibility', '--windowed', '--resolution', '1280x900', '--scene', 'res://tscn/debug/FormationWorkbenchPreview.tscn', '--', '--formation-smoke')
    $formationRun = Start-Process -FilePath $formationEngine -ArgumentList $formationArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $formationOut -RedirectStandardError $formationErr
    $null = $formationRun.Handle
    if (!$formationRun.WaitForExit(45000)) { $formationRun.Kill(); $formationRun.WaitForExit(); throw 'Formation workbench preview timed out.' }
    $formationText = (Get-Content -LiteralPath $formationOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $formationErr -Raw -Encoding utf8)
    if ($formationRun.ExitCode -ne 0 -or $formationText -notmatch 'FORMATION_WORKBENCH_SMOKE_PASS' -or $formationText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') {
        Write-Output $formationText
        throw 'Formation workbench native checks failed.'
    }
    ($formationText -split "`n" | Where-Object { $_ -match 'FORMATION_WORKBENCH_SMOKE_PASS' }) | Write-Output
}
finally {
    if ($null -eq $formationPreviousAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $formationPreviousAppData }
}
