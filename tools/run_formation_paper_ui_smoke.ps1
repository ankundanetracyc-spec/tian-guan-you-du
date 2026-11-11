param([ValidateSet(1920, 1280)][int[]]$Widths = @(1920, 1280), [switch]$PreviewOnly)
$ErrorActionPreference = 'Stop'
$formationPaperRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$formationPaperEngine = Join-Path $formationPaperRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$formationPaperEvidence = Join-Path $formationPaperRoot 'docs/美术规范/阵法纸墨木石界面_20261109/格线与快捷键修订/全链兼容'
$formationPaperRuns = Join-Path $formationPaperRoot '.godot/test-results/formation_paper_ui'
$formationPaperPreviousAppData = $env:APPDATA
$formationPaperStamp = Get-Date -Format 'yyyyMMdd_HHmmss_fff'
try {
    foreach ($formationPaperWidth in $Widths) {
        $formationPaperHeight = if ($formationPaperWidth -eq 1920) { 1080 } else { 720 }
        $formationPaperProfile = Join-Path $formationPaperRuns ($formationPaperStamp + '_' + $formationPaperWidth + '/isolated_appdata')
        $formationPaperOutput = Join-Path $formationPaperEvidence ([string]$formationPaperWidth)
        New-Item -ItemType Directory -Path $formationPaperProfile, $formationPaperOutput -Force | Out-Null
        $env:APPDATA = $formationPaperProfile
        $formationPaperStdout = Join-Path $formationPaperOutput 'native.stdout.log'
        $formationPaperStderr = Join-Path $formationPaperOutput 'native.stderr.log'
        $formationPaperArguments = @(
            '--path', ('"' + $formationPaperRoot + '"'), '--rendering-method', 'gl_compatibility', '--windowed',
            '--resolution', ($formationPaperWidth.ToString() + 'x' + $formationPaperHeight),
            '--scene', 'res://tscn/debug/FormationPaperUiSmoke.tscn', '--',
            '--formation-paper-ui-smoke', ('--formation-paper-width=' + $formationPaperWidth)
        )
        if ($PreviewOnly) { $formationPaperArguments += '--formation-paper-preview-only' }
        # Uses root's already unified project build. This launcher never builds or imports the project.
        $formationPaperRun = Start-Process -FilePath $formationPaperEngine -ArgumentList $formationPaperArguments -WindowStyle Hidden -PassThru -RedirectStandardOutput $formationPaperStdout -RedirectStandardError $formationPaperStderr
        $null = $formationPaperRun.Handle
        if (!$formationPaperRun.WaitForExit(90000)) {
            $formationPaperRun.Kill(); $formationPaperRun.WaitForExit()
            throw ('Formation paper native checks timed out: ' + $formationPaperWidth)
        }
        $formationPaperText = (Get-Content -LiteralPath $formationPaperStdout -Raw -Encoding UTF8) + (Get-Content -LiteralPath $formationPaperStderr -Raw -Encoding UTF8)
        if ($formationPaperRun.ExitCode -ne 0 -or $formationPaperText -notmatch 'FORMATION_PAPER_UI_PASS' -or $formationPaperText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):') {
            Write-Output $formationPaperText
            throw ('Formation paper native checks failed: ' + $formationPaperWidth)
        }
        ($formationPaperText -split "`n" | Where-Object { $_ -match 'FORMATION_PAPER_UI_PASS' }) | Write-Output
    }
}
finally {
    if ($null -eq $formationPaperPreviousAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $formationPaperPreviousAppData }
}
