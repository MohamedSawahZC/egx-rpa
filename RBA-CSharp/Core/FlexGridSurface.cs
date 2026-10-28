// =============================================================================
//  FlexGridSurface.cs - locate and copy VB6 MSHFlexGrid / MSFlexGrid cells.
//
//  Operator dumps showed the inquiry table is MSHFlexGridWndClass sitting inside
//  Frame «البحث». MSAA on the Frame/form is empty; the HWND of the FlexGrid is
//  the control to click and copy from.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace RbaCore
{
    public static class FlexGridSurface
    {
        public static bool IsFlexClass(string className)
        {
            if (string.IsNullOrEmpty(className)) { return false; }
            return Regex.IsMatch(className,
                "(?i)(MSHFlexGridWndClass|MSFlexGridWndClass|VSFlexGrid|DataGridWndClass|FlexGrid)");
        }

        // Prefer a real FlexGrid HWND under the form (or under a Frame the operator clicked).
        public static IntPtr Find(IntPtr root)
        {
            return Find(root, IntPtr.Zero);
        }

        public static IntPtr Find(IntPtr root, IntPtr preferred)
        {
            if (preferred != IntPtr.Zero && Win32Interop.IsWindow(preferred) &&
                IsFlexClass(Win32Driver.GetClassName(preferred)))
            {
                return preferred;
            }
            if (preferred != IntPtr.Zero)
            {
                IntPtr nested = LargestFlex(preferred);
                if (nested != IntPtr.Zero) { return nested; }
            }
            if (root != IntPtr.Zero) { return LargestFlex(root); }
            return IntPtr.Zero;
        }

        // «سحب الأكواد» grid: col0 سحب | col1 اسم+جنسية | col2 كود موحد 12 | col3 شكل قانوني/فرعي.
        // سحب الأكواد uses ClickSahbCell (or a saved click point). ClickTableOnce stays for post-save confirm.
        public static bool ClickSahbCell(IntPtr flex, Action<string, string> log)
        {
            if (flex == IntPtr.Zero || !Win32Interop.IsWindow(flex)) { return false; }
            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(flex);
                if (r.W < 20 || r.H < 20) { return false; }
                int x = r.X + Math.Min(22, Math.Max(10, r.W / 18));
                int y = r.Y + Math.Min(38, Math.Max(24, r.H / 9));
                try { Win32Driver.SetForeground(flex); } catch { }
                Win32Driver.ClickScreen(x, y, 180);
                if (log != null)
                {
                    log("flexgrid: clicked «سحب» cell @" + x + "," + y + " " + Describe(flex), "INFO");
                }
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) { log("flexgrid: click failed — " + ex.Message, "WARN"); }
                return false;
            }
        }

        // One click on the table body (first data row centre) — NOT the «سحب» column.
        // Fixed operator sequence: زر البحث → الجدول → F2 → F9.
        public static bool ClickTableOnce(IntPtr flex, Action<string, string> log)
        {
            if (flex == IntPtr.Zero || !Win32Interop.IsWindow(flex)) { return false; }
            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(flex);
                if (r.W < 40 || r.H < 40) { return false; }
                // Horizontal centre of the grid (away from col0 سحب); first data row band.
                int x = r.X + (r.W / 2);
                int y = r.Y + Math.Min(48, Math.Max(28, r.H / 8));
                try { Win32Driver.SetForeground(flex); } catch { }
                Win32Driver.ClickScreen(x, y, 200);
                if (log != null)
                {
                    log("flexgrid: table clicked once @" + x + "," + y + " " + Describe(flex), "INFO");
                }
                return true;
            }
            catch (Exception ex)
            {
                if (log != null) { log("flexgrid: table click failed — " + ex.Message, "ERROR"); }
                return false;
            }
        }

        // Prefer the 12-digit CODE12. Inquiry often paints only الكود الموحد (6–9);
        // that is what the operator asked to type into «سحب الأكواد» when CODE12
        // is not on the row. Never use the 14-digit national id.
        public static string AsPullSearchCode(string value)
        {
            string d = DigitsOnly(value);
            return LooksLikeBrokerCode12(d) ? d : "";
        }

        // Operator-typed الكود الموحد: any digit length (not OCR).
        public static string AsOperatorPullCode(string value)
        {
            string d = DigitsOnly(value);
            return d.Length > 0 ? d : "";
        }

        // Live inquiry CODE12 for this broker is 12 digits starting with 0 (e.g. 01095…).
        // OCR often glues الكود الموحد into a fake 12-digit that starts with 5.
        public static bool LooksLikeBrokerCode12(string digits)
        {
            if (string.IsNullOrEmpty(digits) || digits.Length != 12) { return false; }
            if (digits[0] != '0') { return false; }
            return UniqueDigitCount(digits) >= 4;
        }

        public static string AsUnifiedCode(string value)
        {
            string d = DigitsOnly(value);
            if (d.Length >= 6 && d.Length <= 9) { return d; }
            return "";
        }

        public static string PreferPullSearchCode(string code12, string nin)
        {
            string a = AsPullSearchCode(code12);
            if (a.Length == 12) { return a; }
            a = AsPullSearchCode(nin);
            if (a.Length == 12) { return a; }
            string n = AsUnifiedCode(nin);
            if (n.Length >= 6) { return n; }
            return AsUnifiedCode(code12);
        }

        public static void ClickFirstDataRow(IntPtr flex, IntPtr screen, Action<string, string> log)
        {
            if (flex == IntPtr.Zero || !Win32Interop.IsWindow(flex)) { return; }
            try
            {
                Win32Driver.RectInfo g = Win32Driver.GetRect(flex);
                Win32Driver.RectInfo f = (screen != IntPtr.Zero && Win32Interop.IsWindow(screen))
                    ? Win32Driver.GetRect(screen) : g;
                int x1 = Math.Max(g.X, f.X + 8);
                int x2 = Math.Min(g.X + g.W, f.X + f.W) - 8;
                int y1 = Math.Max(g.Y, f.Y + 8);
                int y2 = Math.Min(g.Y + g.H, f.Y + f.H);
                int w = x2 - x1;
                int h = y2 - y1;
                if (w < 40 || h < 30) { return; }
                int x = x1 + Math.Max(40, w / 3);
                int y = y1 + Math.Min(42, Math.Max(28, h / 8));
                if (screen != IntPtr.Zero) { Win32Driver.SetForeground(screen); }
                Win32Driver.ClickScreen(x, y, 120);
                if (log != null)
                {
                    log("flexgrid: clicked first data row @" + x + "," + y, "INFO");
                }
            }
            catch (Exception ex)
            {
                if (log != null) { log("flexgrid: row click — " + ex.Message, "WARN"); }
            }
        }

        public static bool TryReadCodes(
            IntPtr flex, IntPtr screen, string searchTerm, Action<string, string> log,
            out string nin, out string code12)
        {
            string rowNid;
            string[] alts;
            return TryReadCodes(flex, screen, searchTerm, log, out nin, out code12, out rowNid, out alts);
        }

        public static bool TryReadCodes(
            IntPtr flex, IntPtr screen, string searchTerm, Action<string, string> log,
            out string nin, out string code12, out string rowNid)
        {
            string[] alts;
            return TryReadCodes(flex, screen, searchTerm, log, out nin, out code12, out rowNid, out alts);
        }

        public static bool TryReadCodes(
            IntPtr flex, IntPtr screen, string searchTerm, Action<string, string> log,
            out string nin, out string code12, out string rowNid, out string[] ninCandidates)
        {
            nin = "";
            code12 = "";
            ninCandidates = new string[0];
            string clip = DigitOcr.Harvest(flex, screen, log);
            rowNid = FirstNationalId(clip);

            // Only codes that several OCR passes agree on may reach the search box.
            OcrReading vote = OcrConsensus.Vote(clip, searchTerm != null ? searchTerm : "");
            if (vote.NinConfident) { nin = vote.Nin; }
            if (vote.Code12Confident) { code12 = vote.Code12; }
            if (vote.NinCandidates != null && vote.NinCandidates.Length > 0)
            {
                ninCandidates = vote.NinCandidates;
            }
            else if (nin.Length > 0)
            {
                ninCandidates = new string[] { nin };
            }
            if (log != null)
            {
                log("flexgrid: OCR vote " + vote.Describe() +
                    (ninCandidates.Length > 1
                        ? " alts=[" + string.Join(",", ninCandidates) + "]"
                        : ""),
                    "INFO");
            }

            if (nin.Length == 0 && code12.Length == 0 && vote.Any && log != null)
            {
                log("flexgrid: rejecting low-confidence OCR (" + vote.Describe() +
                    ") — a single pass is not enough to search with", "WARN");
            }

            if (log != null)
            {
                log("flexgrid: live codes NIN=" +
                    (nin.Length > 0 ? nin : "(none)") +
                    " CODE12=" + (code12.Length > 0 ? code12 : "(none)") +
                    (rowNid.Length > 0 ? " NID=" + rowNid : ""),
                    code12.Length == 12 || nin.Length >= 6 ? "OK" : "WARN");
            }
            return code12.Length == 12 || nin.Length >= 6;
        }

        private static bool HasNationalId(string text)
        {
            return !string.IsNullOrEmpty(text) &&
                   Regex.IsMatch(text, @"[0-9\u0660-\u0669]{14}");
        }

        public static string FirstNationalId(string clip)
        {
            if (string.IsNullOrWhiteSpace(clip)) { return ""; }
            MatchCollection ms = Regex.Matches(clip, @"[0-9\u0660-\u0669]{14}");
            for (int i = 0; i < ms.Count; i++)
            {
                string d = DigitsOnly(ms[i].Value);
                // OCR soup often invents 00111111010100 — never treat that as a person.
                if (OcrConsensus.LooksLikeEgyptianNid(d)) { return d; }
            }
            return "";
        }

        // Inquiry «الإستعلام» paints cells; MSAA/clipboard are empty. Read TextMatrix
        // through the VB6 NativeOM, then sample the visible RTL band where كود 12 sits.
        public static string CopyCells(IntPtr flex, IntPtr screen, Action<string, string> log)
        {
            if (flex == IntPtr.Zero || !Win32Interop.IsWindow(flex)) { return ""; }

            StringBuilder blob = new StringBuilder();
            string ocr = DigitOcr.Harvest(flex, screen, log);
            if (!string.IsNullOrWhiteSpace(ocr)) { blob.Append(ocr); }
            if (HasCode12(blob.ToString())) { return FinishHarvest(blob, log); }
            string bar = HarvestStatusBar(screen, log);
            if (!string.IsNullOrWhiteSpace(bar)) { blob.Append('\t').Append(bar); }
            if (HasCode12(blob.ToString())) { return FinishHarvest(blob, log); }
            string inplace = HarvestInPlace(flex, screen, log);
            if (!string.IsNullOrWhiteSpace(inplace)) { blob.Append('\t').Append(inplace); }
            if (HasCode12(blob.ToString())) { return FinishHarvest(blob, log); }
            string com = ReadComCells(flex, log);
            if (!string.IsNullOrWhiteSpace(com)) { blob.Append(com); }
            List<string> forced = AccessibleText.HarvestForcedIds(flex, 200, log);
            for (int i = 0; i < forced.Count; i++)
            {
                blob.Append('\t').Append(forced[i]);
            }
            if (HasCode12(blob.ToString())) { return FinishHarvest(blob, log); }
            if (screen != IntPtr.Zero && screen != flex)
            {
                List<string> formForced = AccessibleText.HarvestForcedIds(screen, 80, log);
                for (int i = 0; i < formForced.Count; i++)
                {
                    blob.Append('\t').Append(formForced[i]);
                }
            }
            if (HasCode12(blob.ToString())) { return FinishHarvest(blob, log); }
            HarvestVisibleRtlAcc(flex, screen, blob, log);
            if (HasCode12(blob.ToString())) { return FinishHarvest(blob, log); }
            if (log != null)
            {
                log("flexgrid: skip clipboard sweep — this MSHFlexGrid does not copy cells", "INFO");
            }
            return FinishHarvest(blob, log);
        }

        // Full debug dump: HWND tree, COM cells, MSAA, search-box text.
        public static void DumpTable(IntPtr flex, IntPtr screen, string tag, Action<string, string> log)
        {
            if (log == null) { return; }
            log("dump[" + tag + "]: ---- table ----", "INFO");
            if (screen != IntPtr.Zero)
            {
                log("dump[" + tag + "]: form " + Describe(screen) +
                    " title=«" + Win32Driver.GetWindowTitle(screen) + "»", "INFO");
                List<W32WindowInfo> kids = Win32Driver.GetChildren(screen);
                log("dump[" + tag + "]: children=" + kids.Count, "INFO");
                int n = kids.Count < 20 ? kids.Count : 20;
                for (int i = 0; i < n; i++)
                {
                    W32WindowInfo k = kids[i];
                    string txt = "";
                    try { txt = Win32Driver.GetWindowText(k.Handle); } catch { }
                    string pos = "";
                    try
                    {
                        Win32Driver.RectInfo r = Win32Driver.GetRect(k.Handle);
                        pos = " @" + r.X + "," + r.Y + " " + r.W + "x" + r.H;
                    }
                    catch { }
                    log("dump[" + tag + "]:  [" + (i + 1) + "] " +
                        (k.Class != null ? k.Class : "") + pos +
                        " vis=" + k.Visible + " text=«" + OneLine(txt, 80) + "»", "INFO");
                }
            }
            if (flex == IntPtr.Zero || !Win32Interop.IsWindow(flex))
            {
                log("dump[" + tag + "]: FlexGrid HWND missing", "WARN");
                return;
            }
            log("dump[" + tag + "]: grid " + Describe(flex), "INFO");
            string com = ReadComCells(flex, log);
            log("dump[" + tag + "]: COM blob " +
                (string.IsNullOrWhiteSpace(com) ? "(empty)" : OneLine(com, 240)), "INFO");
            List<string> acc = AccessibleText.HarvestDeep(flex);
            log("dump[" + tag + "]: MSAA flex count=" + acc.Count, "INFO");
            int aMax = acc.Count < 30 ? acc.Count : 30;
            for (int i = 0; i < aMax; i++)
            {
                log("dump[" + tag + "]:  MSAA[" + (i + 1) + "] «" + OneLine(acc[i], 120) + "»", "INFO");
            }
            if (screen != IntPtr.Zero)
            {
                List<string> formAcc = AccessibleText.HarvestDeep(screen);
                log("dump[" + tag + "]: MSAA form count=" + formAcc.Count, "INFO");
                int fMax = formAcc.Count < 30 ? formAcc.Count : 30;
                for (int i = 0; i < fMax; i++)
                {
                    log("dump[" + tag + "]:  formMSAA[" + (i + 1) + "] «" + OneLine(formAcc[i], 120) + "»", "INFO");
                }
            }
            List<string> forced = AccessibleText.HarvestForcedIds(flex, 200, log);
            log("dump[" + tag + "]: forced-id cells=" + forced.Count, "INFO");
            string ocr = DigitOcr.Harvest(flex, screen, log);
            log("dump[" + tag + "]: ocr " +
                (string.IsNullOrWhiteSpace(ocr) ? "(empty)" : OneLine(ocr, 160)), "INFO");
        }

        private static string OneLine(string value, int max)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            string s = value.Replace("\r", " ").Replace("\n", " ").Trim();
            if (max > 0 && s.Length > max) { return s.Substring(0, max) + "..."; }
            return s;
        }

        // Inquiry paints الكود الموحد (6–9) and a 14-digit national id. A 12-digit
        // hit next to a 14-digit NID is a misread, not CODE12.
        public static void ParseCodes(string clip, string searchTerm, out string nin, out string code12)
        {
            nin = "";
            code12 = "";
            if (string.IsNullOrWhiteSpace(clip)) { return; }

            string searchDigits = DigitsOnly(searchTerm);
            bool hasNid = HasNationalId(clip);
            TakeCodes(clip, searchDigits, hasNid, ref nin, ref code12);

            string[] parts = clip.Split(new char[] { '\r', '\n', '\t', ' ', '/', '|', ',', ';' },
                StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                // The 14-digit veto is per token: the inquiry row paints the national
                // id next to a real CODE12, and a blanket veto dropped valid codes.
                TakeCodes(parts[i], searchDigits, HasNationalId(parts[i]), ref nin, ref code12);
            }
            StitchUnified(parts, ref nin);
            if (!LooksLikeBrokerCode12(code12) ||
                (nin.Length >= 6 && code12.IndexOf(nin, StringComparison.Ordinal) >= 0))
            {
                code12 = "";
            }
        }

        public static IntPtr AutoSearchBox(FieldMap map)
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

        public static IntPtr AutoSearchButton(FieldMap map)
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
                if (cap.Length == 0 && best == IntPtr.Zero) { best = f.Handle; }
            }
            return best;
        }

        private static IntPtr LargestFlex(IntPtr root)
        {
            if (root == IntPtr.Zero || !Win32Interop.IsWindow(root)) { return IntPtr.Zero; }
            if (IsFlexClass(Win32Driver.GetClassName(root))) { return root; }

            List<W32WindowInfo> kids = Win32Driver.GetChildren(root);
            IntPtr best = IntPtr.Zero;
            long bestArea = 0;
            for (int i = 0; i < kids.Count; i++)
            {
                if (!kids[i].Visible) { continue; }
                if (!IsFlexClass(kids[i].Class)) { continue; }
                try
                {
                    Win32Driver.RectInfo r = Win32Driver.GetRect(kids[i].Handle);
                    long area = (long)r.W * r.H;
                    if (area > bestArea) { bestArea = area; best = kids[i].Handle; }
                }
                catch { }
            }
            return best;
        }

        // VB6 MSHFlexGrid cells live on the COM TextMatrix, not in child HWNDs.
        public static string ReadComCells(IntPtr flex, Action<string, string> log)
        {
            object com = GetFlexDispatch(flex, log);
            if (com == null)
            {
                if (log != null) { log("flexgrid: NativeOM IDispatch not available", "WARN"); }
                return "";
            }
            if (LooksLikeAccessible(com))
            {
                if (log != null) { log("flexgrid: COM object is IAccessible, not the FlexGrid", "WARN"); }
                return "";
            }

            StringBuilder sb = new StringBuilder();
            int maxR = TryIntProp(com, "Rows", 6);
            int maxC = TryIntProp(com, "Cols", 16);
            int fixedRows = TryIntProp(com, "FixedRows", 1);
            if (maxR < 2) { maxR = 6; }
            if (maxC < 2) { maxC = 16; }
            int startR = fixedRows > 0 && fixedRows < maxR ? fixedRows : 1;
            if (log != null)
            {
                log("flexgrid: COM scan rows=" + maxR + " cols=" + maxC +
                    " startR=" + startR, "INFO");
            }

            int filled = 0;
            int empty = 0;
            for (int r = 0; r < maxR; r++)
            {
                for (int c = 0; c < maxC; c++)
                {
                    string cell = ReadTextMatrix(com, r, c);
                    if (string.IsNullOrWhiteSpace(cell)) { empty++; continue; }
                    filled++;
                    if (log != null)
                    {
                        log("flexgrid: cell[" + r + "," + c + "] «" + OneLine(cell.Trim(), 80) + "»", "INFO");
                    }
                    sb.Append(cell.Trim()).Append('\t');
                }
            }
            if (log != null)
            {
                log("flexgrid: TextMatrix filled=" + filled + " empty=" + empty, "INFO");
            }
            try
            {
                object clip = GetProp(com, "Clip");
                if (clip != null && !string.IsNullOrWhiteSpace(clip.ToString()))
                {
                    sb.Append('\t').Append(clip.ToString().Trim());
                }
            }
            catch { }
            WalkCellText(com, startR, maxR, maxC, sb, log);
            return sb.ToString();
        }

        // Painted cells only — no click, so the inquiry highlight stays for F6.
        private static void HarvestVisibleRtlAcc(
            IntPtr flex, IntPtr screen, StringBuilder blob, Action<string, string> log)
        {
            int x, y, w, h;
            if (!VisibleIntersect(flex, screen, out x, out y, out w, out h)) { return; }
            if (log != null)
            {
                log("flexgrid: MSAA point-scan @" + x + "," + y + " " + w + "x" + h, "INFO");
            }

            int yRow = y + Math.Min(110, Math.Max(70, h / 5));
            int[] yOff = new int[] { 0, 18, 36 };
            for (int yi = 0; yi < yOff.Length; yi++)
            {
                int py = yRow + yOff[yi];
                if (py >= y + h - 6) { break; }
                for (int i = 0; i < 8; i++)
                {
                    int px = x + w - 18 - (i * 72);
                    if (px < x + 8) { break; }
                    List<string> acc = AccessibleText.HarvestAtPoint(px, py);
                    for (int a = 0; a < acc.Count; a++)
                    {
                        if (string.IsNullOrWhiteSpace(acc[a])) { continue; }
                        blob.Append('\t').Append(acc[a].Trim());
                        if (log != null)
                        {
                            log("flexgrid: point @" + px + "," + py + " «" +
                                OneLine(acc[a].Trim(), 80) + "»", "INFO");
                        }
                    }
                    if (HasCode12(blob.ToString())) { return; }
                }
            }
        }

        private static string HarvestInPlace(IntPtr flex, IntPtr screen, Action<string, string> log)
        {
            int x, y, w, h;
            if (!VisibleIntersect(flex, screen, out x, out y, out w, out h)) { return ""; }
            int yRow = y + Math.Min(110, Math.Max(90, h / 4));
            int[] xOff = new int[] { 160, 240, 90, 320 };
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < xOff.Length; i++)
            {
                int px = x + w - xOff[i];
                if (px < x + 8) { continue; }
                try
                {
                    Win32Driver.SetForeground(flex);
                    Win32Driver.ClickScreen(px, yRow, 120);
                    Win32Driver.ClickScreen(px, yRow, 140);
                }
                catch { continue; }
                string cell = ReadEditNear(flex, screen, y);
                if (log != null && !string.IsNullOrWhiteSpace(cell))
                {
                    log("flexgrid: in-place @" + px + "," + yRow + " «" + OneLine(cell, 80) + "»", "INFO");
                }
                if (!string.IsNullOrWhiteSpace(cell)) { sb.Append('\t').Append(cell); }
                if (HasCode12(sb.ToString())) { return sb.ToString(); }
            }
            return sb.ToString();
        }

        private static string ReadEditNear(IntPtr flex, IntPtr screen, int gridY)
        {
            StringBuilder sb = new StringBuilder();
            List<IntPtr> roots = new List<IntPtr>();
            roots.Add(flex);
            if (screen != IntPtr.Zero && screen != flex) { roots.Add(screen); }
            for (int r = 0; r < roots.Count; r++)
            {
                List<W32WindowInfo> kids = Win32Driver.GetChildren(roots[r]);
                for (int i = 0; i < kids.Count; i++)
                {
                    string cls = kids[i].Class != null ? kids[i].Class : "";
                    bool edit = cls.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) >= 0
                        || cls.IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!edit) { continue; }
                    try
                    {
                        Win32Driver.RectInfo rr = Win32Driver.GetRect(kids[i].Handle);
                        if (rr.Y + rr.H < gridY - 6) { continue; }
                        string t = Win32Driver.GetWindowText(kids[i].Handle);
                        if (string.IsNullOrWhiteSpace(t))
                        {
                            t = Win32Interop.GetWindowTextValue(kids[i].Handle);
                        }
                        if (!string.IsNullOrWhiteSpace(t)) { sb.Append('\t').Append(t.Trim()); }
                    }
                    catch { }
                }
            }
            return sb.ToString();
        }

        private static string HarvestStatusBar(IntPtr screen, Action<string, string> log)
        {
            if (screen == IntPtr.Zero || !Win32Interop.IsWindow(screen)) { return ""; }
            StringBuilder sb = new StringBuilder();
            List<W32WindowInfo> kids = Win32Driver.GetChildren(screen);
            for (int i = 0; i < kids.Count; i++)
            {
                string cls = kids[i].Class != null ? kids[i].Class : "";
                if (cls.IndexOf("StatusBar", StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                IntPtr bar = kids[i].Handle;
                List<string> acc = AccessibleText.HarvestForcedIds(bar, 40, log);
                for (int a = 0; a < acc.Count; a++) { sb.Append('\t').Append(acc[a]); }
                int parts = (int)Win32Interop.SendMessage(
                    bar, Win32Interop.SB_GETPARTS, IntPtr.Zero, IntPtr.Zero);
                if (parts < 0) { parts = 0; }
                if (parts > 20) { parts = 20; }
                if (parts == 0) { parts = 8; }
                for (int p = 0; p < parts; p++)
                {
                    int len = (int)Win32Interop.SendMessage(
                        bar, Win32Interop.SB_GETTEXTLENGTHW, (IntPtr)p, IntPtr.Zero);
                    if (len <= 0) { continue; }
                    StringBuilder buf = new StringBuilder(len + 4);
                    Win32Interop.SendMessage(bar, Win32Interop.SB_GETTEXTW, (IntPtr)p, buf);
                    if (buf.Length > 0) { sb.Append('\t').Append(buf.ToString().Trim()); }
                }
                if (log != null)
                {
                    log("flexgrid: status-bar «" + OneLine(sb.ToString(), 120) + "»", "INFO");
                }
            }
            return sb.ToString();
        }

        // Visible intersection only. RTL inquiry puts الكود الموحد at the top-right
        // of the selected row; the HWND is wider than the form so left-side clicks miss it.
        private static void HarvestVisibleRtl(
            IntPtr flex, IntPtr screen, StringBuilder blob, Action<string, string> log)
        {
            int x, y, w, h;
            if (!VisibleIntersect(flex, screen, out x, out y, out w, out h)) { return; }
            if (log != null)
            {
                log("flexgrid: visible RTL band @" + x + "," + y + " " + w + "x" + h, "INFO");
            }

            // Stacked headers (الكود الموحد / تاريخ الميلاد / ...) then the blue data row.
            int yRow = y + Math.Min(82, Math.Max(52, h / 5));
            int[] yOff = new int[] { 0, 16, 32 };
            for (int yi = 0; yi < yOff.Length; yi++)
            {
                int py = yRow + yOff[yi];
                if (py >= y + h - 6) { break; }
                for (int i = 0; i < 8; i++)
                {
                    int px = x + w - 18 - (i * 72);
                    if (px < x + 8) { break; }
                    HarvestPoint(flex, screen, px, py, blob, log);
                    if (HasCode12(blob.ToString())) { return; }
                }
            }
        }

        private static bool VisibleIntersect(
            IntPtr flex, IntPtr screen, out int x, out int y, out int w, out int h)
        {
            x = 0; y = 0; w = 0; h = 0;
            try
            {
                Win32Driver.RectInfo g = Win32Driver.GetRect(flex);
                Win32Driver.RectInfo f = (screen != IntPtr.Zero && Win32Interop.IsWindow(screen))
                    ? Win32Driver.GetRect(screen) : g;
                int x1 = Math.Max(g.X, f.X + 4);
                int y1 = Math.Max(g.Y, f.Y + 4);
                int x2 = Math.Min(g.X + g.W, f.X + f.W - 4);
                int y2 = Math.Min(g.Y + g.H, f.Y + f.H - 4);
                x = x1; y = y1; w = x2 - x1; h = y2 - y1;
                return w > 40 && h > 40;
            }
            catch { return false; }
        }

        private static object GetFlexDispatch(IntPtr flex, Action<string, string> log)
        {
            Guid iid = Win32Interop.IID_IDispatch;
            uint[] ids = new uint[] { Win32Interop.OBJID_WINDOW, Win32Interop.OBJID_CLIENT };
            for (int i = 0; i < ids.Length; i++)
            {
                try
                {
                    object obj;
                    int hr = Win32Interop.AccessibleObjectFromWindow(flex, ids[i], ref iid, out obj);
                    string tn = "(null)";
                    if (obj != null)
                    {
                        try { tn = obj.GetType().FullName; } catch { tn = "?"; }
                    }
                    if (log != null)
                    {
                        log("flexgrid: COM try id=0x" + ids[i].ToString("X8") +
                            " hr=" + hr + " type=" + tn, "INFO");
                    }
                    if (hr != 0 || obj == null) { continue; }
                    if (LooksLikeAccessible(obj))
                    {
                        if (log != null) { log("flexgrid: skip IAccessible dispatch", "INFO"); }
                        continue;
                    }
                    ProbeComNames(com: obj, log: log);
                    return obj;
                }
                catch (Exception ex)
                {
                    if (log != null) { log("flexgrid: COM try failed — " + ex.Message, "WARN"); }
                }
            }
            return null;
        }

        private static void ProbeComNames(object com, Action<string, string> log)
        {
            if (log == null || com == null) { return; }
            string[] names = new string[] {
                "Rows", "Cols", "FixedRows", "Text", "Clip", "Row", "Col",
                "TextMatrix", "CellText", "Value"
            };
            for (int i = 0; i < names.Length; i++)
            {
                try
                {
                    object v = GetProp(com, names[i]);
                    log("flexgrid: COM." + names[i] + "=" +
                        (v == null ? "(null)" : OneLine(v.ToString(), 60)), "INFO");
                }
                catch (Exception ex)
                {
                    log("flexgrid: COM." + names[i] + " ERR " + ex.Message, "INFO");
                }
            }
        }

        private static bool LooksLikeAccessible(object com)
        {
            try
            {
                object n = com.GetType().InvokeMember(
                    "accName",
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null, com, new object[] { 0 });
                return true;
            }
            catch { }
            try
            {
                GetProp(com, "accChildCount");
                return true;
            }
            catch { return false; }
        }

        private static int TryIntProp(object com, string name, int fallback)
        {
            try { return ToInt(GetProp(com, name)); }
            catch { return fallback; }
        }

        private static object GetProp(object com, string name)
        {
            return com.GetType().InvokeMember(
                name, BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                null, com, null);
        }

        private static string ReadTextMatrix(object com, int row, int col)
        {
            try
            {
                object v = com.GetType().InvokeMember(
                    "TextMatrix",
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null, com, new object[] { row, col });
                return v != null ? v.ToString() : "";
            }
            catch { return ""; }
        }

        private static void WalkCellText(
            object com, int startR, int maxR, int maxC, StringBuilder sb, Action<string, string> log)
        {
            for (int r = startR; r < maxR; r++)
            {
                try
                {
                    com.GetType().InvokeMember(
                        "Row", BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance,
                        null, com, new object[] { r });
                }
                catch { continue; }
                for (int c = 0; c < maxC; c++)
                {
                    try
                    {
                        com.GetType().InvokeMember(
                            "Col", BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance,
                            null, com, new object[] { c });
                        object v = GetProp(com, "Text");
                        if (v == null || string.IsNullOrWhiteSpace(v.ToString())) { continue; }
                        if (log != null)
                        {
                            log("flexgrid: Text[" + r + "," + c + "] «" +
                                OneLine(v.ToString().Trim(), 80) + "»", "INFO");
                        }
                        sb.Append(v.ToString().Trim()).Append('\t');
                    }
                    catch { }
                }
            }
        }

        private static int ToInt(object v)
        {
            if (v == null) { return 0; }
            try { return Convert.ToInt32(v); }
            catch { return 0; }
        }

        private static void HarvestPoint(
            IntPtr flex, IntPtr screen, int x, int y, StringBuilder blob, Action<string, string> log)
        {
            List<string> acc = AccessibleText.HarvestAtPoint(x, y);
            for (int i = 0; i < acc.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(acc[i])) { blob.Append('\t').Append(acc[i].Trim()); }
            }

            try { Clipboard.Clear(); } catch { }
            try
            {
                Win32Driver.SetForeground(flex);
                Win32Driver.ClickScreen(x, y, 70);
            }
            catch { return; }

            IntPtr keys = Win32Interop.IsWindow(flex) ? flex : screen;
            try { Win32Driver.SendShortcut(keys, "^c", 90); } catch { }
            try { Application.DoEvents(); } catch { }
            AppendClip(blob);

            try
            {
                Win32Interop.SendMessage(flex, 0x0301, IntPtr.Zero, IntPtr.Zero);
                Thread.Sleep(50);
                try { Application.DoEvents(); } catch { }
                AppendClip(blob);
            }
            catch { }
        }

        private static void AppendClip(StringBuilder blob)
        {
            string clip = "";
            try { clip = Clipboard.GetText(); } catch { clip = ""; }
            if (string.IsNullOrWhiteSpace(clip)) { return; }
            clip = clip.Trim();
            if (blob.ToString().IndexOf(clip, StringComparison.Ordinal) >= 0) { return; }
            blob.Append('\t').Append(clip);
        }

        private static bool HasCode12(string blob)
        {
            string nin;
            string code12;
            ParseCodes(blob, "", out nin, out code12);
            return code12.Length == 12;
        }

        private static string FinishHarvest(StringBuilder blob, Action<string, string> log)
        {
            string s = blob != null ? blob.ToString().Trim() : "";
            if (log != null)
            {
                string preview = s.Replace("\r", " ").Replace("\n", " ");
                if (preview.Length > 140) { preview = preview.Substring(0, 140) + "..."; }
                log("flexgrid: inquiry harvest " +
                    (s.Length == 0 ? "(empty)" : s.Length + " chars «" + preview + "»"),
                    s.Length == 0 ? "WARN" : "INFO");
            }
            return s;
        }

        private static void TakeCodes(
            string piece, string searchDigits, bool hasNid, ref string nin, ref string code12)
        {
            if (string.IsNullOrWhiteSpace(piece)) { return; }
            MatchCollection ms = Regex.Matches(piece, @"[0-9\u0660-\u0669]{6,14}");
            for (int i = 0; i < ms.Count; i++)
            {
                string d = DigitsOnly(ms[i].Value);
                if (d.Length == 14) { continue; }
                if (searchDigits.Length > 0 && d == searchDigits) { continue; }
                if (d.Length == 12 && !hasNid && code12.Length == 0 && LooksLikeBrokerCode12(d))
                {
                    code12 = d;
                }
                if (d.Length < 6 || d.Length > 9) { continue; }
                d = OcrConsensus.NormalizeUnified(d);
                if (!OcrConsensus.IsPlausibleUnified(d)) { continue; }
                ConsiderNin(d, ref nin);
            }
        }

        // OCR splits الكود الموحد (35926 + 11 → 3592611). Prefer a 7-digit stitch
        // over an 8-digit glue that starts with a leftover 5.
        private static void StitchUnified(string[] parts, ref string nin)
        {
            if (parts == null) { return; }
            List<string> toks = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string d = DigitsOnly(parts[i]);
                if (d.Length >= 2 && d.Length <= 6) { toks.Add(d); }
            }
            for (int i = 0; i < toks.Count - 1; i++)
            {
                if (toks[i].Length + toks[i + 1].Length == 7)
                {
                    ConsiderNin(toks[i] + toks[i + 1], ref nin);
                }
            }
            if (nin.Length == 8 && nin[0] == '5')
            {
                string inner = nin.Substring(1, 7);
                if (UniqueDigitCount(inner) >= 4) { ConsiderNin(inner, ref nin); }
            }
            nin = OcrConsensus.NormalizeUnified(nin);
        }

        private static void ConsiderNin(string d, ref string nin)
        {
            if (string.IsNullOrEmpty(d) || d.Length < 6 || d.Length > 9) { return; }
            int uniq = UniqueDigitCount(d);
            if (uniq < 3) { return; }
            int have = UniqueDigitCount(nin);
            if (nin.Length == 0)
            {
                nin = d;
                return;
            }
            bool betterSeven = d.Length == 7 && uniq >= 4 && nin.Length != 7;
            bool betterUniq = d.Length == 7 && nin.Length == 7 && uniq > have;
            if (betterSeven || betterUniq) { nin = d; }
        }

        private static int UniqueDigitCount(string digits)
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

        private static string Describe(IntPtr handle)
        {
            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(handle);
                return Win32Driver.GetClassName(handle) + " @" + r.X + "," + r.Y + " " + r.W + "x" + r.H;
            }
            catch { return "hwnd=" + handle; }
        }
    }
}
