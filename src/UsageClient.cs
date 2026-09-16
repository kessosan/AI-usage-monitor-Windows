using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
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
    }

    class FetchResult
    {
        public List<UsageWindow> Windows;   // null en cas d'échec
        public string Plan;
        public string Error;                // message court affichable
        public bool RateLimited;
    }

    /// <summary>Instantané de la configuration d'accès, sûr à utiliser depuis un thread de fond.</summary>
    class AccessConfig
    {
        public string Source;
        public string CredentialsPath;
        public string ManualToken;

        public static AccessConfig From(Settings settings)
        {
            return new AccessConfig
            {
                Source = settings.TokenSource,
                CredentialsPath = settings.CredentialsPath,
                ManualToken = settings.TokenSource == Settings.SourceManual ? TokenStore.Load() : null,
            };
        }
    }

    /// <summary>
    /// Obtient le jeton OAuth selon la configuration et interroge l'endpoint d'usage d'Anthropic.
    /// Le fichier d'identifiants de Claude Code n'est jamais modifié : c'est Claude Code qui renouvelle le jeton.
    /// </summary>
    static class UsageClient
    {
        const string Endpoint = "https://api.anthropic.com/api/oauth/usage";
        const string BetaHeader = "oauth-2025-04-20";

        static readonly string[][] KnownWindows =
        {
            new[] { "five_hour", "Session (5 h)" },
            new[] { "seven_day", "Semaine" },
            new[] { "seven_day_opus", "Semaine · Opus" },
            new[] { "seven_day_sonnet", "Semaine · Sonnet" },
        };

        public static string DefaultCredentialsPath()
        {
            string dir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (string.IsNullOrEmpty(dir))
                dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
            return Path.Combine(dir, ".credentials.json");
        }

        public static FetchResult Fetch(AccessConfig access)
        {
            try
            {
                return FetchCore(access);
            }
            catch (Exception ex)
            {
                return new FetchResult { Error = "Erreur inattendue : " + ex.Message };
            }
        }

        static FetchResult FetchCore(AccessConfig access)
        {
            var result = new FetchResult();
            bool manual = access.Source == Settings.SourceManual;

            string token = manual ? access.ManualToken : ReadCredentials(access, result);
            if (result.Error != null) return result;
            if (string.IsNullOrEmpty(token))
            {
                result.Error = manual
                    ? "Aucun jeton enregistré : clic droit > Configuration"
                    : "Jeton absent : connectez-vous à Claude Code";
                return result;
            }

            string body;
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(Endpoint);
                req.Timeout = 15000;
                req.ReadWriteTimeout = 15000;
                req.Accept = "application/json";
                req.UserAgent = "claude-usage-widget/1.0";
                req.Headers["Authorization"] = "Bearer " + token;
                req.Headers["anthropic-beta"] = BetaHeader;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    body = reader.ReadToEnd();
            }
            catch (WebException ex)
            {
                var resp = ex.Response as HttpWebResponse;
                if (resp == null)
                {
                    result.Error = "Hors ligne ou serveur injoignable";
                    return result;
                }
                int code = (int)resp.StatusCode;
                resp.Close();
                if (code == 401 || code == 403)
                    result.Error = manual
                        ? "Jeton refusé : vérifiez la configuration"
                        : "Jeton refusé : ouvrez Claude Code pour le renouveler";
                else if (code == 429)
                {
                    result.Error = "Trop de requêtes, nouvel essai plus tard";
                    result.RateLimited = true;
                }
                else
                    result.Error = "Erreur serveur (HTTP " + code + ")";
                return result;
            }

            var root = new JavaScriptSerializer().DeserializeObject(body) as Dictionary<string, object>;
            var windows = new List<UsageWindow>();
            foreach (string[] known in KnownWindows)
            {
                var o = Obj(root, known[0]);
                double? pct = Num(o, "utilization");
                if (!pct.HasValue) continue;
                windows.Add(new UsageWindow
                {
                    Key = known[0],
                    Label = known[1],
                    Percent = Clamp(pct.Value),
                    ResetsAt = Date(Str(o, "resets_at")),
                });
            }

            var extra = Obj(root, "extra_usage");
            double? extraPct = Num(extra, "utilization");
            if (true.Equals(Get(extra, "is_enabled")) && extraPct.HasValue)
                windows.Add(new UsageWindow { Key = "extra_usage", Label = "Crédits supplémentaires", Percent = Clamp(extraPct.Value) });

            if (windows.Count == 0)
            {
                result.Error = "Réponse inattendue du serveur";
                return result;
            }
            result.Windows = windows;
            return result;
        }

        /// <summary>Lit le jeton dans un fichier d'identifiants au format Claude Code. Renseigne result.Error en cas d'échec.</summary>
        static string ReadCredentials(AccessConfig access, FetchResult result)
        {
            bool custom = access.Source == Settings.SourceFile;
            string path = custom ? access.CredentialsPath : DefaultCredentialsPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                result.Error = custom
                    ? "Fichier d'identifiants introuvable : vérifiez la configuration"
                    : "Identifiants introuvables : connectez-vous à Claude Code ou ouvrez la configuration";
                return null;
            }

            Dictionary<string, object> oauth;
            try
            {
                oauth = Obj(new JavaScriptSerializer().DeserializeObject(ReadShared(path)) as Dictionary<string, object>, "claudeAiOauth");
            }
            catch (IOException)
            {
                result.Error = "Fichier d'identifiants verrouillé, nouvel essai bientôt";
                return null;
            }
            catch (ArgumentException)
            {
                result.Error = "Fichier d'identifiants illisible (JSON invalide)";
                return null;
            }

            result.Plan = Str(oauth, "subscriptionType");
            double? expiresAt = Num(oauth, "expiresAt");
            if (expiresAt.HasValue && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= expiresAt.Value)
            {
                result.Error = "Jeton expiré : ouvrez Claude Code pour le renouveler";
                return null;
            }
            return Str(oauth, "accessToken");
        }

        static string ReadShared(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(fs, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        static double Clamp(double v) { return Math.Max(0, Math.Min(100, v)); }

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

        static DateTimeOffset? Date(string s)
        {
            DateTimeOffset d;
            if (s != null && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                return d;
            return null;
        }
    }
}
