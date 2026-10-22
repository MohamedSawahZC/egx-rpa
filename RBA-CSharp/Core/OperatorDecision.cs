// =============================================================================
//  OperatorDecision.cs - manual triage statuses (no OCR / inquiry).
// =============================================================================
using System;

namespace RbaCore
{
    public static class OperatorDecision
    {
        public const string NewAr = "جديد";
        public const string PullAr = "سحب";
        public const string SuspendedAr = "موقوف";
        public const string RejectedAr = "مرفوض";

        public const string NewEn = "NEW";
        public const string PullEn = "PULL";
        public const string SuspendedEn = "SUSPENDED";
        public const string RejectedEn = "REJECTED";

        public const string RejectOthersAr = "أسباب أخرى";

        public static readonly string[] RejectReasons = new string[]
        {
            "صورة بطاقة أوضح مطلوبة",
            "كود مشابه/مكرر",
            "مستند ناقص مطلوب",
            "نفس الكود باسم مختلف",
            "مطلوب بطاقة الأب",
            "مطلوب بيان/إقرار بالكود",
            "الاسم الخامس ناقص",
            "بطاقة منتهية",
            "كود أوفلاين",
            "موقوف",
            RejectOthersAr
        };

        public static bool IsOthersReject(string reason)
        {
            string s = (reason ?? "").Trim();
            return string.Equals(s, RejectOthersAr, StringComparison.OrdinalIgnoreCase) ||
                   s.StartsWith(RejectOthersAr, StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatRejectReason(string selected, string otherText)
        {
            string pick = (selected ?? "").Trim();
            if (!IsOthersReject(pick)) { return pick; }
            string extra = (otherText ?? "").Trim();
            if (extra.Length == 0) { return ""; }
            if (extra.StartsWith(RejectOthersAr, StringComparison.OrdinalIgnoreCase)) { return extra; }
            return RejectOthersAr + ": " + extra;
        }

        public static string OthersRejectDetail(string reason)
        {
            string s = (reason ?? "").Trim();
            string prefix = RejectOthersAr + ":";
            if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return s.Substring(prefix.Length).Trim();
            }
            if (string.Equals(s, RejectOthersAr, StringComparison.OrdinalIgnoreCase)) { return ""; }
            return s;
        }

        public static bool IsDecision(string status)
        {
            string s = (status ?? "").Trim();
            if (s.Length == 0) { return false; }
            return string.Equals(s, NewAr, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, PullAr, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, SuspendedAr, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, RejectedAr, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, NewEn, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, PullEn, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, SuspendedEn, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, RejectedEn, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsNew(string status)
        {
            string s = (status ?? "").Trim();
            return string.Equals(s, NewAr, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, NewEn, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPull(string status)
        {
            string s = (status ?? "").Trim();
            return string.Equals(s, PullAr, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, PullEn, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSuspended(string status)
        {
            string s = (status ?? "").Trim();
            return string.Equals(s, SuspendedAr, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, SuspendedEn, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, RejectedAr, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(s, RejectedEn, StringComparison.OrdinalIgnoreCase);
        }

        public static string ReadStatus(System.Collections.Specialized.OrderedDictionary row)
        {
            string ar = CsvUtil.Get(row, "الحالة", "").Trim();
            string en = CsvUtil.Get(row, "Status", "").Trim();
            // الحالة="" must not hide Status=NEW (CsvUtil.Get stops at an empty first column).
            if (IsDecision(ar) || IsTerminalStatus(ar)) { return ar; }
            if (IsDecision(en) || IsTerminalStatus(en)) { return en; }
            return ar.Length > 0 ? ar : en;
        }
        public static bool IsTerminalStatus(string status)
        {
            string s = (status ?? "").Trim();
            if (s.Length == 0) { return false; }
            if (IsDecision(s)) { return false; }
            string u = s.ToUpperInvariant();
            return u.IndexOf("SAVED", StringComparison.Ordinal) >= 0 ||
                   u.IndexOf("CODES_PULLED", StringComparison.Ordinal) >= 0 ||
                   u.IndexOf("FAILED", StringComparison.Ordinal) >= 0 ||
                   u.IndexOf("SKIP", StringComparison.Ordinal) >= 0 ||
                   u.IndexOf("VALIDATED", StringComparison.Ordinal) >= 0 ||
                   s.IndexOf("تم الحفظ", StringComparison.Ordinal) >= 0 ||
                   s.IndexOf("سحب أكواد", StringComparison.Ordinal) >= 0 ||
                   s.IndexOf("تخط", StringComparison.Ordinal) >= 0 ||
                   s.IndexOf("موقوف —", StringComparison.Ordinal) >= 0;
        }

        public static bool NeedsClassify(string status)
        {
            string s = (status ?? "").Trim();
            if (IsDecision(s)) { return false; }
            if (IsTerminalStatus(s)) { return false; }
            return true;
        }
    }
}
