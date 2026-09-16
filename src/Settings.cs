using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClaudeUsageWidget
{
    /// <summary>
    /// Préférences stockées dans %APPDATA%\ClaudeUsageWidget\settings.ini, hors du dossier du projet.
    /// Aucun secret n'y est écrit : un éventuel jeton manuel est géré par <see cref="TokenStore"/>.
    /// </summary>
    class Settings
    {
        public const int Unset = int.MinValue;
        public const string ThemeDark = "dark";
        public const string ThemeLight = "light";
        public const string ThemeSystem = "system";

        public const string SourceClaudeCode = "claude-code";
        public const string SourceFile = "file";
        public const string SourceManual = "manual";

        public int X = Unset;
        public int Y = Unset;
        public bool Compact;
        public bool TopMost = true;
        public bool Visible = true;
        public int IntervalSec = 120;
        public int OpacityPercent = 100;
        public string Theme = ThemeDark;
        public string TokenSource = SourceClaudeCode;
        public string CredentialsPath = "";

        public static string AppDataDir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClaudeUsageWidget"); }
        }

        static string FilePath
        {
            get { return Path.Combine(AppDataDir, "settings.ini"); }
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
                        case "IntervalSec": s.IntervalSec = Math.Max(60, ParseInt(val, 120)); break;
                        case "OpacityPercent": s.OpacityPercent = Math.Max(30, Math.Min(100, ParseInt(val, 100))); break;
                        case "Theme":
                            if (val == ThemeDark || val == ThemeLight || val == ThemeSystem) s.Theme = val;
                            break;
                        case "TokenSource":
                            if (val == SourceClaudeCode || val == SourceFile || val == SourceManual) s.TokenSource = val;
                            break;
                        case "CredentialsPath": s.CredentialsPath = val; break;
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
                    "IntervalSec=" + IntervalSec,
                    "OpacityPercent=" + OpacityPercent,
                    "Theme=" + Theme,
                    "TokenSource=" + TokenSource,
                    "CredentialsPath=" + CredentialsPath,
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

    /// <summary>
    /// Jeton saisi manuellement, chiffré avec DPAPI (lié à la session Windows de l'utilisateur)
    /// dans %APPDATA%\ClaudeUsageWidget\token.dat. Il n'est jamais écrit en clair.
    /// </summary>
    static class TokenStore
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ClaudeUsageWidget.TokenStore");

        static string FilePath
        {
            get { return Path.Combine(Settings.AppDataDir, "token.dat"); }
        }

        public static bool Exists()
        {
            return File.Exists(FilePath);
        }

        public static string Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                byte[] clear = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), Entropy, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clear);
            }
            catch (Exception)
            {
                return null; // fichier corrompu ou créé par un autre compte Windows
            }
        }

        public static void Save(string token)
        {
            Directory.CreateDirectory(Settings.AppDataDir);
            byte[] cipher = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(FilePath, cipher);
        }

        public static void Clear()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }

        /// <summary>Nettoie un jeton collé : espaces et éventuel préfixe « Bearer ».</summary>
        public static string Normalize(string raw)
        {
            string token = (raw ?? "").Trim();
            if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                token = token.Substring(7).Trim();
            return token;
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
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enabled)
                    key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\"");
                else
                    key.DeleteValue(ValueName, false);
            }
        }
    }
}
