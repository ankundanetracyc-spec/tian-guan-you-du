@echo off
setlocal
set "FORMATION_PROJECT=%~dp0.."
dotnet build "%FORMATION_PROJECT%\Stacklands.csproj" --no-restore --nologo -v quiet
if errorlevel 1 exit /b 1
"%FORMATION_PROJECT%\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor.exe" --path "%FORMATION_PROJECT%" --rendering-method gl_compatibility --windowed --resolution 1280x900 --scene res://tscn/debug/FormationWorkbenchPreview.tscn
endlocal
