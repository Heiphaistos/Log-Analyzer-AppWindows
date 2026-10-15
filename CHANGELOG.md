# Journal des modifications

## 1.10.0
- Mise à jour automatique : vérification silencieuse au démarrage puis toutes les 6 h, bouton « Rechercher une mise à jour », réglage « Vérifier automatiquement les mises à jour » (actif par défaut).
- Intégrité : manifeste `latest.json` signé (ECDSA P-256), taille et SHA-256 de l'installateur contrôlés, téléchargement limité aux releases GitHub du dépôt.
- Installation silencieuse (Inno Setup) puis relance de l'application. La copie portable est notifiée avec un lien de téléchargement.
- Script de publication `installer/publier_manifeste.py` (signature du manifeste, ne publie rien).

## 1.9.1
- Icône de l'application (exe, fenêtre, installateur).
