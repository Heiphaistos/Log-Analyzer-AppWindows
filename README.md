# WinLog Analyzer · v1.9
## Démonstration

<video src="https://media.heiphaistos.org/videos/loganalyzer.mp4" controls width="100%" preload="none"></video>\n
Application **desktop Windows** (WPF, .NET 8) de diagnostic : analyse l'Observateur
d'évènements **et** le Planificateur de tâches, traduit les PID/codes en clair, et propose
pour chaque problème connu une **explication + remédiation** (base de connaissance locale).

Interface native — pas de navigateur, pas de serveur web.

## Fonctionnalités

**Onglet Évènements**
- Journaux System / Application / Security **simultanés** (multi-sélection), niveaux Critical / Error / Warning / Information.
- Filtre par **période** (24h / 7j / 30j / tout) **appliqué dans la requête** : on obtient les
  N events de la période, pas un sous-ensemble des N derniers. Recherche live (debounce 250 ms).
- Résolution PID → nom de process (`[termine]` si mort).
- Déduplication avec compteur ×N, liste virtualisée (fluide sur gros volumes).
- Cartes dépliables : message brut + explication + remédiation + liens doc.
- Timeline d'activité par jour.
- **Surveillance temps réel** (un `EventLogWatcher` par journal) avec compteur de nouveaux
  évènements ; les watchers se resynchronisent si on change journaux/niveaux en cours de route.
- **Export** CSV + HTML + **PDF** (QuestPDF).
- **Outils** : services.msc, Moniteur de fiabilité, test RAM (mdsched, confirmation), Observateur.

**Onglet Incidents**
- Corrélation temporelle des évènements (fenêtre 30/60/120/300 s) → cause racine probable (ex: 41 + 6008 + 1001).

**Onglet Planificateur de tâches**
- Liste toutes les tâches : état, dernière exécution, prochaine, **code de résultat traduit**.
- Échecs mis en avant (détection précise via bit de sévérité), filtre "échecs uniquement", recherche.
- Remédiation par code : base curée (`data/taskcodes.json`, 68 codes) **+ décodeur universel**.

**Base d'erreurs exhaustive (offline)**
- `data/errordb.json` : **~11 000 codes** Windows (Win32, HRESULT, NTSTATUS, Windows Update)
  générés depuis les tables systèmes de Windows — couvre tout le catalogue, pas seulement
  ce qui est arrivé sur la machine.
- Générée par `tools/ErrorDbGen` (FormatMessage sur ntdll/wuapi/système), embarquée dans l'app.

**Résolution en cascade — n'importe quel code est interprété**
1. Remédiation **curée** (81 Event IDs + 68 codes tâches) — la plus précise.
2. **Base offline** (~11 000 codes) — message système exact.
3. **Décodeur live** `FormatMessage` (HRESULT/Win32/NTSTATUS) — ultime filet, infini.
- **Remédiation précise pour tout code** via `RemediationEngine` : règles en cascade
  (code Win32 exact → Windows Update → MSI → facilité HRESULT → catégorie NTSTATUS → générique).
  Chaque code de la base de ~11 000 reçoit des étapes ciblées, pas un message brut.
- Les codes **embarqués dans les messages d'évènements** sont aussi décodés.

## Régénérer la base d'erreurs
```bat
dotnet run --project tools/ErrorDbGen -c Release -- src/WinLogAnalyzer.App/data/errordb.json
```

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
Inno Setup 6+ requis : `iscc installer\setup.iss` → `dist\installer\WinLogAnalyzer-Setup-1.9.0.exe`.

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