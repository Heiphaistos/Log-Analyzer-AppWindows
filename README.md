# WinLog Analyzer

Analyseur de logs Windows hybride : **agent local C# (.NET 8)** + **dashboard web**.
Lit l'Observateur d'evenements, extrait les erreurs Critical/Error, traduit les PID en
noms de process, et affiche pour chaque Event ID connu une **explication + remediation**.

## Architecture

```
WinLogAnalyzer/
├── src/
│   ├── WinLogAnalyzer.Core/     # Lib metier (lecture EventLog, mapping, dictionnaire)
│   └── WinLogAnalyzer.Api/      # API REST self-hosted (127.0.0.1:5099) + frontend
│       ├── data/solutions.json  # Base de connaissance Event ID -> solution
│       └── wwwroot/             # Dashboard (HTML/CSS/JS modulaire)
├── build.bat                    # Clean build -> dist/WinLogAnalyzer.Api.exe
├── run.bat                      # Lance l'agent en admin
└── .logs/                       # Logs build/exec (jamais commit)
```

## Prerequis

- **.NET 8 SDK** (`dotnet --version` >= 8.0)
- Windows 10/11

## Build & Run

```bat
build.bat        :: KILL -> CLEAN -> publish single-file -> VERIFY
run.bat          :: lance dist\WinLogAnalyzer.Api.exe (admin)
```

Le navigateur s'ouvre sur `http://127.0.0.1:5099`.

## API

| Endpoint | Description |
|----------|-------------|
| `GET /api/events?log=System&max=100` | Erreurs critiques (log: System/Application/Security, max 1..1000) |
| `GET /api/health` | Statut agent + taille dictionnaire |

## Ajouter une solution

Editer `src/WinLogAnalyzer.Api/data/solutions.json` — **aucune recompilation** :

```json
"1234": {
  "title": "Titre court",
  "explanation": "Cause du probleme.",
  "remediation": "Etapes de correction numerotees.",
  "severity": "critical",
  "links": ["https://learn.microsoft.com/..."]
}
```

La cle = Event ID. `severity` : `critical | error | warning | info` (pilote la couleur).

## Securite

- API bind **127.0.0.1 uniquement** — jamais exposee sur le reseau.
- Manifest `requireAdministrator` (lecture du log Security).
- Donnees locales, jamais transmises.

## Notes techniques

- Lecture **streaming** (`EventLogReader`) : pas de charge complete en RAM.
- `EventRecord.ProcessId` = PID au moment de l'event. Si le process est mort -> `[termine]`.
```
