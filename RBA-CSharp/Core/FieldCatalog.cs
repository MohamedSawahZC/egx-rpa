// =============================================================================
//  FieldCatalog.cs - standard entry fields the operator can click-to-map.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RbaCore
{
    public class FieldCatalogItem
    {
        public string FieldKey;
        public string ArabicName;
        public string SheetColumn;
        public string DatePart;
        public string Hint;
        public UiPickKind Prefer;

        public FieldCatalogItem(string key, string ar, string col, string part, string hint, UiPickKind prefer)
        {
            FieldKey = key;
            ArabicName = ar;
            SheetColumn = col;
            DatePart = part;
            Hint = hint;
            Prefer = prefer;
        }

        public string Display
        {
            get { return ArabicName + "  (" + FieldKey + ")"; }
        }
    }

    public static class FieldCatalog
    {
        // Date boxes are ALWAYS Day → Month → Year (never year-first).
        public const string DobDayKey = "dobDay";
        public const string DobMonthKey = "dobMonth";
        public const string DobYearKey = "dobYear";
        public const string IssueDayKey = "issueDay";
        public const string IssueMonthKey = "issueMonth";
        public const string IssueYearKey = "issueYear";
        public const string ExpiryDayKey = "expiryDay";
        public const string ExpiryMonthKey = "expiryMonth";
        public const string ExpiryYearKey = "expiryYear";

        // Order matches a typical add-customer fill path.
        public static readonly FieldCatalogItem[] All = new FieldCatalogItem[]
        {
            new FieldCatalogItem("brokerCode", "كود الفرع / الوسيط", "BrokerCode", "", "انقر مربع كود الفرع", UiPickKind.Control),
            new FieldCatalogItem("name", "اسم العميل", "Name", "", "انقر خانة الاسم", UiPickKind.Control),
            new FieldCatalogItem(DobDayKey, "يوم الميلاد (١/٣ — أولاً)", "DateOfBirth", "day",
                "تاريخ الميلاد بالترتيب: انقر خانة اليوم أولاً (يوم → شهر → سنة)", UiPickKind.Control),
            new FieldCatalogItem(DobMonthKey, "شهر الميلاد (٢/٣)", "DateOfBirth", "month",
                "تاريخ الميلاد: انقر خانة الشهر ثانياً (بعد اليوم — قبل السنة)", UiPickKind.Control),
            new FieldCatalogItem(DobYearKey, "سنة الميلاد (٣/٣ — أخيراً)", "DateOfBirth", "year",
                "تاريخ الميلاد: انقر خانة السنة أخيراً (يوم → شهر → سنة)", UiPickKind.Control),
            new FieldCatalogItem("address", "العنوان", "Address", "", "انقر خانة العنوان", UiPickKind.Control),
            // Code TextBoxes (NOT dropdowns): type numeric codes from the sheet.
            new FieldCatalogItem("nationality", "كود الجنسية (مثل 21=مصر)", "NationalityCode", "",
                "انقر خانة كود الجنسية — وليس القائمة المنسدلة", UiPickKind.Control),
            new FieldCatalogItem("gender", "كود النوع (مثل 1=ذكر)", "GenderCode", "",
                "انقر خانة كود النوع — وليس القائمة المنسدلة", UiPickKind.Control),
            new FieldCatalogItem("legalForm", "كود الشكل القانوني (مثل 1)", "LegalFormCode", "",
                "انقر خانة كود الشكل القانوني — وليس القائمة", UiPickKind.Control),
            new FieldCatalogItem("subForm", "كود الشكل الفرعي (مثل 10)", "SubFormCode", "",
                "انقر خانة كود الشكل الفرعي — وليس القائمة", UiPickKind.Control),
            new FieldCatalogItem("nationalId", "الرقم القومي", "NationalId", "", "انقر خانة الرقم القومي", UiPickKind.Control),
            new FieldCatalogItem("idType", "كود نوع الهوية (مثل 7)", "IdTypeCode", "",
                "انقر خانة كود نوع الهوية — وليس القائمة المنسدلة", UiPickKind.Control),
            // Issue date: day is locked/auto-filled in Coding — month then year are written.
            new FieldCatalogItem(IssueDayKey, "يوم إصدار الهوية (تلقائي — لا يُملأ)", "IdIssueDate", "day",
                "تاريخ الإصدار: اليوم يُملأ تلقائياً ومقفول — التقاط اختياري (Esc)", UiPickKind.Control),
            new FieldCatalogItem(IssueMonthKey, "شهر إصدار الهوية (٢/٣)", "IdIssueDate", "month",
                "تاريخ الإصدار: انقر خانة الشهر ثانياً", UiPickKind.Control),
            new FieldCatalogItem(IssueYearKey, "سنة إصدار الهوية (٣/٣)", "IdIssueDate", "year",
                "تاريخ الإصدار: انقر خانة السنة أخيراً", UiPickKind.Control),
            new FieldCatalogItem(ExpiryDayKey, "يوم انتهاء الهوية (١/٣)", "IdExpiryDate", "day",
                "تاريخ الانتهاء: انقر خانة اليوم أولاً", UiPickKind.Control),
            new FieldCatalogItem(ExpiryMonthKey, "شهر انتهاء الهوية (٢/٣)", "IdExpiryDate", "month",
                "تاريخ الانتهاء: انقر خانة الشهر ثانياً", UiPickKind.Control),
            new FieldCatalogItem(ExpiryYearKey, "سنة انتهاء الهوية (٣/٣)", "IdExpiryDate", "year",
                "تاريخ الانتهاء: انقر خانة السنة أخيراً", UiPickKind.Control),
            new FieldCatalogItem("governorate", "كود المحافظة (مثل 22)", "GovernorateCode", "",
                "انقر خانة كود المحافظة — وليس القائمة المنسدلة", UiPickKind.Control),
            new FieldCatalogItem("country", "كود الدولة (مثل 21=مصر)", "CountryCode", "",
                "انقر خانة كود الدولة — وليس القائمة المنسدلة", UiPickKind.Control),
            new FieldCatalogItem("mobile", "ت. محمول (موبايل)", "Mobile", "",
                "انقر خانة ت. محمول — رقم موبايل مصري", UiPickKind.Control),
            // Count of images under Sample\\Docs\\{NationalId} only (auto-filled if blank).
            new FieldCatalogItem("docsCount", "عدد المستندات لهذه الهوية", "DocsCount", "",
                "انقر خانة العدد — تُملأ تلقائياً بعدد الصور الموجودة في مجلد الرقم القومي", UiPickKind.Control),
            // Job is filled LAST among KYC fields (after legal form reveals it; after docsCount).
            new FieldCatalogItem("job", "الوظيفة (آخر حقل قبل المستندات)", "Job", "",
                "آخر حقل يُكتب قبل المرفقات — التقطه بعد ظهوره من الشكل القانوني", UiPickKind.Control),
            // Real Coding.exe: CheckBox ملفات once → Add → select FILE in FileListBox → موافق.
            new FieldCatalogItem("chkFiles", "مربع اختيار ملفات فقط (CheckBox)", "", "",
                "انقر مربع الاختيار فقط — ليس النص المكتوب «ملفات» بجانبه", UiPickKind.CheckBox),
            new FieldCatalogItem("docPath", "قائمة الملفات داخل المستند (FileList)", "", "",
                "مهم: انقر قائمة الملفات (FileListBox) وليس قائمة الأقراص Drive — أو اتركه فارغاً للتنقل التلقائي", UiPickKind.Control),
            // Captured explicitly when the automatic class scan picks the wrong list.
            new FieldCatalogItem("docDirList", "قائمة المجلدات داخل المستند (DirList)", "", "",
                "انقر قائمة المجلدات التي يتنقل فيها البرنامج — اتركه فارغاً للاكتشاف التلقائي", UiPickKind.Control),
            new FieldCatalogItem("docDriveList", "قائمة الأقراص داخل المستند (Drive)", "", "",
                "انقر قائمة الأقراص المنسدلة — اتركه فارغاً للاكتشاف التلقائي", UiPickKind.Control),
            new FieldCatalogItem("btnDocAdd1", "زر إضافة ملف ١", "", "",
                "انقر زر إضافة/إرفاق للملف الأول (بعد تفعيل ملفات مرة واحدة)", UiPickKind.Button),
            new FieldCatalogItem("btnDocAdd2", "زر إضافة ملف ٢", "", "",
                "انقر زر إضافة/إرفاق للملف الثاني", UiPickKind.Button),
            new FieldCatalogItem("btnDocAdd3", "زر إضافة ملف ٣", "", "",
                "انقر زر إضافة/إرفاق للملف الثالث", UiPickKind.Button),
            new FieldCatalogItem("btnDocAdd4", "زر إضافة ملف ٤", "", "",
                "انقر زر إضافة/إرفاق للملف الرابع", UiPickKind.Button),
            new FieldCatalogItem("btnDocOk", "تأكيد داخل نافذة المستند", "", "",
                "افتح نافذة المستند ثم انقر زر التأكيد/موافق بعد اختيار الملف من القائمة", UiPickKind.Button),
            new FieldCatalogItem("btnSave", "زر الحفظ (حفظ / حافظ)", "", "",
                "انقر زر حفظ أسفل الشاشة (في التكويد الحقيقي: حفظ)", UiPickKind.Button),
            new FieldCatalogItem("btnNew", "زر جديد (العميل التالي)", "", "",
                "انقر زر «جديد» لتفريغ النموذج قبل العميل التالي — بدون إغلاق الشاشة", UiPickKind.Button),
            // «الإستعلام للتعديل» — duplicate check before every insert (optional: auto-detected).
            new FieldCatalogItem(ClientLookup.SearchBoxKey, "خانة البحث في الإستعلام للتعديل", "", "",
                "اختياري — افتح «الإستعلام للتعديل» وانقر خانة إسم/كود موحد/الرقم القومى", UiPickKind.Control),
            new FieldCatalogItem(ClientLookup.SearchButtonKey, "زر البحث في الإستعلام للتعديل", "", "",
                "اختياري — انقر زر البحث بجوار خانة البحث", UiPickKind.Button),
            new FieldCatalogItem(ClientLookup.GridKey, "جدول نتائج الإستعلام للتعديل", "", "",
                "انقر صفوف الجدول نفسه (MSHFlexGrid) — ليس إطار «البحث» ولا خانة الرقم القومي", UiPickKind.Control),
            new FieldCatalogItem(PullCodes.SearchBoxKey, "خانة البحث في سحب الأكواد", "", "",
                "افتح «سحب الأكواد» وانقر خانة الكود الموحد", UiPickKind.Control),
            new FieldCatalogItem(PullCodes.SearchButtonKey, "زر البحث في سحب الأكواد", "", "",
                "انقر زر البحث في شاشة سحب الأكواد", UiPickKind.Button),
            new FieldCatalogItem(PullCodes.GridKey, "جدول سحب الأكواد", "", "",
                "انقر عمود «سحب» في أول صف (ليس الاسم ولا الكود) ثم يُرسل F2 و F9", UiPickKind.Control),
            // Post-save confirm screen («تأكيد بيانات العملاء قبل إرسالها للتكويد»).
            new FieldCatalogItem(CodingPostSave.ConfirmGridKey, "جدول تأكيد بيانات العملاء", "", "",
                "افتح شاشة التأكيد وانقر وسط الجدول (غالباً Frame مثل الاستعلام) — بعدها F2 ثم F9 تلقائياً", UiPickKind.Control),
            // Period export («عمليات التكويد خلال فترة») — Day → Month → Year for both ends.
            new FieldCatalogItem(CodingPostSave.PeriodFromDayKey, "يوم بداية الفترة (١/٣)", "", "day",
                "شاشة الفترة: انقر خانة يوم البداية أولاً", UiPickKind.Control),
            new FieldCatalogItem(CodingPostSave.PeriodFromMonthKey, "شهر بداية الفترة (٢/٣)", "", "month",
                "شاشة الفترة: انقر خانة شهر البداية ثانياً", UiPickKind.Control),
            new FieldCatalogItem(CodingPostSave.PeriodFromYearKey, "سنة بداية الفترة (٣/٣)", "", "year",
                "شاشة الفترة: انقر خانة سنة البداية أخيراً", UiPickKind.Control),
            new FieldCatalogItem(CodingPostSave.PeriodToDayKey, "يوم نهاية الفترة (١/٣)", "", "day",
                "شاشة الفترة: انقر خانة يوم النهاية أولاً", UiPickKind.Control),
            new FieldCatalogItem(CodingPostSave.PeriodToMonthKey, "شهر نهاية الفترة (٢/٣)", "", "month",
                "شاشة الفترة: انقر خانة شهر النهاية ثانياً", UiPickKind.Control),
            new FieldCatalogItem(CodingPostSave.PeriodToYearKey, "سنة نهاية الفترة (٣/٣)", "", "year",
                "شاشة الفترة: انقر خانة سنة النهاية أخيراً", UiPickKind.Control),
            new FieldCatalogItem(CodingPostSave.PeriodExportBtnKey, "زر Export (تصدير XML)", "", "",
                "انقر زر Export الوحيد في شاشة الفترة — يصدّر Clients*.xml وليس إكسل", UiPickKind.Button),
            new FieldCatalogItem(CodingPostSave.PeriodExcelPathKey, "مسار حفظ ملف XML", "", "",
                "انقر خانة مسار المجلد الذي يُحفظ فيه Clients*.xml", UiPickKind.Control),
        };

        public static readonly string[] DocAddKeys = new string[]
        {
            "btnDocAdd1", "btnDocAdd2", "btnDocAdd3", "btnDocAdd4"
        };

        public const string ChkFilesKey = "chkFiles";
        public const string DocPathKey = "docPath";
        public const string DocDirListKey = "docDirList";
        public const string DocDriveListKey = "docDriveList";
        public const string DocsCountKey = "docsCount";
        public const string BtnDocOkKey = "btnDocOk";

        public static readonly string[] CodeInputKeys = new string[]
        {
            "nationality", "gender", "legalForm", "subForm", "idType", "governorate", "country"
        };

        public static bool IsCodeInputKey(string fieldKey)
        {
            if (string.IsNullOrEmpty(fieldKey)) { return false; }
            for (int i = 0; i < CodeInputKeys.Length; i++)
            {
                if (string.Equals(CodeInputKeys[i], fieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // These are always filled via TextBox codes — never ComboBox dropdowns.
        public static void EnforceCodeInput(FieldSelector s)
        {
            if (s == null || !IsCodeInputKey(s.FieldKey)) { return; }
            s.Kind = "TextBox";
            s.Caption = "";
            s.DatePart = "";
            FieldCatalogItem item = Find(s.FieldKey);
            if (item != null)
            {
                s.SheetColumn = item.SheetColumn;
                if (string.IsNullOrEmpty(s.FieldLabel)) { s.FieldLabel = item.ArabicName; }
            }
        }

        public static bool IsDocAddKey(string fieldKey)
        {
            if (string.IsNullOrEmpty(fieldKey)) { return false; }
            for (int i = 0; i < DocAddKeys.Length; i++)
            {
                if (string.Equals(DocAddKeys[i], fieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public static void EnforceDocButton(FieldSelector s)
        {
            if (s == null || string.IsNullOrEmpty(s.FieldKey)) { return; }
            if (string.Equals(s.FieldKey, ChkFilesKey, StringComparison.OrdinalIgnoreCase))
            {
                s.Kind = "CheckBox";
                s.SheetColumn = "";
                s.DatePart = "";
                if (string.IsNullOrEmpty(s.Section)) { s.Section = "مستندات"; }
                if (string.IsNullOrEmpty(s.FieldLabel)) { s.FieldLabel = "مربع ملفات"; }
                if (string.IsNullOrEmpty(s.Caption)) { s.Caption = "ملفات"; }
                return;
            }
            if (string.Equals(s.FieldKey, DocPathKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "";
                s.DatePart = "";
                // DriveListBox is NOT a file path — clear so VB6 Drive/Dir/File auto-nav is used.
                string cls = s.Class != null ? s.Class : "";
                if (Regex.IsMatch(cls, "(?i)DriveListBox") || Regex.IsMatch(cls, "(?i)DirListBox"))
                {
                    s.Class = "";
                    s.ClassOrdinal = "";
                    s.Index = "";
                    s.ControlId = "";
                    s.Kind = "ListBox";
                    if (string.IsNullOrEmpty(s.Section)) { s.Section = "المستند"; }
                    if (string.IsNullOrEmpty(s.FieldLabel)) { s.FieldLabel = "قائمة الملفات"; }
                    return;
                }
                if (Regex.IsMatch(cls, "(?i)FileListBox"))
                {
                    s.Kind = "ListBox";
                }
                else if (string.IsNullOrEmpty(s.Kind) ||
                    string.Equals(s.Kind, "Other", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Kind, "Button", StringComparison.OrdinalIgnoreCase))
                {
                    s.Kind = "ComboBox";
                }
                if (string.IsNullOrEmpty(s.Section)) { s.Section = "المستند"; }
                if (string.IsNullOrEmpty(s.FieldLabel)) { s.FieldLabel = "قائمة الملفات"; }
                return;
            }
            if (string.Equals(s.FieldKey, DocsCountKey, StringComparison.OrdinalIgnoreCase))
            {
                s.Kind = "TextBox";
                if (string.IsNullOrEmpty(s.SheetColumn)) { s.SheetColumn = "DocsCount"; }
                s.DatePart = "";
                if (string.IsNullOrEmpty(s.FieldLabel)) { s.FieldLabel = "عدد المستندات"; }
                return;
            }
            if (string.Equals(s.FieldKey, BtnDocOkKey, StringComparison.OrdinalIgnoreCase))
            {
                s.Kind = "Button";
                s.SheetColumn = "";
                s.DatePart = "";
                if (string.IsNullOrEmpty(s.Section)) { s.Section = "المستند"; }
                if (string.IsNullOrEmpty(s.FieldLabel)) { s.FieldLabel = "تأكيد داخل المستند"; }
                return;
            }
            if (!IsDocAddKey(s.FieldKey)) { return; }
            s.Kind = "Button";
            s.SheetColumn = "";
            s.DatePart = "";
            // Reject accidental form/MDI captures (caused path paste into الوظيفة).
            string addCls = s.Class != null ? s.Class : "";
            if (addCls.IndexOf("MDIForm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                addCls.IndexOf("FormDC", StringComparison.OrdinalIgnoreCase) >= 0 ||
                addCls.IndexOf("MDIClient", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                s.Class = "";
                s.ClassOrdinal = "";
                s.Index = "";
                s.ControlId = "";
            }
            if (string.IsNullOrEmpty(s.Section)) { s.Section = "مستندات"; }
            if (string.IsNullOrEmpty(s.FieldLabel))
            {
                FieldCatalogItem item = Find(s.FieldKey);
                if (item != null) { s.FieldLabel = item.ArabicName; }
            }
        }

        public static FieldCatalogItem Find(string fieldKey)
        {
            if (string.IsNullOrEmpty(fieldKey)) { return null; }
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    return All[i];
                }
            }
            return null;
        }

        // Lock Day/Month/Year parts so a mis-click never swaps date semantics.
        public static void EnforceDobParts(FieldSelector s)
        {
            EnforceDateParts(s);
        }

        public static void EnforceDateParts(FieldSelector s)
        {
            if (s == null || string.IsNullOrEmpty(s.FieldKey)) { return; }
            string k = s.FieldKey;
            if (string.Equals(k, DobDayKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "DateOfBirth"; s.DatePart = "day";
            }
            else if (string.Equals(k, DobMonthKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "DateOfBirth"; s.DatePart = "month";
            }
            else if (string.Equals(k, DobYearKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "DateOfBirth"; s.DatePart = "year";
            }
            else if (string.Equals(k, IssueDayKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "IdIssueDate"; s.DatePart = "day";
            }
            else if (string.Equals(k, IssueMonthKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "IdIssueDate"; s.DatePart = "month";
            }
            else if (string.Equals(k, IssueYearKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "IdIssueDate"; s.DatePart = "year";
            }
            else if (string.Equals(k, ExpiryDayKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "IdExpiryDate"; s.DatePart = "day";
            }
            else if (string.Equals(k, ExpiryMonthKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "IdExpiryDate"; s.DatePart = "month";
            }
            else if (string.Equals(k, ExpiryYearKey, StringComparison.OrdinalIgnoreCase))
            {
                s.SheetColumn = "IdExpiryDate"; s.DatePart = "year";
            }
        }

        public static void EnforceDobPartsAll(List<FieldSelector> list)
        {
            if (list == null) { return; }
            for (int i = 0; i < list.Count; i++) { EnforceDateParts(list[i]); }
        }

        // Fill order within a date group: Day=1, Month=2, Year=3.
        // Group bands: DOB 1–3, Issue 11–13, Expiry 21–23; other keys = 0.
        public static int DobFillRank(string fieldKey)
        {
            return DateFillRank(fieldKey);
        }

        public static int DateFillRank(string fieldKey)
        {
            if (string.Equals(fieldKey, DobDayKey, StringComparison.OrdinalIgnoreCase)) { return 1; }
            if (string.Equals(fieldKey, DobMonthKey, StringComparison.OrdinalIgnoreCase)) { return 2; }
            if (string.Equals(fieldKey, DobYearKey, StringComparison.OrdinalIgnoreCase)) { return 3; }
            if (string.Equals(fieldKey, IssueDayKey, StringComparison.OrdinalIgnoreCase)) { return 11; }
            if (string.Equals(fieldKey, IssueMonthKey, StringComparison.OrdinalIgnoreCase)) { return 12; }
            if (string.Equals(fieldKey, IssueYearKey, StringComparison.OrdinalIgnoreCase)) { return 13; }
            if (string.Equals(fieldKey, ExpiryDayKey, StringComparison.OrdinalIgnoreCase)) { return 21; }
            if (string.Equals(fieldKey, ExpiryMonthKey, StringComparison.OrdinalIgnoreCase)) { return 22; }
            if (string.Equals(fieldKey, ExpiryYearKey, StringComparison.OrdinalIgnoreCase)) { return 23; }
            return 0;
        }

        public static int DateGroupId(string fieldKey)
        {
            int r = DateFillRank(fieldKey);
            if (r >= 1 && r <= 3) { return 1; }
            if (r >= 11 && r <= 13) { return 2; }
            if (r >= 21 && r <= 23) { return 3; }
            return 0;
        }

        public static bool HasClickMappedFields(List<FieldSelector> list)
        {
            if (list == null || list.Count == 0) { return false; }
            int mapped = 0;
            for (int i = 0; i < list.Count; i++)
            {
                FieldSelector s = list[i];
                if (s == null || string.IsNullOrEmpty(s.FieldKey)) { continue; }
                if (string.Equals(s.SourceKey, "click-map", StringComparison.OrdinalIgnoreCase))
                {
                    mapped++;
                    continue;
                }
                if (!string.IsNullOrEmpty(s.ConfirmedAt) &&
                    !string.IsNullOrEmpty(s.Class) &&
                    !string.IsNullOrEmpty(s.ClassOrdinal))
                {
                    mapped++;
                }
            }
            return mapped >= 2;
        }

        // Keep only catalog keys, catalog order, strip polluted Captions on inputs.
        public static List<FieldSelector> OrderAndClean(List<FieldSelector> list)
        {
            List<FieldSelector> result = new List<FieldSelector>();
            Dictionary<string, FieldSelector> byKey = new Dictionary<string, FieldSelector>(StringComparer.OrdinalIgnoreCase);
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    FieldSelector s = list[i];
                    if (s == null || string.IsNullOrEmpty(s.FieldKey)) { continue; }
                    // Drop legacy noisy keys.
                    if (string.Equals(s.FieldKey, "btnCancel", StringComparison.OrdinalIgnoreCase)) { continue; }
                    // Legacy second button — same Export control that writes XML.
                    if (string.Equals(s.FieldKey, CodingPostSave.PeriodExcelBtnKey, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!byKey.ContainsKey(CodingPostSave.PeriodExportBtnKey))
                        {
                            s.FieldKey = CodingPostSave.PeriodExportBtnKey;
                            byKey[s.FieldKey] = s;
                        }
                        continue;
                    }
                    if (string.Equals(s.FieldKey, "navNewClient", StringComparison.OrdinalIgnoreCase))
                    {
                        byKey[s.FieldKey] = s;
                        continue;
                    }
                    byKey[s.FieldKey] = s;
                }
            }

            // Preserve nav button first if present.
            if (byKey.ContainsKey("navNewClient"))
            {
                result.Add(SanitizeSelector(byKey["navNewClient"]));
            }

            for (int i = 0; i < All.Length; i++)
            {
                FieldCatalogItem item = All[i];
                FieldSelector s;
                if (byKey.ContainsKey(item.FieldKey))
                {
                    s = byKey[item.FieldKey];
                }
                else
                {
                    s = new FieldSelector();
                    s.FieldKey = item.FieldKey;
                    s.FieldLabel = item.ArabicName;
                    s.Label = item.ArabicName;
                    s.SheetColumn = item.SheetColumn;
                    s.DatePart = item.DatePart;
                    s.Kind = item.Prefer == UiPickKind.Button ? "Button" : "";
                }
                // Force catalog sheet/date metadata.
                if (!string.IsNullOrEmpty(item.SheetColumn)) { s.SheetColumn = item.SheetColumn; }
                if (!string.IsNullOrEmpty(item.DatePart)) { s.DatePart = item.DatePart; }
                if (string.IsNullOrEmpty(s.FieldLabel)) { s.FieldLabel = item.ArabicName; }
                EnforceDobParts(s);
                EnforceDocButton(s);
                EnforceCodeInput(s);
                result.Add(SanitizeSelector(s));
            }
            return result;
        }

        public static FieldSelector SanitizeSelector(FieldSelector s)
        {
            if (s == null) { return s; }
            bool isButton = string.Equals(s.Kind, "Button", StringComparison.OrdinalIgnoreCase);
            if (!isButton)
            {
                // Wipe polluted captions/labels that are really typed data.
                if (LooksLikeTypedData(s.Caption)) { s.Caption = ""; }
                if (LooksLikeTypedData(s.Label) && !string.Equals(s.Label, s.FieldLabel, StringComparison.Ordinal))
                {
                    s.Label = s.FieldLabel;
                }
            }
            // Operator exports often leave Label empty — use FieldLabel so post-legalForm
            // label resolve can find الشكل الفرعي / الوظيفة / etc.
            if (string.IsNullOrWhiteSpace(s.Label) && !string.IsNullOrWhiteSpace(s.FieldLabel))
            {
                s.Label = ShortLabelFromFieldLabel(s.FieldKey, s.FieldLabel);
            }
            return s;
        }

        // Prefer the real on-screen Arabic caption fragment over long catalog hints.
        private static string ShortLabelFromFieldLabel(string fieldKey, string fieldLabel)
        {
            string[] tips = null;
            string k = fieldKey != null ? fieldKey.ToLowerInvariant() : "";
            if (k == "subform") { tips = new string[] { "الشكل الفرعي", "فرعي" }; }
            else if (k == "legalform") { tips = new string[] { "الشكل القانوني", "قانوني" }; }
            else if (k == "job") { tips = new string[] { "الوظيفة", "وظيف" }; }
            else if (k == "nationalid") { tips = new string[] { "الرقم القومي", "قومي" }; }
            else if (k == "mobile") { tips = new string[] { "محمول" }; }
            else if (k == "brokercode" || k == "broker") { tips = new string[] { "كود الفرع", "الفرع" }; }
            else if (k == "name") { tips = new string[] { "الاسم", "الإسم" }; }
            else if (k == "address") { tips = new string[] { "العنوان" }; }
            else if (k == "nationality") { tips = new string[] { "الجنسية" }; }
            else if (k == "gender") { tips = new string[] { "النوع" }; }
            else if (k == "idtype") { tips = new string[] { "نوع الهوية" }; }
            else if (k == "governorate") { tips = new string[] { "المحافظة" }; }
            else if (k == "country") { tips = new string[] { "البلد" }; }
            else if (k == "docscount") { tips = new string[] { "عدد المستندات" }; }
            if (tips != null && tips.Length > 0) { return tips[0]; }
            return fieldLabel ?? "";
        }

        private static bool LooksLikeTypedData(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) { return false; }
            string t = text.Trim();
            // National IDs / long numbers / addresses with digits.
            int digits = 0;
            for (int i = 0; i < t.Length; i++)
            {
                if (char.IsDigit(t[i])) { digits++; }
            }
            if (digits >= 6) { return true; }
            if (t.Length > 40) { return true; }
            return false;
        }
    }
}
