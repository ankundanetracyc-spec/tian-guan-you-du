$pawnProjectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$pawnGodot = Join-Path $pawnProjectRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor.exe'
Start-Process -FilePath $pawnGodot -ArgumentList @('--path', $pawnProjectRoot, '--position', '80,60', 'res://tscn/debug/PawnTwoViewPlayground.tscn') -WorkingDirectory $pawnProjectRoot -WindowStyle Normal
