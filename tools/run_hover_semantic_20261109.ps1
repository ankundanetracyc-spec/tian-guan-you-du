param([switch]$SkillOnly)
$ErrorActionPreference = 'Stop'
$semanticRoot = Split-Path -Parent $PSScriptRoot
$semanticRuntime = Join-Path $semanticRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64'
$semanticEngine = Join-Path $semanticRuntime 'Godot_spine3_8_editor_console.exe'
$semanticLogs = Join-Path $semanticRoot 'tmp/hover_semantic_20261109'
New-Item -ItemType Directory -Force -Path $semanticLogs | Out-Null
$env:DOTNET_ROOT = Join-Path $semanticRuntime 'dotnet'
$env:DOTNET_ROLL_FORWARD = 'LatestMinor'
$semanticCases = @(
    @{ Name='skill_1280x720'; Scene='SkillHoverUnifiedPreview'; Size='1280x720'; Extra=@(); Marker='UNIFIED_SKILL_HOVER_OK:' },
    @{ Name='skill_1920x1080'; Scene='SkillHoverUnifiedPreview'; Size='1920x1080'; Extra=@('--large'); Marker='UNIFIED_SKILL_HOVER_OK:' }
)
if (!$SkillOnly) {
    $semanticCases += @(
        @{ Name='attribute'; Scene='AttributeHoverLayoutPreview'; Size='1672x941'; Extra=@(); Marker='ATTRIBUTE_HOVER_PREVIEW_OK' },
        @{ Name='information'; Scene='InformationPanelSmoke'; Size='1672x941'; Extra=@(); Marker='INFORMATION_PANEL_SMOKE_OK' }
    )
}
foreach ($semanticCase in $semanticCases) {
    $semanticOut = Join-Path $semanticLogs ($semanticCase.Name + '.stdout.log')
    $semanticErr = Join-Path $semanticLogs ($semanticCase.Name + '.stderr.log')
    $semanticLog = Join-Path $semanticLogs ($semanticCase.Name + '.log')
    $semanticArgs = @('--path', ('"' + $semanticRoot + '"'), '--rendering-method', 'gl_compatibility', '--rendering-driver', 'opengl3',
        '--windowed', '--resolution', $semanticCase.Size, '--log-file', ('"' + $semanticLog + '"'),
        ('res://tools/' + $semanticCase.Scene + '.tscn'), '--') + $semanticCase.Extra
    $semanticProcess = Start-Process -FilePath $semanticEngine -ArgumentList $semanticArgs -WindowStyle Hidden -PassThru -RedirectStandardOutput $semanticOut -RedirectStandardError $semanticErr
    $semanticDeadline = [DateTime]::UtcNow.AddSeconds(100)
    while (!$semanticProcess.WaitForExit(1000)) {
        if ([DateTime]::UtcNow -gt $semanticDeadline) {
            $semanticProcess.Kill(); $semanticProcess.WaitForExit()
            throw ('Hover semantic timeout: ' + $semanticCase.Name)
        }
    }
    $semanticText = (Get-Content -LiteralPath $semanticOut -Raw -Encoding utf8) + (Get-Content -LiteralPath $semanticErr -Raw -Encoding utf8)
    if ($semanticProcess.ExitCode -ne 0 -or !$semanticText.Contains($semanticCase.Marker) -or $semanticText -match '(?m)^ERROR:|FATAL:') {
        throw ('Hover semantic failed: ' + $semanticCase.Name + '; inspect ' + $semanticOut + ' and ' + $semanticErr)
    }
    ($semanticText -split '\r?\n' | Where-Object { $_ -match $semanticCase.Marker }) | Write-Output
}
