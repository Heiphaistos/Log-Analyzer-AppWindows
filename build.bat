@echo off
REM ============================================================
REM  WinLogAnalyzer - Build single-file .exe (Clean Build)
REM  KILL -> CLEAN -> BUILD -> VERIFY
REM ============================================================
setlocal
set ROOT=%~dp0
set API=%ROOT%src\WinLogAnalyzer.Api
set DIST=%ROOT%dist
set LOGDIR=%ROOT%.logs
set EXE=WinLogAnalyzer.Api.exe

if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set LOGFILE=%LOGDIR%\build.log

echo [%date% %time%] [INFO] Build start >> "%LOGFILE%"

REM --- KILL : liberer le binaire si en cours d'execution ---
echo [BUILD] Kill process %EXE% si verrouille...
taskkill /F /IM %EXE% >nul 2>&1

REM --- CLEAN : supprimer anciens artefacts ---
echo [BUILD] Clean dist/ bin/ obj/...
if exist "%DIST%" rmdir /S /Q "%DIST%"
if exist "%API%\bin" rmdir /S /Q "%API%\bin"
if exist "%API%\obj" rmdir /S /Q "%API%\obj"

REM --- BUILD : publish single-file self-contained ---
echo [BUILD] dotnet publish (win-x64, single-file)...
dotnet publish "%API%\WinLogAnalyzer.Api.csproj" -c Release -o "%DIST%"
if errorlevel 1 (
  echo [ERREUR] Build echoue. Voir sortie ci-dessus.
  echo [%date% %time%] [ERROR] Build failed >> "%LOGFILE%"
  exit /b 1
)

REM --- VERIFY : binaire present ---
if not exist "%DIST%\%EXE%" (
  echo [ERREUR] Binaire introuvable dans dist/.
  echo [%date% %time%] [ERROR] Binary missing >> "%LOGFILE%"
  exit /b 1
)

echo [ETAT] Build OK. Binaire -^> %DIST%\%EXE%
echo [%date% %time%] [INFO] Build OK >> "%LOGFILE%"
endlocal
