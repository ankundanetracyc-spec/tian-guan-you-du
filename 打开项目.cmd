@echo off
setlocal
set "SPINE_ROOT=E:\stacklands\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64"
set "DOTNET_ROOT=%SPINE_ROOT%\dotnet"
set "DOTNET_MULTILEVEL_LOOKUP=0"
set "DOTNET_ROLL_FORWARD=LatestMinor"
set "PATH=%DOTNET_ROOT%;%PATH%"
set "TEMP=E:\stacklands\tmp\godot-runtime"
set "TMP=%TEMP%"
if not exist "%TEMP%" mkdir "%TEMP%"
cd /d "E:\stacklands"
"%SPINE_ROOT%\Godot_spine3_8_editor.exe" --editor --path "E:\stacklands" --rendering-method gl_compatibility --rendering-driver opengl3
