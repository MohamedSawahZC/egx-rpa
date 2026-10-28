// =============================================================================
//  ClientEntry.cs - fill one client row into the Coding entry screen.
//  Ported from RBA/_shared/ClientEntry.ps1. Never presses ترحيل/submit.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;

namespace RbaCore
{
    public class FieldFillResult
    {
        public string FieldKey;
        public string Value;
        public string Kind;
        public string Tier;
        public bool Ok;
        public string Note;
    }

    public class ClientEntryResult
    {
        public bool Ok;
        public List<FieldFillResult> Fields;
        // Save button was clicked AND Coding acknowledged it with a prompt.
        public bool Saved;
        // Save was clicked but Coding stayed silent — outcome unknown, never report success.
        public bool SaveUnverified;
        // Coding rejected the save with «العميل موجود من قبل» — not an error, a duplicate.
        public bool AlreadyExists;
        public string Note;
    }

    public static class ClientEntry
    {
        private static readonly Regex NonEntryKey = new Regex(
            "^(btn|grid|nav|export|lookup|chkFiles|docPath)", RegexOptions.IgnoreCase);

        public static string GetEntryValue(OrderedDictionary row, FieldSelector selector)
        {
            if (row == null || selector == null) { return ""; }
            string col = selector.SheetColumn;
            if (string.IsNullOrWhiteSpace(col)) { return ""; }
            if (!row.Contains(col))
            {
                // case-insensitive key lookup
                object found = null;
                foreach (string key in row.Keys)
                {
                    if (string.Equals(key, col, StringComparison.OrdinalIgnoreCase)) { found = row[key]; break; }
                }
                if (found == null) { return ""; }
                return GetDatePart(found.ToString(), selector.DatePart);
            }
            object rawObj = row[col];
            string raw = rawObj != null ? rawObj.ToString() : "";
            if (string.IsNullOrWhiteSpace(raw)) { return ""; }
            return GetDatePart(raw.Trim(), selector.DatePart);
        }

        // Keep Day → Month → Year fill sequence for DOB / Issue / Expiry.
        private static List<FieldSelector> OrderSelectorsForFill(List<FieldSelector> selectors)
        {
            List<FieldSelector> result = new List<FieldSelector>();
            if (selectors == null) { return result; }

            List<FieldSelector> dob = new List<FieldSelector>();
            List<FieldSelector> issue = new List<FieldSelector>();
            List<FieldSelector> expiry = new List<FieldSelector>();
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                string key = s != null ? s.FieldKey : "";
                int group = FieldCatalog.DateGroupId(key);
                if (group == 1) { dob.Add(s); }
                else if (group == 2) { issue.Add(s); }
                else if (group == 3) { expiry.Add(s); }
                else { result.Add(s); }
            }
            Comparison<FieldSelector> byRank = delegate(FieldSelector a, FieldSelector b)
            {
                return FieldCatalog.DateFillRank(a.FieldKey).CompareTo(FieldCatalog.DateFillRank(b.FieldKey));
            };
            dob.Sort(byRank);
            issue.Sort(byRank);
            expiry.Sort(byRank);

            InsertDateBlock(result, dob, "name", "brokerCode");
            InsertDateBlock(result, issue, "idType", "nationalId");
            InsertDateBlock(result, expiry, IssueYearKeyOrLastIssue(issue), "idType");
            // Job (الوظيفة) = LAST KYC field typed before documents attach.
            MoveJobToLastKyc(result);
            return result;
        }

        // Place «الوظيفة» after every other fillable KYC field (docsCount included).
        private static void MoveJobToLastKyc(List<FieldSelector> list)
        {
            if (list == null || list.Count == 0) { return; }
            int from = IndexAfterKey(list, "job");
            if (from < 0) { return; }
            FieldSelector item = list[from];
            list.RemoveAt(from);
            int insertAt = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                string k = list[i] != null && list[i].FieldKey != null ? list[i].FieldKey : "";
                if (NonEntryKey.IsMatch(k))
                {
                    insertAt = i;
                    break;
                }
            }
            list.Insert(insertAt, item);
        }

        // Move fieldKey to sit immediately after afterKey (no-op if either missing).
        private static void MoveKeyAfter(List<FieldSelector> list, string fieldKey, string afterKey)
        {
            if (list == null || list.Count == 0) { return; }
            int from = IndexAfterKey(list, fieldKey);
            int after = IndexAfterKey(list, afterKey);
            if (from < 0 || after < 0) { return; }
            // IndexAfterKey returns index of the key itself.
            if (from == after + 1) { return; }
            FieldSelector item = list[from];
            list.RemoveAt(from);
            if (from < after) { after--; }
            list.Insert(after + 1, item);
        }

        private static string IssueYearKeyOrLastIssue(List<FieldSelector> issue)
        {
            if (issue != null && issue.Count > 0)
            {
                return issue[issue.Count - 1].FieldKey;
            }
            return FieldCatalog.IssueYearKey;
        }

        private static void InsertDateBlock(
            List<FieldSelector> result,
            List<FieldSelector> block,
            string afterKeyPreferred,
            string afterKeyFallback)
        {
            if (block == null || block.Count == 0) { return; }
            int insertAt = IndexAfterKey(result, afterKeyPreferred);
            if (insertAt < 0) { insertAt = IndexAfterKey(result, afterKeyFallback); }
            if (insertAt < 0) { insertAt = result.Count - 1; }
            else { /* keep insertAt as index of anchor */ }
            int at = insertAt < 0 ? 0 : insertAt + 1;
            for (int i = 0; i < block.Count; i++)
            {
                result.Insert(at + i, block[i]);
            }
        }

        private static int IndexAfterKey(List<FieldSelector> list, string key)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null &&
                    string.Equals(list[i].FieldKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private static string GetDatePart(string raw, string part)
        {
            if (string.IsNullOrEmpty(part)) { return raw; }
            DateTime d;
            string[] fmts = new string[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
            bool parsed = false;
            d = DateTime.MinValue;
            for (int i = 0; i < fmts.Length; i++)
            {
                if (DateTime.TryParseExact(raw, fmts[i], CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                {
                    parsed = true;
                    break;
                }
            }
            if (!parsed) { return ""; }
            if (string.Equals(part, "day", StringComparison.OrdinalIgnoreCase)) { return d.Day.ToString(); }
            if (string.Equals(part, "month", StringComparison.OrdinalIgnoreCase)) { return d.Month.ToString(); }
            if (string.Equals(part, "year", StringComparison.OrdinalIgnoreCase)) { return d.Year.ToString(); }
            return raw;
        }

        public static IntPtr FindEntryForm(IntPtr appWindow, string entryTitle, string entryClass)
        {
            return FindEntryForm(appWindow, entryTitle, entryClass, false);
        }

        public static IntPtr FindEntryForm(
            IntPtr appWindow, string entryTitle, string entryClass, bool includeHidden)
        {
            List<W32WindowInfo> forms = Win32Driver.GetFormWindows(appWindow, 4, includeHidden);
            IntPtr titled = IntPtr.Zero;
            IntPtr classOnly = IntPtr.Zero;
            for (int i = 0; i < forms.Count; i++)
            {
                if (!includeHidden && !forms[i].Visible) { continue; }
                IntPtr h = forms[i].Handle;
                if (!IsClientEntryForm(h, entryTitle)) { continue; }
                string cls = Win32Driver.GetClassName(h);
                if (cls.IndexOf("MDIForm", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                if (titled == IntPtr.Zero) { titled = h; }
                if (classOnly == IntPtr.Zero &&
                    !string.IsNullOrEmpty(entryClass) && cls == entryClass &&
                    Win32Driver.GetChildren(h).Count > 0)
                {
                    classOnly = h;
                }
            }
            if (titled != IntPtr.Zero) { return titled; }
            return classOnly;
        }

        // True only for «بيانات العملاء» — never الإستعلام / الفترة / التأكيد / المستند.
        public static bool IsClientEntryForm(IntPtr hwnd, string entryTitle)
        {
            if (hwnd == IntPtr.Zero || !Win32Interop.IsWindow(hwnd)) { return false; }
            string t = ((Win32Driver.GetWindowTitle(hwnd) ?? "") + " " +
                (Win32Driver.GetWindowText(hwnd) ?? "")).Trim();
            if (IsForeignCodingScreen(t)) { return false; }
            if (!string.IsNullOrWhiteSpace(entryTitle) &&
                (FieldDiscovery.TitleMatch(t, entryTitle) ||
                 ArabicText.Contains(t, "بيانات العملاء")))
            {
                return true;
            }
            return ArabicText.Contains(t, "بيانات العملاء");
        }

        public static bool IsForeignCodingScreen(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) { return false; }
            if (ArabicText.Contains(title, "استعلام") || ArabicText.Contains(title, "الإستعلام"))
            {
                return true;
            }
            if (ArabicText.Contains(title, "خلال فترة") || ArabicText.Contains(title, "عمليات التكويد"))
            {
                return true;
            }
            if (ArabicText.Contains(title, "تأكيد بيانات") || ArabicText.Contains(title, "قبل إرسالها"))
            {
                return true;
            }
            string n = title.Trim();
            if (n == "المستند" || n.IndexOf("المستند", StringComparison.Ordinal) == 0 && n.Length <= 12)
            {
                return true;
            }
            return false;
        }

        // After lookup, the entry MDI child is often still alive but covered/hidden.
        // Restore it instead of hiding it and re-clicking the menu (that hangs client 2).
        public static IntPtr RestoreEntryForm(
            IntPtr appWindow, string entryTitle, string entryClass, Action<string, string> log)
        {
            return RestoreEntryForm(appWindow, entryTitle, entryClass, IntPtr.Zero, log);
        }

        public static IntPtr RestoreEntryForm(
            IntPtr appWindow,
            string entryTitle,
            string entryClass,
            IntPtr knownEntry,
            Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }

            if (knownEntry != IntPtr.Zero && Win32Interop.IsWindow(knownEntry) &&
                IsClientEntryForm(knownEntry, entryTitle))
            {
                if (!Win32Interop.IsWindowVisible(knownEntry))
                {
                    try { Win32Interop.ShowWindow(knownEntry, Win32Interop.SW_SHOW); } catch { }
                    try { Win32Interop.ShowWindow(knownEntry, Win32Interop.SW_RESTORE); } catch { }
                    Thread.Sleep(200);
                }
                try { Win32Driver.SetForeground(knownEntry); } catch { }
                if (EntryLooksReady(knownEntry)) { return knownEntry; }
            }

            IntPtr h = FindEntryForm(appWindow, entryTitle, entryClass, false);
            if (h != IntPtr.Zero && EntryLooksReady(h) && IsClientEntryForm(h, entryTitle))
            {
                return h;
            }

            h = FindEntryForm(appWindow, entryTitle, entryClass, true);
            if (h == IntPtr.Zero || !Win32Interop.IsWindow(h) || !IsClientEntryForm(h, entryTitle))
            {
                return IntPtr.Zero;
            }

            try { Win32Interop.ShowWindow(h, Win32Interop.SW_SHOW); } catch { }
            try { Win32Interop.ShowWindow(h, Win32Interop.SW_RESTORE); } catch { }
            try { Win32Driver.SetForeground(h); } catch { }
            Thread.Sleep(250);
            if (EntryLooksReady(h) && IsClientEntryForm(h, entryTitle))
            {
                log("entry restored after lookup «" + Win32Driver.GetWindowText(h) + "»", "OK");
                return h;
            }
            return IntPtr.Zero;
        }

        public static IntPtr GetEntryScanRoot(
            IntPtr appWindow,
            string entryTitle,
            string entryClass,
            string childPattern,
            string shortcut,
            FieldSelector navSelector,
            int settleMs)
        {
            return GetEntryScanRoot(appWindow, entryTitle, entryClass, childPattern, shortcut,
                navSelector, null, false, settleMs);
        }

        // preferMenuNavigate: always open via menu path (2 clicks) even if a form
        // is already visible — production-critical when the title is unknown until opened.
        public static IntPtr GetEntryScanRoot(
            IntPtr appWindow,
            string entryTitle,
            string entryClass,
            string childPattern,
            string shortcut,
            FieldSelector navSelector,
            string[] menuPath,
            bool preferMenuNavigate,
            int settleMs)
        {
            if (!Win32Interop.IsWindow(appWindow)) { return IntPtr.Zero; }
            if (string.IsNullOrWhiteSpace(entryClass)) { entryClass = "ThunderRT6FormDC"; }

            IntPtr h;
            if (!preferMenuNavigate)
            {
                h = FindEntryForm(appWindow, entryTitle, entryClass);
                if (h != IntPtr.Zero && EntryLooksReady(h)) { return h; }
            }
            else
            {
                // Close/hide any stale entry so the menu path is actually exercised.
                h = FindEntryForm(appWindow, entryTitle, entryClass);
                if (h != IntPtr.Zero)
                {
                    try { Win32Interop.ShowWindow(h, Win32Interop.SW_HIDE); } catch { }
                    Thread.Sleep(200);
                }
            }

            // 1) PRIMARY: two-click Win32 menu (parent → إدخال بيانات العملاء).
            MenuNavResult menu = MenuNavigator.OpenWithFallbacks(appWindow, menuPath, settleMs);
            if (menu.Ok)
            {
                Thread.Sleep(settleMs);
                h = WaitForEntryForm(appWindow, entryTitle, entryClass, settleMs * 5);
                if (h != IntPtr.Zero) { return h; }
            }

            // 2) Mapped nav control (toolbar / single visible item) if operator bound it.
            if (navSelector != null)
            {
                FieldMap navMap = FieldDiscovery.GetFieldMap(appWindow, false);
                ResolveResult rn = FieldDiscovery.ResolveField(navMap, navSelector, "Index");
                if (rn.Handle != IntPtr.Zero)
                {
                    Win32Driver.Click(rn.Handle, settleMs, false);
                    h = WaitForEntryForm(appWindow, entryTitle, entryClass, settleMs * 3);
                    if (h != IntPtr.Zero) { return h; }
                }
            }

            // 3) Shortcut chord (Ctrl+F2 per EGX manual) — still after menu attempt.
            if (!string.IsNullOrEmpty(shortcut))
            {
                try { Win32Driver.SetForeground(appWindow); } catch { }
                try { Win32Driver.SendShortcut(appWindow, shortcut, settleMs); } catch { }
                h = WaitForEntryForm(appWindow, entryTitle, entryClass, settleMs * 4);
                if (h != IntPtr.Zero) { return h; }
            }

            // 4) Last resort only if it is a real entry form (never MDIClient).
            IntPtr fallback = FieldDiscovery.GetScanRoot(appWindow, childPattern, true);
            if (fallback != IntPtr.Zero && EntryLooksReady(fallback)) { return fallback; }
            return IntPtr.Zero;
        }

        private static bool EntryLooksReady(IntPtr entry)
        {
            if (entry == IntPtr.Zero || !Win32Interop.IsWindow(entry)) { return false; }
            if (!Win32Interop.IsWindowVisible(entry)) { return false; }
            if (IsForeignCodingScreen(
                (Win32Driver.GetWindowTitle(entry) ?? "") + " " + (Win32Driver.GetWindowText(entry) ?? "")))
            {
                return false;
            }
            string cls = Win32Driver.GetClassName(entry);
            if (string.IsNullOrEmpty(cls)) { return false; }
            // Never treat the MDI client pane as the entry form.
            if (cls.IndexOf("MDIClient", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }
            if (cls.IndexOf("ThunderRT6MDIForm", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }
            List<W32WindowInfo> kids = Win32Driver.GetChildren(entry);
            int inputs = 0;
            for (int i = 0; i < kids.Count; i++)
            {
                string k = FieldDiscovery.GetControlKind(kids[i].Class);
                if (k == "TextBox" || k == "ComboBox" || k == "Button") { inputs++; }
            }
            return inputs >= 3;
        }

        private static IntPtr WaitForEntryForm(IntPtr app, string title, string cls, int timeoutMs)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(Math.Max(400, timeoutMs));
            while (DateTime.Now < deadline)
            {
                IntPtr h = FindEntryForm(app, title, cls);
                if (h != IntPtr.Zero && EntryLooksReady(h)) { return h; }
                Thread.Sleep(120);
            }
            return IntPtr.Zero;
        }

        public static ClientEntryResult Invoke(
            IntPtr window,
            OrderedDictionary row,
            List<FieldSelector> selectors,
            bool doSave,
            bool watch,
            int settleMs,
            int stepMs)
        {
            return Invoke(window, row, selectors, doSave, watch, settleMs, stepMs, null, null);
        }

        public static ClientEntryResult Invoke(
            IntPtr window,
            OrderedDictionary row,
            List<FieldSelector> selectors,
            bool doSave,
            bool watch,
            int settleMs,
            int stepMs,
            string scansRoot,
            Action<string, string> log)
        {
            Win32Driver.AssertHandle(window, "entry window");
            if (watch)
            {
                try { Win32Driver.SetForeground(window); } catch { }
            }
            if (log == null) { log = delegate(string m, string l) { }; }

            // Restore entry size from Setup capture so ClassOrdinal maps stay valid.
            if (WindowLayout.Active != null &&
                WindowLayout.Active.EntryForm != null &&
                WindowLayout.Active.EntryForm.IsValid)
            {
                WindowLayout.Apply(window, WindowLayout.Active.EntryForm, log);
            }

            FieldMap map = FieldDiscovery.GetFieldMap(window, false);
            List<FieldFillResult> fields = new List<FieldFillResult>();
            List<FieldSelector> ordered = OrderSelectorsForFill(selectors);

            LogMapHealth(selectors, log);

            // Job is filled ONLY after ALL KYC + documents — never right after الشكل القانوني.
            FieldSelector jobSelector = null;
            string jobValue = "";
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i] != null &&
                    string.Equals(ordered[i].FieldKey, "job", StringComparison.OrdinalIgnoreCase))
                {
                    jobSelector = ordered[i];
                    jobValue = GetEntryValue(row, ordered[i]);
                    break;
                }
            }
            log("job: DEFERRED until after all fields + documents (value='" +
                (jobValue ?? "") + "')", "INFO");

            bool legalFormDone = false;
            // Snapshot TextBoxes BEFORE الشكل القانوني so the new «الوظيفة» HWND is detectable.
            Dictionary<long, bool> textBoxesBeforeLegal = FieldDiscovery.SnapshotTextBoxHandles(map);

            for (int i = 0; i < ordered.Count; i++)
            {
                FieldSelector s = ordered[i];
                FieldCatalog.EnforceDateParts(s);
                string key = s.FieldKey != null ? s.FieldKey : "";
                if (NonEntryKey.IsMatch(key)) { continue; }
                // Hard skip — job is never part of this loop.
                if (string.Equals(key, "job", StringComparison.OrdinalIgnoreCase)) { continue; }
                // Identity issue day is auto-filled and locked in Coding — writing it interrupts fill.
                if (string.Equals(key, FieldCatalog.IssueDayKey, StringComparison.OrdinalIgnoreCase))
                {
                    log("issueDay: locked/auto-filled — skip", "INFO");
                    continue;
                }

                // Refresh pre-legal snapshot immediately before writing الشكل القانوني.
                if (string.Equals(key, "legalForm", StringComparison.OrdinalIgnoreCase))
                {
                    map = FieldDiscovery.GetFieldMap(window, false);
                    textBoxesBeforeLegal = FieldDiscovery.SnapshotTextBoxHandles(map);
                }

                string val = GetEntryValue(row, s);
                // Docs count ALWAYS = images under this NationalId folder only.
                if (string.Equals(key, FieldCatalog.DocsCountKey, StringComparison.OrdinalIgnoreCase))
                {
                    string folder = DocumentAttach.ResolveDocsFolder(row, scansRoot);
                    int n = DocumentAttach.ListImages(folder).Count;
                    val = n.ToString(CultureInfo.InvariantCulture);
                    if (n <= 0)
                    {
                        log("docsCount: folder empty — skip field", "WARN");
                        continue;
                    }
                }
                if (string.IsNullOrEmpty(val)) { continue; }

                // Value must fit the field type (blocks wrong-column / wrong-map writes).
                string fitNote;
                if (!ValueFitsField(key, val, out fitNote))
                {
                    FieldFillResult bad = new FieldFillResult();
                    bad.FieldKey = key;
                    bad.Value = val;
                    bad.Kind = s.Kind;
                    bad.Ok = false;
                    bad.Note = "BLOCKED: " + fitNote;
                    log("ANTI-HALLUCINATION: " + key + " — " + fitNote + " value='" + val + "'", "ERROR");
                    fields.Add(bad);
                    continue;
                }

                // After الشكل القانوني: job appears. Prefer operator click-map (valid when
                // remapped with job visible) but NEVER write into the new job HWND.
                if (legalFormDone)
                {
                    map = FieldDiscovery.GetFieldMap(window, false);
                }

                ResolveResult r = ResolveFieldAfterLegalForm(
                    map, s, key, legalFormDone, textBoxesBeforeLegal, log);

                FieldFillResult fr = new FieldFillResult();
                fr.FieldKey = key;
                fr.Value = val;
                fr.Kind = s.Kind;
                fr.Tier = r.Tier;

                if (r.Handle == IntPtr.Zero)
                {
                    fr.Ok = false;
                    fr.Note = "field not found (" + r.Note + ")";
                    fields.Add(fr);
                    continue;
                }

                // Hard anti-hallucination: never put file paths into KYC fields,
                // and never type KYC into Drive/Dir/FileList controls.
                string targetCls = Win32Driver.GetClassName(r.Handle) ?? "";
                if (WindowLayout.IsKycFieldKey(key))
                {
                    if (WindowLayout.LooksLikeFilePath(val))
                    {
                        fr.Ok = false;
                        fr.Note = "BLOCKED path-like value for KYC field";
                        log("ANTI-HALLUCINATION: refused path into " + key + " => '" + val + "'", "ERROR");
                        fields.Add(fr);
                        continue;
                    }
                    if (WindowLayout.IsFileBrowserClass(targetCls))
                    {
                        fr.Ok = false;
                        fr.Note = "BLOCKED: KYC field resolved to file-browser control (" + targetCls + ")";
                        log("ANTI-HALLUCINATION: " + key + " mapped to " + targetCls + " — re-map in Setup", "ERROR");
                        fields.Add(fr);
                        continue;
                    }
                }

                if (watch)
                {
                    try { Win32Driver.Highlight(r.Handle, 2); } catch { }
                    Thread.Sleep(stepMs);
                }

                string kind = s.Kind != null ? s.Kind : "";
                if (string.Equals(kind, "ComboBox", StringComparison.OrdinalIgnoreCase))
                {
                    W32ActionResult res = Win32Driver.SelectComboItem(r.Handle, val);
                    fr.Ok = res.Ok;
                    fr.Note = res.Note;
                }
                else if (string.Equals(kind, "CheckBox", StringComparison.OrdinalIgnoreCase))
                {
                    bool want = Regex.IsMatch(val, "(?i)^(1|true|yes|y|نعم|صح)$");
                    fr.Ok = Win32Driver.SetCheck(r.Handle, want);
                    fr.Note = "checked=" + want;
                }
                else
                {
                    // Focus + clear before write so stale/wrong text cannot linger.
                    try { Win32Driver.ClickPhysical(r.Handle, 80); } catch { }
                    try { Win32Driver.SetText(r.Handle, ""); } catch { }
                    Thread.Sleep(40);
                    W32ActionResult w = Win32Driver.SetText(r.Handle, val);
                    fr.Ok = w.Ok;
                    if (!fr.Ok && Regex.IsMatch(val, "^\\d+$"))
                    {
                        string digitsVal = Regex.Replace(val, "\\D", "");
                        string digitsAct = Regex.Replace(w.Actual != null ? w.Actual : "", "\\D", "");
                        int nWant, nGot;
                        fr.Ok = (digitsVal == digitsAct) ||
                            (int.TryParse(digitsVal, out nWant) && int.TryParse(digitsAct, out nGot) && nWant == nGot);
                    }
                    // Post-write guard: if screen shows a path in a KYC field, treat as fail.
                    if (fr.Ok && WindowLayout.IsKycFieldKey(key) && WindowLayout.LooksLikeFilePath(w.Actual))
                    {
                        fr.Ok = false;
                        fr.Note = "screen shows path after write — map/layout mismatch";
                        log("ANTI-HALLUCINATION: " + key + " shows path after SetText", "ERROR");
                        try { Win32Driver.SetText(r.Handle, ""); } catch { }
                    }
                    else if (fr.Ok && !TextMatchesExpected(key, val, w.Actual))
                    {
                        fr.Ok = false;
                        fr.Note = "screen mismatch expected='" + val + "' got='" + w.Actual + "'";
                        log("ANTI-HALLUCINATION: " + key + " mismatch after SetText", "ERROR");
                        try { Win32Driver.SetText(r.Handle, ""); } catch { }
                    }
                    else
                    {
                        fr.Note = fr.Ok ? "" : ("screen shows '" + w.Actual + "'");
                    }
                }
                fields.Add(fr);

                // After legal / sub form: let Coding create «الوظيفة», but DO NOT Tab into it
                // and DO NOT fill job yet (job is absolute last, after documents).
                if ((string.Equals(key, "legalForm", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(key, "subForm", StringComparison.OrdinalIgnoreCase)) && fr.Ok)
                {
                    if (string.Equals(key, "legalForm", StringComparison.OrdinalIgnoreCase))
                    {
                        // Keep pre-legal TextBox snapshot (do NOT refresh it after job appears).
                        legalFormDone = true;
                        try { Win32Driver.ClickPhysical(r.Handle, 100); } catch { }
                        Thread.Sleep(Math.Max(settleMs, 700));
                        map = FieldDiscovery.GetFieldMap(window, false);
                        log("legalForm set — subsequent fields use LABEL resolve; new TextBoxes blocked (job deferred)", "INFO");
                    }
                    else
                    {
                        try { Win32Driver.ClickPhysical(r.Handle, 100); } catch { }
                        Thread.Sleep(Math.Max(settleMs, 400));
                        map = FieldDiscovery.GetFieldMap(window, false);
                        log("subForm set via stable label resolve — job still DEFERRED", "INFO");
                    }
                }
                else
                {
                    Thread.Sleep(settleMs);
                }
            }

            // Documents first — job comes AFTER attachments so it is never early.
            log("job: still deferred — attaching documents first", "INFO");
            DocumentAttach.ScrubPathLeaksFromEntry(window, log);
            ClearJobFieldIfPresent(window, log);
            DocAttachResult docs = DocumentAttach.Attach(
                window, row, selectors, scansRoot, watch, stepMs, log);
            DocumentAttach.ScrubPathLeaksFromEntry(window, log);
            if (docs.Attempted > 0)
            {
                FieldFillResult dfr = new FieldFillResult();
                dfr.FieldKey = "documents";
                dfr.Kind = "Docs";
                dfr.Value = docs.Attached + "/" + docs.Attempted;
                dfr.Ok = docs.Ok;
                dfr.Note = docs.Note;
                fields.Add(dfr);
            }

            // ABSOLUTE LAST data field: job (after everything else, before Save).
            log("job: NOW filling after all KYC + documents", "INFO");
            FieldFillResult jobFr = FillJobWhenVisible(
                window, jobSelector, jobValue, watch, stepMs, settleMs, log);
            if (jobFr != null) { fields.Add(jobFr); }

            bool saved = false;
            bool alreadyExists = false;
            bool saveFailed = false;
            bool saveUnverified = false;
            if (doSave)
            {
                map = FieldDiscovery.GetFieldMap(window, false);
                IntPtr saveHwnd = ResolveSaveButton(window, map, selectors, log);
                if (saveHwnd != IntPtr.Zero)
                {
                    try { Win32Driver.SetForeground(window); } catch { }
                    Thread.Sleep(120);
                    // Real Coding uses «حفظ»; replica uses «حافظ» — physical click is reliable.
                    Win32Driver.ClickPhysical(saveHwnd, 400);
                    log("clicked Save button", "OK");
                    // Coding asks «تأكيد» (نعم/لا) then an info box. Clicking the button
                    // is not proof of a save — only Coding's own acknowledgement is.
                    try
                    {
                        int pid = Win32Driver.GetProcessId(window);
                        ConfirmDialogResult cd = ConfirmDialog.ConfirmAll(pid, window, 8000, 4, log);
                        if (cd.SaidAlreadyExists)
                        {
                            // «العميل موجود من قبل» + OK means Coding refused the insert.
                            alreadyExists = true;
                            log("Coding rejected the save: العميل موجود من قبل", "WARN");
                        }
                        else if (cd.SaidServerError)
                        {
                            saveFailed = true;
                            log("Coding save failed: server/runtime error after تأكيد", "ERROR");
                        }
                        else if (cd.SaidMissingDocuments)
                        {
                            saveFailed = true;
                            log("Coding refused save: missing client documents — stay on add form", "ERROR");
                        }
                        else if (cd.Handled == 0)
                        {
                            saveUnverified = true;
                            log("no confirmation dialog appeared after Save — save NOT confirmed", "ERROR");
                        }
                        else
                        {
                            saved = true;
                            log("save confirmed by Coding: «" + cd.LastTitle + "» → «" +
                                cd.LastButton + "»", "OK");
                            ActionPace.Wait(ActionPace.AfterSaveMs);
                        }
                    }
                    catch (Exception ex)
                    {
                        saveUnverified = true;
                        log("confirm after Save failed: " + ex.Message, "WARN");
                    }
                }
                else
                {
                    log("Save button not found — map «حفظ» in Setup or check caption", "ERROR");
                    FieldFillResult sfr = new FieldFillResult();
                    sfr.FieldKey = "btnSave";
                    sfr.Kind = "Button";
                    sfr.Ok = false;
                    sfr.Note = "save button not found";
                    fields.Add(sfr);
                }
            }

            int failed = 0;
            int softFailed = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].Ok) { continue; }
                if (IsSoftOptionalFailure(fields[i]))
                {
                    softFailed++;
                    continue;
                }
                failed++;
            }

            ClientEntryResult result = new ClientEntryResult();
            result.Ok = (failed == 0) && !saveFailed && !saveUnverified;
            result.Fields = fields;
            result.Saved = saved;
            result.SaveUnverified = saveUnverified;
            result.AlreadyExists = alreadyExists;
            if (alreadyExists)
            {
                result.Note = "العميل موجود من قبل — لم يُحفظ";
                return result;
            }
            if (saveFailed)
            {
                result.Note = "فشل الحفظ: خطأ من برنامج التكويد";
                return result;
            }
            if (saveUnverified)
            {
                result.Note = "تم ضغط حفظ ولم يرد برنامج التكويد — الحفظ غير مؤكد";
                return result;
            }
            if (failed == 0 && softFailed == 0)
            {
                result.Note = "all fields (" + fields.Count + ") written";
            }
            else if (failed == 0)
            {
                result.Note = softFailed + " optional combo(s) not in list (soft)";
            }
            else
            {
                result.Note = failed + " of " + fields.Count + " failed";
            }
            return result;
        }

        // Reject values that clearly belong to another column / a file path.
        private static bool ValueFitsField(string fieldKey, string value, out string note)
        {
            note = "";
            if (string.IsNullOrEmpty(fieldKey) || value == null) { return true; }
            if (WindowLayout.LooksLikeFilePath(value) && WindowLayout.IsKycFieldKey(fieldKey))
            {
                note = "path-like value not allowed in KYC field";
                return false;
            }
            string k = fieldKey.ToLowerInvariant();
            string v = value.Trim();
            if (k == "job")
            {
                if (Regex.IsMatch(v, "(?i)\\.(jpg|jpeg|png|pdf)$") || v.IndexOf('\\') >= 0 || v.IndexOf('/') >= 0)
                {
                    note = "job cannot be a file path";
                    return false;
                }
                if (Regex.IsMatch(v, "^\\d{10,}$"))
                {
                    note = "job cannot be a long digit string (NID/mobile)";
                    return false;
                }
            }
            if (k == "brokercode" || k == "broker")
            {
                if (v.Length >= 12 && Regex.IsMatch(v, "^\\d+$"))
                {
                    note = "broker code looks like NationalId — check sheet/map";
                    return false;
                }
            }
            if (k == "nationalid")
            {
                string digits = Regex.Replace(v, "\\D", "");
                if (digits.Length != 14)
                {
                    note = "nationalId must be 14 digits";
                    return false;
                }
            }
            if (k == "mobile")
            {
                string digits = Regex.Replace(v, "\\D", "");
                if (digits.Length < 10 || digits.Length > 12)
                {
                    note = "mobile length invalid";
                    return false;
                }
            }
            if (k == "name" || k == "address")
            {
                if (Regex.IsMatch(v, "(?i)\\.(jpg|jpeg|png|pdf)$") || v.IndexOf(":\\", StringComparison.Ordinal) >= 0)
                {
                    note = fieldKey + " cannot contain a file path";
                    return false;
                }
            }
            return true;
        }

        private static bool TextMatchesExpected(string fieldKey, string expected, string actual)
        {
            if (expected == null) { expected = ""; }
            if (actual == null) { actual = ""; }
            if (actual == expected) { return true; }
            // Coding may expand codes to "21 - مصر" — accept if actual starts with expected digits/code.
            string expDigits = Regex.Replace(expected, "\\s+", "");
            string actDigits = Regex.Replace(actual, "\\s+", "");
            if (expDigits.Length > 0 && actDigits.StartsWith(expDigits, StringComparison.Ordinal))
            {
                return true;
            }
            if (Regex.IsMatch(expected, "^\\d+$"))
            {
                string a = Regex.Replace(actual ?? "", "\\D", "");
                int nWant, nGot;
                if (a.StartsWith(expected, StringComparison.Ordinal) || a == expected) { return true; }
                if (int.TryParse(expected, out nWant) && int.TryParse(a, out nGot) && nWant == nGot)
                {
                    return true;
                }
            }
            // Arabic text fields: tolerate trailing spaces / partial if expected is contained.
            if (!string.IsNullOrEmpty(expected) &&
                actual.IndexOf(expected, StringComparison.Ordinal) >= 0)
            {
                return true;
            }
            return false;
        }

        // Surface a fragile click-map at run start so a bad map is obvious in the log.
        private static void LogMapHealth(List<FieldSelector> selectors, Action<string, string> log)
        {
            MapHealthReport rep = MapHealth.Inspect(selectors);
            log("map health: " + rep.MappedCount + " mapped, " +
                rep.WithPositionCount + " with position", "INFO");
            for (int i = 0; i < rep.Issues.Count; i++)
            {
                log("map health — " + rep.Issues[i].FieldKey + ": " + rep.Issues[i].Message,
                    rep.Issues[i].Severity == "ERROR" ? "ERROR" : "WARN");
            }
        }

        // Geometry first, then Index click-map; never blind ClassOrdinal+1 (caused wrong fields).
        private static ResolveResult ResolveFieldAfterLegalForm(
            FieldMap map,
            FieldSelector s,
            string key,
            bool legalFormDone,
            Dictionary<long, bool> textBoxesBeforeLegal,
            Action<string, string> log)
        {
            // A captured position is exact: trust it, or fail — guessing is what
            // produced the wrong-field writes.
            if (s != null && s.HasPosition)
            {
                ResolveResult byPos = FieldDiscovery.ResolveByPosition(map, s);
                if (byPos.Handle == IntPtr.Zero)
                {
                    log(key + ": " + byPos.Note, "WARN");
                    return byPos;
                }
                if (legalFormDone && IsForbiddenJobTarget(map, byPos.Handle, textBoxesBeforeLegal))
                {
                    ResolveResult blocked = new ResolveResult();
                    blocked.Handle = IntPtr.Zero;
                    blocked.Tier = "None";
                    blocked.Note = "position landed on الوظيفة — blocked";
                    log(key + ": " + blocked.Note, "WARN");
                    return blocked;
                }
                return byPos;
            }

            if (!legalFormDone)
            {
                return FieldDiscovery.ResolveField(map, s, "Index");
            }

            ResolveResult r = FieldDiscovery.ResolveField(map, s, "Index");
            if (r.Handle != IntPtr.Zero && !IsForbiddenJobTarget(map, r.Handle, textBoxesBeforeLegal))
            {
                return r;
            }

            bool hitJob = r.Handle != IntPtr.Zero &&
                IsForbiddenJobTarget(map, r.Handle, textBoxesBeforeLegal);
            if (hitJob)
            {
                log(key + ": resolve hit الوظيفة — trying label then Index+1 only", "WARN");
            }

            ResolveResult byLabel = FieldDiscovery.ResolveStableAfterLayoutChange(map, s);
            if (byLabel.Handle != IntPtr.Zero &&
                !IsForbiddenJobTarget(map, byLabel.Handle, textBoxesBeforeLegal))
            {
                return byLabel;
            }

            // Only shift Index when we actually landed on the job box (job inserted above).
            if (hitJob)
            {
                ResolveResult shifted = FieldDiscovery.ResolveWithIndexDelta(map, s, 1);
                if (shifted.Handle != IntPtr.Zero &&
                    !IsForbiddenJobTarget(map, shifted.Handle, textBoxesBeforeLegal))
                {
                    log(key + ": resolved via Index+1 after job insert", "INFO");
                    return shifted;
                }
            }

            ResolveResult miss = new ResolveResult();
            miss.Handle = IntPtr.Zero;
            miss.Tier = "None";
            miss.Note = hitJob
                ? "post-legal: blocked job target, no safe alternate"
                : (r.Note ?? byLabel.Note ?? "post-legal resolve miss");
            return miss;
        }

        private static bool IsForbiddenJobTarget(
            FieldMap map, IntPtr hwnd, Dictionary<long, bool> textBoxesBeforeLegal)
        {
            if (hwnd == IntPtr.Zero) { return true; }
            if (ControlHasJobLabel(map, hwnd) || FieldDiscovery.FieldLooksLikeJob(map, hwnd))
            {
                return true;
            }
            // Brand-new TextBox after legal form = الوظيفة (even if MSAA label missing).
            if (FieldDiscovery.IsNewTextBox(textBoxesBeforeLegal, hwnd))
            {
                return true;
            }
            return false;
        }

        // Wipe accidental text that landed in «الوظيفة» before we intentionally fill it.
        private static void ClearJobFieldIfPresent(IntPtr window, Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            try
            {
                FieldMap map = FieldDiscovery.GetFieldMap(window, false);
                ResolveResult job = TryFindJobTextBox(map, null);
                if (job.Handle == IntPtr.Zero || !IsJobControlSafe(job.Handle)) { return; }
                string cur = "";
                try { cur = Win32Driver.GetWindowText(job.Handle); } catch { }
                if (string.IsNullOrEmpty(cur)) { return; }
                Win32Driver.SetText(job.Handle, "");
                log("cleared premature text from «الوظيفة» before docs/job fill: '" + cur + "'", "WARN");
            }
            catch { }
        }

        // After ALL other KYC fields: wait until «الوظيفة» is really on screen, then fill.
        // Never write job via ClassOrdinal alone — that was the hallucination source.
        private static FieldFillResult FillJobWhenVisible(
            IntPtr window,
            FieldSelector jobSelector,
            string jobValue,
            bool watch,
            int stepMs,
            int settleMs,
            Action<string, string> log)
        {
            if (string.IsNullOrWhiteSpace(jobValue)) { return null; }

            FieldFillResult fr = new FieldFillResult();
            fr.FieldKey = "job";
            fr.Value = jobValue;
            fr.Kind = jobSelector != null && !string.IsNullOrEmpty(jobSelector.Kind)
                ? jobSelector.Kind : "TextBox";

            string fitNote;
            if (!ValueFitsField("job", jobValue, out fitNote))
            {
                fr.Ok = false;
                fr.Note = "BLOCKED: " + fitNote;
                log("ANTI-HALLUCINATION: job — " + fitNote, "ERROR");
                return fr;
            }

            log("job: deferred — waiting until «الوظيفة» appears on screen", "INFO");
            try { Win32Driver.SetForeground(window); } catch { }
            try { Win32Driver.SendShortcut(window, "{TAB}", 150); } catch { }
            Thread.Sleep(Math.Max(settleMs, 400));

            IntPtr hwnd = WaitForVisibleJobHandle(window, jobSelector, 10000, log);
            if (hwnd == IntPtr.Zero)
            {
                fr.Ok = false;
                fr.Note = "job field not visible yet — skipped (no write to avoid wrong field)";
                log("ANTI-HALLUCINATION: job not on screen after wait — will NOT write anywhere", "ERROR");
                return fr;
            }

            // Final existence/visibility gate before any SetText.
            if (!IsJobControlSafe(hwnd))
            {
                fr.Ok = false;
                fr.Note = "job handle found but not a safe visible TextBox";
                log("ANTI-HALLUCINATION: job handle rejected (not safe/visible)", "ERROR");
                return fr;
            }

            if (watch)
            {
                try { Win32Driver.Highlight(hwnd, 2); } catch { }
                Thread.Sleep(stepMs);
            }

            // Focus the late-appearing job box with a double-click, then wait 1s before typing.
            log("job: double-click «الوظيفة» then wait 1s before typing", "INFO");
            try { Win32Driver.SetForeground(window); } catch { }
            Thread.Sleep(120);
            try { Win32Driver.DoubleClickPhysical(hwnd, 200); } catch { }
            Thread.Sleep(1000);

            // Re-check after delay — control must still be the visible job box.
            if (!IsJobControlSafe(hwnd) || !Win32Interop.IsWindow(hwnd))
            {
                // Handle may have been recreated — find again by label.
                hwnd = WaitForVisibleJobHandle(window, jobSelector, 3000, log);
                if (hwnd == IntPtr.Zero || !IsJobControlSafe(hwnd))
                {
                    fr.Ok = false;
                    fr.Note = "job disappeared after double-click wait — skipped";
                    log("ANTI-HALLUCINATION: job not safe after 1s delay — no write", "ERROR");
                    return fr;
                }
                try { Win32Driver.DoubleClickPhysical(hwnd, 150); } catch { }
                Thread.Sleep(400);
            }

            try { Win32Driver.SetText(hwnd, ""); } catch { }
            Thread.Sleep(80);
            W32ActionResult w = Win32Driver.SetText(hwnd, jobValue);
            fr.Ok = w.Ok || TextMatchesExpected("job", jobValue, w.Actual);
            if (fr.Ok && WindowLayout.LooksLikeFilePath(w.Actual))
            {
                fr.Ok = false;
                fr.Note = "screen shows path in job — cleared";
                try { Win32Driver.SetText(hwnd, ""); } catch { }
                log("ANTI-HALLUCINATION: job shows path after write", "ERROR");
            }
            else if (!fr.Ok)
            {
                fr.Note = "screen shows '" + w.Actual + "'";
                try { Win32Driver.SetText(hwnd, ""); } catch { }
            }
            else
            {
                fr.Note = "filled after double-click + 1s delay";
                fr.Tier = "JobVisible";
                log("job filled after double-click + 1s delay", "OK");
            }
            Thread.Sleep(settleMs);
            return fr;
        }

        // Poll until job TextBox is available. Label first; then trust click-map
        // (VB6 often has no MSAA label on «الوظيفة»).
        private static IntPtr WaitForVisibleJobHandle(
            IntPtr window,
            FieldSelector preferred,
            int timeoutMs,
            Action<string, string> log)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(Math.Max(1500, timeoutMs));
            int attempt = 0;
            IntPtr mappedCandidate = IntPtr.Zero;
            while (DateTime.Now < deadline)
            {
                attempt++;
                FieldMap map = FieldDiscovery.GetFieldMap(window, false);

                ResolveResult byLabel = TryFindJobTextBox(map, preferred);
                if (byLabel.Handle != IntPtr.Zero && IsJobControlSafe(byLabel.Handle))
                {
                    log("job visible via label (attempt " + attempt + ")", "OK");
                    return byLabel.Handle;
                }

                if (preferred != null &&
                    (!string.IsNullOrEmpty(preferred.Class) || !string.IsNullOrEmpty(preferred.ClassOrdinal)))
                {
                    ResolveResult mapped = FieldDiscovery.ResolveField(map, preferred, "Index");
                    if (mapped.Handle != IntPtr.Zero && IsJobControlSafe(mapped.Handle))
                    {
                        if (ControlHasJobLabel(map, mapped.Handle) ||
                            FieldDiscovery.FieldLooksLikeJob(map, mapped.Handle))
                        {
                            log("job visible via map+label (attempt " + attempt + ")", "OK");
                            return mapped.Handle;
                        }
                        // Keep candidate — Coding often has no label paired to الوظيفة.
                        mappedCandidate = mapped.Handle;
                        if (attempt >= 4)
                        {
                            string cur = "";
                            try { cur = Win32Driver.GetWindowText(mapped.Handle); } catch { }
                            // Refuse if it already holds NID / path / pure long digits (wrong field).
                            if (!WindowLayout.LooksLikeFilePath(cur) &&
                                !Regex.IsMatch(cur ?? "", "^\\d{10,}$"))
                            {
                                log("job via click-map ClassOrdinal (no MSAA label) attempt " + attempt, "OK");
                                return mapped.Handle;
                            }
                            log("job map candidate rejected (content looks like other field): '" + cur + "'", "WARN");
                        }
                        else if (attempt == 1)
                        {
                            log("job map hit control without «وظيف» label — will retry then trust map", "INFO");
                        }
                    }
                }

                Thread.Sleep(300);
            }
            if (mappedCandidate != IntPtr.Zero && IsJobControlSafe(mappedCandidate))
            {
                log("job: using mapped ClassOrdinal after wait (label unavailable)", "WARN");
                return mappedCandidate;
            }
            return IntPtr.Zero;
        }

        private static bool IsJobControlSafe(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero || !Win32Interop.IsWindow(hwnd)) { return false; }
            if (!Win32Interop.IsWindowVisible(hwnd)) { return false; }
            if (!Win32Interop.IsWindowEnabled(hwnd)) { return false; }
            string cls = Win32Driver.GetClassName(hwnd) ?? "";
            if (WindowLayout.IsFileBrowserClass(cls)) { return false; }
            // Must be an edit/textbox class (VB6 ThunderRT6TextBox).
            if (cls.IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) < 0 &&
                cls.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(hwnd);
                if (r.W < 20 || r.H < 10) { return false; }
            }
            catch { return false; }
            return true;
        }

        private static bool ControlHasJobLabel(FieldMap map, IntPtr hwnd)
        {
            if (map == null || map.Fields == null || hwnd == IntPtr.Zero) { return false; }
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f.Handle != hwnd) { continue; }
                string lab = f.Label != null ? f.Label : "";
                if (lab.IndexOf("وظيف", StringComparison.Ordinal) >= 0) { return true; }
                return false;
            }
            return false;
        }

        private static ResolveResult TryFindJobTextBox(FieldMap map, FieldSelector preferred)
        {
            ResolveResult miss = new ResolveResult();
            miss.Handle = IntPtr.Zero;
            miss.Note = "job textbox not found";
            miss.Tier = "";
            if (map == null || map.Fields == null) { return miss; }

            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f.Kind != "TextBox") { continue; }
                if (!f.Visible || !f.Enabled) { continue; }
                string lab = f.Label != null ? f.Label : "";
                if (lab.IndexOf("وظيف", StringComparison.Ordinal) < 0) { continue; }
                if (WindowLayout.IsFileBrowserClass(f.Class)) { continue; }
                if (!IsJobControlSafe(f.Handle)) { continue; }

                ResolveResult hit = new ResolveResult();
                hit.Handle = f.Handle;
                hit.Tier = "JobLabel";
                hit.Note = "job via label «" + lab + "»";
                return hit;
            }
            return miss;
        }

        // Prefer mapped btnSave; otherwise find CommandButton «حفظ» / «حافظ» on the entry form.
        private static IntPtr ResolveSaveButton(
            IntPtr entryWindow,
            FieldMap map,
            List<FieldSelector> selectors,
            Action<string, string> log)
        {
            FieldSelector mapped = null;
            if (selectors != null)
            {
                for (int i = 0; i < selectors.Count; i++)
                {
                    if (string.Equals(selectors[i].FieldKey, "btnSave", StringComparison.OrdinalIgnoreCase))
                    {
                        mapped = selectors[i];
                        break;
                    }
                }
            }
            if (mapped != null &&
                (!string.IsNullOrEmpty(mapped.Class) || !string.IsNullOrEmpty(mapped.ClassOrdinal) ||
                 !string.IsNullOrEmpty(mapped.Index) || !string.IsNullOrEmpty(mapped.Caption)))
            {
                ResolveResult rb = FieldDiscovery.ResolveField(map, mapped, "Index");
                if (rb.Handle != IntPtr.Zero && IsSaveCaption(Win32Driver.GetWindowText(rb.Handle)))
                {
                    return rb.Handle;
                }
                if (rb.Handle != IntPtr.Zero)
                {
                    log("mapped btnSave hwnd is «" + Win32Driver.GetWindowText(rb.Handle) +
                        "» — not Save, searching caption", "WARN");
                }
                else
                {
                    log("mapped btnSave not found on screen — trying caption حفظ/حافظ", "WARN");
                }
            }

            if (map == null || map.Fields == null) { return IntPtr.Zero; }
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f.Kind != "Button") { continue; }
                string c = f.Caption != null ? f.Caption.Trim() : "";
                if (c.Length == 0) { continue; }
                if (!IsSaveCaption(c)) { continue; }
                string n = c.Replace("&", "").Replace(" ", "");
                if (n.Length == 0) { continue; }
                {
                    // Prefer bottom toolbar Save (usually larger / lower on form).
                    ResolveResult rr = FieldDiscovery.ResolveField(map, new FieldSelector
                    {
                        FieldKey = "btnSave",
                        Kind = "Button",
                        Class = f.Class,
                        ClassOrdinal = f.ClassOrdinal.ToString(),
                        Index = f.Index.ToString(),
                        Caption = f.Caption
                    }, "Index");
                    if (rr.Handle != IntPtr.Zero)
                    {
                        log("Save via caption «" + c + "»", "INFO");
                        return rr.Handle;
                    }
                }
            }

            // Last resort: walk children for button text.
            List<W32WindowInfo> kids = Win32Driver.GetChildren(entryWindow);
            IntPtr best = IntPtr.Zero;
            int bestY = -1;
            for (int i = 0; i < kids.Count; i++)
            {
                if (!kids[i].Visible) { continue; }
                if (FieldDiscovery.GetControlKind(kids[i].Class) != "Button") { continue; }
                string t = kids[i].Text != null ? kids[i].Text.Trim().Replace("&", "") : "";
                if (!IsSaveCaption(t)) { continue; }
                int y = 0;
                try { y = Win32Driver.GetRect(kids[i].Handle).Y; } catch { }
                if (y >= bestY)
                {
                    bestY = y;
                    best = kids[i].Handle;
                }
            }
            if (best != IntPtr.Zero) { log("Save via child enum", "INFO"); }
            return best;
        }

        private static bool IsSaveCaption(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) { return false; }
            string t = text.Replace("&", "").Trim();
            if (t.IndexOf("غلق", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("إغلاق", StringComparison.Ordinal) >= 0 ||
                t.IndexOf("اغلاق", StringComparison.Ordinal) >= 0 ||
                t.Equals("Close", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string n = t.Replace(" ", "");
            return n == "حفظ" || n == "حافظ" ||
                n.IndexOf("حفظ", StringComparison.Ordinal) >= 0 ||
                t.Equals("Save", StringComparison.OrdinalIgnoreCase);
        }

        // «جديد» clears the entry form for the next client without closing the MDI child.
        public static bool ClickNewRecord(
            IntPtr entryWindow,
            List<FieldSelector> selectors,
            Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            if (entryWindow == IntPtr.Zero || !Win32Interop.IsWindow(entryWindow)) { return false; }

            FieldMap map = FieldDiscovery.GetFieldMap(entryWindow, false);
            IntPtr hwnd = IntPtr.Zero;
            if (selectors != null)
            {
                for (int i = 0; i < selectors.Count; i++)
                {
                    if (!string.Equals(selectors[i].FieldKey, "btnNew", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    ResolveResult r = FieldDiscovery.ResolveField(map, selectors[i], "Index");
                    if (r.Handle != IntPtr.Zero) { hwnd = r.Handle; break; }
                    if (TryClickSavedPosition(entryWindow, selectors[i], log))
                    {
                        return true;
                    }
                }
            }

            if (hwnd == IntPtr.Zero)
            {
                hwnd = FindButtonCaption(entryWindow, map, new string[] { "جديد", "New" }, log);
            }
            if (hwnd == IntPtr.Zero) { return false; }

            try { Win32Driver.SetForeground(entryWindow); } catch { }
            Win32Driver.ClickPhysical(hwnd, 280);
            try { Win32Driver.Click(hwnd); } catch { }
            log("clicked «جديد» for next client", "OK");
            return true;
        }

        // Windowless VB6 buttons often have PosX/Y without a reliable HWND / W/H.
        private static bool TryClickSavedPosition(
            IntPtr entryWindow, FieldSelector s, Action<string, string> log)
        {
            if (s == null || entryWindow == IntPtr.Zero) { return false; }
            int px, py;
            if (!int.TryParse(s.PosX, out px) || !int.TryParse(s.PosY, out py)) { return false; }
            int pw = 0, ph = 0;
            int.TryParse(s.PosW, out pw);
            int.TryParse(s.PosH, out ph);
            try
            {
                Win32Driver.RectInfo form = Win32Driver.GetRect(entryWindow);
                int x = form.X + px + Math.Max(pw, 8) / 2;
                int y = form.Y + py + Math.Max(ph, 8) / 2;
                try { Win32Driver.SetForeground(entryWindow); } catch { }
                Win32Driver.ClickScreen(x, y, 250);
                log("جديد via saved position " + x + "," + y, "OK");
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static IntPtr FindButtonCaption(
            IntPtr entryWindow, FieldMap map, string[] captions, Action<string, string> log)
        {
            if (map != null && map.Fields != null)
            {
                for (int c = 0; c < captions.Length; c++)
                {
                    for (int i = 0; i < map.Fields.Count; i++)
                    {
                        AppField f = map.Fields[i];
                        if (f.Kind != "Button") { continue; }
                        if (!IsNewCaption(f.Caption, captions[c])) { continue; }
                        if (f.Handle != IntPtr.Zero) { return f.Handle; }
                    }
                }
            }
            List<W32WindowInfo> kids = Win32Driver.GetChildren(entryWindow);
            for (int c = 0; c < captions.Length; c++)
            {
                for (int i = 0; i < kids.Count; i++)
                {
                    if (!kids[i].Visible) { continue; }
                    if (FieldDiscovery.GetControlKind(kids[i].Class) != "Button") { continue; }
                    if (IsNewCaption(kids[i].Text, captions[c])) { return kids[i].Handle; }
                }
            }
            return IntPtr.Zero;
        }

        private static bool IsNewCaption(string text, string want)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(want)) { return false; }
            string t = text.Replace("&", "").Trim();
            if (ArabicText.Contains(t, "وادي")) { return false; }
            if (string.Equals(t, want, StringComparison.OrdinalIgnoreCase)) { return true; }
            return ArabicText.Contains(t, want) && t.Length <= 12;
        }

        // Missing optional lookups on a limited combo list are warnings, not hard fails.
        private static bool IsSoftOptionalFailure(FieldFillResult fr)
        {
            if (fr == null || fr.Ok) { return false; }
            string key = fr.FieldKey != null ? fr.FieldKey : "";
            if (!Regex.IsMatch(key, "^(subForm|legalForm|broker|brokerCode|docsCount|documents)$",
                RegexOptions.IgnoreCase))
            {
                return false;
            }
            string note = fr.Note != null ? fr.Note : "";
            if (string.Equals(key, "documents", StringComparison.OrdinalIgnoreCase))
            {
                return note.IndexOf("no images", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            if (Regex.IsMatch(key, "^(broker|brokerCode|docsCount)$", RegexOptions.IgnoreCase))
            {
                return note.IndexOf("field not found", StringComparison.OrdinalIgnoreCase) >= 0
                    || note.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return note.IndexOf("item not in list", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
