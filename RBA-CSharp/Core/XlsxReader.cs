// ==============================================================================
//  XlsxReader.cs - reads an .xlsx WITHOUT Excel installed.
//  Port of _shared/XlsxReader.ps1.  Handles both string storage modes:
//  sharedStrings (what real Excel writes) and inlineStr (what our generator
//  writes), plus numeric cells.  Row 1 is treated as the header row.
//
//  A "row" is a System.Collections.Specialized.OrderedDictionary keyed by the
//  header name -> string value.  Insertion order preserves the sheet's column
//  order, exactly as the PowerShell [ordered]@{} did.  Keys are compared
//  case-insensitively so property-name lookups behave like PowerShell's.
// ==============================================================================

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace RbaCore
{
    public class XlsxReader
    {
        // "BC12" -> 55 . Strips the digits, then base-26 over the letters.
        public static int ConvertFromColumnName(string reference)
        {
            if (reference == null) { return 0; }
            string letters = Regex.Replace(reference, "\\d", "");
            int n = 0;
            foreach (char ch in letters.ToCharArray())
            {
                n = n * 26 + ((int)char.ToUpperInvariant(ch) - 64);
            }
            return n;
        }

        private static string GetZipEntryText(ZipArchive zip, string entryName)
        {
            ZipArchiveEntry entry = zip.GetEntry(entryName);
            if (entry == null) { return null; }
            Stream stream = entry.Open();
            try
            {
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                try { return reader.ReadToEnd(); }
                finally { reader.Dispose(); }
            }
            finally { stream.Dispose(); }
        }

        // Unescape the XML entities the writer produces, in the SAME order the
        // PowerShell used (&amp; last), so behaviour matches byte-for-byte.
        private static string XmlUnescape(string text)
        {
            if (text == null) { return ""; }
            string t = text;
            t = t.Replace("&lt;", "<");
            t = t.Replace("&gt;", ">");
            t = t.Replace("&quot;", "\"");
            t = t.Replace("&apos;", "'");
            t = t.Replace("&amp;", "&");
            return t;
        }

        public static List<OrderedDictionary> ImportXlsx(string path)
        {
            return ImportXlsx(path, 1);
        }

        // Reads the first worksheet into rows, using HeaderRow as the column names.
        public static List<OrderedDictionary> ImportXlsx(string path, int headerRow)
        {
            if (!File.Exists(path)) { throw new Exception("Workbook not found: " + path); }

            const RegexOptions S = RegexOptions.Singleline;
            List<OrderedDictionary> outList = new List<OrderedDictionary>();

            ZipArchive zip = ZipFile.OpenRead(Path.GetFullPath(path));
            try
            {
                // ---- shared strings (optional)
                List<string> shared = new List<string>();
                string ssText = GetZipEntryText(zip, "xl/sharedStrings.xml");
                if (!string.IsNullOrEmpty(ssText))
                {
                    foreach (Match si in Regex.Matches(ssText, "<si>(?<body>.*?)</si>", S))
                    {
                        // an <si> may be split into several <t> runs
                        StringBuilder parts = new StringBuilder();
                        foreach (Match t in Regex.Matches(si.Groups["body"].Value, "<t[^>]*>(?<v>.*?)</t>", S))
                        {
                            parts.Append(t.Groups["v"].Value);
                        }
                        shared.Add(XmlUnescape(parts.ToString()));
                    }
                }

                // ---- first worksheet
                string sheetText = GetZipEntryText(zip, "xl/worksheets/sheet1.xml");
                if (string.IsNullOrEmpty(sheetText))
                {
                    throw new Exception("sheet1.xml not found inside " + path);
                }

                // rowIndex -> (colIndex -> value)
                Dictionary<int, Dictionary<int, string>> rows = new Dictionary<int, Dictionary<int, string>>();

                foreach (Match rowMatch in Regex.Matches(sheetText, "<row[^>]*\\br=\"(?<r>\\d+)\"[^>]*>(?<body>.*?)</row>", S))
                {
                    int rIdx = int.Parse(rowMatch.Groups["r"].Value);
                    Dictionary<int, string> cells = new Dictionary<int, string>();

                    foreach (Match cMatch in Regex.Matches(rowMatch.Groups["body"].Value,
                        "<c\\b(?<attrs>[^>]*)>(?<body>.*?)</c>|<c\\b(?<attrs2>[^>]*)/>", S))
                    {
                        string attrs = cMatch.Groups["attrs"].Value;
                        if (string.IsNullOrEmpty(attrs)) { attrs = cMatch.Groups["attrs2"].Value; }

                        Match refMatch = Regex.Match(attrs, "r=\"(?<ref>[A-Z]+\\d+)\"");
                        if (!refMatch.Success) { continue; }
                        int cIdx = ConvertFromColumnName(refMatch.Groups["ref"].Value);

                        Match typeMatch = Regex.Match(attrs, "t=\"(?<t>\\w+)\"");
                        string cellType = "";
                        if (typeMatch.Success) { cellType = typeMatch.Groups["t"].Value; }

                        string body = cMatch.Groups["body"].Value;
                        string value = "";

                        if (cellType == "inlineStr")
                        {
                            StringBuilder parts = new StringBuilder();
                            foreach (Match t in Regex.Matches(body, "<t[^>]*>(?<v>.*?)</t>", S))
                            {
                                parts.Append(t.Groups["v"].Value);
                            }
                            value = parts.ToString();
                        }
                        else if (cellType == "s")
                        {
                            Match vMatch = Regex.Match(body, "<v>(?<v>.*?)</v>", S);
                            if (vMatch.Success)
                            {
                                int siIdx = int.Parse(vMatch.Groups["v"].Value);
                                if (siIdx >= 0 && siIdx < shared.Count) { value = shared[siIdx]; }
                            }
                        }
                        else if (cellType == "str")
                        {
                            Match vMatch = Regex.Match(body, "<f>.*?</f>\\s*<v>(?<v>.*?)</v>", S);
                            if (!vMatch.Success) { vMatch = Regex.Match(body, "<v>(?<v>.*?)</v>", S); }
                            if (vMatch.Success) { value = vMatch.Groups["v"].Value; }
                        }
                        else
                        {
                            Match vMatch = Regex.Match(body, "<v>(?<v>.*?)</v>", S);
                            if (vMatch.Success) { value = vMatch.Groups["v"].Value; }
                        }

                        cells[cIdx] = XmlUnescape(value);
                    }
                    rows[rIdx] = cells;
                }

                if (!rows.ContainsKey(headerRow)) { return outList; }

                // ---- headers (colIndex -> name), sorted by column index
                Dictionary<int, string> headers = new Dictionary<int, string>();
                foreach (KeyValuePair<int, string> kv in rows[headerRow])
                {
                    string h = kv.Value;
                    if (string.IsNullOrEmpty(h) || h.Trim().Length == 0)
                    {
                        h = "Column" + kv.Key.ToString();
                    }
                    headers[kv.Key] = h;
                }

                List<int> headerCols = new List<int>(headers.Keys);
                headerCols.Sort();

                // ---- data rows
                List<int> rowIdxs = new List<int>(rows.Keys);
                rowIdxs.Sort();
                foreach (int rIdx in rowIdxs)
                {
                    if (rIdx <= headerRow) { continue; }
                    OrderedDictionary obj = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
                    foreach (int cIdx in headerCols)
                    {
                        string name = headers[cIdx];
                        string v = "";
                        if (rows[rIdx].ContainsKey(cIdx)) { v = rows[rIdx][cIdx]; }
                        if (!obj.Contains(name)) { obj[name] = v; }
                    }
                    // skip fully-blank rows
                    bool hasValue = false;
                    foreach (object v in obj.Values)
                    {
                        string sv = v == null ? "" : v.ToString();
                        if (sv.Trim().Length > 0) { hasValue = true; break; }
                    }
                    if (hasValue) { outList.Add(obj); }
                }
                return outList;
            }
            finally
            {
                zip.Dispose();
            }
        }
    }
}
