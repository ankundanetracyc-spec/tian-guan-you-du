@echo off
setlocal
set "GUARD_PROJECT=%~dp0.."
set "APPDATA=%GUARD_PROJECT%\.godot\test-results\formation_guard_manual\isolated_appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"
dotnet build "%GUARD_PROJECT%\Stacklands.csproj" --no-restore --nologo -v quiet
if errorlevel 1 exit /b 1
"%GUARD_PROJECT%\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor.exe" --path "%GUARD_PROJECT%" --rendering-method gl_compatibility --windowed --resolution 1280x900 --scene res://tscn/debug/FormationGuardTrial.tscn
endlocal
