param([ValidateSet(1920, 1280)][int[]]$Widths = @(1920, 1280), [switch]$PreviewOnly)
$ErrorActionPreference = 'Stop'
$formationFreeBuildRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$formationFreeBuildEngine = Join-Path $formationFreeBuildRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$formationFreeBuildEvidence = Join-Path $formationFreeBuildRoot 'docs/美术规范/五格复杂阵盘自由构筑_20261109'
$formationFreeBuildRuns = Join-Path $formationFreeBuildRoot '.godot/test-results/formation_freebuild_ui'
$formationFreeBuildPreviousAppData = $env:APPDATA
$formationFreeBuildStamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
try {
    foreach ($formationFreeBuildWidth in $Widths) {
        $formationFreeBuildHeight = if ($formationFreeBuildWidth -eq 1920) { 1080 } else { 720 }
        $formationFreeBuildProfile = Join-Path $formationFreeBuildRuns ($formationFreeBuildStamp + '_' + $formationFreeBuildWidth + '/isolated_appdata')
        $formationFreeBuildOutput = Join-Path $formationFreeBuildEvidence ([string]$formationFreeBuildWidth)
        New-Item -ItemType Directory -Path $formationFreeBuildProfile, $formationFreeBuildOutput -Force | Out-Null
        $env:APPDATA = $formationFreeBuildProfile
        $formationFreeBuildStdout = Join-Path $formationFreeBuildOutput 'native.stdout.log'
        $formationFreeBuildStderr = Join-Path $formationFreeBuildOutput 'native.stderr.log'
        $formationFreeBuildArguments = @(
            '--path', ('"' + $formationFreeBuildRoot + '"'), '--rendering-method', 'gl_compatibility', '--windowed',
            '--resolution', ($formationFreeBuildWidth.ToString() + 'x' + $formationFreeBuildHeight),
            '--scene', 'res://tscn/debug/FormationFreeBuildUiSmoke.tscn', '--',
            '--formation-freebuild-ui-smoke', ('--formation-freebuild-width=' + $formationFreeBuildWidth)
        )
        if ($PreviewOnly) { $formationFreeBuildArguments += '--formation-freebuild-preview-only' }
        # Uses root's already unified project build. This launcher never builds or imports the project.
        $formationFreeBuildRun = Start-Process -FilePath $formationFreeBuildEngine -ArgumentList $formationFreeBuildArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $formationFreeBuildStdout -RedirectStandardError $formationFreeBuildStderr
        $null = $formationFreeBuildRun.Handle
        if (!$formationFreeBuildRun.WaitForExit(90000)) {
            $formationFreeBuildRun.Kill(); $formationFreeBuildRun.WaitForExit()
            throw ('Formation free build native checks timed out: ' + $formationFreeBuildWidth)
        }
        $formationFreeBuildText = (Get-Content -LiteralPath $formationFreeBuildStdout -Raw -Encoding UTF8) + (Get-Content -LiteralPath $formationFreeBuildStderr -Raw -Encoding UTF8)
        if ($formationFreeBuildRun.ExitCode -ne 0 -or $formationFreeBuildText -notmatch 'FORMATION_FREEBUILD_UI_PASS' -or $formationFreeBuildText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') {
            Write-Output $formationFreeBuildText
            throw ('Formation free build native checks failed: ' + $formationFreeBuildWidth)
        }
        ($formationFreeBuildText -split "`n" | Where-Object { $_ -match 'FORMATION_FREEBUILD_UI_PASS' }) | Write-Output
    }
}
finally {
    if ($null -eq $formationFreeBuildPreviousAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $formationFreeBuildPreviousAppData }
}
