// =============================================================================
//  OperatorFlow.cs - SFTP sync → classify → Coding batch (no OCR / inquiry).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;

namespace RbaCore
{
    public class OperatorFlowOptions
    {
        public BotConfig Config;
        public bool SkipClassify;
        public bool SkipExecute;
        public bool ClassifyOnly;
        public Action<string, string> Log;
        // UI thread: show classify dialog; return true if operator finished.
        public Func<string, List<OrderedDictionary>, bool> ShowClassify;
        public Func<bool> IsCancelled;
    }

    public class OperatorFlowResult
    {
        public bool Ok;
        public string SheetPath;
        public int Total;
        public int NewCount;
        public int PullCount;
        public int SuspendedCount;
        public int PendingCount;
        public EntryTestResult Execute;
        public string Note;
    }

    public static class OperatorFlow
    {
        public static OperatorFlowResult Run(OperatorFlowOptions opt)
        {
            OperatorFlowResult result = new OperatorFlowResult();
            Action<string, string> log = opt != null ? opt.Log : null;
            if (log == null) { log = delegate(string m, string l) { }; }
            if (opt == null || opt.Config == null)
            {
                result.Note = "missing config";
                return result;
            }

            BotConfig cfg = opt.Config;
            cfg.App.LookupBeforeEntry = false;

            if (IsStop(opt))
            {
                result.Note = "تم إيقاف الجلسة";
                log(result.Note, "WARN");
                return result;
            }

            string sheetPath;
            if (opt.SkipClassify)
            {
                if (!UseExistingSheet(cfg, log, result, out sheetPath)) { return result; }
            }
            else if (!PrepareSheet(cfg, log, result, out sheetPath))
            {
                return result;
            }

            SheetImportResult sheet = SheetSync.ImportClientSheet(sheetPath);
            List<OrderedDictionary> rows = ClientsWithNationalId(sheet.Rows);
            result.Total = rows.Count;
            CountDecisions(rows, result);

            if (opt.SkipClassify)
            {
                log("استعلام skipped — تكويد only", "INFO");
            }
            else
            {
                log("=== Operator flow: classify sheet rows=" + result.Total +
                    " pending=" + result.PendingCount + " (walk all) ===", "INFO");
                bool done = false;
                if (opt.ShowClassify != null)
                {
                    done = opt.ShowClassify(sheetPath, rows);
                }
                else
                {
                    result.Note = "no classify UI callback";
                    return result;
                }
                sheet = SheetSync.ImportClientSheet(sheetPath);
                rows = ClientsWithNationalId(sheet.Rows);
                CountDecisions(rows, result);
                SftpInbox.PushSheetToInbox(cfg, sheetPath, log);
                if (!done && result.PendingCount > 0)
                {
                    result.Note = "التصنيف لم يكتمل — باقي " + result.PendingCount;
                    log(result.Note, "WARN");
                    return result;
                }
            }

            if (opt.ClassifyOnly || opt.SkipExecute)
            {
                SftpInbox.PushSheetToInbox(cfg, sheetPath, log);
                result.Ok = true;
                result.Note = "classify done — New=" + result.NewCount +
                    " Pull=" + result.PullCount + " Suspended=" + result.SuspendedCount;
                return result;
            }

            if (result.PendingCount > 0)
            {
                SftpInbox.PushSheetToInbox(cfg, sheetPath, log);
                result.Note = "لا يزال هناك عملاء بلا قرار";
                log(result.Note, "WARN");
                return result;
            }

            if (IsStop(opt))
            {
                result.Note = "تم إيقاف الجلسة قبل التنفيذ";
                log(result.Note, "WARN");
                result.Ok = true;
                return result;
            }

            log("=== Operator flow: execute Coding (no inquiry/OCR) ===", "INFO");
            EntryTestOptions et = new EntryTestOptions();
            et.BaseDir = cfg.BaseDir;
            et.SheetPath = sheetPath;
            et.MockOne = false;
            et.DoSave = true;
            et.ForceCompleteFlow = false;
            et.OperatorManualFlow = true;
            et.PostSaveConfirmExport = true;
            et.ExportDateRangeYearToDate = false;
            et.UpdateTracking = true;
            et.SettleMs = ActionPace.OperatorSettleMs;
            et.StepMs = ActionPace.OperatorStepMs;
            et.IsCancelled = opt.IsCancelled;
            et.Log = log;
            result.Execute = EntryTest.Run(et);
            result.Ok = result.Execute != null && result.Execute.Ok;
            result.Note = result.Execute != null ? result.Execute.Note : "execute failed";
            SftpInbox.PushSheetToInbox(cfg, sheetPath, log);
            return result;
        }

        // One-row smoke: first sheet row with الحالة=جديد.
        public static OperatorFlowResult RunTestAdd(BotConfig cfg, Action<string, string> log)
        {
            return RunTestAdd(cfg, log, null);
        }

        public static OperatorFlowResult RunTestAdd(BotConfig cfg, Action<string, string> log, Func<bool> isCancelled)
        {
            OperatorFlowResult result = new OperatorFlowResult();
            if (log == null) { log = delegate(string m, string l) { }; }
            if (cfg == null) { result.Note = "missing config"; return result; }
            cfg.App.LookupBeforeEntry = false;

            string sheetPath;
            if (!PrepareSheet(cfg, log, result, out sheetPath)) { return result; }

            OrderedDictionary row = FindFirstByDecision(sheetPath, true, false);
            if (row == null)
            {
                result.Note = "لا يوجد صف بحالة «جديد» في الشيت للاختبار";
                log(result.Note, "WARN");
                return result;
            }

            string name = CsvUtil.Get(row, "Name", "");
            string nid = CsvUtil.Get(row, "NationalId", "");
            log("=== اختبار إضافة: " + name + " / " + nid + " ===", "INFO");

            EntryTestOptions et = new EntryTestOptions();
            et.BaseDir = cfg.BaseDir;
            et.SheetPath = sheetPath;
            et.MockOne = false;
            et.DoSave = true;
            et.OperatorManualFlow = true;
            et.OperatorOnlyStatus = OperatorDecision.NewAr;
            et.OperatorMaxRows = 1;
            et.PostSaveConfirmExport = true;
            et.UpdateTracking = true;
            et.SettleMs = ActionPace.OperatorSettleMs;
            et.StepMs = ActionPace.OperatorStepMs;
            et.IsCancelled = isCancelled;
            et.Log = log;
            result.Execute = EntryTest.Run(et);
            result.Ok = result.Execute != null && result.Execute.Ok;
            result.Note = result.Execute != null ? result.Execute.Note : "test-add failed";
            SftpInbox.PushSheetToInbox(cfg, sheetPath, log);
            return result;
        }

        // One-row smoke: first سحب row with 12-digit code on the sheet (manual).
        public static OperatorFlowResult RunTestPull(BotConfig cfg, Action<string, string> log)
        {
            return RunTestPull(cfg, log, null);
        }

        public static OperatorFlowResult RunTestPull(BotConfig cfg, Action<string, string> log, Func<bool> isCancelled)
        {
            OperatorFlowResult result = new OperatorFlowResult();
            if (log == null) { log = delegate(string m, string l) { }; }
            if (cfg == null) { result.Note = "missing config"; return result; }
            cfg.App.LookupBeforeEntry = false;

            string sheetPath;
            if (!PrepareSheet(cfg, log, result, out sheetPath)) { return result; }

            OrderedDictionary row = FindFirstByDecision(sheetPath, false, true);
            if (row == null)
            {
                result.Note = "لا يوجد صف «سحب» بكود موحد في الشيت";
                log(result.Note, "WARN");
                return result;
            }

            string code12 = ResolveSheetCode12(row);
            string name = CsvUtil.Get(row, "Name", "");
            string nid = CsvUtil.Get(row, "NationalId", "");
            log("=== اختبار سحب: " + name + " / " + nid + " CODE12=" + code12 + " ===", "INFO");

            EntryTestOptions et = new EntryTestOptions();
            et.BaseDir = cfg.BaseDir;
            et.SheetPath = sheetPath;
            et.MockOne = false;
            et.DoSave = true;
            et.OperatorManualFlow = true;
            et.OperatorOnlyStatus = OperatorDecision.PullAr;
            et.OperatorMaxRows = 1;
            et.PostSaveConfirmExport = false;
            et.UpdateTracking = true;
            et.SettleMs = ActionPace.OperatorSettleMs;
            et.StepMs = ActionPace.OperatorStepMs;
            et.IsCancelled = isCancelled;
            et.Log = log;
            result.Execute = EntryTest.Run(et);
            result.Ok = result.Execute != null && result.Execute.Ok;
            result.Note = result.Execute != null ? result.Execute.Note : "test-pull failed";
            SftpInbox.PushSheetToInbox(cfg, sheetPath, log);
            return result;
        }

        private static bool PrepareSheet(
            BotConfig cfg, Action<string, string> log, OperatorFlowResult result, out string sheetPath)
        {
            sheetPath = "";
            log(cfg.SftpOfflineMode ? "=== Offline files sync ===" : "=== SFTP sync ===", "INFO");
            SftpSyncResult sync = SftpInbox.Sync(cfg, log);
            if (!sync.Ok)
            {
                result.Note = sync.Note;
                return false;
            }
            sheetPath = KycSheetAdapter.NormalizeToWorkingSheet(sync.SheetPath, cfg.ScansRoot, "1095");
            cfg.SheetPath = sheetPath;
            result.SheetPath = sheetPath;
            SftpInbox.LastWorkingSheet = sheetPath;
            return true;
        }

        // تكويد uses the sheet/docs from last استعلام — no second SFTP pull.
        private static bool UseExistingSheet(
            BotConfig cfg, Action<string, string> log, OperatorFlowResult result, out string sheetPath)
        {
            sheetPath = FirstExistingSheet(
                SftpInbox.LastWorkingSheet,
                cfg != null ? cfg.SheetPath : "",
                SftpInbox.LastInboxSheet,
                cfg != null ? Path.Combine(cfg.BaseDir ?? "", "SftpCache", SftpInbox.SheetFileName) : "");

            if (string.IsNullOrEmpty(sheetPath))
            {
                result.Note = "لا يوجد شيت جاهز — شغّل «استعلام / المرور على الشيت» أولاً";
                log(result.Note, "WARN");
                return false;
            }

            if (!string.IsNullOrEmpty(cfg.ScansRoot))
            {
                SftpInbox.LastDocsRoot = cfg.ScansRoot;
            }
            cfg.SheetPath = sheetPath;
            result.SheetPath = sheetPath;
            log("تكويد: using existing sheet (no sync) " + sheetPath, "INFO");
            log("تكويد: docs=" + (cfg.ScansRoot ?? SftpInbox.LastDocsRoot), "INFO");
            return true;
        }

        private static string FirstExistingSheet(params string[] paths)
        {
            if (paths == null) { return ""; }
            for (int i = 0; i < paths.Length; i++)
            {
                string p = paths[i];
                if (!string.IsNullOrWhiteSpace(p) && File.Exists(p)) { return p; }
            }
            return "";
        }

        private static OrderedDictionary FindFirstByDecision(string sheetPath, bool wantNew, bool wantPull)
        {
            SheetImportResult sheet = SheetSync.ImportClientSheet(sheetPath);
            for (int i = 0; i < sheet.Rows.Count; i++)
            {
                OrderedDictionary row = sheet.Rows[i];
                string st = OperatorDecision.ReadStatus(row);
                if (wantNew && OperatorDecision.IsNew(st)) { return row; }
                if (wantPull && OperatorDecision.IsPull(st))
                {
                    if (ResolveSheetCode12(row).Length > 0) { return row; }
                }
            }
            return null;
        }

        private static string ResolveSheetCode12(OrderedDictionary row)
        {
            string a = KycSheetAdapter.DigitsOnly(CsvUtil.Get(row, "CODE12", "كود 12"));
            if (a.Length > 0) { return a; }
            return KycSheetAdapter.DigitsOnly(CsvUtil.Get(row, "الكود الموحد", "NIN"));
        }

        private static List<OrderedDictionary> ClientsWithNationalId(List<OrderedDictionary> rows)
        {
            List<OrderedDictionary> keep = new List<OrderedDictionary>();
            if (rows == null) { return keep; }
            for (int i = 0; i < rows.Count; i++)
            {
                string nid = KycSheetAdapter.DigitsOnly(CsvUtil.Get(rows[i], "NationalId", "الرقم القومي"));
                if (nid.Length >= 10) { keep.Add(rows[i]); }
            }
            return keep;
        }

        private static void CountDecisions(List<OrderedDictionary> rows, OperatorFlowResult result)
        {
            result.NewCount = 0;
            result.PullCount = 0;
            result.SuspendedCount = 0;
            result.PendingCount = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                string st = OperatorDecision.ReadStatus(rows[i]);
                if (OperatorDecision.IsNew(st)) { result.NewCount++; }
                else if (OperatorDecision.IsPull(st)) { result.PullCount++; }
                else if (OperatorDecision.IsSuspended(st)) { result.SuspendedCount++; }
                else if (!OperatorDecision.NeedsClassify(st)) { /* terminal */ }
                else { result.PendingCount++; }
            }
        }

        private static bool IsStop(OperatorFlowOptions opt)
        {
            try { return opt != null && opt.IsCancelled != null && opt.IsCancelled(); }
            catch { return false; }
        }
    }
}
