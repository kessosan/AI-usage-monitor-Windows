using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ClaudeUsageWidget
{
    /// <summary>Objet JSON dont l'ordre des membres et les clés en double sont conservés.</summary>
    class JsonObject : List<KeyValuePair<string, object>>
    {
        public int CountOf(string key)
        {
            int n = 0;
            foreach (var member in this)
                if (member.Key == key) n++;
            return n;
        }

        /// <summary>Dernière valeur associée à la clé (comportement habituel des lecteurs JSON).</summary>
        public object Get(string key)
        {
            object value = null;
            foreach (var member in this)
                if (member.Key == key) value = member.Value;
            return value;
        }

        public List<object> GetAll(string key)
        {
            var values = new List<object>();
            foreach (var member in this)
                if (member.Key == key) values.Add(member.Value);
            return values;
        }

        /// <summary>Remplace toutes les occurrences de la clé par une seule, à la position de la première.</summary>
        public void Set(string key, object value)
        {
            int first = FindIndex(m => m.Key == key);
            RemoveAll(m => m.Key == key);
            var member = new KeyValuePair<string, object>(key, value);
            if (first < 0) Add(member);
            else Insert(first, member);
        }

        public void Remove(string key)
        {
            RemoveAll(m => m.Key == key);
        }
    }

    /// <summary>Nombre JSON conservé tel qu'écrit dans le fichier.</summary>
    class JsonNumber
    {
        public readonly string Raw;
        public JsonNumber(string raw) { Raw = raw; }
    }

    /// <summary>
    /// Lecteur et écrivain JSON minimalistes, pour modifier settings.json de Claude Code
    /// sans perdre l'ordre des clés ni la forme des nombres.
    /// Valeurs : JsonObject, List&lt;object&gt;, string, JsonNumber, bool, null.
    /// </summary>
    static class MiniJson
    {
        // ------------------------------------------------------------------ lecture

        public static object Parse(string text)
        {
            int i = 0;
            object value = ReadValue(text, ref i);
            SkipSpace(text, ref i);
            if (i != text.Length) throw Error(text, i, "contenu inattendu après la valeur");
            return value;
        }

        static object ReadValue(string s, ref int i)
        {
            SkipSpace(s, ref i);
            if (i >= s.Length) throw Error(s, i, "fin de fichier inattendue");
            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (c == '-' || (c >= '0' && c <= '9')) return ReadNumber(s, ref i);
            if (Match(s, ref i, "true")) return true;
            if (Match(s, ref i, "false")) return false;
            if (Match(s, ref i, "null")) return null;
            throw Error(s, i, "valeur invalide");
        }

        static JsonObject ReadObject(string s, ref int i)
        {
            var obj = new JsonObject();
            i++; // {
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return obj; }
            while (true)
            {
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Error(s, i, "clé attendue");
                string key = ReadString(s, ref i);
                SkipSpace(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Error(s, i, "« : » attendu");
                i++;
                obj.Add(new KeyValuePair<string, object>(key, ReadValue(s, ref i)));
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return obj; }
                throw Error(s, i, "« , » ou « } » attendu");
            }
        }

        static List<object> ReadArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            SkipSpace(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return list; }
            while (true)
            {
                list.Add(ReadValue(s, ref i));
                SkipSpace(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return list; }
                throw Error(s, i, "« , » ou « ] » attendu");
            }
        }

        static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // "
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Error(s, i, "séquence \\u incomplète");
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: throw Error(s, i, "séquence d'échappement invalide");
                }
            }
            throw Error(s, i, "chaîne non terminée");
        }

        static JsonNumber ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return new JsonNumber(s.Substring(start, i - start));
        }

        static bool Match(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static FormatException Error(string s, int i, string message)
        {
            int line = 1;
            for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
            return new FormatException("JSON invalide (ligne " + line + ") : " + message);
        }

        // ------------------------------------------------------------------ écriture

        /// <summary>Écrit avec une indentation de 2 espaces, comme Claude Code.</summary>
        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            return sb.Append('\n').ToString();
        }

        static void WriteValue(StringBuilder sb, object value, int depth)
        {
            if (value == null) { sb.Append("null"); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }
            if (value is JsonNumber) { sb.Append(((JsonNumber)value).Raw); return; }
            if (value is int || value is long || value is double)
            {
                sb.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                return;
            }
            var str = value as string;
            if (str != null) { WriteString(sb, str); return; }

            var obj = value as JsonObject;
            if (obj != null)
            {
                if (obj.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n");
                for (int k = 0; k < obj.Count; k++)
                {
                    Indent(sb, depth + 1);
                    WriteString(sb, obj[k].Key);
                    sb.Append(": ");
                    WriteValue(sb, obj[k].Value, depth + 1);
                    sb.Append(k < obj.Count - 1 ? ",\n" : "\n");
                }
                Indent(sb, depth);
                sb.Append('}');
                return;
            }

            var list = value as List<object>;
            if (list != null)
            {
                if (list.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[\n");
                for (int k = 0; k < list.Count; k++)
                {
                    Indent(sb, depth + 1);
                    WriteValue(sb, list[k], depth + 1);
                    sb.Append(k < list.Count - 1 ? ",\n" : "\n");
                }
                Indent(sb, depth);
                sb.Append(']');
                return;
            }
            throw new ArgumentException("Type JSON non pris en charge : " + value.GetType().Name);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        static void Indent(StringBuilder sb, int depth)
        {
            sb.Append(' ', depth * 2);
        }
    }
}
