@echo off
setlocal
set "FORMATION_PAPER_PROJECT=%~dp0.."
set "APPDATA=%FORMATION_PAPER_PROJECT%\.godot\test-results\formation_paper_manual\isolated_appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"
dotnet build "%FORMATION_PAPER_PROJECT%\Stacklands.csproj" --no-restore --nologo -v quiet
if errorlevel 1 exit /b 1
"%FORMATION_PAPER_PROJECT%\runtime\godot\Godot_v4.7.2-stable_spine3_8_mono_win64\Godot_spine3_8_editor.exe" --path "%FORMATION_PAPER_PROJECT%" --rendering-method gl_compatibility --windowed --resolution 1280x720 --scene res://tscn/scene/FormationPaperWorkspace.tscn -- --formation-paper-standalone
endlocal
