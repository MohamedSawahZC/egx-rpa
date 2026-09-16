// =============================================================================
//  EntryTest.cs - end-to-end entry / Test-Save flow (port of Run-EntryTest.ps1).
//  Never presses ترحيل. Save only when DoSave is true (after caller confirms).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

namespace RbaCore
{
    public class EntryTestOptions
    {
        public string BaseDir;
        public string SheetPath;
        public bool MockOne = true;
        public bool DoSave = false;
        // Test-Save: still fill+save when lookup is unknown; found clients go to سحب الأكواد.
        public bool ForceCompleteFlow = false;
        public bool Watch = true;
        public bool AutoMapIfEmpty = true;
        public bool UpdateTracking = true;
        public bool PreferMenuNavigate = true; // open via 2-click menu path
        // After successful saves: confirm → F2/F9 → period export → reconcile.
        // null = follow BotSettings.PostSaveConfirmExport (default true).
        public bool? PostSaveConfirmExport;
        // null = auto: true for Test-Save sample sheet; else BotSettings.ExportDateRangeYearToDate.
        public bool? ExportDateRangeYearToDate;
        // Manual triage flow: no inquiry/OCR — branch on الحالة (جديد/سحب/موقوف).
        public bool OperatorManualFlow = false;
        // When set with OperatorManualFlow: only rows matching this decision (جديد/سحب).
        public string OperatorOnlyStatus = "";
        // 0 = all matching rows; 1 = first matching row only (smoke tests).
        public int OperatorMaxRows = 0;
        public int StepMs = 400;
        public int SettleMs = 400;
        public Action<string, string> Log; // message, level
        public Func<bool> IsCancelled;
    }

    public class EntryTestResult
    {
        public bool Ok;
        public int ClientsOk;
        public int ClientsSkipped;
        public int ClientsTotal;
        public bool Saved;
        public bool PostSaveOk;
        public string PostSaveNote;
        public string Note;
        public List<string> Lines = new List<string>();
        public string SavedEvidencePath;
    }

    public static class EntryTest
    {
        // One run at a time: two overlapping runs fight over SendKeys, modal answers
        // and the sheet, and produce interleaved garbage in Coding.
        private static int _running;

        public static EntryTestResult Run(EntryTestOptions opt)
        {
            EntryTestResult result = new EntryTestResult();
            Action<string, string> log = opt.Log;
            if (log == null) { log = delegate(string m, string l) { }; }

            if (System.Threading.Interlocked.CompareExchange(ref _running, 1, 0) != 0)
            {
                return Fail(result, log, "هناك تشغيل جارٍ بالفعل — انتظر حتى ينتهي");
            }
            try
            {
                return RunCore(opt, result, log);
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _running, 0);
            }
        }

        private static EntryTestResult RunCore(
            EntryTestOptions opt, EntryTestResult result, Action<string, string> log)
        {

            BotConfig cfg = BotConfig.Load(opt.BaseDir);
            string sheetPath = opt.SheetPath;
            if (string.IsNullOrWhiteSpace(sheetPath)) { sheetPath = cfg.SheetPath; }
            if (string.IsNullOrWhiteSpace(sheetPath) || !File.Exists(sheetPath))
            {
                return Fail(result, log, "client sheet not found");
            }
            // Never rewrite operator KYC sheets into Sample\\Test-Save-Client.csv.
            if (opt.DoSave && !opt.OperatorManualFlow)
            {
                sheetPath = RefreshTestSaveSample(opt.BaseDir, sheetPath, log);
            }
            log("sheet: " + sheetPath, "INFO");

            // Defaults matching Run-EntryTest.ps1 / EGX manual
            string entryTitle = cfg.App.EntryWindowTitle;
            if (string.IsNullOrWhiteSpace(entryTitle)) { entryTitle = "بيانات العملاء"; }
            string entryClass = cfg.App.EntryWindowClass;
            if (string.IsNullOrWhiteSpace(entryClass)) { entryClass = "ThunderRT6FormDC"; }
            string shortcut = cfg.App.EntryShortcut;
            if (string.IsNullOrWhiteSpace(shortcut)) { shortcut = "^{F2}"; }

            IntPtr app = FieldDiscovery.GetTargetAppWindow(
                cfg.App.WindowTitlePattern,
                cfg.App.ExePath,
                cfg.App.AutoLaunch,
                cfg.App.LaunchTimeoutSec);
            if (app == IntPtr.Zero)
            {
                // Also try CodingReplica / التكويد titles
                app = FieldDiscovery.GetTargetAppWindow("تكويد", "", false, 5);
            }
            if (app == IntPtr.Zero)
            {
                app = FieldDiscovery.GetTargetAppWindow("برنامج التكويد", "", false, 5);
            }
            if (app == IntPtr.Zero)
            {
                return Fail(result, log, "Coding window not open — start Coding.exe or CodingReplica first");
            }
            log("attached hwnd=" + app + " title=" + Win32Driver.GetWindowTitle(app), "OK");

            FieldSelector navSel = null;
            List<FieldSelector> selectorsEarly = FieldDiscovery.ImportSelectors(cfg.SelectorsPath);
            for (int i = 0; i < selectorsEarly.Count; i++)
            {
                if (string.Equals(selectorsEarly[i].FieldKey, "navNewClient", StringComparison.OrdinalIgnoreCase))
                {
                    navSel = selectorsEarly[i];
                    break;
                }
            }

            // «بيانات العملاء» is opened lazily by EnsureEntryForm for the first client
            // that actually needs an insert. Opening it here left it hanging on runs made
            // only of existing clients (الإستعلام → سحب الأكواد), which never fill anything.
            IntPtr entry = IntPtr.Zero;

            // Restore Coding sizes captured in Setup (critical for stable field maps).
            WindowLayout.Active = cfg.Windows;
            WindowLayout.ApplyAll(app, IntPtr.Zero, IntPtr.Zero, cfg.Windows, log);
            if (cfg.Windows == null ||
                cfg.Windows.EntryForm == null ||
                !cfg.Windows.EntryForm.IsValid)
            {
                log("window sizes not saved yet — open Setup → «حفظ أحجام النوافذ» after sizing Coding/entry", "WARN");
            }

            List<FieldSelector> selectors = FieldDiscovery.ImportSelectors(cfg.SelectorsPath);
            // Auto-map ONLY when there is no operator click-map yet.
            // Never overwrite name/address/etc. that the user captured by hand.
            if (opt.AutoMapIfEmpty && !FieldCatalog.HasClickMappedFields(selectors))
            {
                // Auto-mapping is the one step that needs the real form up front.
                log("no click-map yet — opening «" + entryTitle + "» to auto-map fields", "INFO");
                entry = EnsureEntryForm(
                    entry, app, entryTitle, entryClass, shortcut, navSel, cfg, log);
                if (entry == IntPtr.Zero)
                {
                    return Fail(result, log,
                        "could not open add-clients window via menu (التكويد → إدخال بيانات العملاء)");
                }
                FieldMap map = FieldDiscovery.GetFieldMap(entry, false);
                List<FieldSelector> auto = ReplicaMapper.BuildFromMap(map);
                if (auto.Count > 0)
                {
                    selectors = FieldCatalog.OrderAndClean(auto);
                    FieldDiscovery.SaveSelectors(cfg.SelectorsPath, selectors);
                    log("auto-mapped " + selectors.Count + " selectors -> " + cfg.SelectorsPath, "OK");
                }
            }
            else if (FieldCatalog.HasClickMappedFields(selectors))
            {
                selectors = FieldCatalog.OrderAndClean(selectors);
                log("using operator click-map (" + selectors.Count + " fields) — auto-map skipped", "OK");
            }
            if (selectors.Count == 0)
            {
                return Fail(result, log, "no selectors — run Setup Discover first");
            }

            // Business gate: validate the sheet before typing into Coding.
            log("validate before entry...", "INFO");
            ValidateResult vr = Validator.ValidateFile(sheetPath, cfg.ScansRoot, cfg.TrackingPath);
            for (int i = 0; i < vr.LogLines.Count; i++) { log(vr.LogLines[i], "INFO"); }
            if (opt.OperatorManualFlow && vr.Bad > 0)
            {
                log("operator flow: validation warnings kept soft (" + vr.Bad +
                    " local rejects) — تصنيف يدوي يتابع", "WARN");
            }

            SheetImportResult sheet = SheetSync.ImportClientSheet(sheetPath);
            List<OrderedDictionary> rows = new List<OrderedDictionary>();
            if (opt.OperatorManualFlow)
            {
                // Process every classified row (جديد / سحب / موقوف).
                for (int i = 0; i < sheet.Rows.Count; i++) { rows.Add(sheet.Rows[i]); }
                rows.Sort(CompareManualFlowOrder);
                log("operator manual flow — " + rows.Count +
                    " row(s): جديد أولاً ثم سحب (inquiry/OCR disabled)", "INFO");
            }
            else
            {
                bool allSample = opt.DoSave || IsSampleTestSaveSheet(sheetPath);
                if (allSample || !opt.MockOne)
                {
                    int max = Math.Min(sheet.Rows.Count, 5);
                    for (int i = 0; i < max; i++) { rows.Add(sheet.Rows[i]); }
                }
                else if (sheet.Rows.Count > 0)
                {
                    rows.Add(sheet.Rows[0]);
                }
            }
            if (rows.Count == 0) { return Fail(result, log, "sheet empty"); }

            if (opt.DoSave && !opt.ForceCompleteFlow && !opt.OperatorManualFlow)
            {
                List<OrderedDictionary> tracking = RunTracker.LoadTracking(cfg.TrackingPath);
                List<OrderedDictionary> keep = new List<OrderedDictionary>();
                for (int r = 0; r < rows.Count; r++)
                {
                    string nid = CsvUtil.Get(rows[r], "NationalId", "").Trim();
                    bool bad = false;
                    for (int i = 0; i < tracking.Count; i++)
                    {
                        if (CsvUtil.Get(tracking[i], "NationalId", "").Trim() != nid) { continue; }
                        string st = CsvUtil.Get(tracking[i], "Status", "");
                        if (string.Equals(st, "VALIDATION_FAILED", StringComparison.OrdinalIgnoreCase))
                        {
                            log("SKIP Test-Save client " + nid + " — VALIDATION_FAILED", "WARN");
                            bad = true;
                            break;
                        }
                    }
                    if (!bad) { keep.Add(rows[r]); }
                }
                rows = keep;
                if (rows.Count == 0)
                {
                    return Fail(result, log, "Test-Save clients failed validation — fix sheet first");
                }
                log("Test-Save clients in this run: " + rows.Count, "INFO");
            }
            else if (opt.DoSave && !opt.OperatorManualFlow)
            {
                log("Test-Save clients in this run: " + rows.Count + " (complete flow — no skip)", "INFO");
            }

            string savedEvidence = Path.Combine(opt.BaseDir, @"tests\CodingReplica-saved.csv");
            if (opt.DoSave && File.Exists(savedEvidence))
            {
                try { File.Delete(savedEvidence); } catch { }
            }

            int okClients = 0;
            int skippedClients = 0;
            bool anySaved = false;
            // Confirmed «جديد» saves in this batch — gates ترحيل (confirm F2/F9).
            int newSavedCount = 0;
            int processedNewCount = 0;
            // True while «بيانات العملاء» holds data that Coding has not accepted yet.
            bool entryDirty = false;
            int operatorMatched = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (IsStop(opt))
                {
                    log("STOP — session cancelled before client " + (i + 1), "WARN");
                    result.Note = "تم إيقاف الجلسة عند العميل " + (i + 1);
                    result.Ok = true;
                    break;
                }

                OrderedDictionary row = rows[i];
                string name = CsvUtil.Get(row, "Name", "");

                // Coding.exe may die or freeze mid-run — stop cleanly, mark remaining rows.
                string healthWhy;
                if (!CodingHealth.EnsureAlive(ref app, cfg, log, out healthWhy, true))
                {
                    string gone = healthWhy + " — تم إيقاف الباقي عند العميل " + (i + 1);
                    log(gone, "ERROR");
                    result.Lines.Add(gone);
                    for (int r = i; r < rows.Count; r++)
                    {
                        WriteStatusToSheets(cfg, sheetPath, rows[r],
                            "لم تتم المعالجة",
                            healthWhy.IndexOf("تجم", StringComparison.Ordinal) >= 0
                                ? "APP_HUNG" : "NOT_PROCESSED",
                            gone, true, log);
                    }
                    break;
                }

                // Manual operator flow: never run inquiry/OCR — decision is already on the sheet.
                if (opt.OperatorManualFlow)
                {
                    string decision = OperatorDecision.ReadStatus(row);
                    if (!string.IsNullOrWhiteSpace(opt.OperatorOnlyStatus))
                    {
                        bool wantNew = OperatorDecision.IsNew(opt.OperatorOnlyStatus);
                        bool wantPull = OperatorDecision.IsPull(opt.OperatorOnlyStatus);
                        bool match = (wantNew && OperatorDecision.IsNew(decision)) ||
                                     (wantPull && OperatorDecision.IsPull(decision));
                        if (!match) { continue; }
                    }
                    if (OperatorDecision.IsSuspended(decision))
                    {
                        string skipSus = "SKIP client " + (i + 1) + ": " + name + " — موقوف";
                        log(skipSus, "WARN");
                        result.Lines.Add(skipSus);
                        skippedClients++;
                        WriteStatusToSheets(cfg, sheetPath, row,
                            "موقوف — تخطي", "SKIPPED_SUSPENDED", "تصنيف: موقوف", true, log);
                        if (opt.UpdateTracking)
                        {
                            MarkTracking(cfg.TrackingPath, row, "SKIPPED_SUSPENDED", "موقوف");
                        }
                        continue;
                    }
                    if (OperatorDecision.IsPull(decision))
                    {
                        try { CloseEntryForm(entry, "بيانات العملاء", log); }
                        catch { }
                        entry = IntPtr.Zero;
                        entryDirty = false;
                        string code12 = ResolveCode12(row, null);
                        if (FlexGridSurface.AsOperatorPullCode(code12).Length == 0)
                        {
                            string miss12 = "PULL FAILED client " + (i + 1) + ": " + name +
                                " — حالة سحب بدون كود موحد";
                            log(miss12, "ERROR");
                            result.Lines.Add(miss12);
                            skippedClients++;
                            WriteStatusToSheets(cfg, sheetPath, row,
                                "تعذر سحب الأكواد", "PULL_CODES_FAILED",
                                "سحب بدون CODE12", true, log);
                            if (opt.UpdateTracking)
                            {
                                MarkTracking(cfg.TrackingPath, row, "PULL_CODES_FAILED", "missing CODE12");
                            }
                            operatorMatched++;
                            if (opt.OperatorMaxRows > 0 && operatorMatched >= opt.OperatorMaxRows) { break; }
                            continue;
                        }
                        string pullLine = "PULL client " + (i + 1) + ": " + name +
                            " CODE12 " + FlexGridSurface.AsOperatorPullCode(code12) +
                            " — بحث → جدول → F2/F9";
                        log(pullLine, "INFO");
                        result.Lines.Add(pullLine);
                        skippedClients++;
                        // Manual flow: fixed menu path only (no inquiry / no سحب-cell click).
                        PullCodesResult pulled;
                        try
                        {
                            ActionPace.Wait(ActionPace.BetweenClientsMs);
                            pulled = PullCodesForRow(
                                app, cfg, selectors, row, null, opt.SettleMs, log, true);
                            if (pulled != null && !pulled.Ok)
                            {
                                log("retry سحب once after pause", "WARN");
                                ActionPace.Wait(ActionPace.RetryPauseMs);
                                pulled = PullCodesForRow(
                                    app, cfg, selectors, row, null, opt.SettleMs, log, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            pulled = new PullCodesResult();
                            pulled.Note = "pull aborted — " + ex.Message;
                            log(pulled.Note, "ERROR");
                        }
                        WriteStatusToSheets(cfg, sheetPath, row,
                            pulled.Ok ? "سحب أكواد" : "تعذر سحب الأكواد",
                            pulled.Ok ? "CODES_PULLED" : "PULL_CODES_FAILED",
                            pulled.Note, true, log);
                        if (opt.UpdateTracking)
                        {
                            MarkTracking(cfg.TrackingPath, row,
                                pulled.Ok ? "CODES_PULLED" : "PULL_CODES_FAILED", pulled.Note);
                        }
                        if (pulled.Screen != IntPtr.Zero)
                        {
                            ClientLookup.CloseWindow(pulled.Screen, "سحب الأكواد", log);
                        }
                        string pullAliveWhy;
                        if (!CodingHealth.EnsureAlive(ref app, cfg, log, out pullAliveWhy, false))
                        {
                            for (int r = i + 1; r < rows.Count; r++)
                            {
                                WriteStatusToSheets(cfg, sheetPath, rows[r],
                                    "لم تتم المعالجة",
                                    pullAliveWhy.IndexOf("تجم", StringComparison.Ordinal) >= 0
                                        ? "APP_HUNG" : "NOT_PROCESSED",
                                    pullAliveWhy, true, log);
                            }
                            break;
                        }
                        operatorMatched++;
                        if (opt.OperatorMaxRows > 0 && operatorMatched >= opt.OperatorMaxRows) { break; }
                        continue;
                    }
                    if (!OperatorDecision.IsNew(decision))
                    {
                        string skipOther = "SKIP client " + (i + 1) + ": " + name +
                            " — حالة غير قابلة للتنفيذ («" + decision + "»)";
                        log(skipOther, "WARN");
                        result.Lines.Add(skipOther);
                        skippedClients++;
                        continue;
                    }
                    // جديد → fall through to ClientEntry fill+save below.
                    if (!DocumentAttach.HasClientImages(row, cfg.ScansRoot))
                    {
                        string skipDocs = "SKIP client " + (i + 1) + ": " + name +
                            " — لا توجد ملفات في مجلد المستندات";
                        log(skipDocs, "WARN");
                        result.Lines.Add(skipDocs);
                        skippedClients++;
                        WriteStatusToSheets(cfg, sheetPath, row,
                            "تخطي — لا مستندات", "SKIPPED_NO_DOCS",
                            "مجلد الهوية فارغ أو غير موجود", true, log);
                        if (opt.UpdateTracking)
                        {
                            MarkTracking(cfg.TrackingPath, row, "SKIPPED_NO_DOCS", "no docs files");
                        }
                        operatorMatched++;
                        if (opt.OperatorMaxRows > 0 && operatorMatched >= opt.OperatorMaxRows) { break; }
                        continue;
                    }
                    operatorMatched++;
                    processedNewCount++;
                }

                bool doLookup = !opt.OperatorManualFlow &&
                    (cfg.App.LookupBeforeEntry || opt.ForceCompleteFlow);
                if (doLookup)
                {
                    ClientLookupResult lookup = ClientLookup.Find(
                        app, cfg.App, row, selectors, opt.SettleMs, log);
                    WriteLookupToSheets(cfg, sheetPath, row, lookup, log);
                    if (!string.IsNullOrWhiteSpace(lookup.Code12) ||
                        !string.IsNullOrWhiteSpace(lookup.Nin))
                    {
                        WriteCodesToSheets(cfg, sheetPath, row, lookup.Nin, lookup.Code12, log);
                    }

                    if (lookup.IsDuplicate)
                    {
                        string code12 = ResolveCode12(row, lookup);
                        string skipLine = "SKIP add client " + (i + 1) + ": " + name +
                            " — موجود في الإستعلام → سحب الأكواد" +
                            (code12.Length > 0 ? " CODE12 " + code12 : " (F6 / CODE12 only — no OCR NIN)");
                        log(skipLine, "WARN");
                        result.Lines.Add(skipLine);
                        skippedClients++;
                        if (opt.UpdateTracking)
                        {
                            MarkTracking(cfg.TrackingPath, row, "PULL_CODES",
                                "موجود في الإستعلام — سحب الأكواد" +
                                (code12.Length > 0 ? " " + code12 : ""));
                        }
                        PullCodesResult pulled = PullCodesForRow(
                            app, cfg, selectors, row, lookup, opt.SettleMs, log);
                        WriteStatusToSheets(cfg, sheetPath, row,
                            pulled.Ok ? "سحب أكواد" : "موجود — تعذر سحب الأكواد",
                            pulled.Ok ? "CODES_PULLED" : "PULL_CODES_FAILED",
                            pulled.Note, true, log);
                        continue;
                    }

                    // Screen/box/grid unreadable: insert anyway — Coding still rejects
                    // duplicates at save time, and that path is handled below.
                    if (lookup.Outcome == LookupOutcome.Unknown)
                    {
                        string unk = "LOOKUP UNKNOWN client " + (i + 1) + ": " + name +
                            " — " + lookup.Note + " (سيتم الإدخال والاعتماد على رسالة التكويد)";
                        log(unk, "WARN");
                        result.Lines.Add(unk);
                    }
                    CloseLookupIfOpen(lookup, log);
                }

                // 2) Reuse the same entry form (lookup may have covered it — restore, do not re-menu).
                entry = EnsureEntryForm(
                    entry, app, entryTitle, entryClass, shortcut, navSel, cfg, log);
                if (entry == IntPtr.Zero)
                {
                    string miss = "could not reopen «" + entryTitle + "» for client " + (i + 1);
                    log(miss, "ERROR");
                    result.Lines.Add(miss);
                    if (opt.UpdateTracking)
                    {
                        MarkTracking(cfg.TrackingPath, row, "FAILED",
                            "تعذر فتح شاشة «" + entryTitle + "»");
                    }
                    continue;
                }

                log("filling client " + (i + 1) + " of " + rows.Count + ": " + name, "INFO");
                ActionPace.Wait(ActionPace.BetweenClientsMs);

                entryDirty = true;
                ClientEntryResult fill;
                try
                {
                    if (!CodingHealth.IsAlive(app) || !CodingHealth.IsAlive(entry))
                    {
                        throw new InvalidOperationException(
                            CodingHealth.Describe(CodingHealth.Probe(app != IntPtr.Zero ? app : entry)));
                    }
                    fill = ClientEntry.Invoke(
                        entry, row, selectors, opt.DoSave, opt.Watch, opt.SettleMs, opt.StepMs,
                        cfg.ScansRoot, log);
                    if (opt.OperatorManualFlow && opt.DoSave && fill != null &&
                        !fill.Saved && !fill.AlreadyExists && !fill.Ok)
                    {
                        log("retry fill/save once after pause — Coding is slow", "WARN");
                        ActionPace.Wait(ActionPace.RetryPauseMs);
                        fill = ClientEntry.Invoke(
                            entry, row, selectors, opt.DoSave, opt.Watch, opt.SettleMs, opt.StepMs,
                            cfg.ScansRoot, log);
                    }
                }
                catch (Exception ex)
                {
                    fill = new ClientEntryResult();
                    fill.Fields = new List<FieldFillResult>();
                    fill.Ok = false;
                    fill.Saved = false;
                    fill.SaveUnverified = opt.DoSave;
                    fill.Note = "entry aborted — " + ex.Message;
                    log(fill.Note, "ERROR");
                    result.Lines.Add(fill.Note);
                    if (opt.UpdateTracking)
                    {
                        MarkTracking(cfg.TrackingPath, row, "FAILED", fill.Note);
                    }
                    WriteStatusToSheets(cfg, sheetPath, row,
                        "فشل الإدخال", "FAILED", fill.Note, true, log);
                    // If Coding died/hung, abort remaining; else continue next client.
                    string stopWhy;
                    if (!CodingHealth.EnsureAlive(ref app, cfg, log, out stopWhy, false))
                    {
                        for (int r = i + 1; r < rows.Count; r++)
                        {
                            WriteStatusToSheets(cfg, sheetPath, rows[r],
                                "لم تتم المعالجة",
                                stopWhy.IndexOf("تجم", StringComparison.Ordinal) >= 0
                                    ? "APP_HUNG" : "NOT_PROCESSED",
                                stopWhy, true, log);
                        }
                        break;
                    }
                    entry = IntPtr.Zero;
                    continue;
                }

                log(fill.Note + (fill.Saved ? " (Save pressed)" : ""), fill.Ok ? "OK" : "WARN");
                result.Lines.Add(fill.Note);
                for (int f = 0; f < fill.Fields.Count; f++)
                {
                    FieldFillResult fr = fill.Fields[f];
                    string line = "  " + (fr.Ok ? "OK" : "FAIL") + " " + fr.FieldKey +
                                  " [" + fr.Kind + "] tier=" + fr.Tier + " => '" + fr.Value + "'" +
                                  (fr.Ok ? "" : " (" + fr.Note + ")");
                    log(line, fr.Ok ? "OK" : "WARN");
                    result.Lines.Add(line);
                }
                // Coding itself refused the insert — count as skipped, not as a failure.
                if (fill.AlreadyExists)
                {
                    string dupLine = "SKIP client " + (i + 1) + ": " + name +
                        " — العميل موجود من قبل (رفضه برنامج التكويد عند الحفظ)";
                    log(dupLine, "WARN");
                    result.Lines.Add(dupLine);
                    skippedClients++;
                    if (opt.UpdateTracking)
                    {
                        MarkTracking(cfg.TrackingPath, row, "ALREADY_EXISTS",
                            "رسالة برنامج التكويد: العميل موجود من قبل");
                    }
                    WriteStatusToSheets(cfg, sheetPath, row,
                        "موجود في التكويد", "ALREADY_EXISTS",
                        "رفض الحفظ: العميل موجود من قبل في التكويد", true, log);
                    PullCodesResult pulled = PullCodesForRow(
                        app, cfg, selectors, row, null, opt.SettleMs, log);
                    WriteStatusToSheets(cfg, sheetPath, row,
                        pulled.Ok ? "سحب أكواد" : "موجود — تعذر سحب الأكواد",
                        pulled.Ok ? "CODES_PULLED" : "PULL_CODES_FAILED",
                        pulled.Note, true, log);
                    if (pulled.Screen != IntPtr.Zero)
                    {
                        ClientLookup.CloseWindow(pulled.Screen, "سحب الأكواد", log);
                    }
                    if (i < rows.Count - 1)
                    {
                        entry = ResetEntryForNextClient(
                            entry, app, entryTitle, entryClass, shortcut, navSel, cfg, selectors, log);
                    }
                    continue;
                }

                // Save clicked but Coding never acknowledged: never report success.
                if (fill.SaveUnverified)
                {
                    string unverified = "SAVE UNVERIFIED client " + (i + 1) + ": " + name +
                        " — " + fill.Note;
                    log(unverified, "ERROR");
                    result.Lines.Add(unverified);
                    if (opt.UpdateTracking)
                    {
                        MarkTracking(cfg.TrackingPath, row, "SAVE_UNVERIFIED", fill.Note);
                    }
                    WriteStatusToSheets(cfg, sheetPath, row,
                        "الحفظ غير مؤكد", "SAVE_UNVERIFIED", fill.Note, true, log);
                    if (opt.OperatorManualFlow &&
                        opt.OperatorMaxRows > 0 &&
                        operatorMatched >= opt.OperatorMaxRows)
                    {
                        break;
                    }
                    continue;
                }

                bool clientOk = fill.Ok || (opt.DoSave && CriticalFieldsOk(fill));
                if (clientOk) { okClients++; }
                if (fill.Saved)
                {
                    anySaved = true;
                    // Operator «جديد» confirmed save — required before ترحيل at end of batch.
                    if (opt.OperatorManualFlow) { newSavedCount++; }
                    entryDirty = false;
                    // Safety net: answer any «تأكيد» still on screen after the fill step.
                    int pid = Win32Driver.GetProcessId(entry);
                    ConfirmDialog.ConfirmAll(pid, entry, 3000, 3, log);
                    System.Threading.Thread.Sleep(400);

                    if (opt.UpdateTracking)
                    {
                        MarkTracking(cfg.TrackingPath, row, "SAVED_PENDING",
                            "تم الحفظ في التكويد (بانتظار الترحيل)");
                    }
                    // «الحالة» must move off the lookup result once Coding confirmed the save.
                    WriteStatusToSheets(cfg, sheetPath, row,
                        "تم الحفظ — بانتظار الترحيل", "SAVED_PENDING",
                        "تم الحفظ في التكويد (بانتظار الترحيل)", true, log);
                    if (i < rows.Count - 1)
                    {
                        entry = ResetEntryForNextClient(
                            entry, app, entryTitle, entryClass, shortcut, navSel, cfg, selectors, log);
                    }
                }
                else
                {
                    if (opt.UpdateTracking)
                    {
                        MarkTracking(cfg.TrackingPath, row, clientOk ? "FILLED" : "FAILED", fill.Note);
                    }
                    if (!clientOk)
                    {
                        WriteStatusToSheets(cfg, sheetPath, row,
                            "فشل الإدخال", "FAILED", fill.Note, true, log);
                    }
                    else if (opt.DoSave)
                    {
                        WriteStatusToSheets(cfg, sheetPath, row,
                            "تمت التعبئة بدون حفظ", "FILLED_NOT_SAVED", fill.Note, true, log);
                    }
                }

                if (opt.OperatorManualFlow &&
                    opt.OperatorMaxRows > 0 &&
                    operatorMatched >= opt.OperatorMaxRows)
                {
                    break;
                }
            }

            result.ClientsOk = okClients;
            result.ClientsSkipped = skippedClients;
            result.ClientsTotal = rows.Count;
            result.Saved = anySaved;
            if (skippedClients > 0 && !opt.OperatorManualFlow)
            {
                log(skippedClients + " client(s) not inserted — duplicate at lookup or save", "WARN");
            }
            if (skippedClients > 0 && opt.OperatorManualFlow)
            {
                log(skippedClients + " client(s) skipped/pulled/suspended (manual flow)", "INFO");
            }

            // Every client already coded is a valid outcome, not a failed run.
            int attempted = rows.Count - skippedClients;
            if (attempted <= 0)
            {
                result.Ok = true;
                if (opt.OperatorManualFlow)
                {
                    result.Note = "اكتمل التدفق اليدوي — سحب/موقوف/بدون إدخال جديد (" +
                        rows.Count + " صف)";
                }
                else
                {
                    result.Note = opt.ForceCompleteFlow
                        ? "اكتمل تدفق الاختبار لـ " + rows.Count + " عميل — التكويد رفض الحفظ لأنهم موجودون مسبقاً"
                        : "لا يوجد ما يُدخل — كل العملاء (" + skippedClients + ") موجودون مسبقاً";
                }
                log(result.Note, "OK");
            }
            else if (opt.DoSave && anySaved && opt.UpdateTracking)
            {
                log("tracking -> SAVED_PENDING written per saved client", "OK");
            }

            if (attempted > 0 && opt.DoSave && !opt.OperatorManualFlow)
            {
                result.SavedEvidencePath = savedEvidence;
                if (File.Exists(savedEvidence))
                {
                    string evidence = File.ReadAllText(savedEvidence, Encoding.UTF8);
                    string expectName = CsvUtil.Get(rows[0], "Name", "");
                    string expectId = CsvUtil.Get(rows[0], "NationalId", "");
                    string expectDob = CsvUtil.Get(rows[0], "DateOfBirth", "");
                    bool nameOk = evidence.IndexOf(expectName, StringComparison.Ordinal) >= 0;
                    bool idOk = string.IsNullOrEmpty(expectId) || evidence.IndexOf(expectId, StringComparison.Ordinal) >= 0;
                    bool dobOk = DobPartsInEvidence(evidence, expectDob);
                    log("evidence file: " + savedEvidence, "INFO");
                    log(nameOk ? "PASS: name reached app" : "FAIL: name missing in saved evidence", nameOk ? "OK" : "ERROR");
                    log(idOk ? "PASS: national id reached app" : "FAIL: national id missing in evidence", idOk ? "OK" : "ERROR");
                    log(dobOk ? "PASS: DOB parts reached app" : "WARN: DOB split incomplete in evidence", dobOk ? "OK" : "WARN");
                    result.Ok = fillOk(okClients, attempted) && nameOk && idOk && anySaved;
                    result.Note = result.Ok
                        ? "Test-Save E2E PASS — validated, filled, saved, evidence verified"
                        : "Test-Save E2E incomplete — check field mapping / evidence";
                }
                else
                {
                    // Real Coding.exe won't write CodingReplica-saved.csv — Save press is enough.
                    result.Ok = fillOk(okClients, attempted) && anySaved;
                    result.Note = result.Ok
                        ? "Test-Save PASS — fields filled and Save pressed (no replica evidence file)"
                        : "Test-Save FAIL";
                    log(result.Note, result.Ok ? "OK" : "ERROR");
                }
            }
            else if (attempted > 0 && opt.OperatorManualFlow)
            {
                result.Ok = fillOk(okClients, attempted) && (!opt.DoSave || anySaved || okClients == 0);
                if (opt.DoSave && anySaved)
                {
                    result.Ok = true;
                }
                result.Note = "تدفق يدوي: إدخال " + okClients + " من " + attempted +
                    " — سحب/تخطي " + skippedClients;
                log(result.Note, result.Ok ? "OK" : "ERROR");
            }
            else if (attempted > 0)
            {
                result.Ok = fillOk(okClients, attempted);
                result.Note = "filled+verified " + okClients + " of " + attempted + " (no save)" +
                    (skippedClients > 0 ? " — " + skippedClients + " موجود مسبقاً" : "");
            }

            // After every operator client (جديد then سحب): menu confirm + table + F2/F9.
            if (IsStop(opt))
            {
                log("STOP — skipped confirm/ترحيل after cancel", "WARN");
            }
            else if (opt.OperatorManualFlow && opt.DoSave && processedNewCount > 0)
            {
                string postWhy;
                if (!CodingHealth.EnsureAlive(ref app, cfg, log, out postWhy, true))
                {
                    log("post-save skipped — " + postWhy, "ERROR");
                    result.PostSaveOk = false;
                    result.PostSaveNote = postWhy;
                }
                else
                {
                    log("operator flow: after all clients — close overlays then " +
                        "نظام تكويد العملاء → تأكيد بيانات العملاء قبل إرسالها للتكويد", "INFO");
                    CloseFinishedBotWindows(app, entry, cfg, selectors, log, true);
                    entry = IntPtr.Zero;
                    entryDirty = false;
                    ActionPace.Wait(ActionPace.AfterDialogMs);

                    CodingPostSaveOptions postOpt = new CodingPostSaveOptions();
                    postOpt.Config = cfg;
                    postOpt.Selectors = selectors;
                    postOpt.AppWindow = app;
                    postOpt.ExportDateRangeYearToDate = ResolveYearToDate(opt, cfg, sheetPath);
                    postOpt.SheetPathForWriteBack = sheetPath;
                    postOpt.DoWriteBack = cfg.Settings != null && cfg.Settings.DoWriteBack;
                    postOpt.SettleMs = opt.SettleMs > 0 ? opt.SettleMs : ActionPace.OperatorSettleMs;
                    postOpt.ForceMenuOpen = true;
                    postOpt.Log = log;
                    try
                    {
                        CodingPostSaveResult post = CodingPostSave.RunConfirmOnly(postOpt);
                        result.PostSaveOk = post != null && post.Ok;
                        result.PostSaveNote = post != null ? post.Note : "";
                        if (post != null && !string.IsNullOrEmpty(post.Note))
                        {
                            log("post-save: " + post.Note, post.Ok ? "OK" : "WARN");
                        }
                        if (result.PostSaveOk) { result.Ok = true; }
                    }
                    catch (Exception ex)
                    {
                        result.PostSaveOk = false;
                        result.PostSaveNote = "confirm aborted — " + ex.Message;
                        log(result.PostSaveNote, "ERROR");
                    }
                }
            }
            else if (opt.OperatorManualFlow && opt.DoSave && processedNewCount == 0)
            {
                log("confirm screen skipped — no «جديد» clients in this run", "INFO");
            }
            else if (opt.DoSave)
            {
                log("post-save export skipped — runs once daily at " +
                    DailyExport.Describe(cfg.Settings) + " (use «اختبار التصدير فقط» to try now)", "INFO");
            }

            log(result.Note, result.Ok ? "OK" : "ERROR");
            // Mock / no-save runs always close the form (answer «هل تريد الحفظ؟» with No).
            // Save runs keep it open only while it still holds unsaved data.
            bool closeEntry = !opt.DoSave
                || attempted <= 0
                || (anySaved && !entryDirty &&
                    (cfg.App == null || cfg.App.CloseEntryAfterSave));
            if (!closeEntry)
            {
                log("leaving «بيانات العملاء» open — will not close before Save", "INFO");
            }
            else if (!opt.DoSave && entryDirty)
            {
                log("closing «بيانات العملاء» after mock fill (no save)", "INFO");
            }
            CloseFinishedBotWindows(app, entry, cfg, selectors, log, closeEntry);
            return result;
        }

        // Test-Save / sample sheet → year-to-date; production sheet → today-only unless forced.
        private static bool ResolveYearToDate(EntryTestOptions opt, BotConfig cfg, string sheetPath)
        {
            if (opt.ExportDateRangeYearToDate.HasValue) { return opt.ExportDateRangeYearToDate.Value; }
            if (IsSampleTestSaveSheet(sheetPath)) { return true; }
            if (cfg.Settings != null && cfg.Settings.ExportDateRangeYearToDate) { return true; }
            return false;
        }

        private static bool IsSampleTestSaveSheet(string sheetPath)
        {
            if (string.IsNullOrWhiteSpace(sheetPath)) { return false; }
            string name = Path.GetFileName(sheetPath);
            return string.Equals(name, "Test-Save-Client.csv", StringComparison.OrdinalIgnoreCase);
        }

        private static bool fillOk(int ok, int total) { return total > 0 && ok == total; }

        private static bool IsStop(EntryTestOptions opt)
        {
            try { return opt != null && opt.IsCancelled != null && opt.IsCancelled(); }
            catch { return false; }
        }

        // Test-Save business gate: Name + NationalId + DOB triple must land; optional combos may warn.
        private static bool CriticalFieldsOk(ClientEntryResult fill)
        {
            if (fill == null || fill.Fields == null) { return false; }
            string[] need = new string[] { "name", "nationalId", "dobDay", "dobMonth", "dobYear" };
            for (int n = 0; n < need.Length; n++)
            {
                bool found = false;
                for (int i = 0; i < fill.Fields.Count; i++)
                {
                    if (string.Equals(fill.Fields[i].FieldKey, need[n], StringComparison.OrdinalIgnoreCase))
                    {
                        found = fill.Fields[i].Ok;
                        break;
                    }
                }
                if (!found) { return false; }
            }
            return true;
        }

        private static bool RegexHasDay(string evidence, int n)
        {
            string needle = "\"" + n.ToString() + "\"";
            return evidence.IndexOf(needle, StringComparison.Ordinal) >= 0;
        }

        private static bool DobPartsInEvidence(string evidence, string dob)
        {
            if (string.IsNullOrWhiteSpace(dob) || string.IsNullOrEmpty(evidence)) { return true; }
            DateTime d;
            string[] fmts = new string[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
            if (!DateTime.TryParseExact(dob.Trim(), fmts[0], System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out d) &&
                !DateTime.TryParseExact(dob.Trim(), fmts[1], System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out d) &&
                !DateTime.TryParseExact(dob.Trim(), fmts[2], System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out d))
            {
                return false;
            }
            return RegexHasDay(evidence, d.Day) &&
                   RegexHasDay(evidence, d.Month) &&
                   evidence.IndexOf(d.Year.ToString(), StringComparison.Ordinal) >= 0;
        }

        private static EntryTestResult Fail(EntryTestResult result, Action<string, string> log, string msg)
        {
            result.Ok = false;
            result.Note = msg;
            log(msg, "ERROR");
            return result;
        }

        private static void CloseLookupIfOpen(ClientLookupResult lookup, Action<string, string> log)
        {
            if (lookup == null || lookup.Screen == IntPtr.Zero) { return; }
            if (!Win32Interop.IsWindow(lookup.Screen))
            {
                lookup.Screen = IntPtr.Zero;
                return;
            }
            ClientLookup.CloseWindow(lookup.Screen, "الإستعلام", log);
            lookup.Screen = IntPtr.Zero;
        }

        // Close screens this run opened. Never Yes on «هل تريد الحفظ؟».
        // Leave Coding.exe / Form1 / Messages alone — those are not ours.
        private static void CloseFinishedBotWindows(
            IntPtr app,
            IntPtr entry,
            BotConfig cfg,
            List<FieldSelector> selectors,
            Action<string, string> log,
            bool closeEntryForm)
        {
            BotAppConfig appCfg = cfg != null ? cfg.App : null;
            try
            {
                IntPtr pull = CodingPostSave.FindOpenPullCodes(app, appCfg, selectors);
                if (pull != IntPtr.Zero && pull != entry && pull != app)
                {
                    ClientLookup.CloseWindow(pull, "سحب الأكواد", log);
                }
            }
            catch { }
            try
            {
                IntPtr look = ClientLookup.FindOpenScreen(app, appCfg, selectors);
                if (look != IntPtr.Zero && look != entry && look != app)
                {
                    ClientLookup.CloseWindow(look, "الإستعلام", log);
                }
            }
            catch { }
            if (!closeEntryForm) { return; }

            // The form may have been opened for auto-map only, or left over from an
            // earlier run — close it even when this run never held a handle to it.
            if (entry == IntPtr.Zero || !Win32Interop.IsWindow(entry))
            {
                string title = appCfg != null ? appCfg.EntryWindowTitle : "";
                if (string.IsNullOrWhiteSpace(title)) { title = "بيانات العملاء"; }
                string cls = appCfg != null ? appCfg.EntryWindowClass : "";
                if (string.IsNullOrWhiteSpace(cls)) { cls = "ThunderRT6FormDC"; }
                try { entry = ClientEntry.RestoreEntryForm(app, title, cls, IntPtr.Zero, log); }
                catch { entry = IntPtr.Zero; }
                if (entry != IntPtr.Zero && !ClientEntry.IsClientEntryForm(entry, title))
                {
                    entry = IntPtr.Zero;
                }
            }
            CloseEntryForm(entry, "بيانات العملاء", log);
        }

        // Close without saving. Returns Zero only when the form is actually gone.
        private static IntPtr CloseEntryForm(IntPtr entry, string label, Action<string, string> log)
        {
            if (entry == IntPtr.Zero || !Win32Interop.IsWindow(entry)) { return IntPtr.Zero; }
            bool gone = ClientLookup.CloseWindow(entry, label, log);
            if (gone || !Win32Interop.IsWindow(entry) || !Win32Interop.IsWindowVisible(entry))
            {
                return IntPtr.Zero;
            }
            return entry;
        }

        // After client N: «جديد» (or close without saving) so client N+1 starts blank.
        private static IntPtr ResetEntryForNextClient(
            IntPtr entry,
            IntPtr app,
            string entryTitle,
            string entryClass,
            string shortcut,
            FieldSelector navSel,
            BotConfig cfg,
            List<FieldSelector> selectors,
            Action<string, string> log)
        {
            int pid = app != IntPtr.Zero
                ? Win32Driver.GetProcessId(app)
                : (entry != IntPtr.Zero ? Win32Driver.GetProcessId(entry) : 0);

            if (entry != IntPtr.Zero && Win32Interop.IsWindow(entry) && Win32Interop.IsWindowVisible(entry))
            {
                if (ClientEntry.ClickNewRecord(entry, selectors, log))
                {
                    Thread.Sleep(400);
                    if (pid > 0)
                    {
                        // Save already succeeded, so a leftover تأكيد takes Yes — but
                        // «هل تريد الحفظ؟» here means a dirty form and must take No.
                        IntPtr modal = ConfirmDialog.FindModal(pid, entry);
                        if (modal != IntPtr.Zero &&
                            ConfirmDialog.IsSaveOnCloseMessage(ConfirmDialog.ReadMessage(modal)))
                        {
                            ConfirmDialog.AnswerClosePrompts(pid, entry, 2000, 2, log);
                        }
                        else
                        {
                            ConfirmDialog.ConfirmAll(pid, entry, 2000, 2, log);
                        }
                    }
                    log("entry ready for next client (جديد)", "OK");
                    return entry;
                }
                log("«جديد» not found — reopening after save", "WARN");
            }

            entry = CloseEntryForm(entry, "entry form (before next client)", log);
            return EnsureEntryForm(entry, app, entryTitle, entryClass, shortcut, navSel, cfg, log);
        }

        private static IntPtr EnsureEntryForm(
            IntPtr entry,
            IntPtr app,
            string entryTitle,
            string entryClass,
            string shortcut,
            FieldSelector navSel,
            BotConfig cfg,
            Action<string, string> log)
        {
            IntPtr live = ClientEntry.RestoreEntryForm(
                app, entryTitle, entryClass, entry, log);
            if (live != IntPtr.Zero && ClientEntry.IsClientEntryForm(live, entryTitle))
            {
                if (entry != live)
                {
                    log("entry still open — reusing «" + Win32Driver.GetWindowText(live) + "»", "OK");
                }
                WindowLayout.ApplyAll(app, live, IntPtr.Zero, cfg.Windows, log);
                return live;
            }
            if (live != IntPtr.Zero)
            {
                log("ignored non-entry window «" + Win32Driver.GetWindowText(live) + "»", "WARN");
            }

            // Form is gone — open once. Do not hide+re-click the menu if a child still exists.
            IntPtr fresh = ClientEntry.GetEntryScanRoot(
                app, entryTitle, entryClass, cfg.App.ChildWindow, shortcut, navSel,
                cfg.App.MenuPath, false, 700);
            if (fresh == IntPtr.Zero) { return IntPtr.Zero; }

            log("entry reopened: '" + Win32Driver.GetWindowText(fresh) + "'", "OK");
            WindowLayout.ApplyAll(app, fresh, IntPtr.Zero, cfg.Windows, log);
            return fresh;
        }

        // AppData may still hold a 1-row Test-Save CSV from an older extract. Prefer
        // any candidate with the full 3-client sample (install folder next to the EXE).
        private static string RefreshTestSaveSample(
            string baseDir, string current, Action<string, string> log)
        {
            string dest = Path.Combine(baseDir ?? "", @"Sample\Test-Save-Client.csv");
            List<string> candidates = new List<string>();
            AddIfFile(candidates, current);
            AddIfFile(candidates, dest);
            AddIfFile(candidates, Path.Combine(baseDir ?? "", @"Sample\Test-Save-Client.csv"));
            try
            {
                string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(exeDir))
                {
                    AddIfFile(candidates, Path.Combine(exeDir, @"Sample\Test-Save-Client.csv"));
                }
            }
            catch { }

            string richest = current;
            int bestN = CountSheetRows(current);
            for (int i = 0; i < candidates.Count; i++)
            {
                int n = CountSheetRows(candidates[i]);
                if (n > bestN)
                {
                    bestN = n;
                    richest = candidates[i];
                }
            }

            if (bestN < 3)
            {
                log("Test-Save sheet has " + bestN + " client(s) — expected 3 (Sample\\Test-Save-Client.csv)", "WARN");
            }

            if (bestN >= 2 && !string.IsNullOrEmpty(richest) && File.Exists(richest) &&
                !SameSheetPath(richest, dest))
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.Copy(richest, dest, true);
                    CopySampleDocs(richest, dest);
                    log("Test-Save sheet refreshed: " + bestN + " clients ← " + richest, "OK");
                    return dest;
                }
                catch (Exception ex)
                {
                    log("could not refresh Test-Save sheet: " + ex.Message, "WARN");
                    return richest;
                }
            }
            if (File.Exists(dest)) { return dest; }
            return current;
        }

        private static void AddIfFile(List<string> list, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return; }
            for (int i = 0; i < list.Count; i++)
            {
                if (SameSheetPath(list[i], path)) { return; }
            }
            list.Add(path);
        }

        private static int CountSheetRows(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return 0; }
            try { return CsvUtil.ReadCsv(path).Count; }
            catch { return 0; }
        }

        private static void CopySampleDocs(string srcCsv, string dstCsv)
        {
            string srcDocs = Path.Combine(Path.GetDirectoryName(srcCsv), "Docs");
            string dstDocs = Path.Combine(Path.GetDirectoryName(dstCsv), "Docs");
            if (!Directory.Exists(srcDocs)) { return; }
            Directory.CreateDirectory(dstDocs);
            string[] dirs = Directory.GetDirectories(srcDocs);
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                CopyDirRecursive(dirs[i], Path.Combine(dstDocs, name));
            }
        }

        private static void CopyDirRecursive(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            string[] files = Directory.GetFiles(src);
            for (int i = 0; i < files.Length; i++)
            {
                File.Copy(files[i], Path.Combine(dst, Path.GetFileName(files[i])), true);
            }
            string[] dirs = Directory.GetDirectories(src);
            for (int i = 0; i < dirs.Length; i++)
            {
                CopyDirRecursive(dirs[i], Path.Combine(dst, Path.GetFileName(dirs[i])));
            }
        }

        private static string ResolveCode12(OrderedDictionary row, ClientLookupResult lookup)
        {
            string live12 = lookup != null ? lookup.Code12 : "";
            string sheet12 = "";
            if (row != null)
            {
                sheet12 = CsvUtil.Get(row, "CODE12", "");
                if (string.IsNullOrWhiteSpace(sheet12))
                {
                    sheet12 = CsvUtil.Get(row, "كود 12", "");
                }
                // Manual operator entry may store the 12-digit code in الكود الموحد.
                if (string.IsNullOrWhiteSpace(sheet12) ||
                    FlexGridSurface.AsOperatorPullCode(sheet12).Length == 0)
                {
                    string ninCol = CsvUtil.Get(row, "الكود الموحد", "NIN");
                    if (FlexGridSurface.AsOperatorPullCode(ninCol).Length > 0)
                    {
                        sheet12 = ninCol;
                    }
                }
                if (string.IsNullOrWhiteSpace(sheet12))
                {
                    string note = CsvUtil.Get(row, "TestNote", "");
                    if (string.IsNullOrWhiteSpace(note))
                    {
                        note = CsvUtil.Get(row, "ملاحظات", "");
                    }
                    System.Text.RegularExpressions.Match m =
                        System.Text.RegularExpressions.Regex.Match(
                            note ?? "", @"CODE12\s*([0-9]+)");
                    if (m.Success) { sheet12 = m.Groups[1].Value; }
                }
            }
            string prefer = FlexGridSurface.AsOperatorPullCode(live12);
            if (prefer.Length == 0) { prefer = FlexGridSurface.AsOperatorPullCode(sheet12); }
            return prefer;
        }

        private static int CompareManualFlowOrder(OrderedDictionary a, OrderedDictionary b)
        {
            return ManualFlowRank(a) - ManualFlowRank(b);
        }

        private static int ManualFlowRank(OrderedDictionary row)
        {
            string st = OperatorDecision.ReadStatus(row);
            if (OperatorDecision.IsNew(st)) { return 0; }
            if (OperatorDecision.IsPull(st)) { return 1; }
            if (OperatorDecision.IsSuspended(st)) { return 2; }
            return 3;
        }

        private static string ResolvePullSearch(OrderedDictionary row, ClientLookupResult lookup)
        {
            string code12 = ResolveCode12(row, lookup);
            string nin = lookup != null ? lookup.Nin : "";
            return FlexGridSurface.PreferPullSearchCode(code12, nin);
        }

        private static PullCodesResult PullCodesForRow(
            IntPtr appWindow,
            BotConfig cfg,
            List<FieldSelector> selectors,
            OrderedDictionary row,
            ClientLookupResult lookup,
            int settleMs,
            Action<string, string> log)
        {
            return PullCodesForRow(appWindow, cfg, selectors, row, lookup, settleMs, log, false);
        }

        // preferMenuPull: operator manual flow (no inquiry) → menu سحب + CODE12 + F2/F9.
        private static PullCodesResult PullCodesForRow(
            IntPtr appWindow,
            BotConfig cfg,
            List<FieldSelector> selectors,
            OrderedDictionary row,
            ClientLookupResult lookup,
            int settleMs,
            Action<string, string> log,
            bool preferMenuPull)
        {
            string nid = "";
            if (row != null)
            {
                nid = CsvUtil.Get(row, "NationalId", "").Trim();
                if (nid.Length == 0) { nid = CsvUtil.Get(row, "الرقم القومي", "").Trim(); }
            }
            if (nid.Length != 14 || !OcrConsensus.LooksLikeEgyptianNid(nid))
            {
                PullCodesResult refused = new PullCodesResult();
                refused.Note = "ZERO-TOLERANCE: لا سحب بدون رقم قومي صالح في الشيت";
                if (log != null) { log("pull-codes: " + refused.Note, "ERROR"); }
                return refused;
            }
            // Prefer sheet/live CODE12 only — never hand OCR NIN to menu search.
            string code12 = ResolveCode12(row, lookup);

            IntPtr inquiry = lookup != null ? lookup.Screen : IntPtr.Zero;
            if (inquiry == IntPtr.Zero || !Win32Interop.IsWindow(inquiry))
            {
                BotAppConfig appCfg = cfg != null ? cfg.App : null;
                inquiry = ClientLookup.FindOpenScreen(appWindow, appCfg, selectors);
            }
            bool inquiryOpen = inquiry != IntPtr.Zero && Win32Interop.IsWindow(inquiry);

            // Manual triage (or no open inquiry): known menu path with F2/F9.
            if (preferMenuPull || !inquiryOpen)
            {
                if (log != null)
                {
                    log("pull-codes: menu path سحب الأكواد (CODE12 + F2/F9)", "INFO");
                }
                return PullCodes.Run(
                    appWindow, cfg, selectors, "", code12, "", null, nid, settleMs, log);
            }

            return PullCodes.RunFromInquiry(
                appWindow,
                inquiry,
                cfg, selectors, settleMs, log,
                code12,
                null,
                nid);
        }

        private static void WriteCodesToSheets(
            BotConfig cfg, string workingSheet, OrderedDictionary row,
            string nin, string code12, Action<string, string> log)
        {
            string nid = CsvUtil.Get(row, "NationalId", "").Trim();
            if (nid.Length == 0) { nid = CsvUtil.Get(row, "الرقم القومي", "").Trim(); }
            string[] paths = new string[] { workingSheet, cfg != null ? cfg.SheetPath : "" };
            for (int i = 0; i < paths.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(paths[i]) || !File.Exists(paths[i])) { continue; }
                if (i > 0 && SameSheetPath(paths[0], paths[i])) { continue; }
                try
                {
                    SheetSync.WriteClientCodes(paths[i], nid, nin, code12);
                }
                catch (Exception ex)
                {
                    if (log != null) { log("sheet CODE12 write failed: " + ex.Message, "WARN"); }
                }
            }
        }

        private static void WriteLookupToSheets(
            BotConfig cfg,
            string workingSheet,
            OrderedDictionary row,
            ClientLookupResult lookup,
            Action<string, string> log)
        {
            if (lookup == null) { return; }
            string ar;
            string en;
            string notes;
            if (lookup.Outcome == LookupOutcome.Found)
            {
                ar = "موجود في الإستعلام";
                en = "ALREADY_EXISTS";
                notes = "الإستعلام للتعديل: موجود" +
                    (string.IsNullOrEmpty(lookup.MatchedBy) ? "" : " (" + lookup.MatchedBy + ")") +
                    (string.IsNullOrEmpty(lookup.Note) ? "" : " — " + lookup.Note);
            }
            else if (lookup.Outcome == LookupOutcome.NotFound)
            {
                ar = "غير موجود في الإستعلام";
                en = "NOT_FOUND";
                notes = "الإستعلام للتعديل: غير موجود — يمكن الإدخال";
            }
            else
            {
                ar = "تعذر قراءة الإستعلام";
                en = "LOOKUP_UNKNOWN";
                notes = string.IsNullOrEmpty(lookup.Note)
                    ? "تعذر قراءة نتيجة الإستعلام"
                    : lookup.Note;
            }
            WriteStatusToSheets(cfg, workingSheet, row, ar, en, notes, true, log);
        }

        private static void WriteStatusToSheets(
            BotConfig cfg,
            string workingSheet,
            OrderedDictionary row,
            string statusAr,
            string statusEn,
            string notes,
            bool overwriteStatus,
            Action<string, string> log)
        {
            string nid = CsvUtil.Get(row, "NationalId", "").Trim();
            if (nid.Length == 0) { nid = CsvUtil.Get(row, "الرقم القومي", "").Trim(); }
            TryWriteSheet(workingSheet, nid, statusAr, statusEn, notes, overwriteStatus, "ملف التشغيل", log);
            string original = cfg != null ? cfg.SheetPath : "";
            if (!SameSheetPath(workingSheet, original))
            {
                TryWriteSheet(original, nid, statusAr, statusEn, notes, overwriteStatus, "ملف الفرع", log);
            }
        }

        private static void TryWriteSheet(
            string path,
            string nid,
            string statusAr,
            string statusEn,
            string notes,
            bool overwriteStatus,
            string label,
            Action<string, string> log)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { return; }
            try
            {
                SheetUpdateResult ur = SheetSync.WriteClientStatus(
                    path, nid, statusAr, statusEn, notes, overwriteStatus);
                if (ur != null && ur.Updated > 0)
                {
                    log(label + ": «الحالة» ← " +
                        (overwriteStatus ? statusAr : notes) + " (" + path + ")", "OK");
                }
                else
                {
                    log(label + ": الرقم القومي غير موجود في الملف — " + path, "WARN");
                }
            }
            catch (Exception ex)
            {
                log(label + " write failed: " + ex.Message, "WARN");
            }
        }

        private static bool SameSheetPath(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) { return false; }
            try
            {
                return string.Equals(
                    Path.GetFullPath(a).TrimEnd('\\', '/'),
                    Path.GetFullPath(b).TrimEnd('\\', '/'),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static void MarkTracking(
            string trackingPath, OrderedDictionary row, string status, string note)
        {
            string nid = CsvUtil.Get(row, "NationalId", "").Trim();
            string name = CsvUtil.Get(row, "Name", "");
            string stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            List<OrderedDictionary> tracking = RunTracker.LoadTracking(trackingPath);
            for (int i = 0; i < tracking.Count; i++)
            {
                if (CsvUtil.Get(tracking[i], "NationalId", "").Trim() != nid) { continue; }
                tracking[i]["Status"] = status;
                tracking[i]["Name"] = name;
                tracking[i]["LastUpdated"] = stamp;
                tracking[i]["Notes"] = note;
                RunTracker.SaveTracking(trackingPath, tracking);
                return;
            }

            OrderedDictionary n = new OrderedDictionary(StringComparer.OrdinalIgnoreCase);
            n["NationalId"] = nid;
            n["Name"] = name;
            n["Status"] = status;
            n["NIN"] = "";
            n["CODE12"] = "";
            n["RejectReason"] = "";
            n["Notes"] = note;
            n["SourceFile"] = "Test-Save-Client.csv";
            n["LastUpdated"] = stamp;
            tracking.Add(n);
            RunTracker.SaveTracking(trackingPath, tracking);
        }
    }
}
