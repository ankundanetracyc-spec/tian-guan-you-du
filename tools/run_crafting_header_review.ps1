param(
    [Parameter(Mandatory)][ValidateSet('alchemy','talisman','forging')][string]$Page,
    [ValidateSet(1280,1920)][int]$Width=1280,
    [switch]$FullTalisman,
    [ValidatePattern('^[a-z0-9_]+$')][string]$TagSuffix='final'
)
$ErrorActionPreference='Stop'
$columnsRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$columnsEngine=Join-Path $columnsRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$columnsTag="${Page}_header_${TagSuffix}_$Width" + $(if($FullTalisman){'_full'}else{''})
$columnsOutput=Join-Path $columnsRoot "output/$columnsTag"
$columnsArgs=@('--path',$columnsRoot,'--rendering-method','mobile','--rendering-driver','vulkan','--windowed')
$columnsUserArgs=@()
switch($Page){
    'alchemy' {
        $columnsArgs+=@('--scene','res://tools/NativePaperCaptureHost.tscn')
        $columnsUserArgs=@('--native-paper-scene=res://tools/AlchemyPaperUiSmoke.tscn',"--native-paper-width=$Width","--paper-width=$Width","--paper-output=res://output/$columnsTag")
        $columnsPass='ALCHEMY_PAPER_UI_PASS'
    }
    'talisman' {
        $columnsArgs+=@('--scene','res://tools/NativePaperCaptureHost.tscn')
        $columnsUserArgs=@('--native-paper-scene=res://tscn/debug/TalismanCraftingNativeSmoke.tscn',"--native-paper-width=$Width","--talisman-width=$Width","--talisman-output=res://output/$columnsTag")
        if(-not $FullTalisman){$columnsUserArgs+='--talisman-header-only'}
        $columnsPass='TALISMAN_NATIVE_PASS'
    }
    'forging' {
        $columnsArgs+=@('--scene','res://tools/ForgingPaperUiSmoke.tscn')
        $columnsUserArgs=@("--forge-paper-width=$Width","--forge-paper-output=res://output/$columnsTag")
        $columnsPass='FORGE_PAPER_PASS'
    }
}
$columnsArgs+=@('--')+$columnsUserArgs
$columnsStdout=Join-Path $columnsRoot "output/$columnsTag.stdout.log"
$columnsStderr=Join-Path $columnsRoot "output/$columnsTag.stderr.log"
$columnsLog=Join-Path $columnsRoot "output/$columnsTag.log"
$columnsPreviousAppData=$env:APPDATA
try {
    $env:APPDATA=Join-Path $columnsRoot "output/${columnsTag}_profile"
    New-Item -ItemType Directory -Force -Path $columnsOutput | Out-Null
    # Wait for the Windows launcher and its game process before checking output.
    $columnsProcess=Start-Process -FilePath $columnsEngine -ArgumentList $columnsArgs -Wait -PassThru -WindowStyle Hidden -RedirectStandardOutput $columnsStdout -RedirectStandardError $columnsStderr
    $columnsText=[System.IO.File]::ReadAllText($columnsStdout)+[Environment]::NewLine+[System.IO.File]::ReadAllText($columnsStderr)
    [System.IO.File]::WriteAllText($columnsLog,$columnsText)
    Write-Output ("$columnsTag exit="+$columnsProcess.ExitCode)
    Select-String -LiteralPath $columnsLog -Pattern '_PASS','_FAIL','ERROR:'
    if($columnsProcess.ExitCode -ne 0){exit $columnsProcess.ExitCode}
    if($columnsText -notmatch $columnsPass -or $columnsText -match '(?m)^ERROR:|Fatal error\.') {exit 1}
} finally {$env:APPDATA=$columnsPreviousAppData}
