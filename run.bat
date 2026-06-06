@echo off
REM Lance l'agent (admin requis pour le log Security).
set EXE=%~dp0dist\WinLogAnalyzer.Api.exe
if not exist "%EXE%" (
  echo [ERREUR] %EXE% introuvable. Lance d'abord build.bat
  exit /b 1
)
echo [ETAT] Demarrage agent... navigateur va s'ouvrir sur http://127.0.0.1:5099
powershell -Command "Start-Process '%EXE%' -Verb RunAs"
