// =============================================================================
//  Validator.cs - local KYC validation before contacting Coding.exe.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace RbaCore
{
    public class ValidateResult
    {
        public int Ok;
        public int Bad;
        public int DocBad;
        public int Protected;
        public List<OrderedDictionary> Rows;
        public List<string> LogLines;
    }

    public static class Validator
    {
        public static ValidateResult ValidateFile(string path, string scansRoot, string trackingPath)
        {
            SheetImportResult sheet = SheetSync.ImportClientSheet(path);
            ValidateResult result = new ValidateResult();
            result.Rows = new List<OrderedDictionary>();
            result.LogLines = new List<string>();
            result.LogLines.Add("file: " + Path.GetFileName(path) + " - " + sheet.Rows.Count + " rows");

            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                OrderedDictionary r = sheet.Rows[i];
                List<string> errs = new List<string>();
                List<string> warns = new List<string>();

                string name = First(r, "Name", "الاسم", "الإسم");
                string nid = First(r, "NationalId", "الرقم القومي", "الرقم القومى");
                string expiry = First(r, "IdExpiryDate", "تاريخ الانتهاء");
                string addr = First(r, "Address", "العنوان");

                if (string.IsNullOrWhiteSpace(name)) { errs.Add("الإسم مطلوب"); }
                else if (Regex.Split(name.Trim(), "\\s+").Length < 4) { errs.Add("الإسم يجب أن يكون رباعياً"); }

                EgyptId.IdInfo chk = EgyptId.TestEgyptianNationalId(nid);
                for (int e = 0; e < chk.Errors.Count; e++)
                {
                    if (chk.Errors[e].IndexOf("خانة التحقق") < 0) { errs.Add(chk.Errors[e]); }
                }
                if (chk.IsValid && !chk.ChecksumOk) { warns.Add("خانة التحقق غير مطابقة - تلزم مراجعة بشرية"); }

                if (!string.IsNullOrWhiteSpace(expiry))
                {
                    DateTime d;
                    if (!DateTime.TryParseExact(expiry.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    {
                        errs.Add("تاريخ الانتهاء غير صحيح");
                    }
                    else if (d.Date < DateTime.Now.Date)
                    {
                        errs.Add("البطاقة منتهية الصلاحية (" + expiry + ")");
                    }
                }

                bool hasAddressCol = HasKey(r, "Address") || HasKey(r, "العنوان");
                if (hasAddressCol && string.IsNullOrWhiteSpace(addr)) { errs.Add("العنوان مطلوب"); }

                string docsFolder = First(r, "DocsFolder", "");
                string docsNote;
                bool docsOk = TestDocuments(nid, name, docsFolder, scansRoot, out docsNote);
                if (!docsOk && !string.IsNullOrEmpty(docsNote))
                {
                    warns.Add(docsNote);
                    result.DocBad++;
                }

                string rowStatus = "VALIDATED";
                if (errs.Count > 0)
                {
                    rowStatus = "VALIDATION_FAILED";
                    result.Bad++;
                    result.LogLines.Add("رفض محلي: " + name + " [" + nid + "] :: " + string.Join(" ؛ ", errs.ToArray()));
                }
                else
                {
                    result.Ok++;
                    if (warns.Count > 0)
                    {
                        result.LogLines.Add("تحذير: " + name + " :: " + string.Join(" ؛ ", warns.ToArray()));
                    }
                }

                List<string> notes = new List<string>();
                notes.AddRange(errs);
                notes.AddRange(warns);

                OrderedDictionary outRow = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
                outRow["NationalId"] = nid != null ? nid.Trim() : "";
                outRow["Name"] = name;
                outRow["Status"] = rowStatus;
                outRow["NIN"] = "";
                outRow["CODE12"] = "";
                outRow["RejectReason"] = "";
                outRow["Notes"] = string.Join(" ؛ ", notes.ToArray());
                outRow["SourceFile"] = Path.GetFileName(path);
                outRow["LastUpdated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                result.Rows.Add(outRow);
            }

            List<OrderedDictionary> existing = RunTracker.LoadTracking(trackingPath);
            MergeResult merge = RunTracker.MergeTracking(existing, result.Rows);
            RunTracker.SaveTracking(trackingPath, merge.Rows);
            result.Protected = merge.Protected;
            result.LogLines.Add("مقبول: " + result.Ok + "    مرفوض محلياً: " + result.Bad);
            if (result.DocBad > 0)
            {
                result.LogLines.Add(result.DocBad + " عميل لديه مشكلة في المستندات - راجعها قبل الترحيل");
            }
            if (merge.Protected > 0)
            {
                result.LogLines.Add(merge.Protected + " عميل له نتيجة سابقة من البورصة - تم الإبقاء عليها");
            }
            return result;
        }

        private static bool TestDocuments(string nid, string name, string docsFolder, string scansRoot, out string note)
        {
            note = "";
            // Reuse attach resolver so Setup scansRoot (parent Docs OR NID folder) both work.
            OrderedDictionary fake = new OrderedDictionary();
            if (!string.IsNullOrEmpty(nid)) { fake["NationalId"] = nid; }
            if (!string.IsNullOrEmpty(docsFolder)) { fake["DocsFolder"] = docsFolder; }
            string folder = DocumentAttach.ResolveDocsFolder(fake, scansRoot);
            return DocImageRules.TryValidateFolder(folder, out note);
        }

        private static string First(OrderedDictionary row, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                string v = CsvUtil.Get(row, keys[i], "");
                if (!string.IsNullOrEmpty(v)) { return v; }
            }
            return "";
        }

        private static bool HasKey(OrderedDictionary row, string key)
        {
            foreach (string k in row.Keys)
            {
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase)) { return true; }
            }
            return false;
        }
    }
}
