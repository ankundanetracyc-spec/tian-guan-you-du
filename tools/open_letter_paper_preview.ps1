param([ValidateSet(1280,1600,1920)][int]$Width=1600, [switch]$Empty)
$ErrorActionPreference='Stop'
$lettersProjectRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$lettersEngine=Join-Path $lettersProjectRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$previousAppData=$env:APPDATA
try {
    $env:APPDATA=Join-Path $lettersProjectRoot 'output/letters_paper_interactive_profile'
    $lettersArgs=@('--path',$lettersProjectRoot,'--resolution',"${Width}x$([int]($Width*9/16))",'--windowed','--scene','res://tscn/scene/LetterPaperPreview.tscn')
    if($Empty){$lettersArgs+=@('--','--letters-empty')}
    & $lettersEngine @lettersArgs
} finally { $env:APPDATA=$previousAppData }
