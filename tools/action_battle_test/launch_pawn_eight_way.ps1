$projectRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$godotPath = Join-Path $projectRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor.exe'
Start-Process -FilePath $godotPath -ArgumentList @('--path', $projectRoot, '--position', '80,60', 'res://tscn/debug/PawnEightWayPlayground.tscn') -WorkingDirectory $projectRoot -WindowStyle Normal
