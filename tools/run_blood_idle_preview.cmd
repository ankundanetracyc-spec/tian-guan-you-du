@echo off
setlocal
set "ROOT=%~dp0.."
set "DOTNET_ROOT=%ROOT%\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\dotnet"
set "DOTNET_MULTILEVEL_LOOKUP=0"
set "DOTNET_ROLL_FORWARD=LatestMinor"
"%ROOT%\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor.exe" --path "%ROOT%" --rendering-method gl_compatibility --script res://tools/blood_idle_preview.gd
endlocal
