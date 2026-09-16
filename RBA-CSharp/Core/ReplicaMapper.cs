// =============================================================================
//  ReplicaMapper.cs - build selectors against CodingReplica / VB6 entry layout.
//  Mirrors Verify-Rba.ps1 + full Test-Save customer field set.
// =============================================================================
using System;
using System.Collections.Generic;

namespace RbaCore
{
    public static class ReplicaMapper
    {
        // Form whose map is being converted — used to stamp form-relative geometry.
        [ThreadStatic]
        private static IntPtr _formRoot;

        // Build a working selector set from a live field map of the entry form.
        // CodingReplica layout (creation order):
        //   broker code(52)+combo, name(330), date×3(40), address(330),
        //   nationality/gender/legal/sub code+combo, nationalId(330),
        //   idType/gov/country code+combo, buttons حافظ/الغ/جديد.
        public static List<FieldSelector> BuildFromMap(FieldMap map)
        {
            List<FieldSelector> sels = new List<FieldSelector>();
            if (map == null || map.Fields == null) { return sels; }
            _formRoot = map.Window;

            List<AppField> wide = new List<AppField>();
            List<AppField> dates = new List<AppField>();
            List<AppField> codeBoxes = new List<AppField>();
            List<AppField> midBoxes = new List<AppField>();
            List<AppField> combos = new List<AppField>();
            AppField saveBtn = null;
            AppField newBtn = null;

            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f.Kind == "TextBox")
                {
                    // Date 40px; code 52px; mid (mobile) ~160; wide name/address/nid/job ~330.
                    if (f.W > 200) { wide.Add(f); }
                    else if (f.W > 0 && f.W <= 45) { dates.Add(f); }
                    else if (f.W > 45 && f.W <= 70) { codeBoxes.Add(f); }
                    else if (f.W > 70 && f.W <= 200) { midBoxes.Add(f); }
                }
                else if (f.Kind == "ComboBox") { combos.Add(f); }
                else if (f.Kind == "Button")
                {
                    string cap = f.Caption != null ? f.Caption.Trim() : "";
                    if (cap == "حافظ" || cap == "حفظ" || cap.IndexOf("Save", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        saveBtn = f;
                    }
                    else if (IsNewRecordCaption(cap))
                    {
                        newBtn = f;
                    }
                }
            }

            wide.Sort(CompareOrdinal);
            dates.Sort(CompareByScreenOrder);
            codeBoxes.Sort(CompareOrdinal);
            midBoxes.Sort(CompareOrdinal);
            combos.Sort(CompareOrdinal);

            // Broker: prefer the first narrow code box; combo[0] is the broker list.
            if (codeBoxes.Count >= 1)
            {
                sels.Add(Sel(codeBoxes[0], "brokerCode", "BrokerCode", "", codeBoxes[0].Caption));
            }
            else if (combos.Count >= 1)
            {
                sels.Add(Sel(combos[0], "broker", "BrokerCode", "", combos[0].Caption));
            }

            if (wide.Count >= 1) { sels.Add(Sel(wide[0], "name", "Name", "", wide[0].Caption)); }

            if (dates.Count >= 3)
            {
                // Hard rule: Day → Month → Year (left-to-right on the entry form).
                sels.Add(Sel(dates[0], FieldCatalog.DobDayKey, "DateOfBirth", "day", "يوم"));
                sels.Add(Sel(dates[1], FieldCatalog.DobMonthKey, "DateOfBirth", "month", "شهر"));
                sels.Add(Sel(dates[2], FieldCatalog.DobYearKey, "DateOfBirth", "year", "سنة"));
            }

            if (wide.Count >= 2) { sels.Add(Sel(wide[1], "address", "Address", "", "")); }

            // Prefer numeric CODE TextBoxes (not ComboBox dropdowns).
            // Typical order after broker code: nationality, gender, legal, sub, then later idType.
            if (codeBoxes.Count >= 2) { sels.Add(Sel(codeBoxes[1], "nationality", "NationalityCode", "", "")); }
            if (codeBoxes.Count >= 3) { sels.Add(Sel(codeBoxes[2], "gender", "GenderCode", "", "")); }
            if (codeBoxes.Count >= 4) { sels.Add(Sel(codeBoxes[3], "legalForm", "LegalFormCode", "", "")); }
            if (codeBoxes.Count >= 5) { sels.Add(Sel(codeBoxes[4], "subForm", "SubFormCode", "", "")); }

            if (wide.Count >= 3) { sels.Add(Sel(wide[2], "nationalId", "NationalId", "", "")); }

            if (codeBoxes.Count >= 6) { sels.Add(Sel(codeBoxes[5], "idType", "IdTypeCode", "", "")); }
            // Prefer CODE TextBoxes for governorate / country (22 / 21) — not ComboBox.
            if (codeBoxes.Count >= 7) { sels.Add(Sel(codeBoxes[6], "governorate", "GovernorateCode", "", "")); }
            else if (combos.Count >= 7) { sels.Add(Sel(combos[6], "governorate", "GovernorateCode", "", "")); }
            if (codeBoxes.Count >= 8) { sels.Add(Sel(codeBoxes[7], "country", "CountryCode", "", "")); }
            else if (combos.Count >= 8) { sels.Add(Sel(combos[7], "country", "CountryCode", "", "")); }

            // Mid-width: mobile; next wide after NID is job; docsCount is last code box.
            if (midBoxes.Count >= 1) { sels.Add(Sel(midBoxes[0], "mobile", "Mobile", "", "")); }
            if (codeBoxes.Count >= 9) { sels.Add(Sel(codeBoxes[8], "docsCount", "DocsCount", "", "")); }
            if (wide.Count >= 4) { sels.Add(Sel(wide[3], "job", "Job", "", "الوظيفة")); }

            // «ملفات» CheckBox — click the box only (not the caption Static).
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f.Kind != "CheckBox") { continue; }
                string cap = f.Caption != null ? f.Caption.Trim() : "";
                if (cap == "ملفات" || cap.Replace(" ", "") == "ملفات")
                {
                    FieldSelector cs = Sel(f, FieldCatalog.ChkFilesKey, "", "", cap);
                    cs.Kind = "CheckBox";
                    cs.Section = "مستندات";
                    sels.Add(cs);
                    break;
                }
            }

            if (saveBtn != null)
            {
                FieldSelector s = Sel(saveBtn, "btnSave", "", "", saveBtn.Caption);
                s.Caption = saveBtn.Caption;
                sels.Add(s);
            }
            if (newBtn != null)
            {
                FieldSelector n = Sel(newBtn, "btnNew", "", "", newBtn.Caption);
                n.Caption = newBtn.Caption;
                sels.Add(n);
            }

            // Document panel: three «إضافة» buttons (same caption — ClassOrdinal distinguishes).
            List<AppField> docAdds = new List<AppField>();
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f.Kind != "Button") { continue; }
                string cap = f.Caption != null ? f.Caption.Trim() : "";
                if (DocumentAttach.LooksLikeDocAddCaption(cap)) { docAdds.Add(f); }
            }
            docAdds.Sort(CompareByScreenOrder);
            for (int i = 0; i < docAdds.Count && i < FieldCatalog.DocAddKeys.Length; i++)
            {
                FieldSelector ds = Sel(docAdds[i], FieldCatalog.DocAddKeys[i], "", "", docAdds[i].Caption);
                ds.Section = "مستندات";
                sels.Add(ds);
            }
            return sels;
        }

        private static bool IsNewRecordCaption(string cap)
        {
            if (string.IsNullOrWhiteSpace(cap)) { return false; }
            string t = cap.Replace("&", "").Trim();
            if (t.IndexOf("وادي", StringComparison.Ordinal) >= 0) { return false; }
            return t == "جديد" ||
                   string.Equals(t, "New", StringComparison.OrdinalIgnoreCase) ||
                   (t.IndexOf("جديد", StringComparison.Ordinal) >= 0 && t.Length <= 12);
        }

        private static int CompareOrdinal(AppField a, AppField b)
        {
            return a.ClassOrdinal.CompareTo(b.ClassOrdinal);
        }

        // Date boxes on Coding: Day then Month then Year (increasing X left→right).
        private static int CompareByScreenOrder(AppField a, AppField b)
        {
            int byY = a.Y.CompareTo(b.Y);
            if (byY != 0 && Math.Abs(a.Y - b.Y) > 12) { return byY; }
            int byX = a.X.CompareTo(b.X);
            if (byX != 0) { return byX; }
            return a.ClassOrdinal.CompareTo(b.ClassOrdinal);
        }

        private static FieldSelector Sel(AppField f, string key, string col, string part, string label)
        {
            FieldSelector s = new FieldSelector();
            s.FieldKey = key;
            s.FieldLabel = label;
            s.Kind = f.Kind;
            s.Class = f.Class;
            s.ClassOrdinal = f.ClassOrdinal.ToString();
            s.Index = f.Index.ToString();
            s.ControlId = f.ControlId.ToString();
            s.Label = label;
            // Do not store live TextBox values as Caption — breaks later resolve.
            if (string.Equals(f.Kind, "Button", StringComparison.OrdinalIgnoreCase))
            {
                s.Caption = f.Caption;
            }
            else
            {
                s.Caption = "";
            }
            s.SheetColumn = col;
            s.DatePart = part;
            FieldDiscovery.StampPosition(s, _formRoot, f.Handle);
            return s;
        }
    }
}
