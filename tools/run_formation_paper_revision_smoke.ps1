param([ValidateSet(1920, 1280)][int[]]$Widths = @(1920, 1280))
$ErrorActionPreference = 'Stop'
$formationRevisionRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$formationRevisionEngine = Join-Path $formationRevisionRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$formationRevisionEvidence = Join-Path $formationRevisionRoot 'docs/美术规范/阵法纸墨木石界面_20261109/格线与快捷键修订'
$formationRevisionRuns = Join-Path $formationRevisionRoot '.godot/test-results/formation_paper_revision'
$formationRevisionPreviousAppData = $env:APPDATA
$formationRevisionStamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
try {
    foreach ($formationRevisionWidth in $Widths) {
        $formationRevisionHeight = if ($formationRevisionWidth -eq 1920) { 1080 } else { 720 }
        $formationRevisionProfile = Join-Path $formationRevisionRuns ($formationRevisionStamp + '_' + $formationRevisionWidth + '/isolated_appdata')
        $formationRevisionOutput = Join-Path $formationRevisionEvidence ([string]$formationRevisionWidth)
        New-Item -ItemType Directory -Path $formationRevisionProfile, $formationRevisionOutput -Force | Out-Null
        $env:APPDATA = $formationRevisionProfile
        $formationRevisionStdout = Join-Path $formationRevisionOutput 'native.stdout.log'
        $formationRevisionStderr = Join-Path $formationRevisionOutput 'native.stderr.log'
        $formationRevisionArguments = @(
            '--path', ('"' + $formationRevisionRoot + '"'), '--rendering-method', 'gl_compatibility', '--windowed',
            '--resolution', ($formationRevisionWidth.ToString() + 'x' + $formationRevisionHeight),
            '--scene', 'res://tscn/debug/FormationPaperRevisionSmoke.tscn', '--',
            '--formation-paper-revision-smoke', ('--formation-paper-width=' + $formationRevisionWidth)
        )
        # Uses root's already unified project build. This launcher never builds or imports the project.
        $formationRevisionRun = Start-Process -FilePath $formationRevisionEngine -ArgumentList $formationRevisionArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $formationRevisionStdout -RedirectStandardError $formationRevisionStderr
        $null = $formationRevisionRun.Handle
        if (!$formationRevisionRun.WaitForExit(90000)) {
            $formationRevisionRun.Kill(); $formationRevisionRun.WaitForExit()
            throw ('Formation paper revision native checks timed out: ' + $formationRevisionWidth)
        }
        $formationRevisionText = (Get-Content -LiteralPath $formationRevisionStdout -Raw -Encoding UTF8) + (Get-Content -LiteralPath $formationRevisionStderr -Raw -Encoding UTF8)
        if ($formationRevisionRun.ExitCode -ne 0 -or $formationRevisionText -notmatch 'FORMATION_PAPER_REVISION_PASS' -or $formationRevisionText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') {
            Write-Output $formationRevisionText
            throw ('Formation paper revision native checks failed: ' + $formationRevisionWidth)
        }
        ($formationRevisionText -split "`n" | Where-Object { $_ -match 'FORMATION_PAPER_REVISION_PASS' }) | Write-Output
    }
}
finally {
    if ($null -eq $formationRevisionPreviousAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $formationRevisionPreviousAppData }
}
