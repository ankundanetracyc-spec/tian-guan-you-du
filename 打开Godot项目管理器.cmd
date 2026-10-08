@echo off
setlocal
set "DOTNET_ROOT=E:\stacklands\runtime\godot\app\Godot_v4.7.2-stable_mono_win64\dotnet"
set "DOTNET_MULTILEVEL_LOOKUP=0"
set "DOTNET_ROLL_FORWARD=LatestMinor"
set "PATH=%DOTNET_ROOT%;%PATH%"
set "TEMP=E:\stacklands\tmp\godot-runtime"
set "TMP=%TEMP%"
if not exist "%TEMP%" mkdir "%TEMP%"
"E:\stacklands\runtime\godot\app\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --project-manager
