# AI usage monitor Windows

Mini-widget Windows 11 qui affiche en temps réel le taux d'usage de votre abonnement Claude (Pro / Max) et l'heure de remise à zéro :

- **Session (5 h)** : pourcentage consommé, heure de RAZ et compte à rebours ;
- **Semaine** : pourcentage consommé et date de RAZ ;
- fenêtres hebdomadaires par modèle (Opus, Sonnet) et crédits supplémentaires quand votre offre en comporte.

Deux affichages :

- une **fenêtre flottante** « Utilisation Claude » sans bordure, déplaçable, aimantée aux bords de l'écran, en thème sombre, clair ou selon Windows, avec un mode compact d'une seule ligne (double-clic) ;
- une **icône dans la zone de notification** qui affiche le % de la session. Sa couleur change à 75 % (jaune) puis à 90 % (rouge).

## Installation

Aucun SDK n'est nécessaire : la compilation utilise le compilateur C# inclus dans Windows (.NET Framework 4.8).

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\dist\ClaudeUsageWidget.exe
```

Prérequis : être connecté à **Claude Code** avec votre compte claude.ai (`claude /login`). Sinon, indiquez un jeton ou un fichier d'identifiants dans la configuration (voir plus bas).

Conseils :

- **Afficher l'icône en permanence** : Windows 11 range les nouvelles icônes dans le menu masqué (chevron `^`). Glissez-la dans la barre des tâches, ou activez-la dans *Paramètres > Personnalisation > Barre des tâches > Autres icônes de la barre d'état système*.
- **Démarrage automatique** : clic droit > *Lancer au démarrage de Windows*.

## Utilisation

| Action | Effet |
|---|---|
| Glisser la fenêtre | Déplacer (position mémorisée) |
| Double-clic sur la fenêtre | Basculer entre le mode complet et le mode compact |
| Clic gauche sur l'icône de notification | Afficher / masquer le widget |
| Clic droit (fenêtre ou icône) | Menu : actualiser, configuration, compact, premier plan, thème, opacité, fréquence, démarrage, quitter |
| Alt+F4 sur le widget | Masque la fenêtre sans quitter l'application |

## Configuration de l'accès au compte

Clic droit > **Configuration…** permet de choisir la source du jeton d'accès :

| Source | Fonctionnement |
|---|---|
| **Automatique** (par défaut) | Lit le jeton de Claude Code dans `%USERPROFILE%\.claude\.credentials.json` (ou `%CLAUDE_CONFIG_DIR%`). Claude Code se charge de le renouveler. |
| **Fichier d'identifiants personnalisé** | Même format que Claude Code, à un autre emplacement. |
| **Jeton saisi manuellement** | Le jeton est chiffré avec DPAPI (lié à votre session Windows) avant d'être stocké. Il est effacé dès qu'une autre source est choisie. |

Le bouton **Tester la connexion** vérifie la source choisie avant l'enregistrement.

### Données personnelles et sécurité

- **Aucune donnée sensible dans le dépôt** : ni identifiant, ni mot de passe, ni jeton, ni chemin personnel.
- **Stockage local** : tout est enregistré sur votre poste, hors du dossier du projet, dans `%APPDATA%\ClaudeUsageWidget\` :
  - `settings.ini` : position, apparence, fréquence et source d'accès (jamais de secret) ;
  - `token.dat` : jeton manuel **chiffré**, présent uniquement si cette source est utilisée.
- **Fichier de Claude Code** : le widget le lit sans jamais le modifier.
- **Destination du jeton** : il n'est envoyé qu'à `api.anthropic.com`.

## Fonctionnement

1. Le widget obtient le jeton OAuth selon la source configurée.
2. Il appelle `GET https://api.anthropic.com/api/oauth/usage` avec les en-têtes :
   - `Authorization: Bearer <jeton>`
   - `anthropic-beta: oauth-2025-04-20`
3. La réponse contient notamment :
   ```json
   {
     "five_hour":  { "utilization": 7.0, "resets_at": "2026-09-16T14:30:00+00:00" },
     "seven_day":  { "utilization": 1.0, "resets_at": "2026-09-23T03:00:00+00:00" },
     "seven_day_opus": null,
     "extra_usage": { "is_enabled": false, "utilization": null }
   }
   ```
   `utilization` est déjà exprimé en pourcentage (0 à 100).

L'actualisation a lieu toutes les 2 minutes par défaut, avec un intervalle réglable de 1 à 10 minutes. Le compte à rebours se met à jour localement toutes les 15 secondes. Une actualisation est aussi lancée dès qu'une RAZ est passée et au réveil de l'ordinateur. En cas de réponse HTTP 429, l'intervalle double à chaque fois, sans dépasser 30 minutes.

### Limites connues

- **Endpoint non documenté** : c'est celui qu'utilisent Claude Code et claude.ai. Anthropic peut le modifier sans préavis.
- **Renouvellement du jeton** : le jeton OAuth de Claude Code expire au bout de quelques heures et c'est Claude Code qui le renouvelle. Si Claude Code n'a pas été lancé depuis longtemps, le widget affiche « Jeton expiré » et conserve les dernières valeurs connues. Il suffit d'ouvrir Claude Code pour que tout reprenne. Le widget ne renouvelle pas le jeton lui-même : il devrait alors réécrire le fichier d'identifiants, ce qui risquerait de déconnecter Claude Code.

## Structure

```
src/
  Program.cs       point d'entrée, instance unique, TLS
  UsageClient.cs   obtention du jeton + appel API + parsing (logique à porter pour le web / macOS)
  Settings.cs      préférences .ini, jeton chiffré (DPAPI), lancement au démarrage (registre HKCU)
  ConfigForm.cs    fenêtre de configuration de l'accès au compte
  WidgetForm.cs    fenêtre, dessin, thèmes, icône de notification, menu
build.ps1          compilation avec csc.exe
```

## Pistes pour la suite

- **Application web** : un tout petit serveur local (Node ou Python) qui appelle le même endpoint et sert une page. Le jeton ne doit jamais passer par le navigateur ni par un service tiers.
- **Widget macOS** : sur macOS, Claude Code stocke le jeton dans le Trousseau (service `Claude Code-credentials`), pas dans un fichier. Deux options : un widget SwiftUI/WidgetKit, ou une app de barre de menus.
