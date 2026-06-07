# Audit — WinLog Analyzer (v1.5 → 1.6)

Date : 2026-06-07 · Périmètre : sécurité, base de données, code, qualité, perf.
Légende sévérité : 🔴 critique · 🟠 moyen · 🟡 faible · 🟢 OK · 🔵 info.

---

## 1. Sécurité

| # | Sévérité | Constat | Statut |
|---|----------|---------|--------|
| S1 | 🟢 | Lecture seule stricte (journaux + tâches). Aucune écriture/modif système. | OK |
| S2 | 🟢 | 100% local. Zéro appel réseau, zéro télémétrie, aucun secret dans le repo. | OK |
| S3 | 🟢 | `Process.Start` n'utilise que des noms d'outils **constants** (services.msc, eventvwr.msc, mdsched, perfmon /rel). Pas d'injection (aucune entrée utilisateur dans la commande). | OK |
| S4 | 🟢 | Liens hypertexte ouverts uniquement depuis `solutions.json` curé (URLs Microsoft https). | OK |
| S5 | 🟢 | `FormatMessage` avec `IGNORE_INSERTS` → pas d'exploitation de `%n/%s` dans les messages. | OK |
| S6 | 🟠→🟢 | **DLL planting** : `LoadLibrary("ntdll.dll")` cherchait dans le PATH. Corrigé : `LoadLibraryEx(..., LOAD_LIBRARY_SEARCH_SYSTEM32)` + handle mis en cache (corrige aussi une fuite de handle). | **CORRIGÉ** |
| S7 | 🔵 | Manifest `requireAdministrator` : nécessaire pour le journal Security et `mdsched`. Élévation justifiée, périmètre minimal. | Info |
| S8 | 🟡 | `data/*.json` lus depuis le dossier du binaire : un attaquant pouvant y écrire contrôle le contenu — mais cela exige déjà un accès admin au dossier d'install. Risque résiduel faible. | Accepté |

## 2. Base de données

| # | Sévérité | Constat | Statut |
|---|----------|---------|--------|
| D1 | 🟢 | `errordb.json` : 10 974 codes, ~1.1 Mo, chargé une fois en mémoire (dictionnaire). | OK |
| D2 | 🟢 | Clés uniques (généré via `SortedDictionary`). Lecture `System.Text.Json` insensible à la casse. | OK |
| D3 | 🟢 | Hot-reload `solutions.json` : debounce 400 ms + `try/catch` + relecture protégée. | OK |
| D4 | 🟡 | `taskcodes.json` sans hot-reload (chargé au démarrage) — par conception. | Info |
| D5 | 🔵 | Messages dans la langue de l'OS (FR) car tirés des ressources locales. Attendu. | Info |
| D6 | 🔵 | `errordb.json` versionné dans git (1.1 Mo) — acceptable, régénérable via `tools/ErrorDbGen`. | Info |

## 3. Code & bugs

| # | Sévérité | Constat | Statut |
|---|----------|---------|--------|
| C1 | 🟠→🟢 | `FormatMessage` buffer 2048 fixe → messages longs renvoyaient vide. Corrigé : 8192 + retry 64 Ko sur `ERROR_INSUFFICIENT_BUFFER`. | **CORRIGÉ** |
| C2 | 🟢 (déjà) | Binding `Run.Text` TwoWay sur propriété lecture seule (cascade de dialogues) — corrigé en v1.1. | OK |
| C3 | 🟡 | Surveillance temps réel : `Dispatcher.Invoke` (synchrone) + `Rebuild` complet par évènement entrant. Sous rafale, churn UI. Reco : `BeginInvoke` + throttle. | À planifier |
| C4 | 🟡 | `AppSettings.Save()` appelé à chaque setter (toggle) → écritures disque fréquentes. Reco : debounce. | À planifier |
| C5 | 🟡 | `SolutionProvider._lastReload` non synchronisé entre threads FileSystemWatcher — course bénigne (map en `volatile`). | Faible |
| C6 | 🟢 | Toutes les E/S (fichier, process, registre) sous `try/catch` ; le logger n'échoue jamais. | OK |
| C7 | 🟢 | Cycle de vie propre : `MainViewModel.Dispose` → arrêt watchers, FSW, logger ; `Window.Closed` déclenche Dispose. | OK |
| C8 | 🟢 | Async maîtrisé : `IsLoading` garde la réentrance, `RaiseCanExecuteChanged` sur les commandes. | OK |

## 4. Qualité & architecture

| # | Sévérité | Constat |
|---|----------|---------|
| Q1 | 🟢 | Tous les fichiers source < 800 lignes (max ~350). `Core` sans dépendance UI (séparation nette MVVM). |
| Q2 | 🟢 | Lecture hors thread UI (`Task.Run`) — interface jamais figée. Listes virtualisées. |
| Q3 | 🟠 | **Aucun test unitaire.** Cibles idéales (logique pure, sans I/O) : `Win32ErrorDecoder`, `RemediationEngine`, `EventGrouper`, `Correlator`. Reco prioritaire. |
| Q4 | 🟡 | Pas de CI. Reco : GitHub Actions (build + tests). |
| Q5 | 🔵 | Versionnement cohérent (csproj `Version`). |

## 5. Performance

| # | Sévérité | Constat |
|---|----------|---------|
| P1 | 🟢 | Lecture EventLog en streaming (pas de charge complète RAM). |
| P2 | 🟡 | Onglet Incidents lit 500 events/journal à chaque corrélation ; dedup/timeline recalculés au rebuild. OK aux volumes courants. |

---

## Corrigés dans cet audit (v1.6)
- **S6** DLL planting + fuite de handle → `LoadLibraryEx` System32 + handle caché.
- **C1** Troncature des messages longs → buffer 8 Ko + retry 64 Ko.

## Recommandations restantes (priorisées)
1. **Q3** Tests unitaires sur la logique pure (décodeur, remédiation, dedup, corrélation).
2. **C3/C4** Throttle de la surveillance temps réel + debounce de la sauvegarde des préférences.
3. **Q4** CI GitHub Actions (build Release + tests).

## Verdict
Application **saine** : lecture seule, locale, sans secret ni réseau, gestion d'erreur robuste,
architecture propre. 2 correctifs sécurité/robustesse appliqués. Reste surtout l'ajout de tests
(dette de qualité, pas de bug bloquant connu).
