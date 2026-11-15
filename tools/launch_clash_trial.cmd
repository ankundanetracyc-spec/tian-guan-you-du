@echo off
setlocal
set "GAME_ROOT=%~dp0.."
if not exist "%GAME_ROOT%\tmp" mkdir "%GAME_ROOT%\tmp"
start "" "%GAME_ROOT%\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor_console.exe" --path "%GAME_ROOT%" --log-file "%GAME_ROOT%\tmp\combat_clash_trial.log" --windowed --resolution 1280x720 --rendering-method gl_compatibility --scene "res://tscn/scene/CombatClashTrial.tscn"
