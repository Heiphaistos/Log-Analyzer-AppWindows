# PRD — WinLog Analyzer

> Product Requirements Document — améliorations & nouvelles fonctionnalités
> Version doc : 1.0 · Date : 2026-06-06 · Cible produit : v0.2 → v1.0

---

## 1. Contexte

WinLog Analyzer est une application desktop Windows (WPF, .NET 8) qui lit l'Observateur
d'événements, extrait les erreurs Critical/Error, traduit les PID en noms de process, et
affiche pour chaque Event ID connu une explication + remédiation issues d'une base de
connaissance locale (`data/solutions.json`).

**État actuel (v0.2)**
- Lecture journaux System / Application / Security (Level 1=Critical, 2=Error), max 1000.
- Résolution PID → nom de process (`[termine]` si mort).
- Base de connaissance : 12 Event IDs.
- UI : thème dark, filtre live, cartes dépliables, 4 cartes stats, analyse async.
- Build single-file self-contained, manifest admin.

**Problème central** : l'outil couvre un sous-ensemble étroit (2 niveaux, 3 journaux, 12 IDs,
pas de Planificateur de tâches) et reste passif (lecture ponctuelle, zéro action, zéro export).
Objectif : passer d'un **lecteur de logs** à un **assistant de diagnostic actionnable**.

---

## 2. Objectifs & métriques de succès

| Objectif | Métrique cible |
|----------|----------------|
| Couverture des événements | +500 Event IDs documentés (vs 12) |
| Diagnostic actionnable | 80% des cartes proposent ≥1 action exécutable |
| Réactivité perçue | Liste de 1000 events scrollée à 60 fps (virtualisation) |
| Autonomie | Surveillance temps réel + alertes sans relance manuelle |
| Portabilité du diagnostic | Export rapport (PDF/HTML/CSV) en 1 clic |

---

## 3. Limitations actuelles (dette à corriger)

| # | Limitation | Impact |
|---|-----------|--------|
| L1 | Liste `ItemsControl` sans virtualisation | Lag/RAM sur gros volumes |
| L2 | Niveaux figés à Critical+Error | Rate Warning/Information utiles |
| L3 | Clé dictionnaire = Event ID seul | Collisions inter-providers (ex: 1000) |
| L4 | `solutions.json` rechargé seulement au démarrage | Édition = rebuild |
| L5 | Pas de filtre par date / plage temporelle | Bruit, difficile de cibler un incident |
| L6 | Aucune persistance des préférences | Re-saisie journal/max à chaque lancement |
| L7 | Pas de déduplication | Même event répété N fois pollue la liste |
| L8 | Pas de logs applicatifs dans `.logs/` | Debug difficile |

---

## 4. Améliorations (existant à durcir)

### A1 — Virtualisation de la liste *(P0)*
Remplacer `ItemsControl` par `ListBox`/`VirtualizingStackPanel` (recyclage conteneurs).
Critère : 1000 events, scroll fluide, RAM stable.

### A2 — Filtre niveaux multi-sélection *(P1)*
Cases à cocher Critical / Error / Warning / Information. XPath dynamique
(`Level=1 or Level=2 or 3 or 4`). Défaut : Critical+Error.

### A3 — Clé dictionnaire composite *(P1)*
Lookup `Source:EventId` puis fallback `EventId`. Évite collisions (L3).
Rétro-compatible avec le JSON actuel.

### A4 — Hot-reload base de connaissance *(P2)*
`FileSystemWatcher` sur `solutions.json` → rebuild map à chaud, sans relancer.

### A5 — Déduplication + compteur *(P1)*
Grouper events identiques (EventId+Source+message), badge "×N", dernière occurrence.
Toggle "grouper / liste brute".

### A6 — Persistance préférences *(P2)*
`%AppData%/WinLogAnalyzer/settings.json` : dernier journal, max, niveaux, filtres.

### A7 — Logs applicatifs *(P2)*
Écrire build/exec/erreurs dans `.logs/` (ISO 8601 + niveau), rotation 1 Mo.

---

## 5. Nouvelles fonctionnalités

### F1 — Module Planificateur de tâches *(P0 — objectif initial non livré)*
Onglet dédié. Lister les tâches via `Microsoft.Win32.TaskScheduler` (ou COM `Schedule.Service`).
Afficher : nom, état, dernière exécution, **dernier code de résultat** + traduction
(0x0=OK, 0x41301=en cours, 0x80070002=fichier introuvable…), prochaine exécution.
Mettre en évidence les tâches en échec. Base de connaissance dédiée aux codes de résultat.

### F2 — Surveillance temps réel + alertes *(P1)*
`EventLogWatcher` (souscription push). Toast Windows quand un event critique arrive.
Mode "moniteur" : auto-ajout en tête de liste, pastille de compteur. Pause/reprise.

### F3 — Export de rapport *(P1)*
Bouton "Exporter" → CSV (données brutes), HTML (rapport stylé erreurs+solutions),
PDF (synthèse). Inclure stats + horodatage. Cas d'usage : transmettre à un support IT.

### F4 — Remédiation actionnable *(P1)*
Boutons d'action contextuels par solution : "Ouvrir services.msc", "Lancer mdsched",
"Ouvrir Minidump", "chkdsk (admin)", "Ouvrir dcomcnfg". Confirmation avant toute
commande à effet de bord. Jamais d'action destructive auto.

### F5 — Timeline & graphiques *(P2)*
Histogramme des erreurs par jour/heure (détecte les pics → corrélation incident).
Répartition par source (top providers fautifs). LiveCharts2 ou rendu maison léger.

### F6 — Enrichissement base de connaissance *(P1)*
- Étendre à 100+ Event IDs courants (curated).
- Optionnel : lookup en ligne (Microsoft Learn / EventID.net) en fallback, **opt-in**,
  avec mise en cache locale. Respecte vie privée (aucun envoi par défaut).

### F7 — Corrélation d'événements *(P2)*
Regrouper les events liés dans une fenêtre temporelle (ex: 41 + 6008 + 1001 = même crash).
Vue "incident" agrégeant la cause racine probable.

### F8 — Recherche avancée *(P2)*
Filtres combinés : plage de dates, source, plage d'Event ID, présence de solution.
Debounce sur le champ texte. Sauvegarde de filtres nommés.

### F9 — Multi-journaux *(P2)*
Analyser plusieurs journaux simultanément (System+Application), colonne "journal".
Support journaux applicatifs étendus (Setup, ForwardedEvents, journaux custom).

### F10 — Distribution *(P2)*
Installeur (MSIX ou Inno Setup), raccourci menu Démarrer, vérification de mise à jour
(GitHub Releases), icône d'application.

---

## 6. Priorisation (RICE simplifié)

| Item | Priorité | Effort | Valeur |
|------|----------|--------|--------|
| A1 Virtualisation | P0 | S | Perf bloquante |
| F1 Planificateur tâches | P0 | L | Objectif initial |
| A2 Filtre niveaux | P1 | S | Forte |
| A3 Clé composite | P1 | S | Correctness |
| A5 Dédup + compteur | P1 | M | Lisibilité |
| F2 Temps réel + alertes | P1 | M | Différenciant |
| F3 Export rapport | P1 | M | Forte (support IT) |
| F4 Remédiation actionnable | P1 | M | Cœur de valeur |
| F6 Base 100+ IDs | P1 | M | Couverture |
| A4 Hot-reload | P2 | S | Confort |
| A6 Préférences | P2 | S | Confort |
| A7 Logs app | P2 | S | Maintenance |
| F5 Timeline/graphes | P2 | L | Analytique |
| F7 Corrélation | P2 | L | Analytique |
| F8 Recherche avancée | P2 | M | Confort |
| F9 Multi-journaux | P2 | M | Couverture |
| F10 Distribution | P2 | M | Adoption |

Effort : S < 1j · M 1-3j · L > 3j.

---

## 7. Roadmap proposée

**v0.3 — Fondations & objectif initial**
A1 (virtualisation), A3 (clé composite), A2 (niveaux), **F1 (Planificateur de tâches)**.

**v0.4 — Diagnostic actionnable**
F4 (actions), F6 (base 100+ IDs), A5 (dédup), F3 (export).

**v0.5 — Surveillance**
F2 (temps réel + toasts), A6 (préférences), A4 (hot-reload), A7 (logs).

**v1.0 — Analytique & distribution**
F5 (timeline), F7 (corrélation), F8 (recherche avancée), F9 (multi-journaux), F10 (installeur).

---

## 8. Hors périmètre (non-goals)

- Modification/suppression d'entrées de log (lecture seule stricte).
- Collecte/envoi de télémétrie par défaut.
- Administration de machines distantes (v1 = locale uniquement).
- Édition des tâches planifiées (lecture + actions sûres seulement).

---

## 9. Contraintes techniques

- Lecture seule du journal, jamais d'effet destructeur sans confirmation explicite.
- Aucun fichier source > 800 lignes (découpe par responsabilité).
- Données 100% locales par défaut ; tout appel réseau = opt-in explicite.
- Manifest admin conservé (log Security + actions élevées).
- Réutiliser `WinLogAnalyzer.Core` ; isoler chaque module (Tasks, Monitor, Export) en service dédié.
