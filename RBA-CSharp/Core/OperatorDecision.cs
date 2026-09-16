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

        public const string RejectUpdateData = "تحديث بيانات";
        public const string RejectFamilyCard = "مكود ببطاقه عائليه";

        public static readonly string[] RejectReasons = new string[]
        {
            RejectUpdateData,
            RejectFamilyCard
        };

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
