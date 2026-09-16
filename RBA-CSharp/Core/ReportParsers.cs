// =============================================================================
//  ReportParsers.cs - EGX Coding XML codes + HTM rejection reports (CP1256).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RbaCore
{
    public class CodingExportRow
    {
        public string NIN;
        public string CODE12;
        public string CLIENT_NAME;
        public string NATIONALITY;
        public string ID_TYPE;
        public string ID_NUMBER;
        public string ID_EXPIRE_DATE;
        public string INSERT_DATE;
        public string OPR;
        public string SourceFile;
    }

    public class RejectionRow
    {
        public string Operation;
        public string Name;
        public string RejectReason;
        public string RejectDate;
        public string RawDateCell;
    }

    public static class ReportParsers
    {
        public static List<CodingExportRow> ParseCodingXmlExport(string path)
        {
            if (!File.Exists(path)) { throw new Exception("Export file not found: " + path); }
            string raw = File.ReadAllText(path, Encoding.UTF8);
            List<CodingExportRow> rows = new List<CodingExportRow>();
            foreach (Match m in Regex.Matches(raw, "<z:row\\s+(?<attrs>[^>]*?)/>", RegexOptions.Singleline))
            {
                Dictionary<string, string> attrs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match a in Regex.Matches(m.Groups["attrs"].Value, "(?<k>[A-Za-z0-9_]+)\\s*=\\s*'(?<v>[^']*)'"))
                {
                    attrs[a.Groups["k"].Value] = a.Groups["v"].Value;
                }
                CodingExportRow row = new CodingExportRow();
                row.NIN = GetAttr(attrs, "NIN");
                row.CODE12 = GetAttr(attrs, "CODE12");
                row.CLIENT_NAME = GetAttr(attrs, "CLIENT_NAME").Replace("&apos;", "'");
                row.NATIONALITY = GetAttr(attrs, "NATIONALITY");
                row.ID_TYPE = GetAttr(attrs, "ID_TYPE");
                row.ID_NUMBER = GetAttr(attrs, "ID_NUMBER");
                row.ID_EXPIRE_DATE = GetAttr(attrs, "ID_EXPIRE_DATE");
                row.INSERT_DATE = GetAttr(attrs, "INSERT_DATE");
                row.OPR = GetAttr(attrs, "OPR");
                row.SourceFile = Path.GetFileName(path);
                rows.Add(row);
            }
            return rows;
        }

        public static string GetLatestCodingExport(string folder)
        {
            if (!Directory.Exists(folder)) { return null; }
            string[] files = Directory.GetFiles(folder, "Clients*.xml");
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < files.Length; i++)
            {
                DateTime t = File.GetLastWriteTime(files[i]);
                if (t > bestTime) { bestTime = t; best = files[i]; }
            }
            return best;
        }

        // Prefer a Clients*.xml written after sinceUtc (local LastWriteTime).
        // Used after clicking the period Excel button so we do not merge a stale export.
        public static string WaitForNewCodingExport(
            string folder, DateTime sinceLocal, int timeoutMs, Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            if (!Directory.Exists(folder))
            {
                log("xml-wait: folder missing " + folder, "WARN");
                return null;
            }
            if (timeoutMs <= 0) { timeoutMs = 60000; }

            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            string lastSeen = null;
            while (DateTime.Now < deadline)
            {
                string[] files = Directory.GetFiles(folder, "Clients*.xml");
                string best = null;
                DateTime bestTime = DateTime.MinValue;
                for (int i = 0; i < files.Length; i++)
                {
                    DateTime t = File.GetLastWriteTime(files[i]);
                    if (t <= sinceLocal) { continue; }
                    if (t > bestTime) { bestTime = t; best = files[i]; }
                }
                if (!string.IsNullOrEmpty(best))
                {
                    // File may still be growing — wait until size is stable for a short beat.
                    try
                    {
                        long s1 = new FileInfo(best).Length;
                        System.Threading.Thread.Sleep(400);
                        long s2 = new FileInfo(best).Length;
                        if (s1 > 0 && s1 == s2)
                        {
                            log("xml-wait: ready " + Path.GetFileName(best) + " (" + s2 + " bytes)", "OK");
                            return best;
                        }
                        lastSeen = best;
                    }
                    catch { lastSeen = best; }
                }
                System.Threading.Thread.Sleep(500);
            }

            if (!string.IsNullOrEmpty(lastSeen) && File.Exists(lastSeen))
            {
                log("xml-wait: timeout — returning last seen " + Path.GetFileName(lastSeen), "WARN");
                return lastSeen;
            }
            log("xml-wait: timeout — no Clients*.xml newer than " + sinceLocal.ToString("HH:mm:ss"), "WARN");
            return null;
        }

        public static string GetLatestRejectionReport(string folder)
        {
            if (!Directory.Exists(folder)) { return null; }
            string[] files = Directory.GetFiles(folder, "*.htm");
            string best = null;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < files.Length; i++)
            {
                DateTime t = File.GetLastWriteTime(files[i]);
                if (t > bestTime) { bestTime = t; best = files[i]; }
            }
            return best;
        }

        public static List<RejectionRow> ParseRejectionHtmReport(string path, int codePage)
        {
            if (!File.Exists(path)) { throw new Exception("Rejection report not found: " + path); }
            Encoding enc = Encoding.GetEncoding(codePage > 0 ? codePage : 1256);
            string raw = File.ReadAllText(path, enc);
            List<RejectionRow> records = new List<RejectionRow>();
            RejectionRow pending = null;

            foreach (Match rowMatch in Regex.Matches(raw, "<TR[^>]*>(?<body>.*?)</TR>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
            {
                List<string> cells = new List<string>();
                foreach (Match c in Regex.Matches(rowMatch.Groups["body"].Value, "<T[DH][^>]*>(?<v>.*?)</T[DH]>", RegexOptions.Singleline | RegexOptions.IgnoreCase))
                {
                    string txt = c.Groups["v"].Value;
                    txt = Regex.Replace(txt, "(?is)<BR\\s*/?>", " ");
                    txt = Regex.Replace(txt, "(?s)<[^>]+>", "");
                    txt = HtmlUnescape(txt);
                    txt = Regex.Replace(txt, "\\s+", " ").Trim();
                    if (txt != "") { cells.Add(txt); }
                }
                if (cells.Count == 0) { continue; }

                string joined = string.Join("", cells.ToArray());
                if (Regex.IsMatch(joined, "إدارة التكويد|سبب الرفض|تاريخ الرفض") && cells.Count <= 4)
                {
                    bool hasDate = false;
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (Regex.IsMatch(cells[i], "^\\d{2}/\\d{2}/\\d{4}")) { hasDate = true; break; }
                    }
                    if (!hasDate) { continue; }
                }

                string dateCell = null;
                for (int i = 0; i < cells.Count; i++)
                {
                    if (Regex.IsMatch(cells[i], "^\\d{2}/\\d{2}/\\d{4}")) { dateCell = cells[i]; break; }
                }

                if (dateCell != null)
                {
                    if (pending != null) { records.Add(pending); }
                    List<string> rest = new List<string>();
                    for (int i = 0; i < cells.Count; i++)
                    {
                        if (cells[i] != dateCell) { rest.Add(cells[i]); }
                    }
                    pending = new RejectionRow();
                    pending.Operation = "";
                    pending.RejectDate = dateCell.Split(' ')[0];
                    pending.RawDateCell = dateCell;
                    if (rest.Count >= 2) { pending.RejectReason = rest[0]; pending.Name = rest[1]; }
                    else if (rest.Count == 1) { pending.Name = rest[0]; pending.RejectReason = ""; }
                    else { pending.Name = ""; pending.RejectReason = ""; }
                }
                else if (pending != null && cells.Count == 1 && cells[0].Length <= 12)
                {
                    pending.Operation = cells[0];
                }
            }
            if (pending != null) { records.Add(pending); }
            return records;
        }

        private static string GetAttr(Dictionary<string, string> attrs, string key)
        {
            return attrs.ContainsKey(key) ? attrs[key] : "";
        }

        private static string HtmlUnescape(string text)
        {
            if (text == null) { return ""; }
            string t = text.Replace("&nbsp;", " ");
            t = t.Replace("&amp;", "&");
            t = t.Replace("&lt;", "<");
            t = t.Replace("&gt;", ">");
            t = t.Replace("&quot;", "\"");
            return t;
        }
    }
}
