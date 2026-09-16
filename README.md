# AI usage monitor Windows

Mini-widget Windows 11 qui affiche le taux d'usage de votre abonnement Claude (Pro / Max) et l'heure de remise à zéro :

- **Session (5 h)** : pourcentage consommé, heure de RAZ et compte à rebours ;
- **Semaine** : pourcentage consommé et date de RAZ.

Deux affichages :

- une **fenêtre flottante** « Utilisation Claude » sans bordure, déplaçable, aimantée aux bords de l'écran, en thème sombre, clair ou selon Windows, avec un mode compact d'une seule ligne (double-clic) ;
- une **icône dans la zone de notification** qui affiche le % de la session. Sa couleur change à 75 % (jaune) puis à 90 % (rouge).

Le widget **n'utilise aucun identifiant et n'accède pas au réseau**. Il affiche les quotas que Claude Code transmet officiellement à sa [ligne de statut](https://code.claude.com/docs/en/statusline) (champ `rate_limits`), enregistrés dans un fichier local.

## Installation

### Télécharger l'exécutable

1. Ouvrez la page **Releases** du dépôt et téléchargez `ClaudeUsageWidget.exe` depuis la dernière version.
2. *(Facultatif)* Vérifiez l'intégrité du fichier : l'empreinte affichée par la commande ci-dessous doit correspondre à celle de `SHA256SUMS.txt`.
   ```powershell
   Get-FileHash .\ClaudeUsageWidget.exe -Algorithm SHA256
   ```
3. Placez l'exe à un emplacement définitif (par exemple `%LOCALAPPDATA%\Programs\ClaudeUsageWidget\`), puis lancez-le. Aucune installation n'est nécessaire. L'emplacement doit rester stable, car la configuration de Claude Code y fait référence.

L'exécutable n'est pas signé numériquement. Au premier lancement, Windows SmartScreen peut afficher « Windows a protégé votre ordinateur » : cliquez sur *Informations complémentaires*, puis *Exécuter quand même*.

### Compiler depuis les sources

Aucun SDK n'est nécessaire : la compilation utilise le compilateur C# inclus dans Windows (.NET Framework 4.8).

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\dist\ClaudeUsageWidget.exe
```

### Brancher Claude Code

Prérequis : Claude Code, connecté avec un abonnement claude.ai **Pro ou Max**.

Clic droit sur le widget > **Configuration…** : la fenêtre fournit les blocs prêts à copier. Choisissez **un seul** des deux cas suivants.

**1. Vous n'avez pas encore de ligne de statut.** Ajoutez ce bloc dans `%USERPROFILE%\.claude\settings.json`, en adaptant le chemin de l'exe, puis redémarrez Claude Code :

```json
"statusLine": {
  "type": "command",
  "command": "\"C:/Users/<vous>/AppData/Local/Programs/ClaudeUsageWidget/ClaudeUsageWidget.exe\" --statusline"
}
```

Claude Code appelle alors l'exe à chaque mise à jour de sa ligne de statut. L'exe enregistre les quotas, puis affiche un résumé court (`[Opus 5] | 5h 24% | 7j 41%`).

**2. Vous utilisez déjà [claude-hud](https://github.com/jarrodwatts/claude-hud).** Ajoutez cette ligne dans la section `display` de `%USERPROFILE%\.claude\plugins\claude-hud\config.json` :

```json
"externalUsageWritePath": "C:\\Users\\<vous>\\AppData\\Roaming\\ClaudeUsageWidget\\usage.json"
```

**Autre ligne de statut :** elle peut écrire elle-même le fichier de données (format ci-dessous).

Conseils :

- **Afficher l'icône en permanence** : Windows 11 range les nouvelles icônes dans le menu masqué (chevron `^`). Glissez-la dans la barre des tâches, ou activez-la dans *Paramètres > Personnalisation > Barre des tâches > Autres icônes de la barre d'état système*.
- **Démarrage automatique** : clic droit > *Lancer au démarrage de Windows*.

## Utilisation

| Action | Effet |
|---|---|
| Glisser la fenêtre | Déplacer (position mémorisée) |
| Double-clic sur la fenêtre | Basculer entre le mode complet et le mode compact |
| Clic gauche sur l'icône de notification | Afficher / masquer le widget |
| Clic droit (fenêtre ou icône) | Menu : actualiser, configuration, compact, premier plan, thème, opacité, démarrage, quitter |
| Alt+F4 sur le widget | Masque la fenêtre sans quitter l'application |

L'en-tête indique l'heure des dernières données reçues (« maj 14:05 »).

## Fonctionnement

1. Après chaque réponse, Claude Code transmet à la commande de ligne de statut un JSON qui contient `rate_limits.five_hour` et `rate_limits.seven_day` (pourcentage utilisé et heure de remise à zéro).
2. La commande (`ClaudeUsageWidget.exe --statusline` ou claude-hud) enregistre ces valeurs dans `%APPDATA%\ClaudeUsageWidget\usage.json`. Si rien n'a changé, le fichier n'est réécrit qu'une fois toutes les 30 secondes au plus.
3. Le widget vérifie ce fichier toutes les 5 secondes et se met à jour dès qu'il change.

Format du fichier de données (dates en ISO-8601 ou en secondes Unix) :

```json
{
  "updated_at": "2026-09-16T10:11:52Z",
  "five_hour": { "used_percentage": 23.5, "resets_at": "2026-09-16T12:30:00Z" },
  "seven_day": { "used_percentage": 41.2, "resets_at": "2026-09-21T03:00:00Z" }
}
```

### Données personnelles et sécurité

- **Aucun accès au compte :** le widget ne lit, ne stocke et ne transmet aucun identifiant, mot de passe ou jeton. Il ne fait aucune requête réseau.
- **Fichiers locaux uniquement :** tout est enregistré sur votre poste, hors du dossier du projet, dans `%APPDATA%\ClaudeUsageWidget\` :
  - `settings.ini` : position, apparence et emplacement du fichier de données ;
  - `usage.json` : derniers pourcentages et heures de remise à zéro.

### Limites connues

- **Actualisation liée à Claude Code** : les valeurs ne changent que lorsque Claude Code reçoit une réponse. Ce que vous consommez sur claude.ai ou dans l'application Claude (les quotas sont partagés) n'apparaît qu'au prochain échange dans Claude Code. Une fois l'heure de remise à zéro passée, le widget affiche 0 % en attendant de nouvelles données.
- **Abonnements concernés** : Claude Code ne transmet ces quotas que pour les abonnements Pro et Max.
- **Environnements** : la ligne de statut est une fonctionnalité de Claude Code en terminal. Selon l'environnement (extension d'IDE, par exemple), elle peut ne pas s'exécuter.

## Structure

```
src/
  Program.cs       point d'entrée, instance unique, mode --statusline
  UsageStore.cs    lecture et écriture du fichier de données, pont de ligne de statut
  Settings.cs      préférences .ini, lancement au démarrage (registre HKCU)
  ConfigForm.cs    fenêtre de configuration (fichier de données, branchement de Claude Code)
  WidgetForm.cs    fenêtre, dessin, thèmes, icône de notification, menu
build.ps1          compilation avec csc.exe (paramètre -Version)
.github/workflows/release.yml   compilation automatique et publication des Releases
```

## Publier une nouvelle version

La compilation et la publication sont automatisées par GitHub Actions :

```powershell
git tag v1.1.0
git push origin v1.1.0
```

Le workflow compile l'exe sur un runner Windows avec ce numéro de version, calcule son empreinte SHA-256, puis crée la Release avec les deux fichiers et des notes générées à partir des commits. Un tag contenant un tiret (par exemple `v1.1.0-beta.1`) est publié comme préversion.

Pour compiler sans rien publier, lancez le workflow manuellement (onglet *Actions* > *Build et Release* > *Run workflow*). L'exe est alors disponible en artefact du workflow.
