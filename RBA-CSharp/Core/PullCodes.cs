// =============================================================================
//  PullCodes.cs - existing client: «سحب الأكواد» instead of add-customer.
//
//  Same parent menu as إدخال بيانات العملاء (typically «التكويد»), then leaf
//  «سحب الأكواد». Type CODE12, click زر البحث, click table once, then F2/F9.
//  Fixed path — no «سحب» cell click, no ENTER fallback.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Threading;

namespace RbaCore
{
    public class PullCodesResult
    {
        public bool Ok;
        public string Nin = "";
        public string Note = "";
        public IntPtr Screen;
    }

    public static class PullCodes
    {
        public const string SearchBoxKey = "pullCodesSearchBox";
        public const string SearchButtonKey = "pullCodesSearchBtn";
        public const string GridKey = "pullCodesGrid";

        public const string DefaultTitle = "سحب الأكواد";
        public const string DefaultLeaf = "سحب الأكواد";

        public static bool IsKey(string fieldKey)
        {
            return string.Equals(fieldKey, SearchBoxKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, SearchButtonKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, GridKey, StringComparison.OrdinalIgnoreCase);
        }

        public static string[] MenuPath(BotAppConfig app)
        {
            string parent = "التكويد";
            if (app != null && app.MenuPath != null && app.MenuPath.Length >= 1 &&
                !string.IsNullOrWhiteSpace(app.MenuPath[0]))
            {
                parent = app.MenuPath[0].Trim();
            }
            if (app != null && app.PullCodesMenuPath != null && app.PullCodesMenuPath.Length >= 1 &&
                !string.IsNullOrWhiteSpace(app.PullCodesMenuPath[0]))
            {
                parent = app.PullCodesMenuPath[0].Trim();
            }
            if (ArabicText.Contains(parent, "نظام تكويد") || ArabicText.Contains(parent, "بورصة"))
            {
                parent = "التكويد";
            }

            string leaf = DefaultLeaf;
            if (app != null && app.PullCodesMenuPath != null && app.PullCodesMenuPath.Length >= 2 &&
                !string.IsNullOrWhiteSpace(app.PullCodesMenuPath[1]) &&
                ArabicText.Contains(app.PullCodesMenuPath[1], "سحب"))
            {
                leaf = app.PullCodesMenuPath[1].Trim();
            }
            return new string[] { parent, leaf };
        }

        public static string WindowTitle(BotAppConfig app)
        {
            if (app != null && !string.IsNullOrWhiteSpace(app.PullCodesWindowTitle) &&
                ArabicText.Contains(app.PullCodesWindowTitle, "سحب"))
            {
                return app.PullCodesWindowTitle.Trim();
            }
            string[] path = MenuPath(app);
            if (path.Length >= 2) { return path[1]; }
            return DefaultTitle;
        }

        // Inquiry status bar: F6 = «السحب لدى الشركات». Never F2/F9 on الإستعلام.
        // If F6 does not open سحب الأكواد, fall back to the menu (proven path).
        public static PullCodesResult RunFromInquiry(
            IntPtr appWindow,
            IntPtr inquiry,
            BotConfig cfg,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log)
        {
            return RunFromInquiry(appWindow, inquiry, cfg, selectors, settleMs, log, "", null);
        }

        public static PullCodesResult RunFromInquiry(
            IntPtr appWindow,
            IntPtr inquiry,
            BotConfig cfg,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log,
            string code12)
        {
            return RunFromInquiry(appWindow, inquiry, cfg, selectors, settleMs, log, code12, null, "");
        }

        public static PullCodesResult RunFromInquiry(
            IntPtr appWindow,
            IntPtr inquiry,
            BotConfig cfg,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log,
            string code12,
            string[] altNins)
        {
            return RunFromInquiry(appWindow, inquiry, cfg, selectors, settleMs, log, code12, altNins, "");
        }

        public static PullCodesResult RunFromInquiry(
            IntPtr appWindow,
            IntPtr inquiry,
            BotConfig cfg,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log,
            string code12,
            string[] altNins,
            string expectedNationalId)
        {
            PullCodesResult result = new PullCodesResult();
            if (log == null) { log = delegate(string m, string l) { }; }
            if (settleMs <= 0) { settleMs = 400; }

            if (inquiry == IntPtr.Zero || !Win32Interop.IsWindow(inquiry))
            {
                BotAppConfig appCfg = cfg != null ? cfg.App : null;
                inquiry = ClientLookup.FindOpenScreen(appWindow, appCfg, selectors);
            }
            if (inquiry == IntPtr.Zero || !Win32Interop.IsWindow(inquiry))
            {
                result.Note = "الإستعلام is not open — cannot F6 سحب";
                log("pull-codes: " + result.Note, "WARN");
                return result;
            }

            if (string.IsNullOrWhiteSpace(expectedNationalId) ||
                expectedNationalId.Trim().Length != 14 ||
                !OcrConsensus.LooksLikeEgyptianNid(expectedNationalId.Trim()))
            {
                result.Note = "ZERO-TOLERANCE: سحب مرفوض — الرقم القومي المتوقع غير صالح";
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }
            string expectNid = expectedNationalId.Trim();

            log("pull-codes: inquiry «" + Win32Driver.GetWindowTitle(inquiry) + "» hwnd=" +
                inquiry.ToString() + " expectNID=" + expectNid, "INFO");
            ClientLookup.LogOpenWindows(appWindow, "pull-codes before F6", log);
            LogFormList(appWindow, "pull-codes before F6", log);

            IntPtr flex = FlexGridSurface.Find(inquiry);
            string live12 = "";
            if (flex != IntPtr.Zero)
            {
                string liveNin;
                string rowNid;
                string[] harvested;
                FlexGridSurface.TryReadCodes(
                    flex, inquiry, "", log, out liveNin, out live12, out rowNid, out harvested);
                // Inquiry row must already be the sheet client — otherwise refuse entirely.
                if (OcrConsensus.LooksLikeEgyptianNid(rowNid) &&
                    !string.Equals(rowNid, expectNid, StringComparison.Ordinal))
                {
                    result.Note = "ZERO-TOLERANCE: صف الإستعلام NID " + rowNid +
                        " ≠ الشيت " + expectNid + " — لن يتم السحب";
                    log("pull-codes: " + result.Note, "ERROR");
                    return result;
                }
            }
            if (FlexGridSurface.LooksLikeBrokerCode12(live12)) { code12 = live12; }

            // F6 is a form shortcut. Never focus the search box — that path is a no-op.
            SendF6ForPull(appWindow, inquiry, log);
            Thread.Sleep(500);

            int pid = Win32Driver.GetProcessId(inquiry);
            ConfirmDialog.ConfirmIfPresent(pid, inquiry, 1200, 1, log);
            IntPtr sahb = FindSahbWindow(appWindow, inquiry, cfg, selectors, 1800);
            if (sahb == IntPtr.Zero)
            {
                log("pull-codes: F6 missed — click row + F6 on grid (not the search box)", "WARN");
                ClickInquiryResultRow(inquiry, log);
                SendF6OnGrid(appWindow, inquiry, log);
                Thread.Sleep(400);
                ConfirmDialog.ConfirmIfPresent(pid, inquiry, 1000, 1, log);
                sahb = FindSahbWindow(appWindow, inquiry, cfg, selectors, 1500);
            }

            if (sahb == IntPtr.Zero || sahb == inquiry || IsInquiryTitle(Win32Driver.GetWindowTitle(sahb)))
            {
                log("pull-codes: F6 still no سحب — close الإستعلام so MDI menu «التكويد» is free", "WARN");
                ClientLookup.CloseWindow(inquiry, "الإستعلام", log);
                inquiry = IntPtr.Zero;
                try
                {
                    Win32Driver.SetForeground(appWindow);
                    Thread.Sleep(220);
                }
                catch { }
                // Menu fallback: CODE12 only — never type OCR NIN (wrong-client risk).
                PullCodesResult menu = Run(
                    appWindow, cfg, selectors, "", code12, "", null,
                    expectNid, settleMs, log);
                if (menu.Screen != IntPtr.Zero)
                {
                    ClientLookup.CloseWindow(menu.Screen, "سحب الأكواد", log);
                    menu.Screen = IntPtr.Zero;
                }
                return menu;
            }

            log("pull-codes: سحب window «" + Win32Driver.GetWindowTitle(sahb) +
                "» hwnd=" + sahb.ToString(), "INFO");
            result.Screen = sahb;
            FlexGridSurface.DumpTable(FlexGridSurface.Find(sahb), sahb, "after-F6", log);

            if (cfg != null && cfg.Windows != null &&
                cfg.Windows.PullCodesScreen != null && cfg.Windows.PullCodesScreen.IsValid)
            {
                WindowLayout.Apply(sahb, cfg.Windows.PullCodesScreen, log);
                Thread.Sleep(120);
            }

            result = FinishF2F9(sahb, selectors, settleMs, log, result, expectNid);
            ClientLookup.CloseWindow(sahb, "سحب الأكواد", log);
            result.Screen = IntPtr.Zero;
            return result;
        }

        public static PullCodesResult Run(
            IntPtr appWindow,
            BotConfig cfg,
            List<FieldSelector> selectors,
            string nin,
            int settleMs,
            Action<string, string> log)
        {
            return Run(appWindow, cfg, selectors, nin, "", settleMs, log);
        }

        public static PullCodesResult Run(
            IntPtr appWindow,
            BotConfig cfg,
            List<FieldSelector> selectors,
            string nin,
            string code12,
            int settleMs,
            Action<string, string> log)
        {
            return Run(appWindow, cfg, selectors, nin, code12, "", settleMs, log);
        }

        public static PullCodesResult Run(
            IntPtr appWindow,
            BotConfig cfg,
            List<FieldSelector> selectors,
            string nin,
            string code12,
            string fallbackSearch,
            int settleMs,
            Action<string, string> log)
        {
            return Run(appWindow, cfg, selectors, nin, code12, fallbackSearch, null, settleMs, log);
        }

        public static PullCodesResult Run(
            IntPtr appWindow,
            BotConfig cfg,
            List<FieldSelector> selectors,
            string nin,
            string code12,
            string fallbackSearch,
            string[] altNins,
            int settleMs,
            Action<string, string> log)
        {
            return Run(appWindow, cfg, selectors, nin, code12, fallbackSearch, altNins, "", settleMs, log);
        }

        public static PullCodesResult Run(
            IntPtr appWindow,
            BotConfig cfg,
            List<FieldSelector> selectors,
            string nin,
            string code12,
            string fallbackSearch,
            string[] altNins,
            string expectedNationalId,
            int settleMs,
            Action<string, string> log)
        {
            PullCodesResult result = new PullCodesResult();
            if (log == null) { log = delegate(string m, string l) { }; }
            if (settleMs <= 0) { settleMs = 400; }

            if (string.IsNullOrWhiteSpace(expectedNationalId) ||
                expectedNationalId.Trim().Length != 14 ||
                !OcrConsensus.LooksLikeEgyptianNid(expectedNationalId.Trim()))
            {
                result.Note = "ZERO-TOLERANCE: سحب بالقائمة مرفوض — الرقم القومي المتوقع غير صالح";
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }
            expectedNationalId = expectedNationalId.Trim();

            // SAFETY: never type OCR الكود الموحد into سحب. One wrong digit pulls
            // another client (seen live: 3592611 / 1726351). Menu search is CODE12 only.
            string code = FlexGridSurface.AsOperatorPullCode(code12);
            if (code.Length == 0)
            {
                code = FlexGridSurface.AsOperatorPullCode(fallbackSearch);
            }
            result.Nin = code;
            if (code.Length == 0)
            {
                log("pull-codes: REFUSING menu search — الكود الموحد فارغ", "ERROR");
            }
            else
            {
                log("pull-codes: searching سحب with code «" + code + "» (NID gate " +
                    expectedNationalId + ")", "INFO");
            }

            BotAppConfig app = cfg != null && cfg.App != null ? cfg.App : new BotAppConfig();
            string[] path = MenuPath(app);
            log("pull-codes: menu «" + path[0] + "» → «" + path[1] + "»", "INFO");

            // Modal «الإستعلام» sits on بيانات العملاء and hides the MDI menu bar.
            CloseOpenInquiry(appWindow, app, selectors, log);
            try
            {
                Win32Driver.SetForeground(appWindow);
                Thread.Sleep(220);
            }
            catch { }

            IntPtr screen = CodingPostSave.EnsurePullCodesScreen(
                appWindow, app, selectors, settleMs, log);
            if (screen == IntPtr.Zero)
            {
                result.Note = "«" + WindowTitle(app) + "» did not open";
                log("pull-codes: " + result.Note, "WARN");
                return result;
            }
            result.Screen = screen;

            if (cfg != null && cfg.Windows != null && cfg.Windows.PullCodesScreen != null &&
                cfg.Windows.PullCodesScreen.IsValid)
            {
                WindowLayout.Apply(screen, cfg.Windows.PullCodesScreen, log);
                Thread.Sleep(150);
            }

            if (code.Length == 0)
            {
                result.Note = "تعذر سحب الأكواد — الكود الموحد فارغ";
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }

            FieldMap map = FieldDiscovery.GetFieldMap(screen, false);
            IntPtr box = ResolveMapped(map, selectors, SearchBoxKey, log);
            if (box == IntPtr.Zero) { box = FlexGridSurface.AutoSearchBox(map); }
            if (box == IntPtr.Zero)
            {
                result.Note = "search box not found — map «" + SearchBoxKey + "» in Setup";
                log("pull-codes: " + result.Note, "WARN");
                return result;
            }

            if (!TypeInto(box, code, log))
            {
                result.Note = "could not type search value into سحب الأكواد";
                log("pull-codes: " + result.Note, "WARN");
                return result;
            }

            IntPtr button = ResolveMapped(map, selectors, SearchButtonKey, log);
            if (button == IntPtr.Zero) { button = FlexGridSurface.AutoSearchButton(map); }
            if (button == IntPtr.Zero)
            {
                result.Note = "ZERO-TOLERANCE: زر البحث غير موجود في سحب الأكواد — لن يُرسل F2/F9";
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }
            try
            {
                Win32Driver.ClickPhysical(button, 220);
                log("pull-codes: search button clicked", "OK");
            }
            catch (Exception ex)
            {
                result.Note = "ZERO-TOLERANCE: فشل ضغط زر البحث — " + ex.Message;
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }
            Thread.Sleep(900);

            if (SearchReturnedEmpty(screen, log))
            {
                result.Note = "سحب الأكواد: لا يوجد عملاء for CODE12 «" + code + "»";
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }

            IntPtr grid = ResolveMapped(map, selectors, GridKey, log);
            IntPtr flex = FlexGridSurface.Find(screen, grid);
            if (flex == IntPtr.Zero) { flex = grid; }
            if (flex == IntPtr.Zero) { flex = AutoFindPullGrid(map); }
            if (flex == IntPtr.Zero)
            {
                result.Note = "ZERO-TOLERANCE: جدول سحب الأكواد غير موجود — لن يُرسل F2/F9";
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }

            // Gate: refuse F2/F9 unless the result row is the expected national id.
            string identityWhy;
            if (!PullRowMatchesExpected(flex, screen, expectedNationalId, log, out identityWhy))
            {
                result.Note = "رفض السحب — " + identityWhy;
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }

            try
            {
                Win32Driver.SetForeground(screen);
                Thread.Sleep(80);
                if (!FlexGridSurface.ClickTableOnce(flex, log))
                {
                    result.Note = "ZERO-TOLERANCE: فشل ضغط الجدول قبل F2/F9";
                    log("pull-codes: " + result.Note, "ERROR");
                    return result;
                }
                Thread.Sleep(settleMs);
                Win32Driver.SendShortcut(screen, "{F2}", 400);
                log("pull-codes: F2 sent", "OK");
                Thread.Sleep(200);
                Win32Driver.SendShortcut(screen, "{F9}", 600);
                log("pull-codes: F9 sent", "OK");
                Thread.Sleep(settleMs);
                int pid = Win32Driver.GetProcessId(screen);
                ConfirmDialogResult cd = ConfirmPullSafely(pid, screen, log);
                if (PullBlockedByCoding(cd, log))
                {
                    result.Note = "التكويد رفض السحب (موقوف/غير مسموح) for «" + code + "»";
                    log("pull-codes: " + result.Note, "WARN");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Note = "F2/F9 failed — " + ex.Message;
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }

            string why;
            if (!PullSucceeded(screen, flex, log, out why))
            {
                result.Note = "سحب الأكواد لم يكتمل لـ «" + code + "» — " + why;
                log("pull-codes: " + result.Note, "WARN");
                return result;
            }

            result.Ok = true;
            result.Note = "سحب: بحث → جدول → F2/F9 for CODE12 " + code;
            log("pull-codes: " + result.Note, "OK");
            return result;
        }

        // Before confirming سحب: the result row must show the same 14-digit NID.
        private static bool PullRowMatchesExpected(
            IntPtr flex,
            IntPtr screen,
            string expectedNationalId,
            Action<string, string> log,
            out string why)
        {
            why = "";
            string want = expectedNationalId != null ? expectedNationalId.Trim() : "";
            if (want.Length != 14 || !OcrConsensus.LooksLikeEgyptianNid(want))
            {
                why = "ZERO-TOLERANCE: لا سحب بدون رقم قومي متوقع صالح من الشيت";
                return false;
            }
            if (flex == IntPtr.Zero || !Win32Interop.IsWindow(flex))
            {
                why = "ZERO-TOLERANCE: شبكة سحب فارغة — لن يتم التأكيد";
                return false;
            }

            string nin;
            string c12;
            string rowNid;
            FlexGridSurface.TryReadCodes(flex, screen, "", log, out nin, out c12, out rowNid);
            if (!OcrConsensus.LooksLikeEgyptianNid(rowNid))
            {
                why = "ZERO-TOLERANCE: تعذر قراءة الرقم القومي من صف السحب — رفض";
                return false;
            }
            if (!string.Equals(rowNid, want, StringComparison.Ordinal))
            {
                why = "ZERO-TOLERANCE: صف السحب NID " + rowNid + " ≠ الشيت " + want;
                return false;
            }
            if (log != null)
            {
                log("pull-codes: identity OK — row NID matches sheet " + want, "OK");
            }
            return true;
        }

        private static bool PullBlockedByCoding(ConfirmDialogResult cd, Action<string, string> log)
        {
            if (cd == null || cd.Messages == null) { return false; }
            for (int i = 0; i < cd.Messages.Count; i++)
            {
                string m = cd.Messages[i] ?? "";
                if (ArabicText.ContainsAny(m, new string[]
                    {
                        "موقوف", "لا يمكن سحبه", "لا يمكن السحب", "تعذر السحب"
                    }))
                {
                    if (log != null) { log("pull-codes: Coding blocked pull — «" + m.Trim() + "»", "WARN"); }
                    return true;
                }
            }
            return false;
        }

        private static bool PullSucceeded(
            IntPtr screen, IntPtr flex, Action<string, string> log, out string why)
        {
            why = "";
            if (screen == IntPtr.Zero || !Win32Interop.IsWindow(screen))
            {
                why = "شاشة السحب اختفت بعد F2/F9";
                return false;
            }
            if (flex == IntPtr.Zero || !Win32Interop.IsWindow(flex))
            {
                why = "شبكة النتائج غير موجودة — لم يتم تحديد صف";
                return false;
            }

            int pid = Win32Driver.GetProcessId(screen);
            IntPtr modal = ConfirmDialog.FindModal(pid, screen);
            if (modal == IntPtr.Zero) { return true; }

            string blob = (Win32Driver.GetWindowTitle(modal) ?? "") + " " +
                          ConfirmDialog.ReadMessage(modal);
            if (ConfirmDialog.IsServerErrorMessage(blob) ||
                ArabicText.ContainsAny(blob, new string[]
                {
                    "لا يوجد", "خطأ", "تعذر", "موقوف", "لا يمكن سحبه", "لا يمكن السحب"
                }))
            {
                why = "رسالة من التكويد: " + blob.Trim();
                string clicked;
                ConfirmDialog.ClickAffirmative(modal, out clicked);
                return false;
            }
            return true;
        }

        private static void ClickInquiryResultRow(IntPtr inquiry, Action<string, string> log)
        {
            IntPtr flex = FlexGridSurface.Find(inquiry);
            if (flex == IntPtr.Zero) { return; }
            try
            {
                Win32Driver.RectInfo g = Win32Driver.GetRect(flex);
                Win32Driver.RectInfo f = Win32Driver.GetRect(inquiry);
                int x2 = Math.Min(g.X + g.W, f.X + f.W) - 8;
                int x1 = Math.Max(g.X, f.X + 8);
                int y1 = Math.Max(g.Y, f.Y + 8);
                int w = x2 - x1;
                int h = Math.Min(g.Y + g.H, f.Y + f.H) - y1;
                if (w < 40 || h < 40) { return; }
                int x = x1 + (w / 2);
                int y = y1 + Math.Min(110, Math.Max(90, h / 4));
                Win32Driver.SetForeground(inquiry);
                Win32Driver.ClickScreen(x, y, 180);
                if (log != null)
                {
                    log("pull-codes: clicked inquiry data row @" + x + "," + y, "INFO");
                }
            }
            catch (Exception ex)
            {
                if (log != null) { log("pull-codes: inquiry row click — " + ex.Message, "WARN"); }
            }
        }

        private static void SendF6ForPull(
            IntPtr appWindow, IntPtr inquiry, Action<string, string> log)
        {
            uint attached = 0;
            try
            {
                uint pid;
                uint their = Win32Interop.GetWindowThreadProcessId(inquiry, out pid);
                uint ours = Win32Interop.GetCurrentThreadId();
                if (their != 0 && their != ours)
                {
                    Win32Interop.AttachThreadInput(ours, their, true);
                    attached = their;
                }
            }
            catch { }

            try
            {
                Win32Driver.ActivateMdiChild(appWindow, inquiry);
                try
                {
                    Win32Driver.RectInfo fr = Win32Driver.GetRect(inquiry);
                    Win32Driver.ClickScreen(fr.X + Math.Min(90, Math.Max(24, fr.W / 5)), fr.Y + 8, 80);
                }
                catch { }
                Thread.Sleep(60);
                try { Win32Interop.SetFocus(inquiry); } catch { }
                IntPtr fg = Win32Interop.GetForegroundWindow();
                IntPtr focus = Win32Interop.GetFocus();
                log("pull-codes: F6 foreground «" + Win32Driver.GetWindowTitle(fg) +
                    "» hwnd=" + fg.ToString() +
                    " focus=" + focus.ToString() +
                    " class=" + (focus != IntPtr.Zero ? Win32Driver.GetClassName(focus) : ""), "INFO");

                Win32Driver.PostVirtualKey(inquiry, Win32Interop.VK_F6);
                log("pull-codes: PostMessage VK_F6 → inquiry form (not search box)", "INFO");
                Thread.Sleep(120);
                try { Win32Driver.SendShortcut(inquiry, "{F6}", 200); }
                catch (Exception ex) { log("pull-codes: SendKeys F6 — " + ex.Message, "WARN"); }
                Win32Driver.TapVirtualKey(Win32Interop.VK_F6);
                log("pull-codes: F6 «السحب لدى الشركات»", "INFO");
            }
            catch (Exception ex)
            {
                log("pull-codes: F6 send failed — " + ex.Message, "ERROR");
            }
            finally
            {
                if (attached != 0)
                {
                    try { Win32Interop.AttachThreadInput(Win32Interop.GetCurrentThreadId(), attached, false); }
                    catch { }
                }
            }
        }

        // Row is selected: F6 must hit the form/grid, not the search box (that path is a no-op).
        private static void SendF6OnGrid(IntPtr appWindow, IntPtr inquiry, Action<string, string> log)
        {
            uint attached = 0;
            try
            {
                uint pid;
                uint their = Win32Interop.GetWindowThreadProcessId(inquiry, out pid);
                uint ours = Win32Interop.GetCurrentThreadId();
                if (their != 0 && their != ours)
                {
                    Win32Interop.AttachThreadInput(ours, their, true);
                    attached = their;
                }
            }
            catch { }

            try
            {
                Win32Driver.ActivateMdiChild(appWindow, inquiry);
                IntPtr flex = FlexGridSurface.Find(inquiry);
                if (flex != IntPtr.Zero)
                {
                    try { Win32Interop.SetFocus(flex); } catch { }
                }
                ClickStatusBarHint(inquiry, log);
                IntPtr focus = Win32Interop.GetFocus();
                log("pull-codes: F6-on-grid focus=" + focus.ToString() +
                    " class=" + (focus != IntPtr.Zero ? Win32Driver.GetClassName(focus) : ""), "INFO");
                Win32Driver.PostVirtualKey(inquiry, Win32Interop.VK_F6);
                if (flex != IntPtr.Zero) { Win32Driver.PostVirtualKey(flex, Win32Interop.VK_F6); }
                Thread.Sleep(80);
                try { Win32Driver.SendShortcut(inquiry, "{F6}", 200); } catch { }
                Win32Driver.TapVirtualKey(Win32Interop.VK_F6);
                log("pull-codes: F6 on selected row / status bar", "INFO");
            }
            catch (Exception ex)
            {
                log("pull-codes: F6-on-grid failed — " + ex.Message, "ERROR");
            }
            finally
            {
                if (attached != 0)
                {
                    try { Win32Interop.AttachThreadInput(Win32Interop.GetCurrentThreadId(), attached, false); }
                    catch { }
                }
            }
        }

        private static void ClickStatusBarHint(IntPtr inquiry, Action<string, string> log)
        {
            List<W32WindowInfo> kids = Win32Driver.GetChildren(inquiry);
            for (int i = 0; i < kids.Count; i++)
            {
                string cls = kids[i].Class != null ? kids[i].Class : "";
                if (cls.IndexOf("StatusBar", StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                try
                {
                    Win32Driver.RectInfo r = Win32Driver.GetRect(kids[i].Handle);
                    int y = r.Y + Math.Max(4, r.H / 2);
                    int x = r.X + Math.Max(24, r.W - 90);
                    Win32Driver.ClickScreen(x, y, 100);
                    if (log != null)
                    {
                        log("pull-codes: clicked status-bar hint @" + x + "," + y, "INFO");
                    }
                    Win32Driver.PostVirtualKey(kids[i].Handle, Win32Interop.VK_F6);
                }
                catch { }
                return;
            }
        }

        private static IntPtr FindInquirySearchBox(IntPtr inquiry)
        {
            List<W32WindowInfo> kids = Win32Driver.GetChildren(inquiry);
            IntPtr best = IntPtr.Zero;
            int bestW = 0;
            for (int i = 0; i < kids.Count; i++)
            {
                if (!kids[i].Visible) { continue; }
                string cls = kids[i].Class != null ? kids[i].Class : "";
                if (cls.IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                try
                {
                    Win32Driver.RectInfo r = Win32Driver.GetRect(kids[i].Handle);
                    if (r.W > bestW && r.H < 40) { bestW = r.W; best = kids[i].Handle; }
                }
                catch { }
            }
            return best;
        }

        private static void LogFormList(IntPtr appWindow, string tag, Action<string, string> log)
        {
            if (log == null) { return; }
            List<W32WindowInfo> forms = Win32Driver.GetFormWindows(appWindow, 8);
            int shown = 0;
            for (int i = 0; i < forms.Count; i++)
            {
                if (!forms[i].Visible) { continue; }
                string t = Win32Driver.GetWindowTitle(forms[i].Handle);
                log(tag + ": form hwnd=" + forms[i].Handle.ToString() +
                    " «" + (t != null ? t.Trim() : "") + "» " + forms[i].Class, "INFO");
                shown++;
            }
            try
            {
                List<W32WindowInfo> tops = Win32Driver.GetProcessTopWindows(Win32Driver.GetProcessId(appWindow));
                for (int i = 0; i < tops.Count; i++)
                {
                    if (!tops[i].Visible) { continue; }
                    string t = Win32Driver.GetWindowTitle(tops[i].Handle);
                    log(tag + ": top hwnd=" + tops[i].Handle.ToString() +
                        " «" + (t != null ? t.Trim() : "") + "» " + tops[i].Class, "INFO");
                    shown++;
                }
            }
            catch { }
            log(tag + ": listed " + shown + " windows", "INFO");
        }

        private static bool IsInquiryTitle(string title)
        {
            return ArabicText.Contains(title, "استعلام") || ArabicText.Contains(title, "الإستعلام");
        }

        private static bool IsSahbTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) { return false; }
            if (IsInquiryTitle(title)) { return false; }
            return ArabicText.Contains(title, "سحب");
        }

        private static void CloseOpenInquiry(
            IntPtr appWindow, BotAppConfig app, List<FieldSelector> selectors, Action<string, string> log)
        {
            try
            {
                IntPtr look = ClientLookup.FindOpenScreen(appWindow, app, selectors);
                if (look == IntPtr.Zero || !Win32Interop.IsWindow(look)) { return; }
                log("pull-codes: closing stacked «" + Win32Driver.GetWindowTitle(look) +
                    "» before menu", "INFO");
                ClientLookup.CloseWindow(look, "الإستعلام", log);
            }
            catch { }
        }

        private static IntPtr FindSahbWindow(
            IntPtr appWindow, IntPtr inquiry, BotConfig cfg, List<FieldSelector> selectors)
        {
            return FindSahbWindow(appWindow, inquiry, cfg, selectors, 5000);
        }

        private static IntPtr FindSahbWindow(
            IntPtr appWindow, IntPtr inquiry, BotConfig cfg, List<FieldSelector> selectors, int waitMs)
        {
            BotAppConfig app = cfg != null ? cfg.App : null;
            DateTime until = DateTime.Now.AddMilliseconds(Math.Max(400, waitMs));
            while (DateTime.Now < until)
            {
                IntPtr mapped = CodingPostSave.FindOpenPullCodes(appWindow, app, selectors);
                if (mapped != IntPtr.Zero && mapped != inquiry &&
                    !IsInquiryTitle(Win32Driver.GetWindowTitle(mapped)))
                {
                    return mapped;
                }

                IntPtr hit = ScanSahb(appWindow, inquiry);
                if (hit != IntPtr.Zero) { return hit; }
                Thread.Sleep(150);
            }
            return IntPtr.Zero;
        }

        private static IntPtr ScanSahb(IntPtr appWindow, IntPtr inquiry)
        {
            List<W32WindowInfo> forms = Win32Driver.GetFormWindows(appWindow, 8);
            for (int i = 0; i < forms.Count; i++)
            {
                if (!forms[i].Visible) { continue; }
                if (forms[i].Handle == inquiry || forms[i].Handle == appWindow) { continue; }
                if (IsSahbTitle(Win32Driver.GetWindowTitle(forms[i].Handle)))
                {
                    return forms[i].Handle;
                }
            }
            try
            {
                List<W32WindowInfo> tops = Win32Driver.GetProcessTopWindows(Win32Driver.GetProcessId(appWindow));
                for (int i = 0; i < tops.Count; i++)
                {
                    if (!tops[i].Visible) { continue; }
                    if (tops[i].Handle == inquiry || tops[i].Handle == appWindow) { continue; }
                    if (IsSahbTitle(Win32Driver.GetWindowTitle(tops[i].Handle)))
                    {
                        return tops[i].Handle;
                    }
                }
            }
            catch { }
            return IntPtr.Zero;
        }

        private static PullCodesResult FinishF2F9(
            IntPtr screen,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log,
            PullCodesResult result,
            string expectedNationalId)
        {
            if (screen == IntPtr.Zero || !Win32Interop.IsWindow(screen) ||
                IsInquiryTitle(Win32Driver.GetWindowTitle(screen)))
            {
                result.Note = "refusing F2/F9 on الإستعلام — سحب window missing";
                log("pull-codes: " + result.Note, "WARN");
                return result;
            }

            IntPtr grid = ResolveMapped(
                FieldDiscovery.GetFieldMap(screen, false), selectors, GridKey, log);
            FieldMap mapFinish = FieldDiscovery.GetFieldMap(screen, false);
            IntPtr flex = FlexGridSurface.Find(screen, grid);
            if (flex == IntPtr.Zero) { flex = grid; }
            if (flex == IntPtr.Zero) { flex = AutoFindPullGrid(mapFinish); }
            if (flex == IntPtr.Zero)
            {
                result.Note = "شبكة «سحب الأكواد» غير موجودة — لن يتم إرسال F2/F9";
                log("pull-codes: " + result.Note, "WARN");
                return result;
            }

            // ZERO-TOLERANCE: never F2/F9 until the سحب row NID matches the sheet.
            string identityWhy;
            if (!PullRowMatchesExpected(flex, screen, expectedNationalId, log, out identityWhy))
            {
                result.Note = "ZERO-TOLERANCE: رفض F2/F9 — " + identityWhy;
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }

            try
            {
                Win32Driver.SetForeground(screen);
                Thread.Sleep(80);
                if (!FlexGridSurface.ClickTableOnce(flex, log))
                {
                    result.Note = "ZERO-TOLERANCE: فشل ضغط الجدول قبل F2/F9";
                    log("pull-codes: " + result.Note, "ERROR");
                    return result;
                }
                Thread.Sleep(settleMs);
                Win32Driver.SendShortcut(screen, "{F2}", 400);
                log("pull-codes: F2 sent", "OK");
                Thread.Sleep(200);
                Win32Driver.SendShortcut(screen, "{F9}", 600);
                log("pull-codes: F9 sent", "OK");
                Thread.Sleep(settleMs);
                ConfirmDialogResult cd = ConfirmPullSafely(
                    Win32Driver.GetProcessId(screen), screen, log);
                if (PullBlockedByCoding(cd, log))
                {
                    result.Note = "التكويد رفض السحب (موقوف/غير مسموح) بعد F6";
                    log("pull-codes: " + result.Note, "WARN");
                    return result;
                }
            }
            catch (Exception ex)
            {
                result.Note = "F2/F9 failed — " + ex.Message;
                log("pull-codes: " + result.Note, "ERROR");
                return result;
            }

            string why;
            if (!PullSucceeded(screen, flex, log, out why))
            {
                result.Note = "سحب الأكواد بعد F6 لم يكتمل — " + why;
                log("pull-codes: " + result.Note, "WARN");
                return result;
            }
            result.Ok = true;
            result.Note = "سحب: جدول → F2/F9 (identity verified)";
            log("pull-codes: " + result.Note, "OK");
            return result;
        }

        // Answer pull prompts only when identity was already verified. «موقوف» = fail.
        // Unknown prompts get No / are left — never blind Yes outside the pull confirm.
        private static ConfirmDialogResult ConfirmPullSafely(
            int processId, IntPtr owner, Action<string, string> log)
        {
            ConfirmDialogResult all = new ConfirmDialogResult();
            DateTime until = DateTime.Now.AddMilliseconds(8000);
            int handled = 0;
            while (DateTime.Now < until && handled < 4)
            {
                IntPtr dialog = ConfirmDialog.FindModal(processId, owner);
                if (dialog == IntPtr.Zero)
                {
                    Thread.Sleep(200);
                    continue;
                }
                string msg = (Win32Driver.GetWindowTitle(dialog) ?? "") + " " +
                             ConfirmDialog.ReadMessage(dialog);
                all.Messages.Add(msg);
                all.LastTitle = Win32Driver.GetWindowTitle(dialog) ?? "";
                string clicked = "";
                bool isPullAsk = ArabicText.ContainsAny(msg, new string[]
                {
                    "سوف تقوم بسحب", "هل انت متأكد", "هل أنت متأكد"
                });
                bool isBlocked = ArabicText.ContainsAny(msg, new string[]
                {
                    "موقوف", "لا يمكن سحبه", "لا يمكن السحب"
                });
                if (isBlocked || ConfirmDialog.IsServerErrorMessage(msg))
                {
                    ConfirmDialog.ClickAffirmative(dialog, out clicked);
                    all.LastButton = clicked;
                    handled++;
                    if (log != null) { log("pull-codes: dismiss block «" + msg.Trim() + "»", "WARN"); }
                    Thread.Sleep(250);
                    continue;
                }
                if (isPullAsk)
                {
                    // Identity was verified before F2/F9 — confirm the known-good row only.
                    ConfirmDialog.ClickAffirmative(dialog, out clicked);
                    all.LastButton = clicked;
                    handled++;
                    if (log != null)
                    {
                        log("pull-codes: confirm pull «" + msg.Trim() + "» → «" + clicked + "»", "OK");
                    }
                    Thread.Sleep(250);
                    continue;
                }
                // Unknown modal: never Yes — click No / leave closed path.
                if (ConfirmDialog.ClickNegative(dialog, out clicked))
                {
                    all.LastButton = clicked;
                    handled++;
                    if (log != null)
                    {
                        log("pull-codes: ZERO-TOLERANCE unknown prompt → No «" +
                            msg.Trim() + "»", "WARN");
                    }
                }
                else
                {
                    if (log != null)
                    {
                        log("pull-codes: ZERO-TOLERANCE leaving unknown prompt unanswered «" +
                            msg.Trim() + "»", "ERROR");
                    }
                    break;
                }
                Thread.Sleep(250);
            }
            all.Handled = handled;
            return all;
        }

        private static bool SearchReturnedEmpty(IntPtr screen, Action<string, string> log)
        {
            int pid = screen != IntPtr.Zero ? Win32Driver.GetProcessId(screen) : 0;
            IntPtr dialog = ConfirmDialog.FindModal(pid, screen);
            if (dialog == IntPtr.Zero) { return false; }

            string blob = (Win32Driver.GetWindowTitle(dialog) ?? "") + " " + ConfirmDialog.ReadMessage(dialog);
            if (!ArabicText.ContainsAny(blob, new string[]
                {
                    "لا يوجد عملاء", "لا يوجد عملاء متشابهين", "متشابهين مع بيانات",
                    "لا توجد نتائج", "لا يوجد بيانات"
                }))
            {
                return false;
            }

            string clicked;
            ConfirmDialog.ClickAffirmative(dialog, out clicked);
            if (log != null)
            {
                log("pull-codes: search miss «" + blob.Trim() + "» → «" + clicked +
                    "»", "WARN");
            }
            return true;
        }

        private static IntPtr AutoFindPullGrid(FieldMap map)
        {
            if (map != null && map.Window != IntPtr.Zero)
            {
                IntPtr flex = FlexGridSurface.Find(map.Window);
                if (flex != IntPtr.Zero) { return flex; }
            }
            if (map == null || map.Fields == null) { return IntPtr.Zero; }
            IntPtr best = IntPtr.Zero;
            long bestArea = 0;
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (!f.Visible) { continue; }
                if (f.Kind == "TextBox" || f.Kind == "Button" || f.Kind == "CheckBox" ||
                    f.Kind == "ComboBox" || f.Kind == "Label")
                {
                    continue;
                }
                if (f.W < 180 || f.H < 80) { continue; }
                string cls = f.Class != null ? f.Class : "";
                if (cls.IndexOf("grid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cls.IndexOf("ListView", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cls.IndexOf("WindowsForms10.Window", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    string.Equals(f.Kind, "Grid", StringComparison.OrdinalIgnoreCase))
                {
                    long areaHit = (long)f.W * f.H;
                    if (areaHit > bestArea) { bestArea = areaHit; best = f.Handle; }
                    continue;
                }
                long area = (long)f.W * f.H;
                if (area > bestArea) { bestArea = area; best = f.Handle; }
            }
            return best;
        }

        private static IntPtr ResolveMapped(
            FieldMap map, List<FieldSelector> selectors, string fieldKey, Action<string, string> log)
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
                        log("pull-codes: «" + fieldKey + "» not resolved (" + r.Note + ")", "WARN");
                    }
                    else
                    {
                        log("pull-codes: «" + fieldKey + "» by " + r.Tier, "INFO");
                    }
                }
                return r.Handle;
            }
            return IntPtr.Zero;
        }

        private static bool TypeInto(IntPtr box, string value, Action<string, string> log)
        {
            try
            {
                Win32Driver.SetForeground(box);
                Win32Driver.ClickPhysical(box, 120);
                Win32Driver.SetText(box, "");
                Thread.Sleep(50);
                Win32Driver.SetText(box, value);
                Thread.Sleep(120);
                string back = Win32Driver.GetWindowText(box);
                if (string.IsNullOrEmpty(back)) { back = Win32Interop.GetWindowTextValue(box); }
                if (back != null && back.Trim() == value) { return true; }

                Win32Driver.SendShortcut(box, "^a", 50);
                Win32Driver.SendShortcut(box, "{DEL}", 50);
                System.Text.StringBuilder sb = new System.Text.StringBuilder(value.Length + 8);
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if ("+^%~(){}[]".IndexOf(c) >= 0) { sb.Append('{').Append(c).Append('}'); }
                    else { sb.Append(c); }
                }
                Win32Driver.SendShortcut(box, sb.ToString(), 200);
                back = Win32Driver.GetWindowText(box);
                if (string.IsNullOrEmpty(back)) { back = Win32Interop.GetWindowTextValue(box); }
                return back != null && back.Trim() == value;
            }
            catch (Exception ex)
            {
                if (log != null) { log("pull-codes: type failed — " + ex.Message, "WARN"); }
                return false;
            }
        }
    }
}
