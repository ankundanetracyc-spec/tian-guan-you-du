@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run_formation_paper_revision_smoke.ps1"
exit /b %errorlevel%
