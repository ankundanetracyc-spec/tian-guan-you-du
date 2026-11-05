@echo off
setlocal
set "PROJECT_ROOT=%~dp0.."
set "GODOT_DIR=%PROJECT_ROOT%\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64"
set "DOTNET_ROOT=%GODOT_DIR%\dotnet"
set "DOTNET_ROLL_FORWARD=LatestMinor"
"%GODOT_DIR%\Godot_spine3_8_editor_console.exe" --path "%PROJECT_ROOT%\tools\action_battle_test" --rendering-method gl_compatibility res://sword_combo_preview.tscn
endlocal
