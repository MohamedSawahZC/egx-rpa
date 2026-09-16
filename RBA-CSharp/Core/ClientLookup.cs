// =============================================================================
//  ClientLookup.cs - duplicate check on «الإستعلام للتعديل» before any insert.
//
//  Flow: open the inquiry screen from the menu, search by National Id, fall back
//  to the name when the id returns nothing. Keep the screen open between clients
//  so the next search reuses it. The run closer discards «هل تريد الحفظ؟» with No.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using System.Threading;

namespace RbaCore
{
    public enum LookupOutcome
    {
        NotFound = 0,
        Found = 1,
        Unknown = 2
    }

    public class ClientLookupResult
    {
        public LookupOutcome Outcome = LookupOutcome.Unknown;
        public string MatchedBy = "";
        public string Note = "";
        public string Nin = "";
        public string Code12 = "";
        // Extra OCR guesses (e.g. 1↔8) tried in order if the top NIN misses in سحب.
        public string[] NinCandidates = new string[0];
        public IntPtr Screen;

        public bool IsDuplicate { get { return Outcome == LookupOutcome.Found; } }
    }

    public static class ClientLookup
    {
        public const string SearchBoxKey = "lookupSearchBox";
        public const string SearchButtonKey = "lookupSearchBtn";
        public const string GridKey = "lookupGrid";

        private const string DefaultTitle = "الإستعلام للتعديل";
        private const int GridSettleMs = 1600;
        private const int MinGridWidth = 220;
        private const int MinGridHeight = 90;

        // Locate the inquiry screen without searching for it — used by Setup capture.
        public static IntPtr FindOpenScreen(IntPtr appWindow, BotAppConfig app)
        {
            if (appWindow == IntPtr.Zero || !Win32Interop.IsWindow(appWindow)) { return IntPtr.Zero; }
            return FindScreen(appWindow, app, null);
        }

        public static IntPtr FindOpenScreen(
            IntPtr appWindow, BotAppConfig app, List<FieldSelector> selectors)
        {
            if (appWindow == IntPtr.Zero || !Win32Interop.IsWindow(appWindow)) { return IntPtr.Zero; }
            return FindScreen(appWindow, app, selectors);
        }

        // Open it (menu navigation) so the operator can click its controls.
        public static IntPtr EnsureScreen(
            IntPtr appWindow, BotAppConfig app, int settleMs, Action<string, string> log)
        {
            return EnsureScreen(appWindow, app, null, settleMs, log);
        }

        public static IntPtr EnsureScreen(
            IntPtr appWindow,
            BotAppConfig app,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            return OpenScreen(appWindow, app, selectors, settleMs <= 0 ? 400 : settleMs, log);
        }

        // Search «الإستعلام للتعديل» for this client. Never throws — a broken
        // lookup returns Unknown so the caller can decide, instead of blocking work.
        public static ClientLookupResult Find(
            IntPtr appWindow,
            BotAppConfig app,
            OrderedDictionary row,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log)
        {
            ClientLookupResult result = new ClientLookupResult();
            if (log == null) { log = delegate(string m, string l) { }; }
            if (settleMs <= 0) { settleMs = 400; }

            string nationalId = CsvUtil.Get(row, "NationalId", "").Trim();
            string name = CsvUtil.Get(row, "Name", "").Trim();
            if (nationalId.Length == 0 && name.Length == 0)
            {
                result.Note = "no NationalId/Name to search with";
                log("lookup: " + result.Note, "WARN");
                return result;
            }

            IntPtr screen = IntPtr.Zero;
            try
            {
                screen = OpenScreen(appWindow, app, selectors, settleMs, log);
                if (screen == IntPtr.Zero)
                {
                    result.Note = "«" + LookupTitle(app) + "» did not open";
                    log("lookup: " + result.Note, "WARN");
                    return result;
                }
                result.Screen = screen;

                // Same size as capture time, so the position-mapped search box matches.
                if (WindowLayout.Active != null &&
                    WindowLayout.Active.LookupScreen != null &&
                    WindowLayout.Active.LookupScreen.IsValid)
                {
                    WindowLayout.Apply(screen, WindowLayout.Active.LookupScreen, log);
                }

                LookupQueryRead byId = LookupQueryRead.Skip;
                string nin = "";
                string code12 = "";
                string[] alts = new string[0];
                if (nationalId.Length > 0)
                {
                    byId = RunQuery(screen, selectors, nationalId, nationalId, log,
                        out nin, out code12, out alts);
                    if (byId == LookupQueryRead.Hit)
                    {
                        result.Outcome = LookupOutcome.Found;
                        result.MatchedBy = "NationalId";
                        result.Nin = nin;
                        result.Code12 = code12;
                        result.NinCandidates = alts != null ? alts : new string[0];
                        result.Note = "existing client matched by national id " + nationalId +
                            (code12.Length > 0 ? " CODE12 " + code12 : "") +
                            (nin.Length > 0 ? " NIN " + nin : "");
                        log("lookup: " + result.Note, "WARN");
                        return result;
                    }

                    // National id is unique. A name fallback would pull a homonym
                    // (same Arabic name, different person) — never do that.
                    if (byId == LookupQueryRead.Miss)
                    {
                        result.Outcome = LookupOutcome.NotFound;
                        result.Note = "no existing client for national id " + nationalId +
                            " — not searching by name";
                        log("lookup: " + result.Note, "OK");
                        return result;
                    }
                }

                LookupQueryRead byName = LookupQueryRead.Skip;
                if (name.Length > 0 && nationalId.Length == 0)
                {
                    string nin2 = "";
                    string code122 = "";
                    string[] alts2 = new string[0];
                    byName = RunQuery(screen, selectors, name, "", log,
                        out nin2, out code122, out alts2);
                    if (byName == LookupQueryRead.Hit)
                    {
                        result.Outcome = LookupOutcome.Found;
                        result.MatchedBy = "Name";
                        result.Nin = nin2;
                        result.Code12 = code122;
                        result.NinCandidates = alts2 != null ? alts2 : new string[0];
                        result.Note = "existing client matched by name «" + name + "»" +
                            (code122.Length > 0 ? " CODE12 " + code122 : "") +
                            (nin2.Length > 0 ? " NIN " + nin2 : "");
                        log("lookup: " + result.Note, "WARN");
                        return result;
                    }
                }

                // Coding's own «لا يوجد عملاء متشابهين» (or an empty readable grid) is NotFound.
                if (byId == LookupQueryRead.Miss || byName == LookupQueryRead.Miss)
                {
                    result.Outcome = LookupOutcome.NotFound;
                    result.Note = "no existing client — safe to insert";
                    log("lookup: " + result.Note, "OK");
                    return result;
                }

                // An unreadable MSFlex/Frame grid must not be recorded as «غير موجود».
                if (byId == LookupQueryRead.Unreadable || byName == LookupQueryRead.Unreadable)
                {
                    result.Outcome = LookupOutcome.Unknown;
                    result.Note = "result grid unreadable — cannot confirm duplicate";
                    log("lookup: " + result.Note, "WARN");
                    return result;
                }

                result.Outcome = LookupOutcome.NotFound;
                result.Note = "no existing client — safe to insert";
                log("lookup: " + result.Note, "OK");
                return result;
            }
            catch (Exception ex)
            {
                result.Outcome = LookupOutcome.Unknown;
                result.Note = "lookup failed: " + ex.Message;
                log("lookup: " + result.Note, "WARN");
                return result;
            }
        }

        // Close a form and answer any prompt it raises (used for the entry form too).
        public static bool CloseWindow(IntPtr window, string label, Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            if (window == IntPtr.Zero || !Win32Interop.IsWindow(window)) { return true; }

            int pid = Win32Driver.GetProcessId(window);
            try { Win32Interop.PostMessage(window, Win32Interop.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); }
            catch { }

            // Closing may raise «هل تريد الحفظ؟» — Never Yes here: that re-saves and
            // blocks the next client. Answer No / OK only.
            ConfirmDialog.AnswerClosePrompts(pid, window, 2500, 3, log);

            DateTime until = DateTime.Now.AddMilliseconds(3000);
            while (DateTime.Now < until)
            {
                if (!Win32Interop.IsWindow(window) || !Win32Interop.IsWindowVisible(window))
                {
                    log(label + ": closed", "OK");
                    return true;
                }
                Thread.Sleep(100);
            }
            log(label + ": still open after close request", "WARN");
            return false;
        }

        private static string LookupTitle(BotAppConfig app)
        {
            if (app != null && !string.IsNullOrWhiteSpace(app.LookupWindowTitle))
            {
                return app.LookupWindowTitle.Trim();
            }
            return DefaultTitle;
        }

        private static IntPtr OpenScreen(
            IntPtr appWindow,
            BotAppConfig app,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log)
        {
            string title = LookupTitle(app);

            IntPtr already = FindScreen(appWindow, app, selectors);
            if (already != IntPtr.Zero)
            {
                log("lookup: screen already open «" + Win32Driver.GetWindowTitle(already) + "»", "INFO");
                return already;
            }

            try
            {
                Win32Driver.SetForeground(appWindow);
                Thread.Sleep(150);
            }
            catch { }

            string[] path = app != null ? app.LookupMenuPath : null;
            MenuNavResult menu = MenuNavigator.OpenPath(appWindow, path, settleMs);
            if (!menu.Ok)
            {
                log("lookup: menu «" + Join(path) + "» failed — " + menu.Note, "WARN");
                return IntPtr.Zero;
            }

            DateTime until = DateTime.Now.AddMilliseconds(settleMs * 8);
            while (DateTime.Now < until)
            {
                IntPtr h = FindScreen(appWindow, app, selectors);
                if (h != IntPtr.Zero) { return h; }
                Thread.Sleep(120);
            }

            log("lookup: no window titled «" + title + "» — open now: " +
                ListOpenTitles(appWindow), "WARN");
            return IntPtr.Zero;
        }

        public static void LogOpenWindows(IntPtr appWindow, string tag, Action<string, string> log)
        {
            if (log == null) { return; }
            log(tag + ": open windows → " + ListOpenTitles(appWindow), "INFO");
        }

        // Helps the operator fix the title/menu when the screen is not recognised.
        private static string ListOpenTitles(IntPtr appWindow)
        {
            List<string> titles = new List<string>();
            List<W32WindowInfo> forms = Win32Driver.GetFormWindows(appWindow, 6);
            for (int i = 0; i < forms.Count; i++)
            {
                string t = forms[i].Title != null ? forms[i].Title.Trim() : "";
                if (t.Length > 0 && !titles.Contains(t)) { titles.Add(t); }
            }
            List<W32WindowInfo> tops = Win32Driver.GetProcessTopWindows(Win32Driver.GetProcessId(appWindow));
            for (int i = 0; i < tops.Count; i++)
            {
                string t = tops[i].Title != null ? tops[i].Title.Trim() : "";
                if (t.Length > 0 && !titles.Contains(t)) { titles.Add(t); }
            }
            return titles.Count == 0 ? "(none)" : string.Join(" | ", titles.ToArray());
        }

        // The inquiry screen can be an MDI child OR its own top-level window, and its
        // caption differs between builds — mirror the entry-form search: title first,
        // then shape (a VB6 form that holds a search box and a result grid).
        private static IntPtr FindScreen(
            IntPtr appWindow, BotAppConfig app, List<FieldSelector> selectors)
        {
            List<IntPtr> candidates = CandidateWindows(appWindow);
            List<string> titles = CandidateTitles(app, selectors);

            for (int t = 0; t < titles.Count; t++)
            {
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (TitleMatches(Win32Driver.GetWindowTitle(candidates[i]), titles[t]))
                    {
                        return candidates[i];
                    }
                }
            }

            string entryTitle = app != null ? app.EntryWindowTitle : "";
            for (int i = 0; i < candidates.Count; i++)
            {
                if (LooksLikeLookup(candidates[i], entryTitle)) { return candidates[i]; }
            }
            return IntPtr.Zero;
        }

        private static List<IntPtr> CandidateWindows(IntPtr appWindow)
        {
            List<IntPtr> list = new List<IntPtr>();
            List<W32WindowInfo> forms = Win32Driver.GetFormWindows(appWindow, 6);
            for (int i = 0; i < forms.Count; i++)
            {
                if (!forms[i].Visible) { continue; }
                if (forms[i].Handle == appWindow) { continue; }
                if (!list.Contains(forms[i].Handle)) { list.Add(forms[i].Handle); }
            }

            List<W32WindowInfo> tops = Win32Driver.GetProcessTopWindows(Win32Driver.GetProcessId(appWindow));
            for (int i = 0; i < tops.Count; i++)
            {
                if (tops[i].Handle == appWindow) { continue; }
                if (!Win32Interop.IsWindowVisible(tops[i].Handle)) { continue; }
                if (!list.Contains(tops[i].Handle)) { list.Add(tops[i].Handle); }
            }
            return list;
        }

        // Configured title, menu leaf, the caption saved while mapping, then the default.
        private static List<string> CandidateTitles(BotAppConfig app, List<FieldSelector> selectors)
        {
            List<string> titles = new List<string>();
            if (app != null)
            {
                AddTitle(titles, app.LookupWindowTitle);
                if (app.LookupMenuPath != null && app.LookupMenuPath.Length > 0)
                {
                    AddTitle(titles, app.LookupMenuPath[app.LookupMenuPath.Length - 1]);
                }
            }
            if (selectors != null)
            {
                for (int i = 0; i < selectors.Count; i++)
                {
                    FieldSelector s = selectors[i];
                    if (s == null || !IsLookupKey(s.FieldKey)) { continue; }
                    AddTitle(titles, s.Screen);
                }
            }
            AddTitle(titles, DefaultTitle);
            AddTitle(titles, "استعلام");
            return titles;
        }

        private static void AddTitle(List<string> titles, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return; }
            string v = value.Trim();
            for (int i = 0; i < titles.Count; i++)
            {
                if (ArabicText.Contains(titles[i], v) && ArabicText.Contains(v, titles[i])) { return; }
            }
            titles.Add(v);
        }

        public static bool IsLookupKey(string fieldKey)
        {
            return string.Equals(fieldKey, SearchBoxKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, SearchButtonKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, GridKey, StringComparison.OrdinalIgnoreCase);
        }

        // Shape check used when no caption matches: a small VB6 form with an input,
        // never the MDI frame and never the (input-heavy) entry form.
        private static bool LooksLikeLookup(IntPtr window, string entryTitle)
        {
            if (window == IntPtr.Zero || !Win32Interop.IsWindow(window)) { return false; }
            if (!Win32Interop.IsWindowVisible(window)) { return false; }

            string cls = Win32Driver.GetClassName(window);
            if (string.IsNullOrEmpty(cls)) { return false; }
            if (cls.IndexOf("MDIClient", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }
            if (cls.IndexOf("ThunderRT6MDIForm", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }

            string title = Win32Driver.GetWindowTitle(window);
            if (!string.IsNullOrWhiteSpace(entryTitle) && TitleMatches(title, entryTitle)) { return false; }

            List<W32WindowInfo> kids = Win32Driver.GetChildren(window);
            int inputs = 0;
            for (int i = 0; i < kids.Count; i++)
            {
                string kind = FieldDiscovery.GetControlKind(kids[i].Class);
                if (kind == "TextBox" || kind == "ComboBox") { inputs++; }
            }
            // The entry form carries a dozen inputs; the inquiry screen only a few.
            return inputs >= 1 && inputs <= 6;
        }

        // «الإستعلام» vs «الاستعلام»: hamza spelling differs between builds.
        private static bool TitleMatches(string actual, string wanted)
        {
            return ArabicText.Contains(actual, wanted);
        }

        private enum LookupQueryRead
        {
            Skip = 0,
            Hit = 1,
            Miss = 2,
            Unreadable = 3
        }

        // Type the term, trigger the search, then look for it in the result grid.
        private static LookupQueryRead RunQuery(
            IntPtr screen, List<FieldSelector> selectors, string term, string expectedNid,
            Action<string, string> log, out string foundNin, out string foundCode12)
        {
            string[] alts;
            return RunQuery(screen, selectors, term, expectedNid, log,
                out foundNin, out foundCode12, out alts);
        }

        private static LookupQueryRead RunQuery(
            IntPtr screen, List<FieldSelector> selectors, string term, string expectedNid,
            Action<string, string> log, out string foundNin, out string foundCode12,
            out string[] foundAlts)
        {
            foundNin = "";
            foundCode12 = "";
            foundAlts = new string[0];
            FieldMap map = FieldDiscovery.GetFieldMap(screen, false);

            IntPtr box = ResolveOne(map, selectors, SearchBoxKey, "TextBox", log);
            if (box == IntPtr.Zero) { box = AutoFindSearchBox(map); }
            if (box == IntPtr.Zero)
            {
                log("lookup: search box not found — map «" + SearchBoxKey + "» in Setup", "WARN");
                return LookupQueryRead.Unreadable;
            }
            log("lookup: search box " + Describe(box), "INFO");

            if (!TypeSearchTerm(box, term, log)) { return LookupQueryRead.Unreadable; }

            IntPtr button = ResolveOne(map, selectors, SearchButtonKey, "Button", log);
            if (button == IntPtr.Zero) { button = AutoFindSearchButton(map); }
            if (button != IntPtr.Zero)
            {
                try { Win32Driver.ClickPhysical(button, 200); } catch { }
            }
            else
            {
                try { Win32Driver.SendShortcut(screen, "{ENTER}", 200); } catch { }
            }

            Thread.Sleep(GridSettleMs);

            LookupQueryRead fromBox = ReadSearchPrompt(screen, log);
            if (fromBox == LookupQueryRead.Miss)
            {
                return LookupQueryRead.Miss;
            }
            if (fromBox == LookupQueryRead.Hit)
            {
                return LookupQueryRead.Hit;
            }

            log("lookup: typed «" + term + "» box now «" +
                OneLine(Win32Driver.GetWindowText(box)) + "»", "INFO");

            IntPtr grid = ResolveGrid(map, selectors, box, button, log);
            IntPtr flex = FlexGridSurface.Find(screen, grid);
            if (flex != IntPtr.Zero)
            {
                log("lookup: MSHFlexGrid " + Describe(flex), "INFO");
                FlexGridSurface.DumpTable(flex, screen, "inquiry", log);
                string rowNid = "";
                string[] alts = new string[0];
                FlexGridSurface.TryReadCodes(
                    flex, screen, term, log, out foundNin, out foundCode12, out rowNid, out alts);
                foundAlts = alts != null ? alts : new string[0];
                if (foundCode12.Length == 0 && foundNin.Length == 0)
                {
                    string clip = FlexGridSurface.CopyCells(flex, screen, log);
                    OcrReading vote = OcrConsensus.Vote(clip, term);
                    if (vote.NinConfident) { foundNin = vote.Nin; }
                    if (vote.Code12Confident) { foundCode12 = vote.Code12; }
                    if (vote.NinCandidates != null && vote.NinCandidates.Length > 0)
                    {
                        foundAlts = vote.NinCandidates;
                    }
                    if (rowNid.Length == 0) { rowNid = FlexGridSurface.FirstNationalId(clip); }
                }
                if (!RowMatchesExpectedNid(expectedNid, rowNid, term, log))
                {
                    foundNin = "";
                    foundCode12 = "";
                    return LookupQueryRead.Miss;
                }
            }
            else
            {
                log("lookup: FlexGrid HWND not found — dumping form anyway", "WARN");
                FlexGridSurface.DumpTable(IntPtr.Zero, screen, "inquiry-no-grid", log);
            }
            if (foundCode12.Length == 0)
            {
                IntPtr surface = flex != IntPtr.Zero ? flex : grid;
                if (surface != IntPtr.Zero)
                {
                    try
                    {
                        Win32Driver.RectInfo r = Win32Driver.GetRect(surface);
                        List<string> acc = AccessibleText.HarvestRegion(
                            r.X, r.Y, r.W, Math.Min(r.H, 90));
                        string joined = string.Join("\t", acc.ToArray());
                        string extraNin = "";
                        string extra12 = "";
                        FlexGridSurface.ParseCodes(joined, term, out extraNin, out extra12);
                        if (extra12.Length > 0) { foundCode12 = extra12; }
                        if (foundNin.Length == 0 && extraNin.Length > 0) { foundNin = extraNin; }
                    }
                    catch { }
                }
            }
            if (foundCode12.Length == 0 && foundNin.Length == 0)
            {
                // No «لا يوجد» dialog but nothing readable either: we cannot tell a
                // real hit from an unpainted grid, and a pull without a code always
                // fails. Report Unreadable so the caller decides instead of guessing.
                log("lookup: no dialog and no readable row code — outcome is not decidable", "WARN");
                return LookupQueryRead.Unreadable;
            }

            // Coding shows «لا يوجد عملاء متشابهين» when missing. No that dialog
            // after a successful search means the client is on the table — go to سحب.
            log("lookup: no «لا يوجد» dialog after search → found" +
                (foundCode12.Length > 0 ? " CODE12=" + foundCode12 : "") +
                (foundNin.Length > 0 ? " NIN=" + foundNin : ""), "OK");
            return LookupQueryRead.Hit;
        }

        // Name hits must show the same 14-digit national id. Homonyms are common.
        private static bool RowMatchesExpectedNid(
            string expectedNid, string rowNid, string term, Action<string, string> log)
        {
            string want = expectedNid != null ? expectedNid.Trim() : "";
            string got = rowNid != null ? rowNid.Trim() : "";
            if (want.Length < 14) { return true; }
            // Garbage OCR «national ids» must not veto a real NID search hit.
            if (got.Length == 14 && !OcrConsensus.LooksLikeEgyptianNid(got))
            {
                if (log != null)
                {
                    log("lookup: ignoring OCR national-id soup «" + got + "»", "WARN");
                }
                got = "";
            }
            string typed = term != null ? term.Trim() : "";
            bool searchedByNid = string.Equals(want, typed, StringComparison.Ordinal);
            if (got.Length == 14 && !string.Equals(got, want, StringComparison.Ordinal))
            {
                if (log != null)
                {
                    log("lookup: row national id " + got + " ≠ sheet " + want +
                        " — different person", "WARN");
                }
                return false;
            }
            if (!searchedByNid && got.Length != 14)
            {
                if (log != null)
                {
                    log("lookup: name hit without a readable matching national id — ignored", "WARN");
                }
                return false;
            }
            return true;
        }

        // After search Coding often shows a message box instead of a readable grid.
        private static LookupQueryRead ReadSearchPrompt(IntPtr screen, Action<string, string> log)
        {
            int pid = screen != IntPtr.Zero ? Win32Driver.GetProcessId(screen) : 0;
            IntPtr dialog = ConfirmDialog.FindModal(pid, screen);
            if (dialog == IntPtr.Zero) { return LookupQueryRead.Skip; }

            string blob = (Win32Driver.GetWindowTitle(dialog) ?? "") + " " + ConfirmDialog.ReadMessage(dialog);
            string clicked;
            if (ArabicText.ContainsAny(blob, new string[]
                {
                    "لا يوجد عملاء", "لا يوجد عملاء متشابهين", "متشابهين مع بيانات",
                    "لا توجد نتائج", "لا يوجد بيانات", "not found"
                }))
            {
                ConfirmDialog.ClickAffirmative(dialog, out clicked);
                if (log != null)
                {
                    log("lookup: Coding said no match «" + blob.Trim() + "» → «" + clicked + "»", "OK");
                }
                return LookupQueryRead.Miss;
            }
            if (ArabicText.ContainsAny(blob, new string[]
                {
                    "العميل موجود من قبل", "موجود مسبقا", "تم العثور"
                }))
            {
                ConfirmDialog.ClickAffirmative(dialog, out clicked);
                if (log != null)
                {
                    log("lookup: Coding said duplicate «" + blob.Trim() + "»", "WARN");
                }
                return LookupQueryRead.Hit;
            }
            return LookupQueryRead.Skip;
        }

        // Frame wrappers often expose zero MSAA names; walk children, mapped
        // points, then clipboard. Whole-form harvest strips the search box so
        // the typed NationalId cannot be mistaken for a result row.
        private static List<string> HarvestGridTexts(
            IntPtr grid, IntPtr screen, IntPtr box, List<FieldSelector> selectors,
            string term, Action<string, string> log)
        {
            List<string> texts = new List<string>();
            List<string> boxTexts = box != IntPtr.Zero ? AccessibleText.Harvest(box) : new List<string>();
            if (box != IntPtr.Zero)
            {
                string typed = Win32Driver.GetWindowText(box);
                if (!string.IsNullOrWhiteSpace(typed)) { boxTexts.Add(typed.Trim()); }
            }

            IntPtr flex = FlexGridSurface.Find(screen, grid);
            if (flex == IntPtr.Zero) { flex = FindFlexGridHwnd(grid); }

            if (flex != IntPtr.Zero)
            {
                if (log != null)
                {
                    log("lookup: FlexGrid HWND " + Describe(flex), "INFO");
                }
                AccessibleText.Merge(texts, AccessibleText.HarvestDeep(flex));
            }

            if (grid != IntPtr.Zero && grid != flex && grid != screen && grid != box)
            {
                AccessibleText.Merge(texts, AccessibleText.HarvestDeep(grid));
            }

            int gx, gy, gw, gh;
            if (TryMappedGridRect(screen, selectors, out gx, out gy, out gw, out gh))
            {
                if (log != null)
                {
                    log("lookup: sampling mapped grid " + gx + "," + gy + " " + gw + "x" + gh, "INFO");
                }
                AccessibleText.Merge(texts, AccessibleText.HarvestRegion(gx, gy, gw, gh));
                IntPtr under = Win32Interop.WindowFromPoint(new POINT(gx + gw / 2, gy + Math.Min(40, gh / 4)));
                if (under != IntPtr.Zero && under != box && under != screen)
                {
                    AccessibleText.Merge(texts, AccessibleText.HarvestDeep(under));
                }
            }

            if (AccessibleText.FirstMatch(texts, term) != null)
            {
                return texts;
            }

            List<string> rest = AccessibleText.HarvestDeep(screen);
            AccessibleText.Exclude(rest, boxTexts);
            if (!string.IsNullOrEmpty(term))
            {
                AccessibleText.Exclude(rest, new List<string>(new string[] { term }));
            }
            AccessibleText.Merge(texts, rest);
            if (AccessibleText.FirstMatch(texts, term) != null)
            {
                return texts;
            }

            AccessibleText.Merge(texts, HarvestGridClipboard(screen, grid, flex, selectors, log));
            return texts;
        }

        private static IntPtr ResolveGrid(
            FieldMap map, List<FieldSelector> selectors, IntPtr box, IntPtr button,
            Action<string, string> log)
        {
            IntPtr grid = ResolveOne(map, selectors, GridKey, "", log);
            if (grid == IntPtr.Zero)
            {
                grid = ResolveGridByPosition(map, selectors, log);
            }
            if (grid == IntPtr.Zero) { grid = FlexGridSurface.Find(map != null ? map.Window : IntPtr.Zero); }
            if (grid == IntPtr.Zero) { grid = AutoFindGrid(map, box, button); }
            if (grid != IntPtr.Zero && log != null)
            {
                log("lookup: result surface " + Describe(grid), "INFO");
            }
            return grid;
        }

        private static IntPtr ResolveGridByPosition(
            FieldMap map, List<FieldSelector> selectors, Action<string, string> log)
        {
            if (selectors == null) { return IntPtr.Zero; }
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                if (s == null || !s.HasPosition) { continue; }
                if (!string.Equals(s.FieldKey, GridKey, StringComparison.OrdinalIgnoreCase)) { continue; }
                ResolveResult r = FieldDiscovery.ResolveByPosition(map, s, true);
                if (r.Handle != IntPtr.Zero)
                {
                    if (log != null)
                    {
                        log("lookup: «" + GridKey + "» resolved by " + r.Tier + " ignore-kind", "INFO");
                    }
                    return r.Handle;
                }
            }
            return IntPtr.Zero;
        }

        private static IntPtr FindFlexGridHwnd(IntPtr root)
        {
            return FlexGridSurface.Find(root);
        }

        private static bool TryMappedGridRect(
            IntPtr screen, List<FieldSelector> selectors, out int x, out int y, out int w, out int h)
        {
            x = 0; y = 0; w = 0; h = 0;
            if (screen == IntPtr.Zero || selectors == null) { return false; }
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                if (s == null || !s.HasPosition) { continue; }
                if (!string.Equals(s.FieldKey, GridKey, StringComparison.OrdinalIgnoreCase)) { continue; }
                int rx, ry, rw, rh;
                if (!int.TryParse(s.PosX, out rx) || !int.TryParse(s.PosY, out ry)) { continue; }
                if (!int.TryParse(s.PosW, out rw) || !int.TryParse(s.PosH, out rh)) { continue; }
                if (rw < 40 || rh < 40) { continue; }
                try
                {
                    Win32Driver.RectInfo form = Win32Driver.GetRect(screen);
                    x = form.X + rx;
                    y = form.Y + ry;
                    w = rw;
                    h = rh;
                    return true;
                }
                catch { return false; }
            }
            return false;
        }

        // Click a data row then Ctrl+C — MSFlexGrid copies the current cell/row.
        private static List<string> HarvestGridClipboard(
            IntPtr screen, IntPtr grid, IntPtr flex, List<FieldSelector> selectors,
            Action<string, string> log)
        {
            List<string> texts = new List<string>();
            int cx, cy;
            if (!TryGridClickPoint(screen, grid, flex, selectors, out cx, out cy)) { return texts; }

            try
            {
                try { Win32Driver.SetForeground(screen); } catch { }
                Win32Driver.ClickScreen(cx, cy, 180);
                try { Win32Driver.SendShortcut(screen, "^a", 160); } catch { }
                Win32Driver.SendShortcut(screen, "^c", 280);
                string clip = "";
                try { clip = System.Windows.Forms.Clipboard.GetText(); }
                catch { clip = ""; }
                if (string.IsNullOrWhiteSpace(clip)) { return texts; }

                texts.Add(clip.Trim());
                string[] lines = clip.Split(new char[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length > 0) { texts.Add(line); }
                }
                if (log != null)
                {
                    log("lookup: clipboard from grid (" + clip.Trim().Length + " chars)", "INFO");
                }
            }
            catch (Exception ex)
            {
                if (log != null) { log("lookup: clipboard harvest failed — " + ex.Message, "WARN"); }
            }
            return texts;
        }

        private static bool TryGridClickPoint(
            IntPtr screen, IntPtr grid, IntPtr flex, List<FieldSelector> selectors,
            out int x, out int y)
        {
            x = 0; y = 0;
            IntPtr hwnd = flex != IntPtr.Zero ? flex : grid;
            if (hwnd != IntPtr.Zero)
            {
                try
                {
                    Win32Driver.RectInfo r = Win32Driver.GetRect(hwnd);
                    if (r.W >= 40 && r.H >= 40)
                    {
                        x = r.X + Math.Min(80, r.W / 3);
                        y = r.Y + Math.Min(36, Math.Max(18, r.H / 8));
                        return true;
                    }
                }
                catch { }
            }
            int gx, gy, gw, gh;
            if (TryMappedGridRect(screen, selectors, out gx, out gy, out gw, out gh))
            {
                x = gx + Math.Min(80, gw / 3);
                y = gy + Math.Min(36, Math.Max(18, gh / 8));
                return true;
            }
            return false;
        }

        // Numbered dump of every harvested cell — truncated for long grids.
        private static void LogTableContents(List<string> texts, string term, Action<string, string> log)
        {
            if (log == null || texts == null) { return; }

            int show = texts.Count < 30 ? texts.Count : 30;
            log("lookup: table dump — " + texts.Count + " cell(s), search=«" + term + "»", "INFO");
            for (int i = 0; i < show; i++)
            {
                string line = OneLine(texts[i]);
                if (line.Length == 0) { continue; }
                log("lookup:   [" + (i + 1) + "] " + line, "INFO");
            }
            if (texts.Count > show)
            {
                log("lookup:   … +" + (texts.Count - show) + " more cell(s)", "INFO");
            }
        }

        // Result cells are multi-line ("محمد يوسف ابراهيم خليل" + "حساب راكد"); keep the
        // log on one line so the run log stays readable.
        private static string OneLine(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            string t = value.Replace("\r", " ").Replace("\n", " / ").Replace("\t", " ").Trim();
            while (t.IndexOf("  ", StringComparison.Ordinal) >= 0) { t = t.Replace("  ", " "); }
            return t.Length > 160 ? t.Substring(0, 160) + "…" : t;
        }

        // WM_SETTEXT alone is silently ignored by some VB6 inputs — verify, then
        // fall back to real keystrokes so an empty search box never goes unnoticed.
        private static bool TypeSearchTerm(IntPtr box, string term, Action<string, string> log)
        {
            try { Win32Driver.SetForeground(box); } catch { }
            try { Win32Driver.ClickPhysical(box, 120); } catch { }
            Win32Driver.SetText(box, "");
            Thread.Sleep(60);
            Win32Driver.SetText(box, term);
            Thread.Sleep(150);

            if (ReadBack(box) == term) { return true; }

            log("lookup: WM_SETTEXT did not stick — retrying with keystrokes", "WARN");
            try
            {
                Win32Driver.SetForeground(box);
                Win32Driver.ClickPhysical(box, 120);
                Win32Driver.SendShortcut(box, "^a", 60);
                Win32Driver.SendShortcut(box, "{DEL}", 60);
                Win32Driver.SendShortcut(box, EscapeForSendKeys(term), 250);
            }
            catch (Exception ex)
            {
                log("lookup: typing failed — " + ex.Message, "WARN");
                return false;
            }

            string actual = ReadBack(box);
            if (actual == term) { return true; }

            log("lookup: search box still holds '" + actual + "' instead of '" + term +
                "' — map «" + SearchBoxKey + "» by clicking it in Setup", "ERROR");
            return false;
        }

        private static string ReadBack(IntPtr box)
        {
            string t = Win32Driver.GetWindowText(box);
            if (string.IsNullOrEmpty(t)) { t = Win32Interop.GetWindowTextValue(box); }
            return t != null ? t.Trim() : "";
        }

        // SendKeys treats these as command characters.
        private static string EscapeForSendKeys(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ("+^%~(){}[]".IndexOf(c) >= 0) { sb.Append('{').Append(c).Append('}'); }
                else { sb.Append(c); }
            }
            return sb.ToString();
        }

        private static string Describe(IntPtr handle)
        {
            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(handle);
                return Win32Driver.GetClassName(handle) +
                    " @" + r.X + "," + r.Y + " " + r.W + "x" + r.H;
            }
            catch { return "hwnd=" + handle; }
        }

        // An explicitly clicked selector wins over the expected kind: VB6 search boxes
        // are sometimes reported as ComboBox/Control, and refusing them broke the search.
        private static IntPtr ResolveOne(
            FieldMap map,
            List<FieldSelector> selectors,
            string fieldKey,
            string expectedKind,
            Action<string, string> log)
        {
            if (selectors == null) { return IntPtr.Zero; }
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                if (s == null) { continue; }
                if (!string.Equals(s.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase)) { continue; }
                if (string.IsNullOrWhiteSpace(s.Class) && !s.HasPosition) { return IntPtr.Zero; }

                ResolveResult r = FieldDiscovery.ResolveField(map, s, "Index");
                if (r.Handle == IntPtr.Zero && s.HasPosition)
                {
                    r = FieldDiscovery.ResolveByPosition(map, s, true);
                }
                if (log != null)
                {
                    if (r.Handle == IntPtr.Zero)
                    {
                        log("lookup: «" + fieldKey + "» mapped but not resolved (" + r.Note +
                            ") — أعد التقاطه بنفس حجم الشاشة", "WARN");
                    }
                    else
                    {
                        bool kindMismatch = !string.IsNullOrEmpty(expectedKind) &&
                            !string.IsNullOrWhiteSpace(s.Kind) &&
                            !string.Equals(s.Kind, expectedKind, StringComparison.OrdinalIgnoreCase);
                        log("lookup: «" + fieldKey + "» resolved by " + r.Tier +
                            (kindMismatch ? " (kind=" + s.Kind + ")" : ""), "INFO");
                    }
                }
                return r.Handle;
            }
            return IntPtr.Zero;
        }

        // The inquiry screen holds a single input — safe to pick the widest one.
        private static IntPtr AutoFindSearchBox(FieldMap map)
        {
            if (map == null || map.Fields == null) { return IntPtr.Zero; }
            IntPtr best = IntPtr.Zero;
            int bestW = 0;
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (!f.Visible || f.Kind != "TextBox") { continue; }
                if (f.W > bestW) { bestW = f.W; best = f.Handle; }
            }
            return best;
        }

        private static IntPtr AutoFindSearchButton(FieldMap map)
        {
            if (map == null || map.Fields == null) { return IntPtr.Zero; }
            IntPtr best = IntPtr.Zero;
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (!f.Visible || f.Kind != "Button") { continue; }
                string cap = f.Caption != null ? f.Caption.Replace("&", "").Trim() : "";
                if (ConfirmDialog.IsNegative(cap)) { continue; }
                if (ArabicText.ContainsAny(cap, new string[] { "بحث", "استعلام" }))
                {
                    return f.Handle;
                }
                // Icon-only search button: no caption, keep the first as a fallback.
                if (cap.Length == 0 && best == IntPtr.Zero) { best = f.Handle; }
            }
            return best;
        }

        // The result grid is the one big drawing surface on the screen: pick the widest
        // and tallest control that is neither the search box nor the search button.
        private static IntPtr AutoFindGrid(FieldMap map, IntPtr box, IntPtr button)
        {
            if (map == null) { return IntPtr.Zero; }

            IntPtr named = FlexGridSurface.Find(map.Window);
            if (named != IntPtr.Zero) { return named; }

            List<AppField> pool = map.AllControls != null ? map.AllControls : map.Fields;
            if (pool == null) { return IntPtr.Zero; }

            IntPtr best = IntPtr.Zero;
            long bestArea = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                AppField f = pool[i];
                if (!f.Visible || f.Handle == box || f.Handle == button) { continue; }
                if (f.Kind == "TextBox" || f.Kind == "Button" || f.Kind == "CheckBox") { continue; }
                if (f.W < MinGridWidth || f.H < MinGridHeight) { continue; }

                if (LooksLikeGrid(f.Class) || f.Kind == "Grid") { return f.Handle; }

                long area = (long)f.W * f.H;
                if (area > bestArea) { bestArea = area; best = f.Handle; }
            }
            return best;
        }

        private static bool LooksLikeGrid(string className)
        {
            if (string.IsNullOrEmpty(className)) { return false; }
            return Regex.IsMatch(className,
                "(?i)(grid|listview|SysListView32|SysHeader|MSFlex|MSHFlex|VSFlex)");
        }

        private static void CloseScreen(IntPtr screen, Action<string, string> log)
        {
            if (screen == IntPtr.Zero) { return; }
            CloseWindow(screen, "lookup screen", log);
        }

        private static string Join(string[] path)
        {
            if (path == null || path.Length == 0) { return "(default)"; }
            return string.Join(" → ", path);
        }
    }
}
