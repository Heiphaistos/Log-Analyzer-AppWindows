@echo off
REM Lance l'application desktop.
REM ELEVATION REQUISE : la lecture du journal Security de Windows (EventLogService.cs)
REM exige des privileges administrateur (UnauthorizedAccessException sinon).
REM Le chemin est fixe a %~dp0dist\ — aucune entree utilisateur n'est injectee.
setlocal
set "EXE=%~dp0dist\WinLogAnalyzer.exe"
if not exist "%EXE%" (
  echo [ERREUR] %EXE% introuvable. Lance d'abord build.bat
  exit /b 1
)
echo [ETAT] Demarrage de WinLog Analyzer...
REM Utilise -FilePath avec guillemets pour eviter toute injection par apostrophe dans le chemin.
powershell -NoProfile -Command "Start-Process -FilePath \"%EXE%\" -Verb RunAs"
endlocal
