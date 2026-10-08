@echo off
setlocal
set "ROOT=%~dp0.."
set "GODOT=%ROOT%\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor.exe"
set "DOTNET_ROOT=%ROOT%\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\dotnet"
set "DOTNET_MULTILEVEL_LOOKUP=0"
set "DOTNET_ROLL_FORWARD=LatestMinor"
set "PATH=%DOTNET_ROOT%;%PATH%"
if not exist "%GODOT%" (
  echo Spine-enabled Godot runtime not found: "%GODOT%"
  exit /b 1
)
"%GODOT%" --editor --path "%ROOT%" --rendering-method gl_compatibility --rendering-driver opengl3
endlocal
