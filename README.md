# WinLog Analyzer · v1.1

Application **desktop Windows** (WPF, .NET 8) de diagnostic : analyse l'Observateur
d'évènements **et** le Planificateur de tâches, traduit les PID/codes en clair, et propose
pour chaque problème connu une **explication + remédiation** (base de connaissance locale).

Interface native — pas de navigateur, pas de serveur web.

## Fonctionnalités

**Onglet Évènements**
- Journaux System / Application / Security **simultanés** (multi-sélection), niveaux Critical / Error / Warning / Information.
- Filtre par **période** (24h / 7j / 30j / tout) + recherche live.
- Résolution PID → nom de process (`[termine]` si mort).
- Déduplication avec compteur ×N, liste virtualisée (fluide sur gros volumes).
- Cartes dépliables : message brut + explication + remédiation + liens doc.
- Timeline d'activité par jour.
- **Surveillance temps réel** (un `EventLogWatcher` par journal) avec compteur de nouveaux évènements.
- **Export** CSV + HTML + **PDF** (QuestPDF).
- **Outils** : services.msc, Moniteur de fiabilité, test RAM (mdsched, confirmation), Observateur.

**Onglet Incidents**
- Corrélation temporelle des évènements (fenêtre 30/60/120/300 s) → cause racine probable (ex: 41 + 6008 + 1001).

**Onglet Planificateur de tâches**
- Liste toutes les tâches : état, dernière exécution, prochaine, **code de résultat traduit**.
- Échecs mis en avant (détection précise via bit de sévérité), filtre "échecs uniquement", recherche.
- Remédiation par code : base curée (`data/taskcodes.json`, 68 codes) **+ décodeur universel**.

**Décodeur universel d'erreurs**
- N'importe quel code (HRESULT / Win32 / NTSTATUS / applicatif) est interprété automatiquement
  via `FormatMessage` (message système réel) même s'il n'est pas dans la base curée.
- Détection de la facilité (Win32, COM, RPC, MSI…) + remédiation heuristique + sévérité.
- Les codes d'erreur **embarqués dans les messages d'évènements** sont aussi décodés.
- Bases curées : 81 Event IDs + 68 codes tâches pour la précision sur les cas courants.

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

## État roadmap PRD
Tous les items implémentés : A1–A7, F1–F10, plus corrélation d'incidents (onglet dédié),
recherche par période, multi-journaux simultané, export PDF.

## Notes techniques
- Lecture **seule** ; actions à effet de bord (test RAM) confirmées explicitement.
- Lecture hors thread UI (Task.Run) : interface jamais figée.
- Manifest `requireAdministrator`. Données 100% locales.
