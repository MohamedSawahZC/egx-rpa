// =============================================================================
//  SheetSync.cs - import branch KYC sheet and write results back.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;

namespace RbaCore
{
    public class SheetImportResult
    {
        public List<OrderedDictionary> Rows;
        public string Path;
        public string Format;
    }

    public class SheetUpdateResult
    {
        public int Updated;
        public int Unmatched;
        public string Path;
        public string Backup;
        public List<string> Lines;
    }

    public static class SheetSync
    {
        public static readonly string[] ResultArabic = new string[] {
            "الحالة", "الكود الموحد", "كود 12", "سبب الرفض", "ملاحظات", "آخر تحديث"
        };
        public static readonly string[] ResultEnglish = new string[] {
            "Status", "NIN", "CODE12", "RejectReason", "Notes", "LastUpdated"
        };

        public static SheetImportResult ImportClientSheet(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            SheetImportResult result = new SheetImportResult();

            if (ext == ".csv")
            {
                if (!File.Exists(path)) { throw new Exception("client sheet not found: " + path); }
                result.Rows = CsvUtil.ReadCsv(path);
                result.Path = path;
                result.Format = "csv";
                return result;
            }
            if (ext == ".xlsx" || ext == ".xlsm" || ext == ".xls")
            {
                if (!File.Exists(path)) { throw new Exception("client sheet not found: " + path); }
                result.Rows = XlsxReader.ImportXlsx(path);
                result.Path = path;
                result.Format = "xlsx";
                return result;
            }

            string xlsx = Path.ChangeExtension(path, ".xlsx");
            string csv = Path.ChangeExtension(path, ".csv");
            if (File.Exists(xlsx))
            {
                result.Rows = XlsxReader.ImportXlsx(xlsx);
                result.Path = xlsx;
                result.Format = "xlsx";
                return result;
            }
            if (File.Exists(csv))
            {
                result.Rows = CsvUtil.ReadCsv(csv);
                result.Path = csv;
                result.Format = "csv";
                return result;
            }
            if (File.Exists(path))
            {
                result.Rows = XlsxReader.ImportXlsx(path);
                result.Path = path;
                result.Format = "xlsx";
                return result;
            }
            throw new Exception("no client sheet found at " + xlsx + " (or " + csv + ")");
        }

        public static SheetUpdateResult UpdateClientSheet(string path, List<OrderedDictionary> tracking)
        {
            SheetImportResult sheet = ImportClientSheet(path);
            Dictionary<string, OrderedDictionary> byId = new Dictionary<string, OrderedDictionary>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < tracking.Count; i++)
            {
                string nid = CsvUtil.Get(tracking[i], "NationalId", "الرقم القومي").Trim();
                if (!string.IsNullOrEmpty(nid) && !byId.ContainsKey(nid)) { byId[nid] = tracking[i]; }
            }

            // Ensure result columns exist on every row.
            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                OrderedDictionary row = sheet.Rows[i];
                for (int c = 0; c < ResultArabic.Length; c++)
                {
                    if (!row.Contains(ResultArabic[c])) { row.Add(ResultArabic[c], ""); }
                }
                string nid = FindNationalId(row);
                if (string.IsNullOrEmpty(nid) || !byId.ContainsKey(nid)) { continue; }
                OrderedDictionary t = byId[nid];
                SetResult(row, ResultArabic[0], ResultEnglish[0], CsvUtil.Get(t, "Status", ""));
                SetResult(row, ResultArabic[1], ResultEnglish[1], CsvUtil.Get(t, "NIN", ""));
                SetResult(row, ResultArabic[2], ResultEnglish[2], CsvUtil.Get(t, "CODE12", ""));
                SetResult(row, ResultArabic[3], ResultEnglish[3], CsvUtil.Get(t, "RejectReason", ""));
                SetResult(row, ResultArabic[4], ResultEnglish[4], CsvUtil.Get(t, "Notes", ""));
                SetResult(row, ResultArabic[5], ResultEnglish[5], CsvUtil.Get(t, "LastUpdated", ""));
            }

            string backup = BackupSheet(sheet.Path);
            PersistSheet(sheet);

            SheetUpdateResult ur = new SheetUpdateResult();
            ur.Updated = 0;
            ur.Unmatched = 0;
            ur.Path = sheet.Path;
            ur.Backup = backup;
            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                string nid = FindNationalId(sheet.Rows[i]);
                if (!string.IsNullOrEmpty(nid) && byId.ContainsKey(nid)) { ur.Updated++; }
                else if (!string.IsNullOrEmpty(nid)) { ur.Unmatched++; }
            }
            return ur;
        }

        // Merge Clients*.xml NIN/CODE12 onto the branch sheet by NationalId ← ID_NUMBER.
        public static SheetUpdateResult UpdateFromCodingXml(string sheetPath, string xmlPath)
        {
            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath))
            {
                throw new Exception("client sheet not found: " + sheetPath);
            }
            List<CodingExportRow> coded = ReportParsers.ParseCodingXmlExport(xmlPath);
            Dictionary<string, CodingExportRow> byId =
                new Dictionary<string, CodingExportRow>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CodingExportRow> byDigits =
                new Dictionary<string, CodingExportRow>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, CodingExportRow> byName =
                new Dictionary<string, CodingExportRow>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < coded.Count; i++)
            {
                string id = coded[i].ID_NUMBER != null ? coded[i].ID_NUMBER.Trim() : "";
                if (!string.IsNullOrEmpty(id) && !byId.ContainsKey(id)) { byId[id] = coded[i]; }
                string dig = DigitsOnly(id);
                if (dig.Length >= 10 && !byDigits.ContainsKey(dig)) { byDigits[dig] = coded[i]; }
                string nm = coded[i].CLIENT_NAME != null ? coded[i].CLIENT_NAME.Trim() : "";
                if (!string.IsNullOrEmpty(nm) && !byName.ContainsKey(nm)) { byName[nm] = coded[i]; }
            }

            SheetImportResult sheet = ImportClientSheet(sheetPath);
            int updated = 0;
            int unmatched = 0;
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            List<string> lines = new List<string>();
            lines.Add("xml rows=" + coded.Count + " csv rows=" + sheet.Rows.Count + " file=" + Path.GetFileName(xmlPath));
            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                OrderedDictionary row = sheet.Rows[i];
                EnsureResultColumns(row);
                string nid = FindNationalId(row);
                string name = FindClientName(row);
                CodingExportRow c = MatchXmlRow(nid, name, byId, byDigits, byName);
                if (c == null)
                {
                    if (!string.IsNullOrEmpty(nid)) { unmatched++; }
                    SetResult(row, "الحالة", "Status", "غير موجود في تصدير XML");
                    if (row.Contains("Status")) { row["Status"] = "NOT_IN_EXPORT"; }
                    SetResult(row, "ملاحظات", "Notes",
                        "CSV↔XML: الرقم القومي غير موجود في " + Path.GetFileName(xmlPath));
                    SetResult(row, "آخر تحديث", "LastUpdated", stamp);
                    lines.Add("MISS " + nid + " «" + name + "» — not in XML export");
                    continue;
                }
                SetResult(row, "الحالة", "Status", "مكوّد");
                if (row.Contains("Status")) { row["Status"] = "CODED"; }
                SetResult(row, "كود 12", "CODE12", c.CODE12 != null ? c.CODE12 : "");
                SetResult(row, "سبب الرفض", "RejectReason", "");
                SetResult(row, "ملاحظات", "Notes",
                    "XML CODE12=" + (c.CODE12 != null ? c.CODE12 : ""));
                SetResult(row, "آخر تحديث", "LastUpdated", stamp);
                updated++;
                lines.Add("MATCH " + nid + " → NIN=" + (c.NIN ?? "") + " CODE12=" + (c.CODE12 ?? ""));
            }

            string backup = BackupSheet(sheet.Path);
            PersistSheet(sheet);

            SheetUpdateResult ur = new SheetUpdateResult();
            ur.Updated = updated;
            ur.Unmatched = unmatched;
            ur.Path = sheet.Path;
            ur.Backup = backup;
            ur.Lines = lines;
            return ur;
        }

        // Write استعلام / entry status onto الحالة + Status + ملاحظات for one NationalId.
        public static SheetUpdateResult WriteClientStatus(
            string sheetPath,
            string nationalId,
            string statusAr,
            string statusEn,
            string notes)
        {
            return WriteClientStatus(sheetPath, nationalId, statusAr, statusEn, notes, true);
        }

        public static SheetUpdateResult WriteClientStatus(
            string sheetPath,
            string nationalId,
            string statusAr,
            string statusEn,
            string notes,
            bool overwriteStatus)
        {
            SheetUpdateResult ur = new SheetUpdateResult();
            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath))
            {
                return ur;
            }
            string nidWant = nationalId != null ? nationalId.Trim() : "";
            if (nidWant.Length == 0) { return ur; }

            SheetImportResult sheet = ImportClientSheet(sheetPath);
            ur.Path = sheet.Path;
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                OrderedDictionary row = sheet.Rows[i];
                EnsureResultColumns(row);
                if (!SameNationalId(FindNationalId(row), nidWant)) { continue; }
                if (overwriteStatus)
                {
                    if (!string.IsNullOrEmpty(statusAr))
                    {
                        if (row.Contains("الحالة")) { row["الحالة"] = statusAr; }
                        else { row.Add("الحالة", statusAr); }
                    }
                    if (!string.IsNullOrEmpty(statusEn))
                    {
                        if (row.Contains("Status")) { row["Status"] = statusEn; }
                        else { row.Add("Status", statusEn); }
                    }
                }
                if (!string.IsNullOrEmpty(notes))
                {
                    if (!overwriteStatus)
                    {
                        string prev = CsvUtil.Get(row, "ملاحظات", "Notes");
                        if (!string.IsNullOrWhiteSpace(prev) &&
                            prev.IndexOf(notes, StringComparison.Ordinal) < 0)
                        {
                            notes = prev.Trim() + " | " + notes;
                        }
                    }
                    SetResult(row, "ملاحظات", "Notes", notes);
                    if (notes.StartsWith("رفض:", StringComparison.Ordinal))
                    {
                        string reason = notes.Substring(4).Trim();
                        SetResult(row, "سبب الرفض", "RejectReason", reason);
                    }
                }
                SetResult(row, "آخر تحديث", "LastUpdated", stamp);
                ur.Updated++;
            }
            ur.Unmatched = ur.Updated > 0 ? 0 : 1;
            if (ur.Updated > 0) { PersistSheet(sheet); }
            return ur;
        }

        public static SheetUpdateResult WriteClientCodes(
            string sheetPath, string nationalId, string nin, string code12)
        {
            SheetUpdateResult ur = new SheetUpdateResult();
            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath)) { return ur; }
            string nidWant = nationalId != null ? nationalId.Trim() : "";
            if (nidWant.Length == 0) { return ur; }
            if (string.IsNullOrWhiteSpace(nin) && string.IsNullOrWhiteSpace(code12)) { return ur; }

            SheetImportResult sheet = ImportClientSheet(sheetPath);
            ur.Path = sheet.Path;
            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                OrderedDictionary row = sheet.Rows[i];
                EnsureResultColumns(row);
                if (!SameNationalId(FindNationalId(row), nidWant)) { continue; }
                if (!string.IsNullOrWhiteSpace(nin))
                {
                    SetResult(row, "الكود الموحد", "NIN", nin.Trim());
                }
                if (!string.IsNullOrWhiteSpace(code12))
                {
                    SetResult(row, "كود 12", "CODE12", code12.Trim());
                }
                ur.Updated++;
            }
            ur.Unmatched = ur.Updated > 0 ? 0 : 1;
            if (ur.Updated > 0) { PersistSheet(sheet); }
            return ur;
        }

        private static void EnsureResultColumns(OrderedDictionary row)
        {
            for (int c = 0; c < ResultArabic.Length; c++)
            {
                if (!row.Contains(ResultArabic[c])) { row.Add(ResultArabic[c], ""); }
            }
            for (int c = 0; c < ResultEnglish.Length; c++)
            {
                if (!row.Contains(ResultEnglish[c])) { row.Add(ResultEnglish[c], ""); }
            }
        }

        private static void SetResult(OrderedDictionary row, string arabic, string english, string value)
        {
            if (row.Contains(arabic)) { row[arabic] = value; }
            else { row.Add(arabic, value); }
            if (row.Contains(english)) { row[english] = value; }
            else { row.Add(english, value); }
        }

        private static string FindNationalId(OrderedDictionary row)
        {
            string[] keys = new string[] {
                "NationalId", "الرقم القومي", "الرقم القومى", "ID Number", "IdNumber", "ID_NUMBER"
            };
            for (int i = 0; i < keys.Length; i++)
            {
                string v = CsvUtil.Get(row, keys[i], "");
                if (!string.IsNullOrWhiteSpace(v)) { return v.Trim(); }
            }
            return "";
        }

        private static string FindClientName(OrderedDictionary row)
        {
            string[] keys = new string[] {
                "Name", "Client Name", "الاسم", "الإسم", "اسم العميل", "CLIENT_NAME"
            };
            for (int i = 0; i < keys.Length; i++)
            {
                string v = CsvUtil.Get(row, keys[i], "");
                if (!string.IsNullOrWhiteSpace(v)) { return v.Trim(); }
            }
            return "";
        }

        private static CodingExportRow MatchXmlRow(
            string nid,
            string name,
            Dictionary<string, CodingExportRow> byId,
            Dictionary<string, CodingExportRow> byDigits,
            Dictionary<string, CodingExportRow> byName)
        {
            if (!string.IsNullOrEmpty(nid))
            {
                if (byId.ContainsKey(nid)) { return byId[nid]; }
                string d = DigitsOnly(nid);
                if (d.Length >= 10 && byDigits.ContainsKey(d)) { return byDigits[d]; }
            }
            if (!string.IsNullOrEmpty(name) && byName.ContainsKey(name)) { return byName[name]; }
            if (!string.IsNullOrEmpty(name))
            {
                string want = ArabicText.Normalize(name);
                foreach (string k in byName.Keys)
                {
                    if (string.Equals(ArabicText.Normalize(k), want, StringComparison.Ordinal))
                    {
                        return byName[k];
                    }
                }
            }
            return null;
        }

        private static string DigitsOnly(string value)
        {
            return KycSheetAdapter.DigitsOnly(value);
        }

        private static bool SameNationalId(string a, string b)
        {
            string aa = (a ?? "").Trim();
            string bb = (b ?? "").Trim();
            if (string.Equals(aa, bb, StringComparison.OrdinalIgnoreCase)) { return true; }
            string da = DigitsOnly(aa);
            string db = DigitsOnly(bb);
            return da.Length >= 10 && string.Equals(da, db, StringComparison.Ordinal);
        }

        private static string BackupSheet(string path)
        {
            string backup = path + ".bak-" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            try { File.Copy(path, backup, true); }
            catch { backup = ""; }
            return backup;
        }

        private static void PersistSheet(SheetImportResult sheet)
        {
            string temp = sheet.Path + ".tmp";
            if (sheet.Format == "csv")
            {
                List<string> cols = new List<string>();
                if (sheet.Rows.Count > 0)
                {
                    foreach (string k in sheet.Rows[0].Keys) { cols.Add(k); }
                }
                CsvUtil.WriteCsv(temp, cols.ToArray(), sheet.Rows);
            }
            else
            {
                XlsxWriter.ExportXlsx(sheet.Rows, temp, "Clients", true);
            }
            // Excel keeps the sheet locked while the operator has it open. Retry, and
            // never delete the original before the replacement is safely in place.
            const int Attempts = 5;
            for (int i = 0; i < Attempts; i++)
            {
                try
                {
                    if (File.Exists(sheet.Path))
                    {
                        File.Replace(temp, sheet.Path, null, true);
                    }
                    else
                    {
                        File.Move(temp, sheet.Path);
                    }
                    return;
                }
                catch (IOException)
                {
                    if (i == Attempts - 1)
                    {
                        try { if (File.Exists(temp)) { File.Delete(temp); } } catch { }
                        throw new IOException(
                            "تعذر تحديث الملف «" + sheet.Path +
                            "» — أغلق الملف من Excel ثم أعد المحاولة.");
                    }
                    Thread.Sleep(400);
                }
            }
        }
    }
}
