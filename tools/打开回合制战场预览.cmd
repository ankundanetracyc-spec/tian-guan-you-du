@echo off
setlocal
set "STAGE_PREVIEW_ENGINE=%~dp0..\runtime\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
start "" "%STAGE_PREVIEW_ENGINE%" --path "%~dp0action_battle_test" --rendering-method gl_compatibility --rendering-driver opengl3 "res://turn_based_stage_preview_20261105.tscn"
exit /b
