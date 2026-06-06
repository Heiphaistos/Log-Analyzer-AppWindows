# WinLog Analyzer · v1.0

Application **desktop Windows** (WPF, .NET 8) de diagnostic : analyse l'Observateur
d'évènements **et** le Planificateur de tâches, traduit les PID/codes en clair, et propose
pour chaque problème connu une **explication + remédiation** (base de connaissance locale).

Interface native — pas de navigateur, pas de serveur web.

## Fonctionnalités

**Onglet Évènements**
- Journaux System / Application / Security, niveaux Critical / Error / Warning / Information (multi-sélection).
- Résolution PID → nom de process (`[termine]` si mort).
- Déduplication avec compteur ×N, filtre live, liste virtualisée (fluide sur gros volumes).
- Cartes dépliables : message brut + explication + remédiation + liens doc.
- Timeline d'activité par jour.
- **Surveillance temps réel** (push `EventLogWatcher`) avec compteur de nouveaux évènements.
- **Export** CSV + HTML (rapport stylé).
- **Outils** : services.msc, Moniteur de fiabilité, test RAM (mdsched, confirmation), Observateur.

**Onglet Planificateur de tâches**
- Liste toutes les tâches : état, dernière exécution, prochaine, **code de résultat traduit**.
- Échecs mis en avant, filtre "échecs uniquement", recherche.
- Remédiation par code (`data/taskcodes.json`).

**Transverses**
- Préférences persistées (`%AppData%/WinLogAnalyzer/settings.json`).
- Hot-reload de la base de connaissance (édition `solutions.json` sans relancer).
- Logs applicatifs (`%AppData%/WinLogAnalyzer/logs/app.log`, rotation 1 Mo).

## Architecture

```
WinLogAnalyzer/
├── src/
│   ├── WinLogAnalyzer.Core/      # Métier (aucune dépendance UI)
│   │   ├── Reader/EventLogService.cs      # lecture streaming + watcher temps réel
│   │   ├── Process/ProcessResolver.cs     # PID -> nom
│   │   ├── Knowledge/SolutionProvider.cs  # clé composite + hot-reload
│   │   ├── Tasks/                         # TaskSchedulerService + codes résultat
│   │   ├── Diagnostics/                   # EventGrouper (dedup) + Correlator
│   │   ├── Export/ReportExporter.cs       # CSV / HTML
│   │   ├── Settings/AppSettings.cs        # préférences
│   │   └── Logging/FileLogger.cs
│   └── WinLogAnalyzer.App/       # WPF (MVVM)
│       ├── data/{solutions,taskcodes}.json
│       ├── Themes/Dark.xaml
│       ├── ViewModels/  Views/  Infrastructure/
│       └── MainWindow.xaml                # shell à onglets
├── installer/setup.iss          # Inno Setup
├── build.bat / run.bat
└── docs/PRD.md
```

## Prérequis
- **.NET 8 SDK**, Windows 10/11.

## Build & Run
```bat
build.bat   :: KILL -> CLEAN -> publish single-file -> dist\WinLogAnalyzer.exe
run.bat     :: lance en admin
```

## Installeur (optionnel)
Inno Setup 6+ requis : `iscc installer\setup.iss` → `dist\installer\WinLogAnalyzer-Setup-1.0.0.exe`.

## Ajouter une solution / un code
Éditer `data/solutions.json` (Event ID, clé `id` ou `source:id`) ou `data/taskcodes.json`
(code hex `0x........`). Hot-reload pour `solutions.json` ; rebuild pour copier dans `dist`.

## Limites connues (cf. docs/PRD.md)
- Export PDF non inclus (utiliser HTML → imprimer en PDF).
- Corrélation d'incidents disponible côté Core (`Correlator`), pas encore d'onglet dédié.
- Analyse mono-journal à la fois (multi-journaux simultané : prévu).

## Notes techniques
- Lecture **seule** ; actions à effet de bord (test RAM) confirmées explicitement.
- Lecture hors thread UI (Task.Run) : interface jamais figée.
- Manifest `requireAdministrator`. Données 100% locales.
