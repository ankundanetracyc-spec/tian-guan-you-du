@echo off
setlocal
set "PAIR_PREVIEW_ENGINE=%~dp0..\runtime\godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe"
start "" "%PAIR_PREVIEW_ENGINE%" --path "%~dp0.." --rendering-method gl_compatibility --rendering-driver opengl3 --log-file "%~dp0..\output\turn_based_pair_live_20261106.log" "res://tools/TurnBasedPairAvatarReview.tscn"
exit /b
