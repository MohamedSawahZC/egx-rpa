using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RbaCore
{
    // Egyptian National ID helpers (generate / validate / parse).
    // Ported from RBA\_shared\EgyptId.ps1 - behaviour preserved.
    //
    // NOTE: ChecksumOk is reported SEPARATELY from IsValid on purpose. Some
    // genuinely-issued older IDs do not satisfy the modern check-digit rule,
    // so a checksum failure is a WARNING to review, not a hard reject.
    public class EgyptId
    {
        // Governorate codes as encoded in digits 8-9 of the national ID.
        public static readonly Dictionary<string, string> EgGovernorates =
            BuildGovernorates();

        // Weights applied to the first 13 digits to derive the 14th (check) digit.
        public static readonly int[] EgIdWeights =
            new int[] { 2, 7, 6, 5, 4, 3, 2, 7, 6, 5, 4, 3, 2 };

        private static readonly Random _rng = new Random();

        private static Dictionary<string, string> BuildGovernorates()
        {
            Dictionary<string, string> g = new Dictionary<string, string>();
            g["01"] = "القاهرة";
            g["02"] = "الإسكندرية";
            g["03"] = "بورسعيد";
            g["04"] = "السويس";
            g["11"] = "دمياط";
            g["12"] = "الدقهلية";
            g["13"] = "الشرقية";
            g["14"] = "القليوبية";
            g["15"] = "كفر الشيخ";
            g["16"] = "الغربية";
            g["17"] = "المنوفية";
            g["18"] = "البحيرة";
            g["19"] = "الإسماعيلية";
            g["21"] = "الجيزة";
            g["22"] = "بني سويف";
            g["23"] = "الفيوم";
            g["24"] = "المنيا";
            g["25"] = "أسيوط";
            g["26"] = "سوهاج";
            g["27"] = "قنا";
            g["28"] = "أسوان";
            g["29"] = "الأقصر";
            g["31"] = "البحر الأحمر";
            g["32"] = "الوادي الجديد";
            g["33"] = "مطروح";
            g["34"] = "شمال سيناء";
            g["35"] = "جنوب سيناء";
            g["88"] = "خارج الجمهورية";
            return g;
        }

        // Result of Test-EgyptianNationalId. Mirrors the PSCustomObject fields.
        public class IdInfo
        {
            public string Id;
            public bool IsValid;
            public bool ChecksumOk;
            public DateTime? DateOfBirth;
            public string Gender;
            public string GenderCode;
            public string Governorate;
            public string GovCode;
            public List<string> Errors;

            public IdInfo()
            {
                this.Errors = new List<string>();
            }
        }

        // Returns the check digit for a 13-digit national ID stem,
        // or -1 if the stem produces an unusable remainder.
        public static int GetEgIdCheckDigit(string stem13)
        {
            if (stem13 == null || stem13.Length != 13) { return -1; }
            int sum = 0;
            for (int i = 0; i < 13; i++)
            {
                sum += (stem13[i] - '0') * EgIdWeights[i];
            }
            int remainder = sum % 11;
            if (remainder == 0) { return 0; }
            int check = 11 - remainder;
            if (check >= 10) { return -1; }   // caller should re-roll the sequence
            return check;
        }

        // Structural + checksum validation. Never throws.
        public static IdInfo TestEgyptianNationalId(string id)
        {
            List<string> errors = new List<string>();
            IdInfo result = new IdInfo();
            result.Id = id;
            result.IsValid = false;
            result.ChecksumOk = false;

            if (string.IsNullOrEmpty(id) || id.Trim().Length == 0)
            {
                result.Errors = new List<string>();
                result.Errors.Add("رقم تحقيق الشخصية فارغ");
                return result;
            }

            id = id.Trim();
            result.Id = id;
            if (!Regex.IsMatch(id, @"^\d{14}$"))
            {
                errors.Add("الرقم القومي يجب أن يكون 14 رقماً (الطول الحالي: " +
                    id.Length.ToString(CultureInfo.InvariantCulture) + ")");
                result.Errors = errors;
                return result;
            }

            // --- century digit -> birth century
            int centuryDigit = id[0] - '0';
            int baseYear;
            if (centuryDigit == 2) { baseYear = 1900; }
            else if (centuryDigit == 3) { baseYear = 2000; }
            else { baseYear = -1; }
            if (baseYear < 0)
            {
                errors.Add("خانة القرن غير صحيحة (" +
                    centuryDigit.ToString(CultureInfo.InvariantCulture) +
                    ") - المتوقع 2 أو 3");
            }

            // --- date of birth
            int yy = int.Parse(id.Substring(1, 2), CultureInfo.InvariantCulture);
            int mm = int.Parse(id.Substring(3, 2), CultureInfo.InvariantCulture);
            int dd = int.Parse(id.Substring(5, 2), CultureInfo.InvariantCulture);
            if (baseYear > 0)
            {
                int year = baseYear + yy;
                try
                {
                    result.DateOfBirth = new DateTime(year, mm, dd, 0, 0, 0);
                }
                catch
                {
                    errors.Add("تاريخ الميلاد المستخرج غير صحيح (" +
                        dd.ToString(CultureInfo.InvariantCulture) + "/" +
                        mm.ToString(CultureInfo.InvariantCulture) + "/" +
                        year.ToString(CultureInfo.InvariantCulture) + ")");
                }
            }

            // --- governorate
            string gov = id.Substring(7, 2);
            result.GovCode = gov;
            if (EgGovernorates.ContainsKey(gov))
            {
                result.Governorate = EgGovernorates[gov];
            }
            else
            {
                errors.Add("كود المحافظة غير معروف (" + gov + ")");
            }

            // --- gender: 13th digit odd = male, even = female
            int genderDigit = id[12] - '0';
            if (genderDigit % 2 == 1)
            {
                result.Gender = "ذكر";
                result.GenderCode = "1";
            }
            else
            {
                result.Gender = "أنثى";
                result.GenderCode = "2";
            }

            // --- checksum
            int expected = GetEgIdCheckDigit(id.Substring(0, 13));
            int actual = id[13] - '0';
            if (expected >= 0 && expected == actual)
            {
                result.ChecksumOk = true;
            }
            else
            {
                errors.Add("خانة التحقق لا تطابق الحساب (المتوقع: " +
                    expected.ToString(CultureInfo.InvariantCulture) + " / الفعلي: " +
                    actual.ToString(CultureInfo.InvariantCulture) + ")");
            }

            result.Errors = errors;
            // IsValid = structurally sound. Checksum handled as a separate warning.
            int nonChecksumErrors = 0;
            for (int i = 0; i < errors.Count; i++)
            {
                if (!errors[i].StartsWith("خانة التحقق")) { nonChecksumErrors++; }
            }
            result.IsValid = (nonChecksumErrors == 0);
            return result;
        }

        // Generates a SYNTHETIC but structurally valid Egyptian national ID.
        // For test data only - these are not issued to any real person.
        public static string NewEgyptianNationalId(DateTime dateOfBirth, string gender, string govCode)
        {
            if (gender != "M" && gender != "F")
            {
                throw new ArgumentException("Gender must be 'M' or 'F'.", "gender");
            }

            if (string.IsNullOrEmpty(govCode) || govCode.Trim().Length == 0)
            {
                List<string> keys = new List<string>();
                foreach (string k in EgGovernorates.Keys)
                {
                    if (k != "88") { keys.Add(k); }
                }
                govCode = keys[_rng.Next(0, keys.Count)];
            }

            string century;
            if (dateOfBirth.Year < 2000) { century = "2"; } else { century = "3"; }
            string datePart = dateOfBirth.ToString("yyMMdd", CultureInfo.InvariantCulture);

            for (int attempt = 0; attempt < 200; attempt++)
            {
                // digits 10-12 free, digit 13 carries gender parity
                string seq3 = _rng.Next(0, 1000).ToString("000", CultureInfo.InvariantCulture);
                int genderDigit;
                if (gender == "M")
                {
                    int[] odd = new int[] { 1, 3, 5, 7, 9 };
                    genderDigit = odd[_rng.Next(0, odd.Length)];
                }
                else
                {
                    int[] even = new int[] { 0, 2, 4, 6, 8 };
                    genderDigit = even[_rng.Next(0, even.Length)];
                }
                string stem = century + datePart + govCode + seq3 +
                    genderDigit.ToString(CultureInfo.InvariantCulture);
                int check = GetEgIdCheckDigit(stem);
                if (check >= 0)
                {
                    return stem + check.ToString(CultureInfo.InvariantCulture);
                }
            }
            throw new Exception("Could not generate a valid national ID after 200 attempts.");
        }

        // Convenience overload: random governorate.
        public static string NewEgyptianNationalId(DateTime dateOfBirth, string gender)
        {
            return NewEgyptianNationalId(dateOfBirth, gender, null);
        }

        // Takes a valid ID and corrupts ONLY the check digit - for testing the gate.
        public static string GetEgIdWithBadChecksum(string id)
        {
            int good = id[13] - '0';
            int bad = (good + 1) % 10;
            return id.Substring(0, 13) + bad.ToString(CultureInfo.InvariantCulture);
        }
    }
}
