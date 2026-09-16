// =============================================================================
//  CsvUtil.cs - small CSV helpers shared by config / tracking / runs.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Text;

namespace RbaCore
{
    public static class CsvUtil
    {
        public static List<OrderedDictionary> ReadCsv(string path)
        {
            List<OrderedDictionary> rows = new List<OrderedDictionary>();
            if (!File.Exists(path)) { return rows; }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length < 1) { return rows; }
            string[] headers = ParseLine(lines[0]);
            for (int i = 0; i < headers.Length; i++)
            {
                headers[i] = headers[i].Trim().TrimStart('\ufeff');
            }
            for (int r = 1; r < lines.Length; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) { continue; }
                string[] cols = ParseLine(lines[r]);
                OrderedDictionary row = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < headers.Length; c++)
                {
                    string v = c < cols.Length ? cols[c] : "";
                    if (!row.Contains(headers[c])) { row.Add(headers[c], v); }
                }
                rows.Add(row);
            }
            return rows;
        }

        public static void WriteCsv(string path, string[] columns, List<OrderedDictionary> rows)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Join(",", EscapeAll(columns)));
            for (int r = 0; r < rows.Count; r++)
            {
                string[] vals = new string[columns.Length];
                for (int c = 0; c < columns.Length; c++)
                {
                    object v = rows[r].Contains(columns[c]) ? rows[r][columns[c]] : "";
                    vals[c] = Escape(v != null ? v.ToString() : "");
                }
                sb.AppendLine(string.Join(",", vals));
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        public static string Get(OrderedDictionary row, string key, string fallback)
        {
            if (row == null) { return ""; }
            if (row.Contains(key))
            {
                object v = row[key];
                return v != null ? v.ToString() : "";
            }
            if (!string.IsNullOrEmpty(fallback) && row.Contains(fallback))
            {
                object v = row[fallback];
                return v != null ? v.ToString() : "";
            }
            foreach (string k in row.Keys)
            {
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(k, fallback, StringComparison.OrdinalIgnoreCase))
                {
                    object v = row[k];
                    return v != null ? v.ToString() : "";
                }
            }
            return "";
        }

        private static string[] EscapeAll(string[] cols)
        {
            string[] outCols = new string[cols.Length];
            for (int i = 0; i < cols.Length; i++) { outCols[i] = Escape(cols[i]); }
            return outCols;
        }

        public static string Escape(string v)
        {
            if (v == null) { v = ""; }
            if (v.IndexOfAny(new char[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            }
            return v;
        }

        public static string[] ParseLine(string line)
        {
            List<string> parts = new List<string>();
            StringBuilder cur = new StringBuilder();
            bool inQ = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQ)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                        else { inQ = false; }
                    }
                    else { cur.Append(ch); }
                }
                else
                {
                    if (ch == '"') { inQ = true; }
                    else if (ch == ',') { parts.Add(cur.ToString()); cur.Length = 0; }
                    else { cur.Append(ch); }
                }
            }
            parts.Add(cur.ToString());
            return parts.ToArray();
        }
    }
}
