param(
    [ValidateSet('Preview', 'Capture', 'Verify')][string]$Mode = 'Preview',
    [ValidateSet('core', 'path', 'complete')][string]$Example = 'path',
    [string]$RunDirectory = '',
    [switch]$LargeWindow,
    [switch]$WaitForClose,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$previewRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$previewEngine = Join-Path $previewRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
if (!$RunDirectory) {
    $RunDirectory = Join-Path $previewRoot ('.godot/test-results/realm_presentation_preview/' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff') + '_' + $Mode.ToLowerInvariant() + '_' + $Example)
}
$RunDirectory = [IO.Path]::GetFullPath($RunDirectory)
$previewAppData = Join-Path $RunDirectory 'isolated_appdata'
New-Item -ItemType Directory -Path $previewAppData -Force | Out-Null
$previewOldAppData = $env:APPDATA
Push-Location $previewRoot
try {
    $env:APPDATA = $previewAppData
    if (!$SkipBuild) {
        & dotnet build Stacklands.csproj --no-restore --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw '预览编译失败。' }
    }
    $previewArgs = @('--path', ('"' + $previewRoot + '"'), '--rendering-method', 'gl_compatibility', '--scene', 'res://tools/RealmPresentationPreview.tscn')
    if ($Mode -eq 'Verify') { $previewArgs += '--headless' }
    else { $previewArgs += @('--windowed', '--resolution', $(if ($LargeWindow) { '1920x1080' } else { '1280x720' })) }
    $previewArgs += @('--', ('--example=' + $Example))
    if ($LargeWindow) { $previewArgs += '--large-window' }
    if ($Mode -eq 'Verify') { $previewArgs += '--verify' }
    if ($Mode -eq 'Capture') { $previewArgs += ('"--capture=' + (Join-Path $RunDirectory 'frames') + '"') }
    $previewWindowStyle = if ($Mode -eq 'Preview') { 'Normal' } else { 'Hidden' }
    $previewStdout = Join-Path $RunDirectory 'stdout.log'
    $previewStderr = Join-Path $RunDirectory 'stderr.log'
    $previewProcess = Start-Process -FilePath $previewEngine -ArgumentList $previewArgs -WindowStyle $previewWindowStyle -PassThru -RedirectStandardOutput $previewStdout -RedirectStandardError $previewStderr
    Write-Output ('Preview directory: ' + $RunDirectory)
    if ($Mode -eq 'Preview') {
        Write-Output ('Preview launched: ' + $previewProcess.Id)
        if ($WaitForClose) { while (!$previewProcess.WaitForExit(1000)) {} }
        return
    }
    if (!$previewProcess.WaitForExit(60000)) { $previewProcess.Kill(); $previewProcess.WaitForExit(); throw '预览检查超时。' }
    $previewText = (Get-Content -LiteralPath $previewStdout -Raw -Encoding utf8) + (Get-Content -LiteralPath $previewStderr -Raw -Encoding utf8)
    Set-Content -LiteralPath (Join-Path $RunDirectory 'combined.log') -Value $previewText -Encoding utf8
    $previewMarker = if ($Mode -eq 'Verify') { 'REALM_PRESENTATION_OK:' } else { 'REALM_PRESENTATION_CAPTURE_OK:' }
    if ($previewProcess.ExitCode -ne 0 -or $previewText -notmatch $previewMarker -or $previewText -match '(?m)^(ERROR|SCRIPT ERROR|FATAL):|were leaked|still in use at exit') {
        Write-Output $previewText
        throw '突破演出检查失败，请查看预览日志。'
    }
    ($previewText -split '\r?\n' | Where-Object { $_ -match $previewMarker }) | Write-Output
}
finally {
    if ($null -eq $previewOldAppData) { Remove-Item Env:APPDATA -ErrorAction SilentlyContinue }
    else { $env:APPDATA = $previewOldAppData }
    Pop-Location
}
