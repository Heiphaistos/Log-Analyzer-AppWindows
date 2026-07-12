; ============================================================
;  WinLog Analyzer - Script d'installation Inno Setup
;  Prerequis : Inno Setup 6+ (https://jrsoftware.org/isdl.php)
;  Build d'abord : build.bat (genere dist\WinLogAnalyzer.exe)
;  Compiler : iscc installer\setup.iss
; ============================================================

#define AppName "WinLog Analyzer"
#define AppVersion "1.8.0"
#define AppPublisher "heiphaistos44"
#define AppExe "WinLogAnalyzer.exe"

[Setup]
AppId={{B7E3F1A2-5C4D-4E8F-9A1B-WINLOG000001}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\WinLogAnalyzer
DefaultGroupName={#AppName}
OutputDir=..\dist\installer
OutputBaseFilename=WinLogAnalyzer-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; L'app lit le journal Security -> elevation requise.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Creer un raccourci sur le Bureau"; GroupDescription: "Raccourcis"

[Files]
; Binaire single-file + dossier data (base de connaissance).
Source: "..\dist\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\data\*"; DestDir: "{app}\data"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Desinstaller {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Lancer {#AppName}"; Flags: postinstall nowait skipifsilent runascurrentuser
