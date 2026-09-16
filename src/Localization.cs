using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ClaudeUsageWidget
{
    /// <summary>
    /// Textes et formats de l'interface, repris des réglages Windows :
    /// - langue des textes : langue d'affichage (CurrentUICulture), anglais si elle n'est pas traduite ;
    /// - dates, heures et pourcentages : format régional (CurrentCulture).
    /// Pour ajouter une langue : créer un dictionnaire avec les mêmes clés et l'enregistrer dans <see cref="Languages"/>.
    /// Une clé absente d'une traduction retombe sur l'anglais.
    /// </summary>
    static class L
    {
        static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            // Widget
            { "Title", "Claude usage" },
            { "ShortTitle", "Claude" },
            { "Loading", "Loading…" },
            { "LoadingShort", "loading…" },
            { "ErrorShort", "error" },
            { "WindowSession", "Session (5h)" },
            { "WindowWeek", "Week" },
            { "WindowSpend", "Spend limit" },
            { "CompactSession", "5h" },
            { "CompactWeek", "wk" },
            { "Updated", "updated {0}" },
            { "ResetsIn", "Resets {0} · in {1}" },
            { "ResetDone", "Reset at {0} · waiting for Claude Code" },
            { "NoSession", "No active session" },
            { "Tomorrow", "tomorrow {0}" },
            { "TomorrowShort", "tmrw {0}" },
            { "SpanDaysHours", "{0}d {1}h" },
            { "SpanHoursMinutes", "{0}h {1:00}m" },
            { "SpanMinutes", "{0} min" },
            { "TipSession", "5h: {0}" },
            { "TipWeek", "Week: {0}" },

            // Menu
            { "MenuShow", "Show widget" },
            { "MenuRefresh", "Refresh now" },
            { "MenuSettings", "Settings…" },
            { "MenuCompact", "Compact mode (double-click)" },
            { "MenuTopMost", "Always on top" },
            { "MenuTheme", "Theme" },
            { "ThemeDark", "Dark" },
            { "ThemeLight", "Light" },
            { "ThemeSystem", "Match Windows" },
            { "MenuOpacity", "Opacity" },
            { "MenuStartup", "Start with Windows" },
            { "MenuQuit", "Quit" },

            // Fichier de données
            { "ErrWaiting", "Waiting for Claude Code: click here" },
            { "ErrUnreadable", "Data file unreadable" },
            { "ErrNoQuota", "No quota in the data file" },
            { "ErrLocked", "Data file locked, retrying shortly" },

            // Ligne de statut de Claude Code
            { "StatusSession", "5h {0}%" },
            { "StatusWeek", "7d {0}%" },

            // Configuration
            { "CfgTitle", "Claude usage · Settings" },
            { "CfgSourceHint", "The widget accesses neither your account nor the network. It shows the quotas that Claude Code passes to its status line (an official feature), saved to a local file." },
            { "CfgFileHeading", "Data file" },
            { "Browse", "Browse…" },
            { "Default", "Default" },
            { "Check", "Check" },
            { "CfgStatusLineHint", "Manual configuration: add this block to %USERPROFILE%\\.claude\\settings.json, then restart Claude Code." },
            { "CfgExistingHint", "Already using another status line? It can write this file itself: the format is described in the README." },
            { "Copy", "Copy" },
            { "Copied", "Copied" },
            { "CopyFailed", "Failed" },
            { "Save", "Save" },
            { "Cancel", "Cancel" },
            { "CfgNoFile", "No file specified" },
            { "CfgNoData", "No data received yet." },
            { "CfgReceived", "Data received {0} ({1})." },
            { "AgoNow", "just now" },
            { "AgoMinutes", "{0} min ago" },
            { "AgoHours", "{0} h ago" },
            { "AgoDays", "{0} d ago" },
            { "JsonFilter", "JSON files (*.json)|*.json" },
            { "ErrPathInvalid", "Enter a full path to a .json file." },
            { "ErrCreateFolder", "Unable to create the folder: {0}" },

            // Mise en route
            { "CfgSetupHeading", "Getting started" },
            { "SetupCli", "Claude Code (command line)" },
            { "SetupCliFound", "Installed: {0}" },
            { "SetupCliMissing", "Not found. The VS Code extension and the desktop app are not enough: install Claude Code for the terminal." },
            { "SetupInstall", "Install…" },
            { "SetupStatusLine", "Status line" },
            { "SlConnected", "Connected" },
            { "SlOutdated", "Connected to another copy of the widget: update it." },
            { "SlNotConfigured", "Not connected" },
            { "SlOther", "Not connected. Your current status line will be kept." },
            { "SlInvalid", "settings.json is unreadable: {0}" },
            { "SlDuplicate", " Duplicate statusLine keys will be merged." },
            { "Connect", "Connect" },
            { "Repair", "Update" },
            { "Disconnect", "Disconnect" },
            { "SetupData", "Usage data" },
            { "DataReceived", "Received {0} ({1})" },
            { "DataWaitingRunning", "Claude Code is open: send it a message to receive data." },
            { "DataWaiting", "None yet: launch Claude Code and send it a message." },
            { "LaunchClaude", "Launch Claude Code" },
            { "OptLaunchOnStart", "Launch Claude Code when the widget starts (if it isn't already open)" },
            { "OptWorkDir", "Claude Code working folder:" },
            { "OptShowAssistant", "Show this window at startup while the status line isn't connected" },
            { "FolderDialog", "Folder in which Claude Code is launched" },
            { "CfgAdvancedHeading", "Advanced" },
            { "ConfirmConnect", "The widget will add its command to the Claude Code status line in:\n{0}\n\nA backup copy of this file is created first." },
            { "ConfirmConnectOther", "\n\nYour current status line will be kept: both will work." },
            { "ConfirmConnectInstall", "\n\nThe widget will first be copied to {0} so that the path saved in Claude Code stays valid, then restarted from there." },
            { "ConfirmContinue", "\n\nContinue?" },
            { "ConnectDone", "Status line connected.{0}\n\nRestart the Claude Code sessions that are already open so they take it into account." },
            { "BackupInfo", "\nBackup: {0}" },
            { "RestartInstalled", "\n\nThe widget will now restart from {0}." },
            { "ConfirmDisconnect", "The widget's command will be removed from the Claude Code status line. A backup copy of settings.json is created first.\n\nContinue?" },
            { "DisconnectDone", "Status line disconnected.{0}" },
            { "ActionFailed", "The operation failed: {0}" },
            { "LaunchFailed", "Unable to launch Claude Code: {0}" },
            { "MenuLaunchOnStart", "Launch Claude Code at startup" },

            // À propos
            { "MenuAbout", "About…" },
            { "AboutTitle", "About" },
            { "AboutVersion", "Version {0}" },
            { "AboutDescription", "Shows your Claude subscription usage (5-hour session and week) and reset times, from the data Claude Code passes to its status line." },
            { "AboutAuthor", "Author: {0}" },
            { "AboutRepository", "Source code" },
            { "AboutDisclaimer", "Independent tool, not affiliated with or endorsed by Anthropic." },
            { "Close", "Close" },

            // Désinstallation
            { "MenuUninstall", "Uninstall…" },
            { "UninstallTitle", "Uninstall Claude usage" },
            { "UninstallIntro", "The following steps will be carried out:" },
            { "UninstallStepStatusLine", "• Disconnect the Claude Code status line and restore the original one, if any" },
            { "UninstallStepStartup", "• Remove the start with Windows entry" },
            { "UninstallStepFiles", "• Delete the installed copy ({0}) and its entry in Installed apps" },
            { "UninstallRemoveData", "Also delete my settings and usage data:" },
            { "UninstallKeepBackups", "Backup copies of settings.json are kept." },
            { "UninstallButton", "Uninstall" },
            { "UninstallDone", "Claude usage has been uninstalled." },
            { "UninstallDoneRestart", "Restart the Claude Code sessions that are open so they stop calling the widget." },
            { "UninstallDoneDownloaded", "You can also delete the file you launched:\n{0}" },
            { "UninstallErrors", "Some steps failed:\n{0}" },
            { "UninstallErrStatusLine", "status line: {0}" },
            { "UninstallErrRegistry", "Windows registry: {0}" },
            { "UninstallErrData", "settings and data: {0}" },
            { "UninstallErrFiles", "installed copy: {0}" },
        };

        static readonly Dictionary<string, string> French = new Dictionary<string, string>
        {
            // Widget
            { "Title", "Utilisation Claude" },
            { "ShortTitle", "Claude" },
            { "Loading", "Chargement…" },
            { "LoadingShort", "chargement…" },
            { "ErrorShort", "erreur" },
            { "WindowSession", "Session (5 h)" },
            { "WindowWeek", "Semaine" },
            { "WindowSpend", "Plafond de dépenses" },
            { "CompactSession", "5 h" },
            { "CompactWeek", "sem." },
            { "Updated", "maj {0}" },
            { "ResetsIn", "RAZ {0} · dans {1}" },
            { "ResetDone", "RAZ à {0} · en attente de Claude Code" },
            { "NoSession", "Aucune session en cours" },
            { "Tomorrow", "demain {0}" },
            { "TomorrowShort", "dem. {0}" },
            { "SpanDaysHours", "{0} j {1} h" },
            { "SpanHoursMinutes", "{0} h {1:00}" },
            { "SpanMinutes", "{0} min" },
            { "TipSession", "5 h : {0}" },
            { "TipWeek", "Semaine : {0}" },

            // Menu
            { "MenuShow", "Afficher le widget" },
            { "MenuRefresh", "Actualiser maintenant" },
            { "MenuSettings", "Configuration…" },
            { "MenuCompact", "Mode compact (double-clic)" },
            { "MenuTopMost", "Toujours au premier plan" },
            { "MenuTheme", "Thème" },
            { "ThemeDark", "Sombre" },
            { "ThemeLight", "Clair" },
            { "ThemeSystem", "Selon Windows" },
            { "MenuOpacity", "Opacité" },
            { "MenuStartup", "Lancer au démarrage de Windows" },
            { "MenuQuit", "Quitter" },

            // Fichier de données
            { "ErrWaiting", "En attente de Claude Code : cliquez ici" },
            { "ErrUnreadable", "Fichier de données illisible" },
            { "ErrNoQuota", "Aucun quota dans le fichier de données" },
            { "ErrLocked", "Fichier de données verrouillé, nouvel essai bientôt" },

            // Ligne de statut de Claude Code
            { "StatusSession", "5h {0}%" },
            { "StatusWeek", "7j {0}%" },

            // Configuration
            { "CfgTitle", "Utilisation Claude · Configuration" },
            { "CfgSourceHint", "Le widget n'accède ni à votre compte ni au réseau. Il affiche les quotas que Claude Code transmet à sa ligne de statut (fonctionnalité officielle), enregistrés dans un fichier local." },
            { "CfgFileHeading", "Fichier de données" },
            { "Browse", "Parcourir…" },
            { "Default", "Par défaut" },
            { "Check", "Vérifier" },
            { "CfgStatusLineHint", "Configuration manuelle : ajoutez ce bloc dans %USERPROFILE%\\.claude\\settings.json, puis redémarrez Claude Code." },
            { "CfgExistingHint", "Vous utilisez déjà une autre ligne de statut ? Elle peut écrire ce fichier elle-même : le format est décrit dans le README." },
            { "Copy", "Copier" },
            { "Copied", "Copié" },
            { "CopyFailed", "Échec" },
            { "Save", "Enregistrer" },
            { "Cancel", "Annuler" },
            { "CfgNoFile", "Aucun fichier indiqué" },
            { "CfgNoData", "Aucune donnée reçue pour l'instant." },
            { "CfgReceived", "Données reçues le {0} ({1})." },
            { "AgoNow", "à l'instant" },
            { "AgoMinutes", "il y a {0} min" },
            { "AgoHours", "il y a {0} h" },
            { "AgoDays", "il y a {0} j" },
            { "JsonFilter", "Fichiers JSON (*.json)|*.json" },
            { "ErrPathInvalid", "Indiquez un chemin complet vers un fichier .json." },
            { "ErrCreateFolder", "Impossible de créer le dossier : {0}" },

            // Mise en route
            { "CfgSetupHeading", "Mise en route" },
            { "SetupCli", "Claude Code (ligne de commande)" },
            { "SetupCliFound", "Installé : {0}" },
            { "SetupCliMissing", "Introuvable. L'extension VS Code et l'application de bureau ne suffisent pas : installez Claude Code pour le terminal." },
            { "SetupInstall", "Installer…" },
            { "SetupStatusLine", "Ligne de statut" },
            { "SlConnected", "Connectée" },
            { "SlOutdated", "Connectée à une autre copie du widget : mettez-la à jour." },
            { "SlNotConfigured", "Non connectée" },
            { "SlOther", "Non connectée. Votre ligne de statut actuelle sera conservée." },
            { "SlInvalid", "settings.json est illisible : {0}" },
            { "SlDuplicate", " Les clés statusLine en double seront fusionnées." },
            { "Connect", "Connecter" },
            { "Repair", "Mettre à jour" },
            { "Disconnect", "Déconnecter" },
            { "SetupData", "Données d'utilisation" },
            { "DataReceived", "Reçues le {0} ({1})" },
            { "DataWaitingRunning", "Claude Code est ouvert : envoyez-lui un message pour recevoir les données." },
            { "DataWaiting", "Aucune pour l'instant : lancez Claude Code et envoyez-lui un message." },
            { "LaunchClaude", "Lancer Claude Code" },
            { "OptLaunchOnStart", "Lancer Claude Code au démarrage du widget (s'il n'est pas déjà ouvert)" },
            { "OptWorkDir", "Dossier de travail de Claude Code :" },
            { "OptShowAssistant", "Afficher cette fenêtre au démarrage tant que la ligne de statut n'est pas connectée" },
            { "FolderDialog", "Dossier dans lequel Claude Code est lancé" },
            { "CfgAdvancedHeading", "Avancé" },
            { "ConfirmConnect", "Le widget va ajouter sa commande à la ligne de statut de Claude Code, dans :\n{0}\n\nUne copie de sauvegarde de ce fichier est créée avant la modification." },
            { "ConfirmConnectOther", "\n\nVotre ligne de statut actuelle sera conservée : les deux fonctionneront." },
            { "ConfirmConnectInstall", "\n\nLe widget sera d'abord copié dans {0}, pour que le chemin enregistré dans Claude Code reste valable, puis redémarré depuis cet emplacement." },
            { "ConfirmContinue", "\n\nContinuer ?" },
            { "ConnectDone", "Ligne de statut connectée.{0}\n\nRedémarrez les sessions Claude Code déjà ouvertes pour qu'elles la prennent en compte." },
            { "BackupInfo", "\nSauvegarde : {0}" },
            { "RestartInstalled", "\n\nLe widget va maintenant redémarrer depuis {0}." },
            { "ConfirmDisconnect", "La commande du widget va être retirée de la ligne de statut de Claude Code. Une copie de sauvegarde de settings.json est créée avant la modification.\n\nContinuer ?" },
            { "DisconnectDone", "Ligne de statut déconnectée.{0}" },
            { "ActionFailed", "L'opération a échoué : {0}" },
            { "LaunchFailed", "Impossible de lancer Claude Code : {0}" },
            { "MenuLaunchOnStart", "Lancer Claude Code au démarrage" },

            // À propos
            { "MenuAbout", "À propos…" },
            { "AboutTitle", "À propos" },
            { "AboutVersion", "Version {0}" },
            { "AboutDescription", "Affiche l'utilisation de votre abonnement Claude (session de 5 h et semaine) et les heures de remise à zéro, à partir des données que Claude Code transmet à sa ligne de statut." },
            { "AboutAuthor", "Auteur : {0}" },
            { "AboutRepository", "Code source" },
            { "AboutDisclaimer", "Outil indépendant, non affilié à Anthropic et non approuvé par Anthropic." },
            { "Close", "Fermer" },

            // Désinstallation
            { "MenuUninstall", "Désinstaller…" },
            { "UninstallTitle", "Désinstaller Utilisation Claude" },
            { "UninstallIntro", "Les opérations suivantes vont être effectuées :" },
            { "UninstallStepStatusLine", "• Déconnecter la ligne de statut de Claude Code et restaurer celle d'origine, s'il y en avait une" },
            { "UninstallStepStartup", "• Retirer le lancement au démarrage de Windows" },
            { "UninstallStepFiles", "• Supprimer la copie installée ({0}) et son entrée dans « Applications installées »" },
            { "UninstallRemoveData", "Supprimer aussi mes réglages et données d'utilisation :" },
            { "UninstallKeepBackups", "Les copies de sauvegarde de settings.json sont conservées." },
            { "UninstallButton", "Désinstaller" },
            { "UninstallDone", "Utilisation Claude a été désinstallé." },
            { "UninstallDoneRestart", "Redémarrez les sessions Claude Code ouvertes pour qu'elles n'appellent plus le widget." },
            { "UninstallDoneDownloaded", "Vous pouvez aussi supprimer le fichier que vous avez lancé :\n{0}" },
            { "UninstallErrors", "Certaines étapes ont échoué :\n{0}" },
            { "UninstallErrStatusLine", "ligne de statut : {0}" },
            { "UninstallErrRegistry", "registre Windows : {0}" },
            { "UninstallErrData", "réglages et données : {0}" },
            { "UninstallErrFiles", "copie installée : {0}" },
        };

        /// <summary>Traductions disponibles, par code de langue ISO 639-1.</summary>
        static readonly Dictionary<string, Dictionary<string, string>> Languages = new Dictionary<string, Dictionary<string, string>>
        {
            { "en", English },
            { "fr", French },
        };

        static readonly Dictionary<string, string> Table;

        /// <summary>Culture utilisée pour les dates, heures et nombres.</summary>
        public static readonly CultureInfo Format = CultureInfo.CurrentCulture;

        /// <summary>Jour et mois abrégés, sans l'année (ex. « 16/09 », « 9/16 », « 16.09 »).</summary>
        static readonly string MonthDayPattern;

        static L()
        {
            Dictionary<string, string> table;
            Table = Languages.TryGetValue(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, out table) ? table : English;

            // Dérive le motif du format de date court régional en retirant l'année et son séparateur.
            string pattern = Regex.Replace(Format.DateTimeFormat.ShortDatePattern, @"^y+[^dMy]*|[^dMy]*y+$|y+[^dMy]*", "").Trim();
            if (pattern.Length == 0) pattern = "d/M";
            MonthDayPattern = pattern.Length == 1 ? "%" + pattern : pattern;
        }

        /// <summary>Texte traduit.</summary>
        public static string T(string key)
        {
            string value;
            if (Table.TryGetValue(key, out value) || English.TryGetValue(key, out value)) return value;
            return key;
        }

        /// <summary>Texte traduit avec paramètres, formatés selon la culture régionale.</summary>
        public static string F(string key, params object[] args)
        {
            return string.Format(Format, T(key), args);
        }

        public static string Time(DateTime local)
        {
            return local.ToString("t", Format);
        }

        public static string MonthDay(DateTime local)
        {
            return local.ToString(MonthDayPattern, Format);
        }

        public static string WeekdayMonthDay(DateTime local)
        {
            return local.ToString("ddd", Format) + " " + MonthDay(local);
        }

        public static string Percent(double value)
        {
            return (Math.Round(value) / 100.0).ToString("P0", Format);
        }
    }
}
