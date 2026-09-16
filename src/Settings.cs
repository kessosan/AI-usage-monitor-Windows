using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClaudeUsageWidget
{
    /// <summary>Préférences stockées dans %APPDATA%\ClaudeUsageWidget\settings.ini, hors du dossier du projet.</summary>
    class Settings
    {
        public const int Unset = int.MinValue;
        public const string ThemeDark = "dark";
        public const string ThemeLight = "light";
        public const string ThemeSystem = "system";

        public int X = Unset;
        public int Y = Unset;
        public bool Compact;
        public bool TopMost = true;
        public bool Visible = true;
        public int OpacityPercent = 100;
        public string Theme = ThemeDark;
        public string CustomDataPath = "";   // vide : emplacement par défaut
        public bool LaunchClaudeOnStart = true;
        public string ClaudeWorkDir = "";    // vide : dossier de l'utilisateur
        public bool ShowAssistantOnStart = true;

        public static string AppDataDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeUsageWidget"); }
        }

        static string FilePath
        {
            get { return Path.Combine(AppDataDir, "settings.ini"); }
        }

        /// <summary>Dossier dans lequel Claude Code est lancé.</summary>
        public string ClaudeWorkDirOrDefault
        {
            get { return string.IsNullOrEmpty(ClaudeWorkDir) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : ClaudeWorkDir; }
        }

        /// <summary>Fichier de données alimenté par la ligne de statut de Claude Code.</summary>
        public string DataPath
        {
            get { return string.IsNullOrEmpty(CustomDataPath) ? UsageStore.DefaultPath() : CustomDataPath; }
        }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "X": s.X = ParseInt(val, Unset); break;
                        case "Y": s.Y = ParseInt(val, Unset); break;
                        case "Compact": s.Compact = val == "1"; break;
                        case "TopMost": s.TopMost = val == "1"; break;
                        case "Visible": s.Visible = val == "1"; break;
                        case "OpacityPercent": s.OpacityPercent = Math.Max(30, Math.Min(100, ParseInt(val, 100))); break;
                        case "Theme":
                            if (val == ThemeDark || val == ThemeLight || val == ThemeSystem) s.Theme = val;
                            break;
                        case "DataPath": s.CustomDataPath = val; break;
                        case "LaunchClaudeOnStart": s.LaunchClaudeOnStart = val == "1"; break;
                        case "ClaudeWorkDir": s.ClaudeWorkDir = val; break;
                        case "ShowAssistantOnStart": s.ShowAssistantOnStart = val == "1"; break;
                    }
                }
            }
            catch (Exception)
            {
                // Fichier illisible : on repart des valeurs par défaut.
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(AppDataDir);
                File.WriteAllLines(FilePath, new[]
                {
                    "X=" + X,
                    "Y=" + Y,
                    "Compact=" + (Compact ? 1 : 0),
                    "TopMost=" + (TopMost ? 1 : 0),
                    "Visible=" + (Visible ? 1 : 0),
                    "OpacityPercent=" + OpacityPercent,
                    "Theme=" + Theme,
                    "DataPath=" + CustomDataPath,
                    "LaunchClaudeOnStart=" + (LaunchClaudeOnStart ? 1 : 0),
                    "ClaudeWorkDir=" + ClaudeWorkDir,
                    "ShowAssistantOnStart=" + (ShowAssistantOnStart ? 1 : 0),
                });
            }
            catch (Exception)
            {
                // Non bloquant : les préférences seront perdues, pas le widget.
            }
        }

        static int ParseInt(string s, int fallback)
        {
            int v;
            return int.TryParse(s, out v) ? v : fallback;
        }
    }

    /// <summary>Lancement automatique via HKCU\...\Run (aucun droit administrateur requis).</summary>
    static class Startup
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "ClaudeUsageWidget";

        public static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(ValueName) != null;
        }

        public static void Set(bool enabled)
        {
            Set(enabled, Application.ExecutablePath);
        }

        public static void Set(bool enabled, string exePath)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                    key.SetValue(ValueName, "\"" + exePath + "\"");
                else
                    key.DeleteValue(ValueName, false);
            }
        }
    }
}
