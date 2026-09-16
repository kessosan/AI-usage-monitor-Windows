using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ClaudeUsageWidget
{
    /// <summary>Détection et lancement de Claude Code en ligne de commande.</summary>
    static class ClaudeCli
    {
        public const string InstallDocsUrl = "https://code.claude.com/docs/en/setup";

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(int access, bool inherit, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr handle);

        const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        /// <summary>Chemin de la commande claude en ligne de commande, ou null si elle est introuvable.</summary>
        public static string Find()
        {
            var candidates = new List<string>();
            string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
            string userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            foreach (string dir in (pathVar + ";" + Environment.ExpandEnvironmentVariables(userPath)).Split(';'))
            {
                if (dir.Trim().Length == 0) continue;
                candidates.Add(Path.Combine(dir.Trim(), "claude.exe"));
                candidates.Add(Path.Combine(dir.Trim(), "claude.cmd"));
            }
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            candidates.Add(Path.Combine(home, @".local\bin\claude.exe"));             // installateur natif
            candidates.Add(Path.Combine(local, @"Microsoft\WinGet\Links\claude.exe")); // WinGet
            candidates.Add(Path.Combine(roaming, @"npm\claude.cmd"));                  // npm

            foreach (string candidate in candidates)
            {
                try
                {
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
                catch (Exception)
                {
                    // Entrée de PATH invalide : ignorée.
                }
            }
            return null;
        }

        /// <summary>
        /// Vrai si Claude Code tourne en ligne de commande. Les processus claude.exe de l'application
        /// de bureau et des extensions d'IDE sont ignorés : ils n'exécutent pas la ligne de statut.
        /// </summary>
        public static bool IsRunning(string cliPath)
        {
            foreach (Process process in Process.GetProcessesByName("claude"))
            {
                using (process)
                {
                    string image = ImagePath(process.Id);
                    if (image == null) continue;
                    if (cliPath != null && string.Equals(image, cliPath, StringComparison.OrdinalIgnoreCase)) return true;
                    string lower = image.ToLowerInvariant();
                    if (lower.EndsWith(@"\.local\bin\claude.exe")
                        || lower.Contains(@"\node_modules\@anthropic-ai\")
                        || lower.Contains(@"\winget\packages\anthropic.claudecode"))
                        return true;
                }
            }
            return false;
        }

        static string ImagePath(int processId)
        {
            IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
            if (handle == IntPtr.Zero) return null;
            try
            {
                var name = new StringBuilder(1024);
                int size = name.Capacity;
                return QueryFullProcessImageName(handle, 0, name, ref size) ? name.ToString(0, size) : null;
            }
            finally
            {
                CloseHandle(handle);
            }
        }

        /// <summary>Ouvre Claude Code dans Windows Terminal, ou dans l'invite de commandes à défaut.</summary>
        public static void Launch(string cliPath, string workingDirectory)
        {
            string dir = Directory.Exists(workingDirectory)
                ? workingDirectory.TrimEnd('\\')
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (dir.EndsWith(":")) dir += "\\."; // « C:\ » : évite un antislash final avant le guillemet

            string wt = FindWindowsTerminal();
            var start = wt != null
                ? new ProcessStartInfo(wt, "-d \"" + dir + "\" \"" + cliPath + "\"")
                : new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "cmd.exe"), "/k \"\"" + cliPath + "\"\"");
            start.WorkingDirectory = dir;
            start.UseShellExecute = true;
            Process.Start(start);
        }

        static string FindWindowsTerminal()
        {
            string alias = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\wt.exe");
            return File.Exists(alias) ? alias : null;
        }

        public static void OpenInstallDocs()
        {
            try { Process.Start(InstallDocsUrl); }
            catch (Win32Exception) { }
        }
    }

    enum StatusLineState
    {
        NotConfigured,      // aucune ligne de statut
        Connected,          // commande du widget présente, chemin à jour
        OutdatedPath,       // commande du widget présente, mais vers un autre exécutable
        OtherStatusLine,    // une autre ligne de statut est configurée
        Invalid,            // settings.json illisible
    }

    class StatusLineInfo
    {
        public StatusLineState State;
        public bool HasDuplicateKeys;
        public string Error;
    }

    /// <summary>
    /// Connexion du widget à la ligne de statut de Claude Code, dans settings.json.
    /// Une ligne de statut existante est conservée en enchaînant les deux commandes (Git Bash).
    /// Chaque modification crée une copie de sauvegarde de settings.json.
    /// </summary>
    static class StatusLineSetup
    {
        const string ChainPrefix = "input=$(cat); ";
        static readonly Regex WidgetCommand = new Regex("\"([^\"]*ClaudeUsageWidget\\.exe)\"\\s+--statusline", RegexOptions.IgnoreCase);
        static readonly Regex ChainedInner = new Regex(@"\|\s*\{\s(.*);\s\}\s*$", RegexOptions.Singleline);

        public static string SettingsPath()
        {
            string dir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            return Path.Combine(dir, "settings.json");
        }

        /// <summary>Ligne de statut d'origine, mémorisée pour pouvoir la restaurer à la déconnexion.</summary>
        static string OriginalPath
        {
            get { return Path.Combine(Settings.AppDataDir, "statusline-original.json"); }
        }

        public static string CommandPathFor(string exePath)
        {
            return exePath.Replace('\\', '/');
        }

        // ------------------------------------------------------------------ état

        public static StatusLineInfo Inspect(string exePath)
        {
            var info = new StatusLineInfo();
            JsonObject root;
            try
            {
                root = Load();
            }
            catch (Exception ex)
            {
                info.State = StatusLineState.Invalid;
                info.Error = ex.Message;
                return info;
            }

            info.HasDuplicateKeys = root.CountOf("statusLine") > 1;
            JsonObject widget = FindWidgetEntry(root);
            if (widget != null)
            {
                Match m = WidgetCommand.Match(CommandOf(widget));
                info.State = SamePath(m.Groups[1].Value, exePath) ? StatusLineState.Connected : StatusLineState.OutdatedPath;
            }
            else if (FindOtherEntry(root) != null)
                info.State = StatusLineState.OtherStatusLine;
            else
                info.State = StatusLineState.NotConfigured;
            return info;
        }

        // ------------------------------------------------------------------ connexion

        /// <summary>Ajoute (ou met à jour) la commande du widget. Retourne le chemin de la sauvegarde, ou null.</summary>
        public static string Connect(string exePath)
        {
            JsonObject root = Load();
            string own = "\"" + CommandPathFor(exePath) + "\" " + Program.StatusLineArgument;
            JsonObject widget = FindWidgetEntry(root);
            JsonObject other = FindOtherEntry(root);
            JsonObject result;

            if (widget != null && IsChained(CommandOf(widget)))
            {
                // Déjà enchaînée : on met seulement à jour le chemin de l'exécutable.
                result = CopyWithCommand(widget, WidgetCommand.Replace(CommandOf(widget), own));
            }
            else if (other != null)
            {
                // Conserver la ligne de statut existante en l'enchaînant après le widget.
                SaveOriginal(other);
                result = CopyWithCommand(other,
                    ChainPrefix +
                    "printf '%s' \"$input\" | " + own + " >/dev/null 2>&1; " +
                    "printf '%s' \"$input\" | { " + CommandOf(other) + "; }");
            }
            else
            {
                result = widget != null ? CopyWithCommand(widget, own) : new JsonObject
                {
                    new KeyValuePair<string, object>("type", "command"),
                    new KeyValuePair<string, object>("command", own),
                };
            }

            root.Set("statusLine", result);
            return Save(root);
        }

        /// <summary>Retire la commande du widget et restaure la ligne de statut d'origine s'il y en avait une.</summary>
        public static string Disconnect()
        {
            JsonObject root = Load();
            JsonObject widget = FindWidgetEntry(root);
            if (widget == null) return null;

            string command = CommandOf(widget);
            if (IsChained(command))
            {
                string inner = ChainedInner.Match(command).Groups[1].Value;
                JsonObject original = LoadOriginal();
                root.Set("statusLine", original != null && CommandOf(original) == inner ? original : CopyWithCommand(widget, inner));
            }
            else
            {
                JsonObject other = FindOtherEntry(root);
                if (other != null) root.Set("statusLine", other);
                else root.Remove("statusLine");
            }

            string backup = Save(root);
            try { File.Delete(OriginalPath); } catch (Exception) { }
            return backup;
        }

        // ------------------------------------------------------------------ outils

        static JsonObject Load()
        {
            string path = SettingsPath();
            if (!File.Exists(path)) return new JsonObject();
            string text = File.ReadAllText(path, Encoding.UTF8);
            if (text.Trim().Length == 0) return new JsonObject();
            var root = MiniJson.Parse(text) as JsonObject;
            if (root == null) throw new FormatException("settings.json ne contient pas un objet JSON");
            return root;
        }

        /// <summary>Sauvegarde settings.json puis écrit la nouvelle version. Retourne le chemin de la sauvegarde.</summary>
        static string Save(JsonObject root)
        {
            string path = SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string backup = null;
            if (File.Exists(path))
            {
                backup = path + ".bak-usage-widget-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(path, backup, true);
            }
            string tmp = path + ".tmp-usage-widget";
            File.WriteAllText(tmp, MiniJson.Write(root), new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
            return backup;
        }

        static JsonObject FindWidgetEntry(JsonObject root)
        {
            foreach (object value in root.GetAll("statusLine"))
            {
                var entry = value as JsonObject;
                if (entry != null && WidgetCommand.IsMatch(CommandOf(entry))) return entry;
            }
            return null;
        }

        static JsonObject FindOtherEntry(JsonObject root)
        {
            foreach (object value in root.GetAll("statusLine"))
            {
                var entry = value as JsonObject;
                if (entry != null && CommandOf(entry).Length > 0 && !WidgetCommand.IsMatch(CommandOf(entry))) return entry;
            }
            return null;
        }

        static string CommandOf(JsonObject entry)
        {
            return entry.Get("command") as string ?? "";
        }

        static bool IsChained(string command)
        {
            return command.StartsWith(ChainPrefix, StringComparison.Ordinal) && ChainedInner.IsMatch(command);
        }

        static JsonObject CopyWithCommand(JsonObject source, string command)
        {
            var copy = new JsonObject();
            copy.AddRange(source);
            copy.Set("command", command);
            if (copy.Get("type") == null) copy.Insert(0, new KeyValuePair<string, object>("type", "command"));
            return copy;
        }

        static bool SamePath(string a, string b)
        {
            return string.Equals(a.Replace('\\', '/'), b.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        }

        static void SaveOriginal(JsonObject entry)
        {
            Directory.CreateDirectory(Settings.AppDataDir);
            File.WriteAllText(OriginalPath, MiniJson.Write(entry), new UTF8Encoding(false));
        }

        static JsonObject LoadOriginal()
        {
            try
            {
                return File.Exists(OriginalPath) ? MiniJson.Parse(File.ReadAllText(OriginalPath, Encoding.UTF8)) as JsonObject : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Installation du widget dans %LOCALAPPDATA%\Programs\ClaudeUsageWidget, pour que la commande
    /// enregistrée dans Claude Code pointe vers un emplacement stable.
    /// </summary>
    static class SelfInstall
    {
        public const string ReplaceArgument = "--replace";

        public static string InstallDir
        {
            get
            {
                return string.IsNullOrEmpty(Settings.TestRoot)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\ClaudeUsageWidget")
                    : Path.Combine(Settings.TestRoot, "Programs");
            }
        }

        public static string InstalledExe
        {
            get { return Path.Combine(InstallDir, "ClaudeUsageWidget.exe"); }
        }

        public static bool IsRunningInstalled
        {
            get { return string.Equals(Path.GetFullPath(Application.ExecutablePath), InstalledExe, StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>Vrai si le widget lancé est plus récent que la copie installée utilisée par Claude Code.</summary>
        public static bool InstalledCopyIsOlder()
        {
            if (IsRunningInstalled || !File.Exists(InstalledExe)) return false;
            try
            {
                Version running = new Version(FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion);
                Version installed = new Version(FileVersionInfo.GetVersionInfo(InstalledExe).FileVersion);
                return running > installed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Copie l'exécutable courant à l'emplacement d'installation (et y déplace le démarrage automatique).</summary>
        public static void CopyToInstallDir()
        {
            Directory.CreateDirectory(InstallDir);
            File.Copy(Application.ExecutablePath, InstalledExe, true);
            if (Startup.IsEnabled()) Startup.Set(true, InstalledExe);
            Uninstaller.Register();
        }

        /// <summary>Lance la copie installée, qui attend la fermeture de l'instance actuelle.</summary>
        public static void StartInstalled()
        {
            Process.Start(new ProcessStartInfo(InstalledExe, ReplaceArgument) { UseShellExecute = false });
        }
    }
}
