// =============================================================================
//  KycSheetAdapter.cs - map branch KYC export columns → Coding entry fields.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;

namespace RbaCore
{
    public static class KycSheetAdapter
    {
        public static readonly string[] WorkingColumns = new string[]
        {
            "RowId", "BrokerCode", "Name", "NationalId", "DateOfBirth", "Address",
            "NationalityCode", "Nationality", "GenderCode", "Gender",
            "LegalFormCode", "LegalForm", "SubFormCode", "SubForm",
            "IdTypeCode", "IdType", "IdIssueDate", "IdExpiryDate",
            "Landline", "Mobile", "Email", "Job",
            "GovernorateCode", "Governorate", "CountryCode", "Country",
            "AgentName", "AgentNationalId", "DocsFolder", "DocsCount",
            "الحالة", "Status", "الكود الموحد", "NIN", "كود 12", "CODE12",
            "سبب الرفض", "RejectReason", "ملاحظات", "Notes", "آخر تحديث", "LastUpdated"
        };

        // Normalize in-place: keep original path, rewrite as working Coding CSV.
        public static string NormalizeToWorkingSheet(string path, string scansRoot, string defaultBroker)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new Exception("KYC sheet not found: " + path);
            }
            SheetImportResult sheet = SheetSync.ImportClientSheet(path);
            List<OrderedDictionary> outRows = new List<OrderedDictionary>();
            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                OrderedDictionary raw = sheet.Rows[i];
                string rawNid = DigitsOnly(First(raw,
                    "NationalId", "الرقم القومي", "الرقم القومى", "ID Number", "IdNumber", "ID_NUMBER"));
                if (rawNid.Length < 10) { continue; }
                outRows.Add(NormalizeRow(raw, outRows.Count + 1, scansRoot, defaultBroker));
            }
            string outPath = path;
            // Always persist as CSV working sheet next to cache (stable for write-back).
            if (!string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase))
            {
                outPath = Path.ChangeExtension(path, ".csv");
            }
            CsvUtil.WriteCsv(outPath, WorkingColumns, outRows);
            return outPath;
        }

        public static OrderedDictionary NormalizeRow(
            OrderedDictionary raw, int rowId, string scansRoot, string defaultBroker)
        {
            OrderedDictionary row = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
            EnsureKeys(row);

            string nid = First(raw,
                "NationalId", "الرقم القومي", "الرقم القومى", "ID Number", "IdNumber", "ID_NUMBER");
            nid = DigitsOnly(nid);
            string name = First(raw, "Name", "Client Name", "الاسم", "اسم العميل", "CLIENT_NAME");
            string address = First(raw, "Address", "العنوان");
            if (string.IsNullOrWhiteSpace(address))
            {
                address = FakeAddress(nid, rowId);
            }
            string mobile = NormalizeMobile(First(raw, "Mobile", "Phone Number", "Phone", "ت. محمول"));
            string email = First(raw, "Email", "البريد");
            if (string.IsNullOrWhiteSpace(email))
            {
                email = "client" + rowId.ToString(CultureInfo.InvariantCulture) + "@example.com";
            }
            string job = First(raw, "Job", "Occupation", "الوظيفة");
            if (string.IsNullOrWhiteSpace(job)) { job = "موظف"; }
            string dob = First(raw, "DateOfBirth", "Date of Birth", "تاريخ الميلاد");
            string issue = First(raw, "IdIssueDate", "Identity Issue Date", "تاريخ الإصدار");
            string expiry = First(raw, "IdExpiryDate", "Identity Expiry Date", "تاريخ الانتهاء");
            if (string.IsNullOrWhiteSpace(issue)) { issue = "01/01/2020"; }
            if (string.IsNullOrWhiteSpace(expiry)) { expiry = "01/01/2030"; }

            string idType = First(raw, "IdTypeCode", "ID Type", "IdType");
            string nationality = First(raw, "NationalityCode", "Nationality");
            string genderCode = First(raw, "GenderCode", "Gender");
            string legal = First(raw, "LegalFormCode", "Legal Form");
            string country = First(raw, "CountryCode", "Country");
            string gov = First(raw, "GovernorateCode", "Governorate");
            string broker = First(raw, "BrokerCode", "كود الفرع");
            if (string.IsNullOrWhiteSpace(broker)) { broker = defaultBroker; }
            if (string.IsNullOrWhiteSpace(broker)) { broker = "1095"; }

            EgyptId.IdInfo idInfo = null;
            if (nid.Length == 14)
            {
                try { idInfo = EgyptId.TestEgyptianNationalId(nid); } catch { idInfo = null; }
            }
            if (string.IsNullOrWhiteSpace(dob) && idInfo != null && idInfo.DateOfBirth.HasValue)
            {
                dob = idInfo.DateOfBirth.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
            }
            if (string.IsNullOrWhiteSpace(genderCode) && idInfo != null)
            {
                genderCode = idInfo.GenderCode ?? "";
            }
            if (string.IsNullOrWhiteSpace(gov) && idInfo != null && !string.IsNullOrEmpty(idInfo.GovCode))
            {
                gov = idInfo.GovCode;
            }

            // Coding codes used in production maps.
            if (string.IsNullOrWhiteSpace(nationality) || nationality == "1")
            {
                // KYC often stores Egyptian as 1; Coding entry uses 21.
                nationality = "21";
            }
            if (string.IsNullOrWhiteSpace(country)) { country = "21"; }
            if (string.IsNullOrWhiteSpace(idType)) { idType = "7"; }
            if (string.IsNullOrWhiteSpace(legal)) { legal = "1"; }
            string subForm = First(raw, "SubFormCode", "SubForm");
            if (string.IsNullOrWhiteSpace(subForm)) { subForm = "10"; }
            if (string.IsNullOrWhiteSpace(genderCode)) { genderCode = "1"; }

            string statusAr = First(raw, "الحالة", "Status");
            string statusEn = First(raw, "Status", "");
            if (OperatorDecision.IsNew(statusAr) || OperatorDecision.IsNew(statusEn))
            {
                statusAr = OperatorDecision.NewAr;
                statusEn = OperatorDecision.NewEn;
            }
            else if (OperatorDecision.IsPull(statusAr) || OperatorDecision.IsPull(statusEn))
            {
                statusAr = OperatorDecision.PullAr;
                statusEn = OperatorDecision.PullEn;
            }
            else if (OperatorDecision.IsSuspended(statusAr) || OperatorDecision.IsSuspended(statusEn))
            {
                if (string.Equals((statusAr ?? "").Trim(), OperatorDecision.RejectedAr, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals((statusEn ?? "").Trim(), OperatorDecision.RejectedEn, StringComparison.OrdinalIgnoreCase))
                {
                    statusAr = OperatorDecision.RejectedAr;
                    statusEn = OperatorDecision.RejectedEn;
                }
                else
                {
                    statusAr = OperatorDecision.SuspendedAr;
                    statusEn = OperatorDecision.SuspendedEn;
                }
            }

            string code12 = DigitsOnly(First(raw, "كود 12", "CODE12"));
            string nin = DigitsOnly(First(raw, "الكود الموحد", "NIN"));

            int docsCount = 0;
            string docsFolder = nid;
            if (!string.IsNullOrEmpty(nid) && !string.IsNullOrEmpty(scansRoot))
            {
                string folder = Path.Combine(scansRoot, nid);
                if (Directory.Exists(folder))
                {
                    try
                    {
                        string[] files = Directory.GetFiles(folder);
                        docsCount = files != null ? files.Length : 0;
                    }
                    catch { docsCount = 0; }
                }
            }

            Set(row, "RowId", rowId.ToString(CultureInfo.InvariantCulture));
            Set(row, "BrokerCode", broker);
            Set(row, "Name", name);
            Set(row, "NationalId", nid);
            Set(row, "DateOfBirth", dob);
            Set(row, "Address", address);
            Set(row, "NationalityCode", nationality);
            Set(row, "Nationality", nationality == "21" ? "مصري" : "");
            Set(row, "GenderCode", genderCode);
            Set(row, "Gender", genderCode == "2" ? "أنثى" : "ذكر");
            Set(row, "LegalFormCode", legal);
            Set(row, "LegalForm", "فرد");
            Set(row, "SubFormCode", subForm);
            Set(row, "SubForm", "طبيعي");
            Set(row, "IdTypeCode", idType);
            Set(row, "IdType", "رقم قومى");
            Set(row, "IdIssueDate", issue);
            Set(row, "IdExpiryDate", expiry);
            Set(row, "Landline", "");
            Set(row, "Mobile", mobile);
            Set(row, "Email", email);
            Set(row, "Job", job);
            Set(row, "GovernorateCode", gov);
            Set(row, "Governorate", idInfo != null ? (idInfo.Governorate ?? "") : "");
            Set(row, "CountryCode", country);
            Set(row, "Country", "مصر");
            Set(row, "AgentName", "");
            Set(row, "AgentNationalId", "");
            Set(row, "DocsFolder", docsFolder);
            Set(row, "DocsCount", docsCount > 0 ? docsCount.ToString(CultureInfo.InvariantCulture) : "");
            Set(row, "الحالة", statusAr);
            Set(row, "Status", statusEn);
            Set(row, "الكود الموحد", nin);
            Set(row, "NIN", nin);
            Set(row, "كود 12", code12);
            Set(row, "CODE12", code12);
            Set(row, "سبب الرفض", First(raw, "سبب الرفض", "RejectReason"));
            Set(row, "RejectReason", First(raw, "RejectReason", "سبب الرفض"));
            Set(row, "ملاحظات", First(raw, "ملاحظات", "Notes"));
            Set(row, "Notes", First(raw, "Notes", "ملاحظات"));
            Set(row, "آخر تحديث", First(raw, "آخر تحديث", "LastUpdated"));
            Set(row, "LastUpdated", First(raw, "LastUpdated", "آخر تحديث"));
            return row;
        }

        private static void EnsureKeys(OrderedDictionary row)
        {
            for (int i = 0; i < WorkingColumns.Length; i++)
            {
                if (!row.Contains(WorkingColumns[i])) { row.Add(WorkingColumns[i], ""); }
            }
        }

        private static void Set(OrderedDictionary row, string key, string value)
        {
            if (row.Contains(key)) { row[key] = value ?? ""; }
            else { row.Add(key, value ?? ""); }
        }

        private static string First(OrderedDictionary row, params string[] keys)
        {
            if (row == null || keys == null) { return ""; }
            for (int i = 0; i < keys.Length; i++)
            {
                string v = CsvUtil.Get(row, keys[i], "");
                if (!string.IsNullOrWhiteSpace(v)) { return v.Trim(); }
            }
            return "";
        }

        public static string NormalizeMobile(string raw)
        {
            string d = DigitsOnly(raw);
            if (d.StartsWith("20") && d.Length >= 12) { d = d.Substring(2); }
            if (d.Length == 10 && d.StartsWith("1")) { d = "0" + d; }
            return d;
        }

        public static string DigitsOnly(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            char[] buf = new char[value.Length];
            int n = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char ascii = AsAsciiDigit(value[i]);
                if (ascii != '\0') { buf[n++] = ascii; }
            }
            return n == 0 ? "" : new string(buf, 0, n);
        }

        // Latin 0-9, Arabic-Indic ٠-٩, and Extended Arabic-Indic ۰-۹.
        public static char AsAsciiDigit(char c)
        {
            if (c >= '0' && c <= '9') { return c; }
            if (c >= '\u0660' && c <= '\u0669') { return (char)('0' + (c - '\u0660')); }
            if (c >= '\u06F0' && c <= '\u06F9') { return (char)('0' + (c - '\u06F0')); }
            return '\0';
        }

        private static string FakeAddress(string nid, int rowId)
        {
            string[] streets = new string[]
            {
                "شارع النيل", "شارع الجامعة", "شارع الهرم", "شارع فيصل", "شارع الجمهورية"
            };
            string[] areas = new string[]
            {
                "المعادي", "مدينة نصر", "الدقي", "مصر الجديدة", "العجوزة"
            };
            int s = Math.Abs((nid ?? "").GetHashCode()) % streets.Length;
            int a = Math.Abs(rowId * 17 + s) % areas.Length;
            return "مبنى " + (10 + rowId) + " " + streets[s] + " - " + areas[a] + " - القاهرة";
        }
    }
}
