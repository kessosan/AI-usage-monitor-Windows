# AI usage monitor Windows

Mini-widget Windows 11 qui affiche le taux d'usage de votre abonnement Claude (Pro / Max) et l'heure de remise à zéro :

- **Session (5 h)** : pourcentage consommé, heure de RAZ et compte à rebours ;
- **Semaine** : pourcentage consommé et date de RAZ.

Deux affichages :

- une **fenêtre flottante** « Utilisation Claude » sans bordure, déplaçable, aimantée aux bords de l'écran, en thème sombre, clair ou selon Windows, avec un mode compact d'une seule ligne (double-clic) ;
- une **icône dans la zone de notification** qui affiche le % de la session. Sa couleur change à 75 % (jaune) puis à 90 % (rouge).

Le widget **n'utilise aucun identifiant et n'accède pas au réseau**. Il affiche les quotas que Claude Code transmet officiellement à sa [ligne de statut](https://code.claude.com/docs/en/statusline) (champ `rate_limits`), enregistrés dans un fichier local.

## Installation

### Prérequis

- Un abonnement claude.ai **Pro ou Max**.
- **Claude Code en ligne de commande** (commande `claude`). L'extension VS Code et l'application de bureau ne suffisent pas : voir [Résolution de problèmes](#résolution-de-problèmes) pour l'installer.
- [Git for Windows](https://git-scm.com/downloads/win), uniquement si vous utilisez déjà une autre ligne de statut dans Claude Code.

### Télécharger et lancer

1. Ouvrez la page **Releases** du dépôt et téléchargez `ClaudeUsageWidget.exe` depuis la dernière version.
2. *(Facultatif)* Vérifiez l'intégrité du fichier : l'empreinte affichée par la commande ci-dessous doit correspondre à celle de `SHA256SUMS.txt`.
   ```powershell
   Get-FileHash .\ClaudeUsageWidget.exe -Algorithm SHA256
   ```
3. Lancez l'exe, depuis le dossier de téléchargement par exemple. L'assistant de mise en route s'ouvre (voir ci-dessous).

L'exécutable n'est pas signé numériquement. Au premier lancement, Windows SmartScreen peut afficher « Windows a protégé votre ordinateur » : cliquez sur *Informations complémentaires*, puis *Exécuter quand même*.

### Mise en route

Au premier lancement, la fenêtre **Mise en route** vérifie trois étapes et propose l'action correspondante :

| Étape | Vérification | Action |
|---|---|---|
| **Claude Code (ligne de commande)** | La commande `claude` est installée | *Installer…* ouvre la page d'installation |
| **Ligne de statut** | Claude Code transmet ses quotas au widget | *Connecter* configure Claude Code automatiquement |
| **Données d'utilisation** | Des quotas ont été reçus | *Lancer Claude Code* ouvre un terminal |

**Connecter** fait, après confirmation :

- **installation du widget** : copie de l'exe dans `%LOCALAPPDATA%\Programs\ClaudeUsageWidget\`, puis redémarrage depuis cet emplacement stable ;
- **configuration de Claude Code** : ajout de la commande du widget à la ligne de statut, dans `%USERPROFILE%\.claude\settings.json`. Une copie de sauvegarde du fichier est créée avant chaque modification ;
- **ligne de statut existante** (par exemple un autre outil d'affichage) : elle est **conservée**, et les deux fonctionnent.

Il suffit ensuite d'envoyer un message dans Claude Code : les pourcentages s'affichent après la réponse. Les sessions Claude Code déjà ouvertes doivent être redémarrées pour prendre en compte la connexion.

**Déconnecter** retire la commande du widget et restaure la ligne de statut d'origine.

Par défaut, le widget **lance Claude Code à son démarrage**, dans un terminal (Windows Terminal, ou l'invite de commandes à défaut), s'il n'est pas déjà ouvert. Cette option et le dossier de travail se règlent dans la même fenêtre (clic droit > *Configuration…*).

### Mettre à jour le widget

Quittez le widget (clic droit > *Quitter*), téléchargez la nouvelle version et lancez-la. L'assistant détecte que la copie installée est plus ancienne et propose **Mettre à jour**.

### Désinstaller le widget

Deux possibilités :

- **Depuis le widget** : clic droit > *Désinstaller…* ;
- **Depuis Windows** : *Paramètres > Applications > Applications installées*, entrée « Utilisation Claude » > *Désinstaller*.

Après confirmation, la désinstallation :

1. retire la commande du widget de la ligne de statut de Claude Code et restaure la ligne de statut d'origine s'il y en avait une ;
2. supprime le lancement au démarrage de Windows et l'entrée dans « Applications installées » ;
3. supprime la copie installée et, sauf si vous décochez la case, les réglages et données (`%APPDATA%\ClaudeUsageWidget\`).

Les copies de sauvegarde de `settings.json` sont conservées. Redémarrez ensuite les sessions Claude Code ouvertes. Si vous aviez lancé le widget depuis un autre dossier (téléchargements par exemple), supprimez aussi ce fichier.

### Compiler depuis les sources

Aucun SDK n'est nécessaire : la compilation utilise le compilateur C# inclus dans Windows (.NET Framework 4.8).

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\dist\ClaudeUsageWidget.exe
```

### Conseils

- **Afficher l'icône en permanence** : Windows 11 range les nouvelles icônes dans le menu masqué (chevron `^`). Glissez-la dans la barre des tâches, ou activez-la dans *Paramètres > Personnalisation > Barre des tâches > Autres icônes de la barre d'état système*.
- **Démarrage automatique** : clic droit > *Lancer au démarrage de Windows*.

## Utilisation

| Action | Effet |
|---|---|
| Glisser la fenêtre | Déplacer (position mémorisée) |
| Double-clic sur la fenêtre | Basculer entre le mode complet et le mode compact |
| Clic sur « En attente de Claude Code » | Ouvrir la fenêtre de mise en route |
| Clic gauche sur l'icône de notification | Afficher / masquer le widget |
| Clic droit (fenêtre ou icône) | Menu : actualiser, lancer Claude Code, configuration, compact, premier plan, thème, opacité, démarrage avec Windows, lancement de Claude Code au démarrage, à propos, désinstaller, quitter |
| Alt+F4 sur le widget | Masque la fenêtre sans quitter l'application |

L'en-tête indique l'heure des dernières données reçues (« maj 14:05 »).

### Langue et formats

Le widget reprend les réglages de Windows :

- **langue des textes** : langue d'affichage de Windows. Français et anglais sont disponibles ; pour toute autre langue, les textes s'affichent en anglais ;
- **dates, heures et pourcentages** : format régional de Windows (par exemple `14:36` et `24 %` en France, `2:36 PM` et `24%` aux États-Unis).

Pour ajouter une langue, créez un dictionnaire de traductions dans `src/Localization.cs`, avec les mêmes clés que l'anglais.

## Fonctionnement

1. Après chaque réponse, Claude Code transmet à la commande de ligne de statut un JSON qui contient `rate_limits.five_hour` et `rate_limits.seven_day` (pourcentage utilisé et heure de remise à zéro).
2. La commande de ligne de statut (`ClaudeUsageWidget.exe --statusline`, ajoutée par *Connecter*) enregistre ces valeurs dans `%APPDATA%\ClaudeUsageWidget\usage.json`, puis affiche un résumé court dans Claude Code (`[Opus 5] | 5h 24% | 7j 41%`). Si rien n'a changé, le fichier n'est réécrit qu'une fois toutes les 30 secondes au plus.
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
  - `settings.ini` : position, apparence, options de lancement et emplacement du fichier de données ;
  - `usage.json` : derniers pourcentages et heures de remise à zéro ;
  - `statusline-original.json` : votre ligne de statut d'origine, si elle a été conservée à la connexion, pour pouvoir la restaurer.
- **Registre Windows (utilisateur courant uniquement) :** lancement au démarrage (`HKCU\...\Run`) si vous l'activez, et entrée « Applications installées » (`HKCU\...\Uninstall`) pour la copie installée. Les deux sont retirés à la désinstallation.
- **Configuration de Claude Code :** le widget ne modifie `settings.json` que lorsque vous cliquez sur *Connecter*, *Mettre à jour*, *Déconnecter* ou *Désinstaller*, et seule la clé `statusLine` change. Les autres réglages sont conservés, dans le même ordre, avec une indentation normalisée à deux espaces. Une sauvegarde `settings.json.bak-usage-widget-<date>` est créée avant chaque modification.

### Limites connues

- **Actualisation liée à Claude Code** : les valeurs ne changent que lorsque Claude Code reçoit une réponse. Ce que vous consommez sur claude.ai ou dans l'application Claude (les quotas sont partagés) n'apparaît qu'au prochain échange dans Claude Code. Une fois l'heure de remise à zéro passée, le widget affiche 0 % en attendant de nouvelles données.
- **Abonnements concernés** : Claude Code ne transmet ces quotas que pour les abonnements Pro et Max.
- **Environnements** : la ligne de statut est une fonctionnalité de Claude Code en terminal. Selon l'environnement (extension d'IDE, par exemple), elle peut ne pas s'exécuter.

## Résolution de problèmes

### `'claude' n'est pas reconnu en tant que commande interne ou externe`

L'extension VS Code et l'application de bureau utilisent leur propre copie de Claude Code : elles n'installent pas la commande `claude` dans le terminal. Deux causes sont possibles.

- **Claude Code n'est pas installé en ligne de commande** : installez-le depuis PowerShell (invite `PS C:\`), puis ouvrez un nouveau terminal :
  ```powershell
  irm https://claude.ai/install.ps1 | iex
  ```
- **Claude Code est installé, mais son dossier n'est pas dans le PATH** : c'est le cas si `%USERPROFILE%\.local\bin\claude.exe` existe. Ajoutez ce dossier au PATH utilisateur : *Paramètres > Système > Informations système > Paramètres avancés du système > Variables d'environnement*, puis sélectionnez `Path` dans les variables utilisateur, cliquez sur *Modifier > Nouveau* et saisissez `%USERPROFILE%\.local\bin`.

Les terminaux déjà ouverts gardent l'ancien PATH : fermez-les, puis ouvrez-en un nouveau. Pour le terminal intégré de VS Code, fermez **complètement** VS Code (toutes les fenêtres), puis relancez-le. Vérifiez ensuite avec `claude --version`.

En attendant, vous pouvez lancer Claude Code avec son chemin complet : `%USERPROFILE%\.local\bin\claude.exe`.

### Le widget reste sur « En attente de Claude Code »

- **Vous utilisez Claude Code dans l'extension VS Code ou l'application de bureau** : la ligne de statut n'y est pas exécutée, et le widget ne reçoit donc aucune donnée. Lancez `claude` dans un terminal (le terminal intégré de VS Code convient).
- **Aucune réponse reçue depuis le lancement** : Claude Code ne transmet les quotas qu'après la première réponse d'une session. Envoyez un message et attendez la réponse.
- **Claude Code n'a pas été redémarré** après la connexion de la ligne de statut.
- **La ligne de statut n'est pas connectée, ou pointe vers une autre copie de l'exe** : cliquez sur le message du widget (ou clic droit > *Configuration…*), puis sur *Connecter* ou *Mettre à jour*.

La fenêtre de mise en route indique l'état de chaque étape et l'heure des dernières données reçues. `claude doctor` signale aussi les erreurs dans `settings.json`.

### Configuration manuelle de la ligne de statut

*Connecter* configure la ligne de statut automatiquement. Si vous préférez modifier `settings.json` vous-même, retenez que Claude Code n'accepte qu'**une seule** clé `statusLine`. Si vous ajoutez le bloc du widget sous une ligne de statut existante, le fichier contient deux clés `statusLine`, et seule la dernière est prise en compte : l'ancienne ligne de statut disparaît sans message d'erreur. *Connecter* corrige d'ailleurs ce cas en fusionnant les deux.

Sans autre ligne de statut, le bloc à ajouter est fourni dans *Configuration… > Avancé*. Pour conserver une ligne de statut existante, utilisez une seule commande qui transmet les données au widget, puis à votre ligne de statut :

```json
"statusLine": {
  "type": "command",
  "command": "input=$(cat); printf '%s' \"$input\" | \"C:/Users/<vous>/AppData/Local/Programs/ClaudeUsageWidget/ClaudeUsageWidget.exe\" --statusline >/dev/null 2>&1; printf '%s' \"$input\" | { <commande existante>; }"
}
```

- Remplacez `<commande existante>` par la valeur de `command` de votre ligne de statut actuelle, en gardant ses guillemets échappés (`\"`).
- Conservez les autres options de l'ancien bloc, par exemple `refreshInterval`.
- Cette syntaxe suppose que Claude Code exécute la commande avec Git Bash, ce qui est le cas lorsque Git for Windows est installé.

Pensez à sauvegarder `settings.json` avant de le modifier.

## Structure

```
src/
  Program.cs       point d'entrée, instance unique, mode --statusline
  ClaudeCode.cs    détection et lancement de Claude Code, connexion de la ligne de statut, installation
  MiniJson.cs      lecture et écriture de settings.json en conservant l'ordre des clés
  Localization.cs  textes traduits (français, anglais) et formats régionaux
  UsageStore.cs    lecture et écriture du fichier de données, pont de ligne de statut
  Settings.cs      préférences .ini, lancement au démarrage (registre HKCU)
  ConfigForm.cs    fenêtre de mise en route et de configuration
  AboutForm.cs     fenêtre « À propos » (auteur et version injectés à la compilation)
  Uninstall.cs     inscription dans « Applications installées » et désinstallation
  WidgetForm.cs    fenêtre, dessin, thèmes, icône de notification, menu
build.ps1          compilation avec csc.exe (paramètre -Version)
```
