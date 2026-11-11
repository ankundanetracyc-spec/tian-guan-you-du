param([ValidateSet(1280,1600,1920)][int]$Width=1600)
$ErrorActionPreference='Stop'
$npcPaperProjectRoot=(Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$npcPaperEngine=Join-Path $npcPaperProjectRoot 'runtime/godot/Godot_v4.7.2-stable_spine3_8_mono_win64/Godot_spine3_8_editor_console.exe'
$previousAppData=$env:APPDATA
try {
    $env:APPDATA=Join-Path $npcPaperProjectRoot 'output/npc_paper_interactive_profile'
    & $npcPaperEngine --path $npcPaperProjectRoot --resolution "${Width}x$([int]($Width*9/16))" --windowed --scene 'res://tscn/scene/NpcInteractionPaperPreview.tscn'
} finally { $env:APPDATA=$previousAppData }
