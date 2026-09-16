// =============================================================================
//  ArabicText.cs - tolerant matching for Coding.exe captions and messages.
//
//  The same screen ships as «الإستعلام» or «الاستعلام», and messages differ in
//  ة/ه and ى/ي between builds. Comparing raw strings misses those variants.
// =============================================================================
using System;
using System.Text;

namespace RbaCore
{
    public static class ArabicText
    {
        // Fold hamza/alef/ya/ta-marbuta variants, tashkeel and spacing away.
        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsWhiteSpace(c)) { continue; }
                if (c >= '\u064B' && c <= '\u0652') { continue; } // tashkeel
                if (c == '\u0640') { continue; }                  // tatweel
                switch (c)
                {
                    case 'أ': case 'إ': case 'آ': case 'ٱ': c = 'ا'; break;
                    case 'ى': c = 'ي'; break;
                    case 'ة': c = 'ه'; break;
                    case 'ؤ': c = 'و'; break;
                    case 'ئ': c = 'ي'; break;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool Contains(string haystack, string needle)
        {
            string h = Normalize(haystack);
            string n = Normalize(needle);
            if (h.Length == 0 || n.Length == 0) { return false; }
            return h.IndexOf(n, StringComparison.Ordinal) >= 0;
        }

        public static bool ContainsAny(string haystack, string[] needles)
        {
            if (needles == null) { return false; }
            for (int i = 0; i < needles.Length; i++)
            {
                if (Contains(haystack, needles[i])) { return true; }
            }
            return false;
        }
    }
}
