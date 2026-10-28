// =============================================================================
//  CodingPostSave.cs - after successful client saves: confirm → F2/F9 → period
//  export → wait Clients*.xml → Reconcile (NIN/CODE12) → optional sheet write-back.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;

namespace RbaCore
{
    public class CodingPostSaveOptions
    {
        public BotConfig Config;
        public List<FieldSelector> Selectors;
        public IntPtr AppWindow;
        // When true: from = 01/01/current year … to = today (Test-Save / sample).
        // When false: from = today AND to = today (production).
        public bool ExportDateRangeYearToDate;
        public string SheetPathForWriteBack;
        public bool DoWriteBack;
        public int SettleMs = 400;
        public int XmlWaitTimeoutMs = 90000;
        public Action<string, string> Log;
        // Isolated tests leave the Coding form open so the operator can inspect F2/F9 / export.
        public bool LeaveScreensOpen;
        // Always click the MDI menu (parent then leaf) — do not reuse a leftover form.
        public bool ForceMenuOpen;
        // F2/F9 once per newly saved client on the confirm (ترحيل) screen.
        public int ConfirmRepeatCount = 1;
    }

    public class CodingPostSaveResult
    {
        public bool Ok;
        public bool ConfirmDone;
        public bool PeriodExportDone;
        public bool ReconcileDone;
        public int Coded;
        public int Rejected;
        public string XmlPath;
        public string Note;
        public List<string> Lines = new List<string>();
    }

    public static class CodingPostSave
    {
        public const string ConfirmGridKey = "confirmGrid";
        public const string PeriodFromDayKey = "periodFromDay";
        public const string PeriodFromMonthKey = "periodFromMonth";
        public const string PeriodFromYearKey = "periodFromYear";
        public const string PeriodToDayKey = "periodToDay";
        public const string PeriodToMonthKey = "periodToMonth";
        public const string PeriodToYearKey = "periodToYear";
        public const string PeriodExportBtnKey = "periodExportBtn";
        public const string PeriodExcelPathKey = "periodExcelPath";
        public const string PeriodExcelBtnKey = "periodExcelBtn";

        private const string DefaultConfirmTitle = "تأكيد بيانات العملاء قبل إرسالها للتكويد";
        private const string DefaultPeriodTitle = "عمليات التكويد خلال فترة";
        // Short menu/form caption used on some Coding builds (avoid a quoted Arabic token).
        private const string PostingCaption = "\u062a\u0631\u062d\u064a\u0644";

        public static bool IsConfirmKey(string fieldKey)
        {
            return string.Equals(fieldKey, ConfirmGridKey, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPeriodKey(string fieldKey)
        {
            if (string.IsNullOrEmpty(fieldKey)) { return false; }
            return string.Equals(fieldKey, PeriodFromDayKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, PeriodFromMonthKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, PeriodFromYearKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, PeriodToDayKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, PeriodToMonthKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, PeriodToYearKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, PeriodExportBtnKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, PeriodExcelPathKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fieldKey, PeriodExcelBtnKey, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPostSaveKey(string fieldKey)
        {
            return IsConfirmKey(fieldKey) || IsPeriodKey(fieldKey);
        }

        public static IntPtr FindOpenPullCodes(IntPtr appWindow, BotAppConfig app, List<FieldSelector> selectors)
        {
            return FindScreen(appWindow, PullCodes.WindowTitle(app), selectors, "pullcodes");
        }

        public static IntPtr EnsurePullCodesScreen(
            IntPtr appWindow, BotAppConfig app, List<FieldSelector> selectors,
            int settleMs, Action<string, string> log)
        {
            return OpenByMenu(appWindow, PullCodes.MenuPath(app), PullCodes.WindowTitle(app),
                selectors, settleMs, log, "pullcodes");
        }

        public static CodingPostSaveResult Run(CodingPostSaveOptions opt)
        {
            CodingPostSaveResult result;
            Action<string, string> log;
            BotAppConfig app;
            List<FieldSelector> selectors;
            int settle;
            if (!TryBegin(opt, out result, out log, out app, out selectors, out settle))
            {
                return result;
            }

            LogDateMode(opt, result, log);

            if (!DoConfirm(opt, result, log, app, selectors, settle)) { return result; }
            if (!DoPeriodExport(opt, result, log, app, selectors, settle)) { return result; }
            DoReconcile(opt, result, log);

            result.Ok = result.ConfirmDone && result.PeriodExportDone && result.ReconcileDone;
            result.Note = result.Ok
                ? "post-save OK — confirm+export+reconcile coded=" + result.Coded
                : "post-save incomplete";
            log(result.Note, result.Ok ? "OK" : "ERROR");
            return result;
        }

        // Isolated: menu → grid click → F2 → F9. No period export.
        public static CodingPostSaveResult RunConfirmOnly(CodingPostSaveOptions opt)
        {
            CodingPostSaveResult result;
            Action<string, string> log;
            BotAppConfig app;
            List<FieldSelector> selectors;
            int settle;
            if (!TryBegin(opt, out result, out log, out app, out selectors, out settle))
            {
                return result;
            }

            log("post-save: CONFIRM-ONLY test (no export)", "INFO");
            if (!DoConfirm(opt, result, log, app, selectors, settle)) { return result; }

            result.Ok = result.ConfirmDone;
            result.Note = "confirm-only OK — F2/F9 sent";
            log(result.Note, "OK");
            return result;
        }

        // Isolated: period dates → path → one Export click → wait Clients*.xml → reconcile.
        public static CodingPostSaveResult RunExportOnly(CodingPostSaveOptions opt)
        {
            CodingPostSaveResult result;
            Action<string, string> log;
            BotAppConfig app;
            List<FieldSelector> selectors;
            int settle;
            if (!TryBegin(opt, out result, out log, out app, out selectors, out settle))
            {
                return result;
            }

            LogDateMode(opt, result, log);
            log("post-save: EXPORT-ONLY test (no confirm F2/F9)", "INFO");
            if (!DoPeriodExport(opt, result, log, app, selectors, settle)) { return result; }
            DoReconcile(opt, result, log);

            result.Ok = result.PeriodExportDone && result.ReconcileDone;
            result.Note = result.Ok
                ? "export-only OK — xml=" + Path.GetFileName(result.XmlPath) + " coded=" + result.Coded
                : "export-only incomplete";
            log(result.Note, result.Ok ? "OK" : "ERROR");
            return result;
        }

        private static bool TryBegin(
            CodingPostSaveOptions opt,
            out CodingPostSaveResult result,
            out Action<string, string> log,
            out BotAppConfig app,
            out List<FieldSelector> selectors,
            out int settle)
        {
            result = new CodingPostSaveResult();
            log = opt != null && opt.Log != null ? opt.Log : delegate(string m, string l) { };
            app = null;
            selectors = null;
            settle = 400;

            if (opt == null || opt.Config == null)
            {
                Fail(result, log, "CodingPostSave: missing config");
                return false;
            }
            if (opt.AppWindow == IntPtr.Zero || !Win32Interop.IsWindow(opt.AppWindow))
            {
                Fail(result, log, "CodingPostSave: Coding window not attached");
                return false;
            }
            CodingHealthState health = CodingHealth.Probe(opt.AppWindow);
            if (health != CodingHealthState.Ok)
            {
                Fail(result, log, "CodingPostSave: " + CodingHealth.Describe(health));
                return false;
            }

            app = opt.Config.App != null ? opt.Config.App : new BotAppConfig();
            SanitizeAppPaths(app, log);
            settle = opt.SettleMs > 0 ? opt.SettleMs : 400;
            selectors = opt.Selectors != null
                ? opt.Selectors
                : FieldDiscovery.ImportSelectors(opt.Config.SelectorsPath);
            return true;
        }

        private static void LogDateMode(
            CodingPostSaveOptions opt, CodingPostSaveResult result, Action<string, string> log)
        {
            string dateMode = opt.ExportDateRangeYearToDate
                ? "YEAR_TO_DATE (01/01 → today) — test/sample"
                : "TODAY_ONLY (today → today) — production";
            log("post-save: date range mode = " + dateMode, "INFO");
            result.Lines.Add("dateMode=" + dateMode);
        }

        private static bool DoConfirm(
            CodingPostSaveOptions opt,
            CodingPostSaveResult result,
            Action<string, string> log,
            BotAppConfig app,
            List<FieldSelector> selectors,
            int settle)
        {
            BotConfig cfg = opt.Config;
            app.ConfirmMenuPath = new string[]
            {
                "نظام تكويد العملاء", "تأكيد بيانات العملاء قبل إرسالها للتكويد"
            };
            app.ConfirmWindowTitle = DefaultConfirmTitle;
            log("post-save: menu نظام تكويد العملاء → تأكيد بيانات العملاء قبل إرسالها للتكويد", "INFO");
            IntPtr confirm = OpenByMenu(
                opt.AppWindow, app.ConfirmMenuPath, DefaultConfirmTitle, selectors, settle, log, "confirm",
                opt.ForceMenuOpen);
            if (confirm == IntPtr.Zero)
            {
                Fail(result, log,
                    "post-save: confirm screen did not open ('" + ConfirmTitle(app) +
                    "') - open windows: " + DumpOpenTitles(opt.AppWindow));
                return false;
            }

            if (cfg.Windows != null && cfg.Windows.ConfirmScreen != null && cfg.Windows.ConfirmScreen.IsValid)
            {
                WindowLayout.Apply(confirm, cfg.Windows.ConfirmScreen, log);
                Thread.Sleep(200);
            }
            RememberScreen(cfg, confirm, true, false, log);

            int times = opt.ConfirmRepeatCount < 1 ? 1 : opt.ConfirmRepeatCount;
            log("post-save: ترحيل F2/F9 × " + times + " (عدد العملاء الجدد المحفوظين)", "INFO");
            if (!RunConfirmKeys(confirm, selectors, settle, log, times))
            {
                if (!opt.LeaveScreensOpen)
                {
                    ClientLookup.CloseWindow(confirm, "confirm screen", log);
                }
                Fail(result, log, "post-save: confirm F2/F9 failed — map «" + ConfirmGridKey + "» in Setup");
                return false;
            }

            result.ConfirmDone = true;
            log("post-save: confirm F2/F9 done × " + times, "OK");
            if (opt.LeaveScreensOpen)
            {
                log("post-save: left confirm screen open for inspection", "INFO");
            }
            else
            {
                ClientLookup.CloseWindow(confirm, "confirm screen", log);
            }
            Thread.Sleep(settle);
            return true;
        }

        private static bool DoPeriodExport(
            CodingPostSaveOptions opt,
            CodingPostSaveResult result,
            Action<string, string> log,
            BotAppConfig app,
            List<FieldSelector> selectors,
            int settle)
        {
            BotConfig cfg = opt.Config;
            IntPtr period = OpenByMenu(
                opt.AppWindow, app.PeriodMenuPath, PeriodTitle(app), selectors, settle, log, "period");
            if (period == IntPtr.Zero)
            {
                Fail(result, log, "post-save: period screen did not open («" + PeriodTitle(app) + "»)");
                return false;
            }

            if (cfg.Windows != null && cfg.Windows.PeriodScreen != null && cfg.Windows.PeriodScreen.IsValid)
            {
                WindowLayout.Apply(period, cfg.Windows.PeriodScreen, log);
                Thread.Sleep(250);
                WindowLayout.Apply(period, cfg.Windows.PeriodScreen, log);
                Thread.Sleep(150);
            }
            else
            {
                log("post-save: period window size not saved yet — capturing current size (map may miss until you size it in Setup)", "WARN");
            }
            RememberScreen(cfg, period, false, false, log);

            DateTime toDate = DateTime.Today;
            DateTime fromDate = opt.ExportDateRangeYearToDate
                ? new DateTime(toDate.Year, 1, 1)
                : toDate;
            log("post-save: filling dates from=" + fromDate.ToString("dd/MM/yyyy") +
                " to=" + toDate.ToString("dd/MM/yyyy"), "INFO");

            if (!FillPeriodDates(period, selectors, fromDate, toDate, settle, log))
            {
                if (!opt.LeaveScreensOpen)
                {
                    ClientLookup.CloseWindow(period, "period screen", log);
                }
                Fail(result, log, "post-save: could not fill period date fields");
                return false;
            }

            string exportFolder = cfg.ExportFolder;
            if (string.IsNullOrWhiteSpace(exportFolder)) { exportFolder = @"C:\ClientsFilesExcel"; }
            if (!Directory.Exists(exportFolder))
            {
                try { Directory.CreateDirectory(exportFolder); }
                catch (Exception ex)
                {
                    if (!opt.LeaveScreensOpen)
                    {
                        ClientLookup.CloseWindow(period, "period screen", log);
                    }
                    Fail(result, log, "post-save: cannot create export folder — " + ex.Message);
                    return false;
                }
            }

            try { Win32Driver.SetForeground(period); } catch { }
            Thread.Sleep(120);

            DateTime xmlSince = DateTime.Now.AddSeconds(-2);
            if (!ClickExportAndExcel(period, selectors, exportFolder, settle, log))
            {
                if (!opt.LeaveScreensOpen)
                {
                    ClientLookup.CloseWindow(period, "period screen", log);
                }
                Fail(result, log, "post-save: export/excel click failed — map period buttons in Setup");
                return false;
            }
            result.PeriodExportDone = true;
            log("post-save: export/excel clicked — waiting for Clients*.xml in " + exportFolder, "INFO");

            int pid = Win32Driver.GetProcessId(opt.AppWindow);
            ConfirmDialog.ConfirmAll(pid, period, 5000, 4, log);

            int waitMs = opt.XmlWaitTimeoutMs > 0 ? opt.XmlWaitTimeoutMs : 90000;
            string xml = ReportParsers.WaitForNewCodingExport(exportFolder, xmlSince, waitMs, log);
            if (!opt.LeaveScreensOpen)
            {
                ClientLookup.CloseWindow(period, "period screen", log);
            }
            else
            {
                log("post-save: left period screen open for inspection", "INFO");
            }

            if (string.IsNullOrEmpty(xml))
            {
                xml = ReportParsers.GetLatestCodingExport(exportFolder);
                if (string.IsNullOrEmpty(xml))
                {
                    Fail(result, log, "post-save: no Clients*.xml appeared in " + exportFolder);
                    return false;
                }
                log("post-save: using latest XML (wait timed out): " + Path.GetFileName(xml), "WARN");
            }
            result.XmlPath = xml;
            log("post-save: XML ready → " + xml, "OK");
            return true;
        }

        private static void DoReconcile(
            CodingPostSaveOptions opt, CodingPostSaveResult result, Action<string, string> log)
        {
            BotConfig cfg = opt.Config;
            string exportFolder = cfg.ExportFolder;
            if (string.IsNullOrWhiteSpace(exportFolder)) { exportFolder = @"C:\ClientsFilesExcel"; }

            ReconcileResult rr = Reconcile.Run(exportFolder, cfg.TrackingPath, 1256);
            for (int i = 0; i < rr.LogLines.Count; i++)
            {
                log(rr.LogLines[i], "INFO");
                result.Lines.Add(rr.LogLines[i]);
            }
            result.Coded = rr.Coded;
            result.Rejected = rr.Rejected;
            result.ReconcileDone = true;

            List<string> sheets = new List<string>();
            AddSheet(sheets, opt.SheetPathForWriteBack);
            AddSheet(sheets, cfg.SheetPath);
            if (!string.IsNullOrWhiteSpace(cfg.BaseDir))
            {
                AddSheet(sheets, Path.Combine(cfg.BaseDir, @"Sample\Test-Save-Client.csv"));
            }
            try
            {
                string exeDir = Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(exeDir))
                {
                    AddSheet(sheets, Path.Combine(exeDir, @"Sample\Test-Save-Client.csv"));
                }
            }
            catch { }

            bool wantWrite = opt.DoWriteBack || sheets.Count > 0;
            if (wantWrite && !string.IsNullOrEmpty(result.XmlPath) && File.Exists(result.XmlPath))
            {
                for (int s = 0; s < sheets.Count; s++)
                {
                    try
                    {
                        SheetUpdateResult ur = SheetSync.UpdateFromCodingXml(sheets[s], result.XmlPath);
                        if (ur.Lines != null)
                        {
                            for (int n = 0; n < ur.Lines.Count; n++)
                            {
                                log(ur.Lines[n], ur.Lines[n].IndexOf("MISS", StringComparison.OrdinalIgnoreCase) >= 0
                                    ? "WARN" : "OK");
                            }
                        }
                        log("post-save: CSV↔XML «" + Path.GetFileName(ur.Path) + "» matched=" +
                            ur.Updated + " missing=" + ur.Unmatched, ur.Unmatched > 0 ? "WARN" : "OK");
                        result.Lines.Add("writeBack=" + ur.Path + " updated=" + ur.Updated +
                            " unmatched=" + ur.Unmatched);
                    }
                    catch (Exception ex)
                    {
                        log("post-save: sheet write-back failed (" + sheets[s] + ") — " + ex.Message, "WARN");
                        result.Lines.Add("writeBackFail=" + ex.Message);
                    }
                }
            }
            else if (string.IsNullOrEmpty(result.XmlPath))
            {
                log("post-save: no XML — cannot update main sheet with NIN/CODE12", "WARN");
            }
        }

        private static void AddSheet(List<string> sheets, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return; }
            string full = Path.GetFullPath(path);
            for (int i = 0; i < sheets.Count; i++)
            {
                if (string.Equals(sheets[i], full, StringComparison.OrdinalIgnoreCase)) { return; }
            }
            sheets.Add(full);
        }

        private static bool ClickConfirmTable(
            IntPtr screen, List<FieldSelector> selectors, Action<string, string> log)
        {
            FieldMap map = FieldDiscovery.GetFieldMap(screen, false);
            IntPtr grid = ResolveOne(map, selectors, ConfirmGridKey, log);
            if (grid == IntPtr.Zero) { grid = AutoFindGrid(map); }
            if (grid != IntPtr.Zero)
            {
                try
                {
                    Win32Driver.SetForeground(screen);
                    Thread.Sleep(80);
                    if (FlexGridSurface.IsFlexClass(Win32Driver.GetClassName(grid)))
                    {
                        if (FlexGridSurface.ClickTableOnce(grid, log)) { return true; }
                    }
                    Win32Driver.ClickPhysical(grid, 200);
                    log("post-save: confirm grid clicked " + Describe(grid), "INFO");
                    return true;
                }
                catch (Exception ex)
                {
                    log("post-save: confirm grid click failed — " + ex.Message, "ERROR");
                    return false;
                }
            }
            if (ClickMappedPosition(screen, selectors, ConfirmGridKey, log))
            {
                log("post-save: confirm grid clicked via mapped position", "INFO");
                return true;
            }
            return false;
        }

        private static bool RunConfirmKeys(
            IntPtr screen, List<FieldSelector> selectors, int settleMs, Action<string, string> log, int times)
        {
            if (times < 1) { times = 1; }
            try
            {
                for (int n = 1; n <= times; n++)
                {
                    if (!ClickConfirmTable(screen, selectors, log))
                    {
                        log("post-save: ZERO-TOLERANCE — جدول التأكيد غير موجود؛ لن يُرسل F2/F9", "ERROR");
                        return false;
                    }

                    Thread.Sleep(settleMs);
                    Win32Driver.SendShortcut(screen, "{F2}", 400);
                    log("post-save: F2 sent (" + n + "/" + times + ")", "OK");
                    Thread.Sleep(200);
                    Win32Driver.SendShortcut(screen, "{F9}", 600);
                    log("post-save: F9 sent (" + n + "/" + times + ")", "OK");
                    Thread.Sleep(settleMs);
                    int pid = Win32Driver.GetProcessId(screen);
                    ConfirmDialogResult pop = ConfirmDialog.ConfirmAll(pid, screen, 12000, 6, log);
                    if (pop.Handled > 0)
                    {
                        log("post-save: confirm popup «" + pop.LastTitle + "» → «" +
                            pop.LastButton + "» (" + pop.Handled + ") [" + n + "/" + times + "]", "OK");
                    }
                    else
                    {
                        log("post-save: no confirm popup after F9 (watched 12s) [" + n + "/" + times + "]", "WARN");
                    }
                    if (pop.SaidServerError)
                    {
                        log("post-save: Coding runtime error after F2/F9 — stopping ترحيل", "ERROR");
                        return false;
                    }
                    if (!Win32Interop.IsWindow(screen))
                    {
                        log("post-save: confirm window closed after F2/F9 — stopping", "ERROR");
                        return false;
                    }
                    if (n < times) { Thread.Sleep(Math.Max(400, settleMs)); }
                }
                return true;
            }
            catch (Exception ex)
            {
                log("post-save: confirm keys failed — " + ex.Message, "ERROR");
                return false;
            }
        }

        private static bool FillPeriodDates(
            IntPtr screen,
            List<FieldSelector> selectors,
            DateTime fromDate,
            DateTime toDate,
            int settleMs,
            Action<string, string> log)
        {
            FieldMap map = FieldDiscovery.GetFieldMap(screen, false);
            string[] keys = new string[]
            {
                PeriodFromDayKey, PeriodFromMonthKey, PeriodFromYearKey,
                PeriodToDayKey, PeriodToMonthKey, PeriodToYearKey
            };
            string[] values = new string[]
            {
                fromDate.Day.ToString(), fromDate.Month.ToString(), fromDate.Year.ToString(),
                toDate.Day.ToString(), toDate.Month.ToString(), toDate.Year.ToString()
            };

            int ok = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                IntPtr box = ResolveOne(map, selectors, keys[i], log);
                if (box == IntPtr.Zero)
                {
                    log("post-save: date field «" + keys[i] + "» not mapped", "ERROR");
                    continue;
                }
                if (TypeInto(box, values[i], log))
                {
                    ok++;
                    log("post-save: " + keys[i] + " ← " + values[i], "OK");
                }
                else
                {
                    log("post-save: failed typing " + keys[i] + "=" + values[i], "ERROR");
                }
                Thread.Sleep(Math.Max(80, settleMs / 4));
            }
            return ok == keys.Length;
        }

        private static bool ClickExportAndExcel(
            IntPtr screen,
            List<FieldSelector> selectors,
            string exportFolder,
            int settleMs,
            Action<string, string> log)
        {
            FieldMap map = FieldDiscovery.GetFieldMap(screen, false);
            try { Win32Driver.SetForeground(screen); } catch { }

            IntPtr pathBox = ResolveOne(map, selectors, PeriodExcelPathKey, log);
            if (pathBox == IntPtr.Zero || !IsInputHwnd(pathBox, map))
            {
                pathBox = AutoFindPeriodPathBox(map);
                if (pathBox != IntPtr.Zero)
                {
                    log("post-save: XML path via widest TextBox on period form", "INFO");
                }
            }
            if (pathBox == IntPtr.Zero)
            {
                if (ClickMappedPosition(screen, selectors, PeriodExcelPathKey, log))
                {
                    map = FieldDiscovery.GetFieldMap(screen, false);
                    pathBox = ResolveOne(map, selectors, PeriodExcelPathKey, log);
                    if (pathBox == IntPtr.Zero) { pathBox = AutoFindPeriodPathBox(map); }
                }
            }
            if (pathBox != IntPtr.Zero && IsInputHwnd(pathBox, map))
            {
                string pathValue = exportFolder;
                if (!pathValue.EndsWith("\\") && !pathValue.EndsWith("/"))
                {
                    pathValue = pathValue + "\\";
                }
                if (!TypeInto(pathBox, pathValue, log))
                {
                    log("post-save: could not set XML path — continuing with mapped path as-is", "WARN");
                }
                else
                {
                    log("post-save: XML path ← " + pathValue, "OK");
                }
                try { Win32Driver.SendShortcut(pathBox, "{TAB}", 80); } catch { }
                Thread.Sleep(Math.Max(settleMs, 250));
            }
            else
            {
                log("post-save: «" + PeriodExcelPathKey + "» not mapped — relying on Coding default path", "WARN");
            }

            try { Win32Driver.SetForeground(screen); } catch { }
            Thread.Sleep(120);
            map = FieldDiscovery.GetFieldMap(screen, false);

            // One Export button → Clients*.xml. Never click the title bar (form-origin stamp).
            string[] exportTips = new string[] { "Export", "تصدير", "إكسل", "اكسل", "Excel" };
            if (AccessibleText.ClickNamed(screen, exportTips, log))
            {
                log("post-save: Export via MSAA name", "OK");
            }
            else if (!ClickPeriodControl(screen, map, selectors, PeriodExportBtnKey, exportTips, log) &&
                !ClickPeriodControl(screen, map, selectors, PeriodExcelBtnKey, exportTips, log))
            {
                log("post-save: Export button not clicked — أعد التقاط زر Export (ليس شريط العنوان)", "ERROR");
                return false;
            }
            Thread.Sleep(Math.Max(settleMs, 500));
            ConfirmDialog.ConfirmIfPresent(Win32Driver.GetProcessId(screen), screen, 4000, 3, log);
            return true;
        }

        private static bool TryMappedClickPoint(
            IntPtr screen,
            List<FieldSelector> selectors,
            string fieldKey,
            IntPtr pathBox,
            out int x,
            out int y)
        {
            x = 0;
            y = 0;
            if (selectors == null || screen == IntPtr.Zero) { return false; }
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                if (s == null || !string.Equals(s.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                int xRel, yRel;
                if (!int.TryParse(s.PosX, out xRel) || !int.TryParse(s.PosY, out yRel))
                {
                    continue;
                }
                int w = 24;
                int h = 24;
                int parsedW, parsedH;
                if (int.TryParse(s.PosW, out parsedW) && parsedW > 0) { w = parsedW; }
                if (int.TryParse(s.PosH, out parsedH) && parsedH > 0) { h = parsedH; }
                // A form-sized stamp means the capture hit the window, not the button.
                if (w > 160) { w = 24; }
                if (h > 60) { h = 24; }
                try
                {
                    Win32Driver.RectInfo form = Win32Driver.GetRect(screen);
                    x = form.X + xRel + w / 2;
                    y = form.Y + yRel + h / 2;
                    NudgeOffPathBox(pathBox, ref x, ref y);
                    return true;
                }
                catch { continue; }
            }
            return false;
        }

        private static void NudgeOffPathBox(IntPtr pathBox, ref int x, ref int y)
        {
            if (pathBox == IntPtr.Zero || !Win32Interop.IsWindow(pathBox)) { return; }
            try
            {
                Win32Driver.RectInfo p = Win32Driver.GetRect(pathBox);
                if (x < p.X || x > p.Right || y < p.Y || y > p.Bottom) { return; }
                int distL = x - p.X;
                int distR = p.Right - x;
                int distT = y - p.Y;
                int distB = p.Bottom - y;
                int pad = 28;
                if (distL <= distR && distL <= distT && distL <= distB) { x = p.X - pad; }
                else if (distR <= distT && distR <= distB) { x = p.Right + pad; }
                else if (distT <= distB) { y = p.Y - pad; }
                else { y = p.Bottom + pad; }
            }
            catch { }
        }

        private static IntPtr ResolveButtonHwnd(
            FieldMap map, List<FieldSelector> selectors, string fieldKey, Action<string, string> log)
        {
            IntPtr hwnd = ResolveOne(map, selectors, fieldKey, log);
            if (hwnd == IntPtr.Zero)
            {
                hwnd = ResolveByPositionIgnoreKind(map, selectors, fieldKey, log);
            }
            if (hwnd != IntPtr.Zero && IsInputHwnd(hwnd, map))
            {
                if (log != null)
                {
                    log("post-save: «" + fieldKey + "» HWND is an input (" +
                        Describe(hwnd) + ") — ignoring, will click mapped point", "WARN");
                }
                return IntPtr.Zero;
            }
            return hwnd;
        }

        private static bool IsInputHwnd(IntPtr hwnd, FieldMap map)
        {
            if (hwnd == IntPtr.Zero) { return false; }
            string cls = Win32Driver.GetClassName(hwnd) ?? "";
            string kind = FieldDiscovery.GetControlKind(cls);
            if (kind == "TextBox" || kind == "ComboBox" || kind == "ListBox" ||
                kind == "DatePicker")
            {
                return true;
            }
            if (map != null && map.Fields != null)
            {
                for (int i = 0; i < map.Fields.Count; i++)
                {
                    if (map.Fields[i].Handle != hwnd) { continue; }
                    string k = map.Fields[i].Kind ?? "";
                    return k == "TextBox" || k == "ComboBox" || k == "ListBox";
                }
            }
            return false;
        }

        // HWND → caption/MSAA → mapped point (never the title bar).
        private static bool ClickPeriodControl(
            IntPtr screen,
            FieldMap map,
            List<FieldSelector> selectors,
            string fieldKey,
            string[] captionTips,
            Action<string, string> log)
        {
            IntPtr hwnd = ResolveButtonHwnd(map, selectors, fieldKey, log);
            if (hwnd != IntPtr.Zero)
            {
                ClickHwnd(hwnd, fieldKey, log);
                return true;
            }
            if (AccessibleText.ClickNamed(screen, captionTips, log))
            {
                return true;
            }
            hwnd = FindByCaptionTips(map, screen, captionTips, log);
            if (hwnd != IntPtr.Zero && !IsInputHwnd(hwnd, map))
            {
                log("post-save: «" + fieldKey + "» matched caption «" +
                    Win32Driver.GetWindowText(hwnd) + "»", "INFO");
                ClickHwnd(hwnd, fieldKey, log);
                return true;
            }
            if (ClickMappedPosition(screen, selectors, fieldKey, log))
            {
                return true;
            }
            DumpPeriodButtons(map, screen, log);
            return false;
        }

        private static IntPtr AutoFindPeriodPathBox(FieldMap map)
        {
            if (map == null || map.Fields == null) { return IntPtr.Zero; }
            IntPtr best = IntPtr.Zero;
            int bestW = 70;
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f == null || !f.Visible) { continue; }
                if (!string.Equals(f.Kind, "TextBox", StringComparison.OrdinalIgnoreCase)) { continue; }
                if (f.W < bestW) { continue; }
                if (f.H > 40) { continue; }
                bestW = f.W;
                best = f.Handle;
            }
            return best;
        }

        private static void ClickHwnd(IntPtr hwnd, string fieldKey, Action<string, string> log)
        {
            try { Win32Driver.SetForeground(hwnd); } catch { }
            try { Win32Driver.ClickPhysical(hwnd, 180); }
            catch (Exception ex)
            {
                if (log != null) { log("post-save: physical click «" + fieldKey + "» — " + ex.Message, "WARN"); }
            }
            try { Win32Driver.Click(hwnd); }
            catch { }
            if (log != null)
            {
                log("post-save: clicked HWND for «" + fieldKey + "» " + Describe(hwnd), "OK");
            }
        }

        private static IntPtr ResolveByPositionIgnoreKind(
            FieldMap map, List<FieldSelector> selectors, string fieldKey, Action<string, string> log)
        {
            if (selectors == null) { return IntPtr.Zero; }
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                if (s == null || !string.Equals(s.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!s.HasPosition) { continue; }
                string oldKind = s.Kind;
                string oldClass = s.Class;
                s.Kind = "";
                s.Class = "";
                ResolveResult r = FieldDiscovery.ResolveByPosition(map, s);
                s.Kind = oldKind;
                s.Class = oldClass;
                if (r.Handle != IntPtr.Zero)
                {
                    if (log != null)
                    {
                        log("post-save: «" + fieldKey + "» resolved by Position (kind ignored)", "INFO");
                    }
                    return r.Handle;
                }
            }
            return IntPtr.Zero;
        }

        private static IntPtr FindByCaptionTips(
            FieldMap map, IntPtr screen, string[] tips, Action<string, string> log)
        {
            if (tips == null || tips.Length == 0) { return IntPtr.Zero; }
            List<AppField> pool = new List<AppField>();
            if (map != null && map.AllControls != null) { pool.AddRange(map.AllControls); }
            else if (map != null && map.Fields != null) { pool.AddRange(map.Fields); }

            List<IntPtr> hits = new List<IntPtr>();
            for (int t = 0; t < tips.Length; t++)
            {
                hits.Clear();
                for (int i = 0; i < pool.Count; i++)
                {
                    AppField f = pool[i];
                    if (f == null || f.Handle == IntPtr.Zero) { continue; }
                    if (CaptionHits(f.Caption, tips[t]) || CaptionHits(f.Label, tips[t]) ||
                        CaptionHits(f.AccName, tips[t]))
                    {
                        hits.Add(f.Handle);
                    }
                }
                if (hits.Count == 1) { return hits[0]; }
                if (hits.Count > 1)
                {
                    IntPtr button = FirstKind(pool, hits, "Button");
                    return button != IntPtr.Zero ? button : hits[0];
                }
            }

            if (screen != IntPtr.Zero)
            {
                List<W32WindowInfo> kids = Win32Driver.GetChildren(screen);
                for (int t = 0; t < tips.Length; t++)
                {
                    for (int i = 0; i < kids.Count; i++)
                    {
                        if (CaptionHits(kids[i].Text, tips[t])) { return kids[i].Handle; }
                    }
                }
            }
            return IntPtr.Zero;
        }

        private static IntPtr FirstKind(List<AppField> pool, List<IntPtr> handles, string kind)
        {
            for (int i = 0; i < handles.Count; i++)
            {
                for (int p = 0; p < pool.Count; p++)
                {
                    if (pool[p].Handle == handles[i] &&
                        string.Equals(pool[p].Kind, kind, StringComparison.OrdinalIgnoreCase))
                    {
                        return handles[i];
                    }
                }
            }
            return IntPtr.Zero;
        }

        private static bool CaptionHits(string text, string tip)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(tip)) { return false; }
            if (ArabicText.Contains(text, tip)) { return true; }
            return text.IndexOf(tip, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void DumpPeriodButtons(FieldMap map, IntPtr screen, Action<string, string> log)
        {
            if (log == null) { return; }
            List<AppField> pool = map != null && map.AllControls != null ? map.AllControls
                : (map != null ? map.Fields : null);
            int n = 0;
            if (pool != null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    AppField f = pool[i];
                    if (f == null) { continue; }
                    bool interesting = string.Equals(f.Kind, "Button", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(f.Kind, "Picture", StringComparison.OrdinalIgnoreCase) ||
                        !string.IsNullOrWhiteSpace(f.Caption);
                    if (!interesting) { continue; }
                    n++;
                    log("post-save: control[" + n + "] kind=" + f.Kind + " class=" + f.Class +
                        " cap=«" + (f.Caption ?? "") + "» lab=«" + (f.Label ?? "") + "»", "INFO");
                }
            }
            if (n == 0 && screen != IntPtr.Zero)
            {
                List<W32WindowInfo> kids = Win32Driver.GetChildren(screen);
                for (int i = 0; i < kids.Count && n < 20; i++)
                {
                    if (string.IsNullOrWhiteSpace(kids[i].Text)) { continue; }
                    n++;
                    log("post-save: child[" + n + "] «" + kids[i].Text + "» [" + kids[i].Class + "]", "INFO");
                }
            }
        }

        private static bool TypeInto(IntPtr box, string value, Action<string, string> log)
        {
            try
            {
                Win32Driver.SetForeground(box);
                Win32Driver.ClickPhysical(box, 100);
                Win32Driver.SetText(box, "");
                Thread.Sleep(40);
                Win32Driver.SetText(box, value);
                Thread.Sleep(80);
                string back = Win32Driver.GetWindowText(box);
                if (string.IsNullOrEmpty(back)) { back = Win32Interop.GetWindowTextValue(box); }
                if (back != null && back.Trim() == value) { return true; }

                Win32Driver.SendShortcut(box, "^a", 40);
                Win32Driver.SendShortcut(box, "{DEL}", 40);
                Win32Driver.SendShortcut(box, EscapeSendKeys(value), 120);
                back = Win32Driver.GetWindowText(box);
                if (string.IsNullOrEmpty(back)) { back = Win32Interop.GetWindowTextValue(box); }
                return back != null && back.Trim() == value;
            }
            catch (Exception ex)
            {
                if (log != null) { log("post-save: TypeInto failed — " + ex.Message, "WARN"); }
                return false;
            }
        }

        private static string EscapeSendKeys(string value)
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

        private static IntPtr OpenByMenu(
            IntPtr appWindow,
            string[] menuPath,
            string title,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log,
            string kind)
        {
            return OpenByMenu(appWindow, menuPath, title, selectors, settleMs, log, kind, false);
        }

        private static IntPtr OpenByMenu(
            IntPtr appWindow,
            string[] menuPath,
            string title,
            List<FieldSelector> selectors,
            int settleMs,
            Action<string, string> log,
            string kind,
            bool forceMenu)
        {
            menuPath = SanitizeMenuPath(menuPath, kind);
            if (!forceMenu)
            {
                IntPtr already = FindScreen(appWindow, title, selectors, kind);
                if (already != IntPtr.Zero)
                {
                    log("post-save: " + kind + " already open «" + Win32Driver.GetWindowTitle(already) + "»", "INFO");
                    return already;
                }
            }
            else
            {
                log("post-save: forcing menu clicks «" + Join(menuPath) + "»", "INFO");
            }

            try
            {
                Win32Driver.SetForeground(appWindow);
                Thread.Sleep(150);
            }
            catch { }

            MenuNavResult menu;
            if (string.Equals(kind, "pullcodes", StringComparison.OrdinalIgnoreCase))
            {
                menu = MenuNavigator.OpenPullWithFallbacks(appWindow, menuPath, settleMs);
            }
            else if (string.Equals(kind, "confirm", StringComparison.OrdinalIgnoreCase))
            {
                menu = MenuNavigator.OpenConfirmWithFallbacks(appWindow, menuPath, settleMs);
            }
            else
            {
                menu = MenuNavigator.OpenPath(appWindow, menuPath, settleMs);
            }
            if (!menu.Ok)
            {
                log("post-save: menu «" + Join(menuPath) + "» failed — " + menu.Note, "WARN");
                // Operator may already have the form open; keep searching (common Setup mistake).
                IntPtr openAnyway = WaitForScreen(appWindow, title, selectors, kind, settleMs * 8);
                if (openAnyway != IntPtr.Zero)
                {
                    log("post-save: " + kind + " found open after menu miss «" +
                        Win32Driver.GetWindowTitle(openAnyway) + "»", "OK");
                    return openAnyway;
                }
                log("post-save: open titles after menu miss → " + DumpOpenTitles(appWindow), "WARN");
                return IntPtr.Zero;
            }

            IntPtr h = WaitForScreen(appWindow, title, selectors, kind, settleMs * 12);
            if (h != IntPtr.Zero) { return h; }
            log("post-save: no '" + kind + "' window after menu (wanted '" + title +
                "') - open: " + DumpOpenTitles(appWindow), "WARN");
            return IntPtr.Zero;
        }

        private static IntPtr WaitForScreen(
            IntPtr appWindow, string title, List<FieldSelector> selectors, string kind, int waitMs)
        {
            DateTime until = DateTime.Now.AddMilliseconds(Math.Max(800, waitMs));
            while (DateTime.Now < until)
            {
                if (appWindow == IntPtr.Zero || !Win32Interop.IsWindow(appWindow))
                {
                    return IntPtr.Zero;
                }
                CodingHealthState health = CodingHealth.Probe(appWindow, 800);
                if (health == CodingHealthState.Hung || health == CodingHealthState.Closed)
                {
                    return IntPtr.Zero;
                }
                IntPtr h = FindScreen(appWindow, title, selectors, kind);
                if (h != IntPtr.Zero) { return h; }
                Thread.Sleep(120);
            }
            return FindScreen(appWindow, title, selectors, kind);
        }

        public static IntPtr FindOpenConfirm(IntPtr appWindow, BotAppConfig app, List<FieldSelector> selectors)
        {
            return FindScreen(appWindow, ConfirmTitle(app), selectors, "confirm");
        }

        public static IntPtr FindOpenPeriod(IntPtr appWindow, BotAppConfig app, List<FieldSelector> selectors)
        {
            return FindScreen(appWindow, PeriodTitle(app), selectors, "period");
        }

        public static IntPtr EnsureConfirmScreen(
            IntPtr appWindow, BotAppConfig app, List<FieldSelector> selectors,
            int settleMs, Action<string, string> log)
        {
            return OpenByMenu(appWindow, app.ConfirmMenuPath, ConfirmTitle(app), selectors, settleMs, log, "confirm");
        }

        public static IntPtr EnsurePeriodScreen(
            IntPtr appWindow, BotAppConfig app, List<FieldSelector> selectors,
            int settleMs, Action<string, string> log)
        {
            return OpenByMenu(appWindow, app.PeriodMenuPath, PeriodTitle(app), selectors, settleMs, log, "period");
        }

        private static IntPtr FindScreen(
            IntPtr appWindow, string title, List<FieldSelector> selectors, string kind)
        {
            List<IntPtr> candidates = CandidateWindows(appWindow);

            List<string> titles = new List<string>();
            AddTitle(titles, title);
            if (string.Equals(kind, "confirm", StringComparison.OrdinalIgnoreCase))
            {
                AddTitle(titles, DefaultConfirmTitle);
                AddTitle(titles, "تأكيد بيانات العملاء");
                AddTitle(titles, "تأكيد بيانات");
                AddTitle(titles, "تأكيد");
                AddTitle(titles, PostingCaption);
            }
            else if (string.Equals(kind, "pullcodes", StringComparison.OrdinalIgnoreCase))
            {
                AddTitle(titles, PullCodes.DefaultTitle);
                AddTitle(titles, "سحب الأكواد");
                AddTitle(titles, "سحب اكواد");
            }
            else
            {
                AddTitle(titles, DefaultPeriodTitle);
                AddTitle(titles, "عملاء التكويد خلال فترة");
                AddTitle(titles, "خلال فترة");
                AddTitle(titles, "عمليات التكويد");
            }
            if (selectors != null)
            {
                for (int i = 0; i < selectors.Count; i++)
                {
                    FieldSelector s = selectors[i];
                    if (s == null) { continue; }
                    bool match;
                    if (string.Equals(kind, "confirm", StringComparison.OrdinalIgnoreCase))
                    {
                        match = IsConfirmKey(s.FieldKey);
                    }
                    else if (string.Equals(kind, "pullcodes", StringComparison.OrdinalIgnoreCase))
                    {
                        match = PullCodes.IsKey(s.FieldKey);
                    }
                    else
                    {
                        match = IsPeriodKey(s.FieldKey);
                    }
                    if (match) { AddTitle(titles, s.Screen); }
                }
            }

            for (int t = 0; t < titles.Count; t++)
            {
                // Skip titles that are only the parent menu caption — they match the MDI frame.
                if (IsParentMenuOnly(titles[t])) { continue; }
                for (int i = 0; i < candidates.Count; i++)
                {
                    string winTitle = Win32Driver.GetWindowTitle(candidates[i]);
                    if (string.Equals(kind, "confirm", StringComparison.OrdinalIgnoreCase) &&
                        IsEntryAddTitle(winTitle))
                    {
                        continue;
                    }
                    if (TitleMatches(winTitle, titles[t])) { return candidates[i]; }
                }
            }

            // Shape fallback: confirm/period forms stay open while title config is wrong.
            for (int i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(kind, "confirm", StringComparison.OrdinalIgnoreCase))
                {
                    if (LooksLikeConfirm(candidates[i])) { return candidates[i]; }
                }
                else if (string.Equals(kind, "pullcodes", StringComparison.OrdinalIgnoreCase))
                {
                    if (LooksLikePullCodes(candidates[i])) { return candidates[i]; }
                }
                else if (LooksLikePeriod(candidates[i]))
                {
                    return candidates[i];
                }
            }
            return IntPtr.Zero;
        }

        private static List<IntPtr> CandidateWindows(IntPtr appWindow)
        {
            List<IntPtr> candidates = new List<IntPtr>();
            List<W32WindowInfo> forms = Win32Driver.GetFormWindows(appWindow, 8);
            for (int i = 0; i < forms.Count; i++)
            {
                if (!forms[i].Visible || forms[i].Handle == appWindow) { continue; }
                if (!candidates.Contains(forms[i].Handle)) { candidates.Add(forms[i].Handle); }
            }
            List<W32WindowInfo> tops = Win32Driver.GetProcessTopWindows(Win32Driver.GetProcessId(appWindow));
            for (int i = 0; i < tops.Count; i++)
            {
                if (tops[i].Handle == appWindow) { continue; }
                if (!Win32Interop.IsWindowVisible(tops[i].Handle)) { continue; }
                if (!candidates.Contains(tops[i].Handle)) { candidates.Add(tops[i].Handle); }
            }
            return candidates;
        }

        private static bool TitleMatches(string windowTitle, string wanted)
        {
            if (string.IsNullOrWhiteSpace(wanted)) { return false; }
            if (ArabicText.Contains(windowTitle, wanted)) { return true; }
            if (ArabicText.Contains(wanted, windowTitle) &&
                !string.IsNullOrWhiteSpace(windowTitle) &&
                ArabicText.Normalize(windowTitle).Length >= 4)
            {
                return true;
            }
            return false;
        }

        private static bool IsParentMenuOnly(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) { return true; }
            string n = ArabicText.Normalize(title);
            string parent = ArabicText.Normalize("نظام تكويد العملاء");
            if (n == parent) { return true; }
            // Too short / generic to identify the confirm or period form.
            if (n.Length < 6) { return true; }
            return false;
        }

        private static bool LooksLikeConfirm(IntPtr window)
        {
            if (!LooksLikeVbForm(window)) { return false; }
            string title = Win32Driver.GetWindowTitle(window);
            if (IsEntryAddTitle(title)) { return false; }
            if (ArabicText.Contains(title, "تأكيد")) { return true; }
            if (ArabicText.Contains(title, PostingCaption)) { return true; }
            return false;
        }

        private static bool IsEntryAddTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) { return false; }
            return ArabicText.Contains(title, "إضافة") ||
                   ArabicText.Contains(title, "اضافه") ||
                   ArabicText.Contains(title, "إدخال بيانات") ||
                   ArabicText.Contains(title, "ادخال بيانات");
        }

        private static bool LooksLikePullCodes(IntPtr window)
        {
            if (!LooksLikeVbForm(window)) { return false; }
            string title = Win32Driver.GetWindowTitle(window);
            return ArabicText.Contains(title, "سحب");
        }

        private static bool LooksLikePeriod(IntPtr window)
        {
            if (!LooksLikeVbForm(window)) { return false; }
            string title = Win32Driver.GetWindowTitle(window);
            if (ArabicText.ContainsAny(title, new string[] { "فترة", "عمليات التكويد", "عملاء التكويد" }))
            {
                return true;
            }
            FieldMap map = FieldDiscovery.GetFieldMap(window, false);
            if (map == null || map.Fields == null) { return false; }
            int boxes = 0;
            for (int i = 0; i < map.Fields.Count; i++)
            {
                if (map.Fields[i].Kind == "TextBox") { boxes++; }
            }
            return boxes >= 4;
        }

        private static bool LooksLikeVbForm(IntPtr window)
        {
            if (window == IntPtr.Zero || !Win32Interop.IsWindow(window)) { return false; }
            if (!Win32Interop.IsWindowVisible(window)) { return false; }
            string cls = Win32Driver.GetClassName(window);
            if (string.IsNullOrEmpty(cls)) { return false; }
            if (cls.IndexOf("MDIClient", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }
            if (cls.IndexOf("ThunderRT6MDIForm", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }
            return cls.IndexOf("Thunder", StringComparison.OrdinalIgnoreCase) >= 0
                || cls.IndexOf("WindowsForms", StringComparison.OrdinalIgnoreCase) >= 0
                || cls == "#32770";
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

        private static string ConfirmTitle(BotAppConfig app)
        {
            string raw = "";
            if (app != null && !string.IsNullOrWhiteSpace(app.ConfirmWindowTitle))
            {
                raw = app.ConfirmWindowTitle.Trim();
            }
            else if (app != null && app.ConfirmMenuPath != null && app.ConfirmMenuPath.Length >= 2)
            {
                raw = app.ConfirmMenuPath[app.ConfirmMenuPath.Length - 1];
            }
            if (string.IsNullOrWhiteSpace(raw) || IsParentMenuOnly(raw) ||
                (!ArabicText.Contains(raw, "تأكيد") && !ArabicText.Contains(raw, PostingCaption)))
            {
                return DefaultConfirmTitle;
            }
            return raw;
        }

        private static string PeriodTitle(BotAppConfig app)
        {
            string raw = "";
            if (app != null && !string.IsNullOrWhiteSpace(app.PeriodWindowTitle))
            {
                raw = app.PeriodWindowTitle.Trim();
            }
            else if (app != null && app.PeriodMenuPath != null && app.PeriodMenuPath.Length >= 2)
            {
                raw = app.PeriodMenuPath[app.PeriodMenuPath.Length - 1];
            }
            if (string.IsNullOrWhiteSpace(raw) || IsParentMenuOnly(raw))
            {
                return DefaultPeriodTitle;
            }
            return raw;
        }

        private static void SanitizeAppPaths(BotAppConfig app, Action<string, string> log)
        {
            if (app == null) { return; }
            string[] c = SanitizeMenuPath(app.ConfirmMenuPath, "confirm");
            if (!SamePath(app.ConfirmMenuPath, c))
            {
                log("post-save: fixed confirm menu path → " + Join(c), "WARN");
            }
            app.ConfirmMenuPath = c;
            app.ConfirmWindowTitle = ConfirmTitle(app);

            string[] p = SanitizeMenuPath(app.PeriodMenuPath, "period");
            if (!SamePath(app.PeriodMenuPath, p))
            {
                log("post-save: fixed period menu path → " + Join(p), "WARN");
            }
            app.PeriodMenuPath = p;
            app.PeriodWindowTitle = PeriodTitle(app);
        }

        // Parent≠leaf; leaf must identify the real form (not the top-level menu caption).
        public static string[] SanitizeMenuPath(string[] path, string kind)
        {
            string parent = "نظام تكويد العملاء";
            string leaf = string.Equals(kind, "period", StringComparison.OrdinalIgnoreCase)
                ? DefaultPeriodTitle
                : DefaultConfirmTitle;
            if (string.Equals(kind, "pullcodes", StringComparison.OrdinalIgnoreCase))
            {
                parent = "التكويد";
                leaf = PullCodes.DefaultLeaf;
            }

            if (path != null && path.Length >= 1 && !string.IsNullOrWhiteSpace(path[0]))
            {
                parent = path[0].Trim();
            }
            if (path != null && path.Length >= 2 && !string.IsNullOrWhiteSpace(path[1]))
            {
                string cand = path[1].Trim();
                bool bad = ArabicText.Normalize(cand) == ArabicText.Normalize(parent)
                    || IsParentMenuOnly(cand);
                if (string.Equals(kind, "confirm", StringComparison.OrdinalIgnoreCase) &&
                    !ArabicText.Contains(cand, "تأكيد") &&
                    !ArabicText.Contains(cand, PostingCaption))
                {
                    bad = true;
                }
                if (string.Equals(kind, "pullcodes", StringComparison.OrdinalIgnoreCase) &&
                    !ArabicText.Contains(cand, "سحب"))
                {
                    bad = true;
                }
                if (!bad) { leaf = cand; }
            }
            return new string[] { parent, leaf };
        }

        private static bool SamePath(string[] a, string[] b)
        {
            if (a == null || b == null || a.Length != b.Length) { return false; }
            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal)) { return false; }
            }
            return true;
        }

        private static IntPtr ResolveOne(
            FieldMap map, List<FieldSelector> selectors, string fieldKey, Action<string, string> log)
        {
            if (selectors == null) { return IntPtr.Zero; }
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                if (s == null) { continue; }
                if (!string.Equals(s.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase)) { continue; }
                // Position-only maps are valid for VB6 windowless / Frame grids.
                if (string.IsNullOrWhiteSpace(s.Class) && !s.HasPosition) { return IntPtr.Zero; }
                ResolveResult r = FieldDiscovery.ResolveField(map, s, "Index");
                if (log != null)
                {
                    if (r.Handle == IntPtr.Zero)
                    {
                        log("post-save: «" + fieldKey + "» mapped but not resolved (" + r.Note + ")", "WARN");
                    }
                    else
                    {
                        log("post-save: «" + fieldKey + "» resolved by " + r.Tier, "INFO");
                    }
                }
                return r.Handle;
            }
            return IntPtr.Zero;
        }

        private static IntPtr AutoFindGrid(FieldMap map)
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
                    string.Equals(f.Kind, "Grid", StringComparison.OrdinalIgnoreCase))
                {
                    return f.Handle;
                }
                long area = (long)f.W * f.H;
                if (area > bestArea) { bestArea = area; best = f.Handle; }
            }
            return best;
        }

        private static bool ClickMappedPosition(
            IntPtr screen, List<FieldSelector> selectors, string fieldKey, Action<string, string> log)
        {
            if (selectors == null) { return false; }
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                if (s == null || !string.Equals(s.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                int xRel, yRel;
                if (!int.TryParse(s.PosX, out xRel) || !int.TryParse(s.PosY, out yRel))
                {
                    continue;
                }
                int w = 40;
                int h = 20;
                int parsedW, parsedH;
                if (int.TryParse(s.PosW, out parsedW) && parsedW > 0) { w = parsedW; }
                if (int.TryParse(s.PosH, out parsedH) && parsedH > 0) { h = parsedH; }
                if (w > 160) { w = 24; }
                if (h > 60) { h = 24; }
                int hitX = xRel + w / 2;
                int hitY = yRel + h / 2;
                // Form-origin stamps land on the title bar (log: 1127,349 on a form at 1115,337).
                if (hitX < 36 && hitY < 36)
                {
                    if (log != null)
                    {
                        log("post-save: ignoring title-bar map for «" + fieldKey +
                            "» at rel " + hitX + "," + hitY, "WARN");
                    }
                    continue;
                }
                if (hitY < 28)
                {
                    if (log != null)
                    {
                        log("post-save: ignoring chrome map for «" + fieldKey + "»", "WARN");
                    }
                    continue;
                }
                try
                {
                    Win32Driver.RectInfo form = Win32Driver.GetRect(screen);
                    int x = form.X + hitX;
                    int y = form.Y + hitY;
                    try { Win32Driver.SetForeground(screen); } catch { }
                    Win32Driver.ClickScreen(x, y, 220);
                    if (log != null)
                    {
                        log("post-save: clicked mapped pos " + x + "," + y + " for «" + fieldKey + "»", "OK");
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    if (log != null) { log("post-save: mapped click failed — " + ex.Message, "WARN"); }
                    continue;
                }
            }
            return false;
        }

        private static void ClickFormClientCenter(IntPtr screen, Action<string, string> log)
        {
            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(screen);
                int x = r.X + r.W / 2;
                int y = r.Y + Math.Max(40, r.H / 2);
                Win32Driver.ClickScreen(x, y, 200);
                if (log != null) { log("post-save: clicked form centre " + x + "," + y, "INFO"); }
            }
            catch (Exception ex)
            {
                if (log != null) { log("post-save: form centre click failed — " + ex.Message, "WARN"); }
            }
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

        private static string DumpOpenTitles(IntPtr appWindow)
        {
            try
            {
                List<string> titles = new List<string>();
                List<IntPtr> wins = CandidateWindows(appWindow);
                for (int i = 0; i < wins.Count; i++)
                {
                    string t = Win32Driver.GetWindowTitle(wins[i]);
                    if (string.IsNullOrWhiteSpace(t)) { continue; }
                    t = t.Trim();
                    if (!titles.Contains(t)) { titles.Add(t); }
                }
                return titles.Count == 0 ? "(none)" : string.Join(" | ", titles.ToArray());
            }
            catch { return "(dump failed)"; }
        }

        private static string Join(string[] path)
        {
            if (path == null || path.Length == 0) { return "(empty)"; }
            return string.Join(" → ", path);
        }

        private static void RememberScreen(
            BotConfig cfg, IntPtr screen, bool confirm, bool overwrite, Action<string, string> log)
        {
            if (cfg == null || screen == IntPtr.Zero) { return; }
            WindowBounds b = WindowLayout.Capture(screen);
            if (!b.IsValid) { return; }
            if (cfg.Windows == null) { cfg.Windows = new WindowLayoutConfig(); }

            WindowBounds existing = confirm ? cfg.Windows.ConfirmScreen : cfg.Windows.PeriodScreen;
            if (!overwrite && existing != null && existing.IsValid)
            {
                if (log != null)
                {
                    log("post-save: keeping saved " + (confirm ? "confirm" : "period") +
                        " size " + existing.ToShort() + " (live was " + b.ToShort() + ")", "INFO");
                }
                return;
            }

            if (confirm) { cfg.Windows.ConfirmScreen = b; }
            else { cfg.Windows.PeriodScreen = b; }
            try
            {
                cfg.SaveBotConfig();
                if (log != null)
                {
                    log("post-save: saved " + (confirm ? "confirm" : "period") +
                        " window size " + b.ToShort(), "INFO");
                }
            }
            catch (Exception ex)
            {
                if (log != null) { log("post-save: could not save window size — " + ex.Message, "WARN"); }
            }
        }

        private static CodingPostSaveResult Fail(
            CodingPostSaveResult result, Action<string, string> log, string msg)
        {
            result.Ok = false;
            result.Note = msg;
            log(msg, "ERROR");
            return result;
        }
    }
}
