// =============================================================================
//  OcrConsensus.cs - majority vote over CLEAN OCR passes of one grid row.
//
//  NEVER invent alternate codes (1↔8, trailing 11→09, sliding windows). Typing a
//  guessed الكود الموحد into «سحب الأكواد» can pull a different client — that
//  happened with 3592611 / 1726351. Only a code that clean passes actually agree
//  on may leave this class; otherwise callers must refuse the pull.
// =============================================================================
using System;
using System.Collections.Generic;

namespace RbaCore
{
    public class OcrReading
    {
        public string Nin = "";
        public string Code12 = "";
        public int NinVotes;
        public int Code12Votes;
        public int Passes;

        public bool NinConfident
        {
            get { return Nin.Length >= 6 && NinVotes >= OcrConsensus.MinVotes; }
        }

        public bool Code12Confident
        {
            get { return Code12.Length == 12 && Code12Votes >= OcrConsensus.MinVotes; }
        }

        public bool Any
        {
            get { return Nin.Length > 0 || Code12.Length > 0; }
        }

        public string Describe()
        {
            return "NIN=" + (Nin.Length > 0 ? Nin + "(" + NinVotes + ")" : "(none)") +
                   " CODE12=" + (Code12.Length > 0 ? Code12 + "(" + Code12Votes + ")" : "(none)") +
                   " passes=" + Passes;
        }

        // At most the single voted code — never guessed siblings.
        public string[] NinCandidates = new string[0];
    }

    public static class OcrConsensus
    {
        public const int MinVotes = 2;

        public static OcrReading Vote(string harvest, string searchTerm)
        {
            OcrReading r = new OcrReading();
            if (string.IsNullOrWhiteSpace(harvest)) { return r; }

            string[] passes = harvest.Split(new char[] { '\t', '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            Dictionary<string, int> ninVotes = new Dictionary<string, int>(StringComparer.Ordinal);
            Dictionary<string, int> code12Votes = new Dictionary<string, int>(StringComparer.Ordinal);

            string wholeNin;
            string whole12;
            FlexGridSurface.ParseCodes(harvest, searchTerm != null ? searchTerm : "",
                out wholeNin, out whole12);
            wholeNin = NormalizeUnified(wholeNin);
            if (IsPlausibleUnified(wholeNin))
            {
                Bump(ninVotes, wholeNin, wholeNin.Length == 7 ? 3 : 2);
            }
            if (FlexGridSurface.LooksLikeBrokerCode12(whole12))
            {
                Bump(code12Votes, whole12, 2);
            }

            for (int i = 0; i < passes.Length; i++)
            {
                string pass = passes[i].Trim();
                if (pass.Length == 0) { continue; }
                if (IsDigitSoup(pass)) { continue; }
                r.Passes++;

                string nin;
                string code12;
                FlexGridSurface.ParseCodes(pass, searchTerm != null ? searchTerm : "",
                    out nin, out code12);
                nin = NormalizeUnified(nin);
                if (IsPlausibleUnified(nin))
                {
                    Bump(ninVotes, nin, nin.Length == 7 ? 2 : 1);
                }
                if (FlexGridSurface.LooksLikeBrokerCode12(code12))
                {
                    Bump(code12Votes, code12, 1);
                }
            }

            r.Nin = Winner(ninVotes, true, out r.NinVotes);
            r.Code12 = Winner(code12Votes, false, out r.Code12Votes);
            if (r.NinConfident)
            {
                r.NinCandidates = new string[] { r.Nin };
            }
            return r;
        }

        public static bool LooksLikeEgyptianNid(string digits)
        {
            if (string.IsNullOrEmpty(digits) || digits.Length != 14) { return false; }
            if (digits[0] != '2' && digits[0] != '3') { return false; }
            if (UniqueDigits(digits) < 5) { return false; }
            int zeros = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                if (digits[i] == '0' || digits[i] == '1') { zeros++; }
            }
            if (zeros >= 11) { return false; }
            return true;
        }

        public static string NormalizeUnified(string d)
        {
            if (string.IsNullOrEmpty(d)) { return ""; }
            d = DigitsOnly(d);
            if (d.Length == 8 && d[0] == '5' && IsPlausibleUnified(d.Substring(1)))
            {
                return d.Substring(1);
            }
            if (d.Length == 9 && d[0] == '5' && IsPlausibleUnified(d.Substring(1, 7)))
            {
                return d.Substring(1, 7);
            }
            return d;
        }

        public static bool IsPlausibleUnified(string d)
        {
            if (string.IsNullOrEmpty(d) || d.Length < 6 || d.Length > 9) { return false; }
            if (UniqueDigits(d) < 4) { return false; }
            if (IsDigitSoup(d)) { return false; }
            return true;
        }

        private static bool IsDigitSoup(string text)
        {
            string d = DigitsOnly(text);
            if (d.Length < 4) { return false; }
            int low = 0;
            for (int i = 0; i < d.Length; i++)
            {
                if (d[i] == '0' || d[i] == '1') { low++; }
            }
            return low * 10 >= d.Length * 7;
        }

        private static void Bump(Dictionary<string, int> votes, string key, int weight)
        {
            if (string.IsNullOrEmpty(key) || weight <= 0) { return; }
            int n;
            votes[key] = (votes.TryGetValue(key, out n) ? n : 0) + weight;
        }

        private static string Winner(Dictionary<string, int> votes, bool preferSeven, out int count)
        {
            count = 0;
            string best = "";
            foreach (KeyValuePair<string, int> kv in votes)
            {
                if (preferSeven && !IsPlausibleUnified(kv.Key) && kv.Key.Length != 12)
                {
                    continue;
                }
                int score = kv.Value;
                if (preferSeven && kv.Key.Length == 7) { score += 2; }
                if (preferSeven && kv.Key.Length == 8 && kv.Key[0] == '5') { score -= 3; }

                if (score > count)
                {
                    best = kv.Key;
                    count = score;
                    continue;
                }
                if (score != count || best.Length == 0) { continue; }
                if (preferSeven && kv.Key.Length == 7 && best.Length != 7)
                {
                    best = kv.Key;
                    continue;
                }
                if (preferSeven && kv.Key.Length != 7 && best.Length == 7) { continue; }
                if (UniqueDigits(kv.Key) > UniqueDigits(best)) { best = kv.Key; }
            }
            count = best.Length == 0 ? 0 : (votes.ContainsKey(best) ? votes[best] : count);
            return best;
        }

        private static int UniqueDigits(string digits)
        {
            if (string.IsNullOrEmpty(digits)) { return 0; }
            int seen = 0;
            int n = 0;
            for (int i = 0; i < digits.Length; i++)
            {
                int d = digits[i] - '0';
                if (d < 0 || d > 9) { continue; }
                int bit = 1 << d;
                if ((seen & bit) != 0) { continue; }
                seen |= bit;
                n++;
            }
            return n;
        }

        private static string DigitsOnly(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= '0' && c <= '9') { sb.Append(c); }
                else if (c >= '\u0660' && c <= '\u0669') { sb.Append((char)('0' + (c - '\u0660'))); }
            }
            return sb.ToString();
        }
    }
}
