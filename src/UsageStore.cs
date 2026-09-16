using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace ClaudeUsageWidget
{
    class UsageWindow
    {
        public string Key;
        public string Label;
        public double Percent;
        public DateTimeOffset? ResetsAt;

        /// <summary>La fenêtre a atteint son heure de remise à zéro depuis la dernière donnée reçue.</summary>
        public bool IsReset
        {
            get { return ResetsAt.HasValue && ResetsAt.Value <= DateTimeOffset.Now; }
        }

        /// <summary>Pourcentage à afficher : 0 une fois la remise à zéro passée.</summary>
        public double CurrentPercent
        {
            get { return IsReset ? 0 : Percent; }
        }
    }

    class UsageSnapshot
    {
        public DateTimeOffset UpdatedAt;
        public List<UsageWindow> Windows = new List<UsageWindow>();
    }

    class ReadResult
    {
        public UsageSnapshot Snapshot;   // null en cas d'échec
        public string Error;             // message court affichable
    }

    /// <summary>
    /// Fichier d'échange local contenant les quotas transmis par la ligne de statut de Claude Code
    /// (champ documenté « rate_limits »). Le widget ne manipule aucun identifiant et n'accède pas au réseau.
    ///
    /// Format, compatible avec l'option « externalUsageWritePath » de claude-hud :
    /// { "updated_at": ISO-8601,
    ///   "five_hour":   { "used_percentage": 0-100, "resets_at": ISO-8601 | epoch s | null },
    ///   "seven_day":   { ... },
    ///   "spend_limit": { ... } }   (facultatif)
    /// </summary>
    static class UsageStore
    {
        const int WriteThrottleSeconds = 30;

        static readonly string[][] KnownWindows =
        {
            new[] { "five_hour", "Session (5 h)" },
            new[] { "seven_day", "Semaine" },
            new[] { "spend_limit", "Plafond de dépenses" },
        };

        public static string DefaultPath()
        {
            return Path.Combine(Settings.AppDataDir, "usage.json");
        }

        // ------------------------------------------------------------------ lecture (widget)

        public static ReadResult Read(string path)
        {
            if (!File.Exists(path))
                return Fail("En attente de Claude Code : clic droit > Configuration");
            try
            {
                var root = Parse(ReadShared(path));
                DateTimeOffset? updated = ParseDate(Get(root, "updated_at"));
                if (root == null || !updated.HasValue)
                    return Fail("Fichier de données illisible");

                var snapshot = new UsageSnapshot { UpdatedAt = updated.Value };
                foreach (string[] known in KnownWindows)
                {
                    var o = Obj(root, known[0]);
                    double? pct = Num(o, "used_percentage");
                    if (!pct.HasValue) continue;
                    snapshot.Windows.Add(new UsageWindow
                    {
                        Key = known[0],
                        Label = known[1],
                        Percent = Math.Max(0, pct.Value),
                        ResetsAt = ParseDate(Get(o, "resets_at")),
                    });
                }
                if (snapshot.Windows.Count == 0)
                    return Fail("Aucun quota dans le fichier de données");
                return new ReadResult { Snapshot = snapshot };
            }
            catch (IOException)
            {
                return Fail("Fichier de données verrouillé, nouvel essai bientôt");
            }
            catch (Exception)
            {
                return Fail("Fichier de données illisible");
            }
        }

        static ReadResult Fail(string message)
        {
            return new ReadResult { Error = message };
        }

        // ------------------------------------------------------------------ écriture (pont de ligne de statut)

        /// <summary>
        /// Écrit les quotas reçus de Claude Code. Ne fait rien si l'entrée n'en contient pas
        /// (les dernières valeurs connues sont conservées). Retourne true si le fichier a été écrit.
        /// </summary>
        public static bool WriteFromStatusLine(Dictionary<string, object> input, string path, DateTimeOffset now)
        {
            var rate = Obj(input, "rate_limits");
            var windows = new Dictionary<string, object>();
            foreach (string[] known in KnownWindows)
            {
                var o = Obj(rate, known[0]);
                double? pct = Num(o, "used_percentage");
                if (!pct.HasValue) continue;
                DateTimeOffset? reset = ParseDate(Get(o, "resets_at"));
                windows[known[0]] = new Dictionary<string, object>
                {
                    { "used_percentage", Math.Round(pct.Value, 2) },
                    { "resets_at", reset.HasValue ? reset.Value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) : null },
                };
            }
            if (windows.Count == 0) return false;

            var json = new JavaScriptSerializer();
            string windowsJson = json.Serialize(windows);
            if (!ShouldWrite(path, windowsJson, now)) return false;

            var snapshot = new Dictionary<string, object>
            {
                { "updated_at", now.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) },
                { "source", "claude-code-statusline" },
            };
            foreach (var pair in windows) snapshot[pair.Key] = pair.Value;

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(tmp, json.Serialize(snapshot), new UTF8Encoding(false));
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
            return true;
        }

        /// <summary>Évite de réécrire le fichier à chaque rafraîchissement si rien n'a changé.</summary>
        static bool ShouldWrite(string path, string windowsJson, DateTimeOffset now)
        {
            try
            {
                if (!File.Exists(path)) return true;
                if ((now.UtcDateTime - File.GetLastWriteTimeUtc(path)).TotalSeconds > WriteThrottleSeconds) return true;

                var current = Parse(ReadShared(path));
                var currentWindows = new Dictionary<string, object>();
                foreach (string[] known in KnownWindows)
                {
                    var o = Obj(current, known[0]);
                    if (o != null) currentWindows[known[0]] = o;
                }
                return new JavaScriptSerializer().Serialize(currentWindows) != windowsJson;
            }
            catch (Exception)
            {
                return true;
            }
        }

        /// <summary>Texte court affiché par Claude Code dans sa ligne de statut.</summary>
        public static string FormatStatusLine(Dictionary<string, object> input)
        {
            var parts = new List<string>();
            string model = Str(Obj(input, "model"), "display_name");
            if (!string.IsNullOrEmpty(model)) parts.Add("[" + model + "]");

            var rate = Obj(input, "rate_limits");
            double? session = Num(Obj(rate, "five_hour"), "used_percentage");
            double? week = Num(Obj(rate, "seven_day"), "used_percentage");
            if (session.HasValue) parts.Add("5h " + Math.Round(session.Value).ToString(CultureInfo.InvariantCulture) + "%");
            if (week.HasValue) parts.Add("7j " + Math.Round(week.Value).ToString(CultureInfo.InvariantCulture) + "%");

            return parts.Count > 0 ? string.Join(" | ", parts) : "Claude";
        }

        // ------------------------------------------------------------------ outils JSON

        public static Dictionary<string, object> Parse(string text)
        {
            return new JavaScriptSerializer().DeserializeObject(text) as Dictionary<string, object>;
        }

        static string ReadShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }

        static Dictionary<string, object> Obj(Dictionary<string, object> d, string key)
        {
            return Get(d, key) as Dictionary<string, object>;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            return Get(d, key) as string;
        }

        static double? Num(Dictionary<string, object> d, string key)
        {
            object v = Get(d, key);
            if (v == null || v is string || v is bool) return null;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            catch (Exception) { return null; }
        }

        /// <summary>Accepte une date ISO-8601 ou un horodatage Unix (secondes ou millisecondes).</summary>
        static DateTimeOffset? ParseDate(object value)
        {
            var s = value as string;
            if (s != null)
            {
                DateTimeOffset d;
                if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d)) return d;
                return null;
            }
            if (value == null || value is bool) return null;
            try
            {
                double n = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (n <= 0) return null;
                return n > 1e12 ? DateTimeOffset.FromUnixTimeMilliseconds((long)n) : DateTimeOffset.FromUnixTimeSeconds((long)n);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Mode « ClaudeUsageWidget.exe --statusline » : commande de ligne de statut de Claude Code.
    /// Lit le JSON de session sur l'entrée standard, enregistre les quotas, affiche un résumé court.
    /// </summary>
    static class StatusLineBridge
    {
        public static int Run()
        {
            Dictionary<string, object> input = null;
            try
            {
                using (var reader = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false)))
                    input = UsageStore.Parse(reader.ReadToEnd());
            }
            catch (Exception)
            {
                // Entrée absente ou invalide : on affiche tout de même une ligne.
            }

            try
            {
                UsageStore.WriteFromStatusLine(input, Settings.Load().DataPath, DateTimeOffset.UtcNow);
            }
            catch (Exception)
            {
                // Ne jamais casser la ligne de statut de Claude Code.
            }

            try
            {
                using (var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)))
                    stdout.Write(UsageStore.FormatStatusLine(input));
            }
            catch (Exception)
            {
                // Sortie standard indisponible.
            }
            return 0;
        }
    }
}
