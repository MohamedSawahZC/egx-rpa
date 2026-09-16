// =============================================================================
//  MapHealth.cs - validate a click-map before it is trusted at run time.
//
//  Ordinal/Index identity is only valid inside ONE discovery pass on ONE form
//  state. Coding shows «الوظيفة» after الشكل القانوني is set, which shifts every
//  later control, so a map captured across two states silently points at the
//  wrong boxes. These checks surface that instead of letting it hallucinate.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RbaCore
{
    public class MapHealthIssue
    {
        public string FieldKey;
        public string Severity;   // "ERROR" | "WARN"
        public string Message;
    }

    public class MapHealthReport
    {
        public List<MapHealthIssue> Issues = new List<MapHealthIssue>();
        public int MappedCount;
        public int WithPositionCount;

        public bool HasErrors
        {
            get
            {
                for (int i = 0; i < Issues.Count; i++)
                {
                    if (Issues[i].Severity == "ERROR") { return true; }
                }
                return false;
            }
        }

        public string ToArabicSummary()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("حقول مربوطة: " + MappedCount +
                "  |  محفوظة بالإحداثيات: " + WithPositionCount);
            if (Issues.Count == 0)
            {
                sb.AppendLine();
                sb.AppendLine("لا توجد مشاكل — الربط سليم.");
                return sb.ToString();
            }
            sb.AppendLine();
            for (int i = 0; i < Issues.Count; i++)
            {
                MapHealthIssue it = Issues[i];
                string tag = it.Severity == "ERROR" ? "[خطأ]" : "[تنبيه]";
                sb.AppendLine(tag + " " + it.FieldKey + ": " + it.Message);
            }
            return sb.ToString();
        }
    }

    public static class MapHealth
    {
        // Captures more than this far apart almost certainly saw different form states.
        private const int SameSessionMinutes = 20;

        public static MapHealthReport Inspect(List<FieldSelector> selectors)
        {
            MapHealthReport rep = new MapHealthReport();
            if (selectors == null) { return rep; }

            List<FieldSelector> mapped = new List<FieldSelector>();
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                if (s == null || string.IsNullOrWhiteSpace(s.Class)) { continue; }
                mapped.Add(s);
            }
            rep.MappedCount = mapped.Count;

            CheckPositions(mapped, rep);
            // Ordinal/Index identity is per-form: «الإستعلام للتعديل» reuses the same
            // numbers as the entry form, so compare inside each screen only.
            Dictionary<string, List<FieldSelector>> byScreen = GroupByScreen(mapped);
            foreach (KeyValuePair<string, List<FieldSelector>> kv in byScreen)
            {
                CheckDuplicates(kv.Value, rep);
                CheckCaptureSessions(kv.Value, rep);
            }
            return rep;
        }

        private static Dictionary<string, List<FieldSelector>> GroupByScreen(List<FieldSelector> mapped)
        {
            Dictionary<string, List<FieldSelector>> byScreen =
                new Dictionary<string, List<FieldSelector>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < mapped.Count; i++)
            {
                string screen = mapped[i].Screen != null ? mapped[i].Screen.Trim() : "";
                if (!byScreen.ContainsKey(screen)) { byScreen[screen] = new List<FieldSelector>(); }
                byScreen[screen].Add(mapped[i]);
            }
            return byScreen;
        }

        private static void CheckPositions(List<FieldSelector> mapped, MapHealthReport rep)
        {
            List<string> missing = new List<string>();
            for (int i = 0; i < mapped.Count; i++)
            {
                if (mapped[i].HasPosition) { rep.WithPositionCount++; }
                else { missing.Add(Name(mapped[i])); }
            }
            if (missing.Count == 0) { return; }

            MapHealthIssue it = new MapHealthIssue();
            it.FieldKey = "الإحداثيات";
            it.Severity = "ERROR";
            it.Message = missing.Count + " حقل بدون إحداثيات محفوظة — أعد التقاطه بالنقر: " +
                Join(missing);
            rep.Issues.Add(it);
        }

        private static void CheckDuplicates(List<FieldSelector> mapped, MapHealthReport rep)
        {
            Dictionary<string, List<string>> byOrdinal =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<string>> byIndex =
                new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < mapped.Count; i++)
            {
                FieldSelector s = mapped[i];
                if (!string.IsNullOrWhiteSpace(s.ClassOrdinal))
                {
                    Add(byOrdinal, s.Class + "|" + s.Kind + "|" + s.ClassOrdinal, Name(s));
                }
                if (!string.IsNullOrWhiteSpace(s.Index))
                {
                    Add(byIndex, s.Index, Name(s));
                }
            }

            ReportDuplicates(byOrdinal, rep, "ترتيب النوع (ClassOrdinal)");
            ReportDuplicates(byIndex, rep, "الترتيب العام (Index)");
        }

        private static void ReportDuplicates(
            Dictionary<string, List<string>> groups, MapHealthReport rep, string what)
        {
            foreach (KeyValuePair<string, List<string>> kv in groups)
            {
                if (kv.Value.Count < 2) { continue; }
                MapHealthIssue it = new MapHealthIssue();
                it.FieldKey = what;
                it.Severity = "WARN";
                it.Message = "نفس القيمة لأكثر من حقل (" + Join(kv.Value) +
                    ") — تُستخدم الإحداثيات بدلاً منها.";
                rep.Issues.Add(it);
            }
        }

        private static void CheckCaptureSessions(List<FieldSelector> mapped, MapHealthReport rep)
        {
            DateTime min = DateTime.MaxValue;
            DateTime max = DateTime.MinValue;
            int counted = 0;
            for (int i = 0; i < mapped.Count; i++)
            {
                DateTime t;
                if (!DateTime.TryParse(
                        mapped[i].ConfirmedAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out t))
                {
                    continue;
                }
                if (t < min) { min = t; }
                if (t > max) { max = t; }
                counted++;
            }
            if (counted < 2) { return; }
            if ((max - min).TotalMinutes <= SameSessionMinutes) { return; }

            MapHealthIssue it = new MapHealthIssue();
            it.FieldKey = "جلسات الالتقاط";
            it.Severity = "WARN";
            it.Message = "الحقول التُقطت على فترات متباعدة (" +
                min.ToString("HH:mm") + " → " + max.ToString("HH:mm") +
                ") وربما بحالة نموذج مختلفة. يُفضّل إعادة الالتقاط في جلسة واحدة.";
            rep.Issues.Add(it);
        }

        private static void Add(Dictionary<string, List<string>> d, string key, string value)
        {
            if (!d.ContainsKey(key)) { d[key] = new List<string>(); }
            d[key].Add(value);
        }

        private static string Name(FieldSelector s)
        {
            if (s == null) { return "?"; }
            if (!string.IsNullOrWhiteSpace(s.FieldLabel)) { return s.FieldLabel; }
            return string.IsNullOrWhiteSpace(s.FieldKey) ? "?" : s.FieldKey;
        }

        private static string Join(List<string> names)
        {
            if (names == null || names.Count == 0) { return ""; }
            int take = names.Count > 8 ? 8 : names.Count;
            string text = string.Join("، ", names.GetRange(0, take).ToArray());
            if (names.Count > take) { text += " … (+" + (names.Count - take) + ")"; }
            return text;
        }
    }
}
