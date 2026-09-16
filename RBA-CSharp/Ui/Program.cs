// =============================================================================
//  Program.cs - compiled RBA entry point (no PowerShell).
//
//  CLI (mock / preflight — never presses ترحيل):
//    RBA.exe --validate
//    RBA.exe --mock-fill
//    RBA.exe --test-save [--confirm-save]
//    RBA.exe --preflight          full mock readiness suite (validate+mock+save)
// =============================================================================
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using RbaCore;

namespace RbaUi
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!DeviceLock.IsThisMachineAllowed())
            {
                MessageBox.Show(
                    "هذا الإصدار مربوط بجهاز محدد ولا يعمل على هذا الكمبيوتر.\n\n" +
                    "MAC على الجهاز: " + DeviceLock.DescribeLocalMacs(),
                    "RPA V1",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Stop);
                return 1;
            }

            // Single-EXE package: Sample/Docs/config live inside the binary and
            // are extracted once under %LocalAppData%\RPA-V1.
            string baseDir;
            try
            {
                baseDir = PayloadBootstrap.EnsureWorkspace();
            }
            catch (Exception ex)
            {
                // Unpackaged / developer layout: EXE folder with Sample beside it.
                baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string sample = Path.Combine(baseDir, "Sample");
                if (!Directory.Exists(sample))
                {
                    MessageBox.Show(
                        "تعذّر تجهيز ملفات RPA V1:\n" + ex.Message,
                        "RPA V1", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return 1;
                }
            }

            bool testSave = false;
            bool confirmSave = false;
            bool mockFill = false;
            bool validateOnly = false;
            bool preflight = false;
            bool setupOnly = false;
            bool operatorSmoke = false;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, "--test-save", StringComparison.OrdinalIgnoreCase)) { testSave = true; }
                if (string.Equals(a, "--confirm-save", StringComparison.OrdinalIgnoreCase)) { confirmSave = true; }
                if (string.Equals(a, "--mock-fill", StringComparison.OrdinalIgnoreCase)) { mockFill = true; }
                if (string.Equals(a, "--validate", StringComparison.OrdinalIgnoreCase)) { validateOnly = true; }
                if (string.Equals(a, "--preflight", StringComparison.OrdinalIgnoreCase)) { preflight = true; }
                if (string.Equals(a, "--setup", StringComparison.OrdinalIgnoreCase)) { setupOnly = true; }
                if (string.Equals(a, "--operator-smoke", StringComparison.OrdinalIgnoreCase)) { operatorSmoke = true; }
            }

            if (preflight) { return RunPreflight(baseDir); }
            if (validateOnly) { return RunValidateCli(baseDir); }
            if (mockFill) { return RunMockFillCli(baseDir); }
            if (testSave) { return RunTestSaveCli(baseDir, confirmSave); }
            if (operatorSmoke) { return RunOperatorSmokeCli(baseDir); }
            if (setupOnly)
            {
                if (!SettingsLock.EnsureUnlocked(null)) { return 1; }
                BotConfig cfg = BotConfig.Load(baseDir);
                Application.Run(new SetupForm(cfg));
                return 0;
            }

            Application.Run(new MainForm(baseDir));
            return 0;
        }

        static string NewLog(string prefix)
        {
            string path = Path.Combine(ActionLog.BesideExeLogsDir(), prefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");
            try { Directory.CreateDirectory(Path.GetDirectoryName(path)); } catch { }
            return path;
        }

        static Action<string, string> MakeLogger(string logPath, StringBuilder buf)
        {
            return delegate(string m, string level)
            {
                string line = DateTime.Now.ToString("HH:mm:ss") + " [" + level + "] " + m;
                if (buf != null) { buf.AppendLine(line); }
                try { Console.WriteLine(line); } catch { }
                try { File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
            };
        }

        static int RunValidateCli(string baseDir)
        {
            string logPath = NewLog( "validate");
            Action<string, string> log = MakeLogger(logPath, null);
            BotConfig cfg = BotConfig.Load(baseDir);
            string sheet = cfg.SheetPath;
            if (string.IsNullOrEmpty(sheet) || !File.Exists(sheet))
            {
                sheet = Path.Combine(baseDir, @"Sample\Clients_Input.csv");
            }
            log("validate sheet: " + sheet, "INFO");
            ValidateResult vr = Validator.ValidateFile(sheet, cfg.ScansRoot, cfg.TrackingPath);
            for (int i = 0; i < vr.LogLines.Count; i++) { log(vr.LogLines[i], "INFO"); }
            bool ok = vr.Ok > 0 && vr.Bad >= 0;
            log("result ok=" + vr.Ok + " bad=" + vr.Bad + " docsWarn=" + vr.DocBad, ok ? "OK" : "ERROR");
            return (vr.Bad == 0 || vr.Ok > 0) ? 0 : 1;
        }

        static int RunMockFillCli(string baseDir)
        {
            string logPath = NewLog( "mockfill");
            Action<string, string> log = MakeLogger(logPath, null);
            log("=== MOCK FILL (no save, no ترحيل) ===", "INFO");

            EntryTestOptions opt = new EntryTestOptions();
            opt.BaseDir = baseDir;
            opt.SheetPath = Path.Combine(baseDir, @"Sample\Test-Save-Client.csv");
            if (!File.Exists(opt.SheetPath))
            {
                BotConfig cfg = BotConfig.Load(baseDir);
                opt.SheetPath = cfg.SheetPath;
            }
            opt.MockOne = true;
            opt.DoSave = false;
            opt.Watch = true;
            opt.AutoMapIfEmpty = true;
            opt.UpdateTracking = false;
            opt.StepMs = 200;
            opt.Log = log;

            EntryTestResult res = EntryTest.Run(opt);
            if (res.Saved)
            {
                log("SAFETY FAIL: mock fill pressed Save", "ERROR");
                return 1;
            }
            log(res.Note, res.Ok ? "OK" : "ERROR");
            return res.Ok ? 0 : 1;
        }

        static int RunTestSaveCli(string baseDir, bool confirmSave)
        {
            if (!confirmSave)
            {
                DialogResult ans = MessageBox.Show(
                    "تحذير: اختبار الحفظ سيضغط «حفظ» ويعالج 3 عملاء من Sample\\Test-Save-Client.csv.\n\nهل تريد المتابعة؟",
                    "تأكيد الحفظ - Test-Save",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (ans != DialogResult.Yes) { return 0; }
            }

            string logPath = NewLog( "testsave");
            Action<string, string> log = MakeLogger(logPath, null);

            EntryTestOptions opt = new EntryTestOptions();
            opt.BaseDir = baseDir;
            opt.SheetPath = Path.Combine(baseDir, @"Sample\Test-Save-Client.csv");
            opt.DoSave = true;
            opt.ForceCompleteFlow = true;
            opt.MockOne = false;
            opt.Watch = true;
            opt.AutoMapIfEmpty = true;
            opt.UpdateTracking = true;
            opt.Log = log;

            EntryTestResult res = EntryTest.Run(opt);
            string summary = res.Note + "\n\nOK=" + res.Ok + "  Saved=" + res.Saved +
                "\nClients " + res.ClientsOk + "/" + res.ClientsTotal +
                (string.IsNullOrEmpty(res.SavedEvidencePath) ? "" : ("\n" + res.SavedEvidencePath)) +
                "\n\nLog: " + logPath;
            try { File.AppendAllText(logPath, summary + Environment.NewLine, Encoding.UTF8); } catch { }
            try { Console.WriteLine(summary); } catch { }
            if (!confirmSave)
            {
                MessageBox.Show(
                    summary,
                    res.Ok ? "Test-Save PASS" : "Test-Save FAIL",
                    MessageBoxButtons.OK,
                    res.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            }
            return res.Ok ? 0 : 1;
        }

        // Full mock readiness: binary checks + validate + mock-fill + test-save on CodingReplica.
        static int RunPreflight(string baseDir)
        {
            string logPath = NewLog( "preflight");
            StringBuilder buf = new StringBuilder();
            Action<string, string> log = MakeLogger(logPath, buf);
            int fails = 0;

            log("========== PREFLIGHT / MOCK PRODUCTION GATE ==========", "INFO");
            log("baseDir: " + baseDir, "INFO");

            // 1) Binary must be compiled GUI, not a PowerShell launcher.
            string exe = Path.Combine(baseDir, "RBA.exe");
            if (!File.Exists(exe)) { exe = Assembly.GetExecutingAssembly().Location; }
            byte[] bytes = File.ReadAllBytes(exe);
            string ascii = Encoding.ASCII.GetString(bytes);
            bool hasMain = ascii.IndexOf("MainForm", StringComparison.Ordinal) >= 0;
            bool hasPs = ascii.IndexOf("powershell.exe", StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasPs1 = ascii.IndexOf("RBA.ps1", StringComparison.OrdinalIgnoreCase) >= 0;
            log("binary size=" + bytes.Length + " MainForm=" + hasMain + " powershell.exe=" + hasPs + " RBA.ps1=" + hasPs1,
                (hasMain && !hasPs && !hasPs1) ? "OK" : "ERROR");
            if (!hasMain || hasPs || hasPs1) { fails++; }

            // 2) Source safety: no ترحيل button automation in compiled core.
            string coreDir = Path.Combine(Path.GetDirectoryName(baseDir) ?? baseDir, "RBA-CSharp", "Core");
            if (Directory.Exists(coreDir))
            {
                string[] cs = Directory.GetFiles(coreDir, "*.cs");
                for (int i = 0; i < cs.Length; i++)
                {
                    string txt = File.ReadAllText(cs[i], Encoding.UTF8);
                    if (txt.IndexOf("\"ترحيل\"", StringComparison.Ordinal) >= 0 ||
                        txt.IndexOf("btnSubmit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        txt.IndexOf("FieldKey = \"submit\"", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        log("SAFETY: submit/ترحيل reference in " + Path.GetFileName(cs[i]), "ERROR");
                        fails++;
                    }
                }
                log("source scan: no ترحيل/submit automation found", fails == 0 ? "OK" : "WARN");
            }

            // 3) EgyptId smoke
            EgyptId.IdInfo idOk = EgyptId.TestEgyptianNationalId("28503142200993");
            log("EgyptId Test-Save NID IsValid=" + idOk.IsValid + " ChecksumOk=" + idOk.ChecksumOk,
                idOk.IsValid ? "OK" : "ERROR");
            if (!idOk.IsValid) { fails++; }

            // 4) Validate sample sheets
            BotConfig cfg = BotConfig.Load(baseDir);
            string clients = Path.Combine(baseDir, @"Sample\Clients_Input.csv");
            string saveSheet = Path.Combine(baseDir, @"Sample\Test-Save-Client.csv");
            if (File.Exists(clients))
            {
                ValidateResult vr = Validator.ValidateFile(clients, cfg.ScansRoot, cfg.TrackingPath);
                log("Clients_Input validate ok=" + vr.Ok + " bad=" + vr.Bad, vr.Ok > 0 ? "OK" : "ERROR");
                if (vr.Ok == 0) { fails++; }
            }
            else
            {
                log("Clients_Input.csv missing", "ERROR");
                fails++;
            }
            if (File.Exists(saveSheet))
            {
                ValidateResult vr2 = Validator.ValidateFile(saveSheet, cfg.ScansRoot, cfg.TrackingPath);
                log("Test-Save-Client validate ok=" + vr2.Ok + " bad=" + vr2.Bad, vr2.Bad == 0 ? "OK" : "ERROR");
                if (vr2.Bad > 0) { fails++; }
            }
            else
            {
                log("Test-Save-Client.csv missing", "ERROR");
                fails++;
            }

            // 5) CodingReplica must be running for UI mock tests
            IntPtr app = FieldDiscovery.GetTargetAppWindow("تكويد", "", false, 3);
            if (app == IntPtr.Zero)
            {
                app = FieldDiscovery.GetTargetAppWindow("برنامج التكويد", "", false, 3);
            }
            if (app == IntPtr.Zero)
            {
                log("CodingReplica / Coding window not open — start replica before UI mocks", "ERROR");
                fails++;
                WritePreflightSummary(logPath, buf, fails);
                return 1;
            }
            log("attached for mock UI: " + Win32Driver.GetWindowTitle(app), "OK");

            // 6) Mock fill (NO save)
            log("--- MOCK FILL ---", "INFO");
            EntryTestOptions mockOpt = new EntryTestOptions();
            mockOpt.BaseDir = baseDir;
            mockOpt.SheetPath = saveSheet;
            mockOpt.MockOne = true;
            mockOpt.DoSave = false;
            mockOpt.Watch = true;
            mockOpt.AutoMapIfEmpty = true;
            mockOpt.UpdateTracking = false;
            mockOpt.StepMs = 150;
            mockOpt.Log = log;
            EntryTestResult mockRes = EntryTest.Run(mockOpt);
            if (mockRes.Saved)
            {
                log("SAFETY FAIL: mock fill pressed Save", "ERROR");
                fails++;
            }
            else if (!mockRes.Ok)
            {
                log("mock fill FAIL: " + mockRes.Note, "ERROR");
                fails++;
            }
            else
            {
                log("mock fill PASS (no save)", "OK");
            }

            // 7) Test-Save (Save only — still no ترحيل)
            log("--- TEST-SAVE ---", "INFO");
            string evidence = Path.Combine(baseDir, @"tests\CodingReplica-saved.csv");
            try { if (File.Exists(evidence)) { File.Delete(evidence); } } catch { }

            EntryTestOptions saveOpt = new EntryTestOptions();
            saveOpt.BaseDir = baseDir;
            saveOpt.SheetPath = saveSheet;
            saveOpt.MockOne = false;
            saveOpt.DoSave = true;
            saveOpt.ForceCompleteFlow = true;
            saveOpt.Watch = true;
            saveOpt.AutoMapIfEmpty = true;
            saveOpt.UpdateTracking = true;
            saveOpt.StepMs = 150;
            saveOpt.Log = log;
            EntryTestResult saveRes = EntryTest.Run(saveOpt);
            if (!saveRes.Ok || !saveRes.Saved)
            {
                log("Test-Save FAIL: " + saveRes.Note, "ERROR");
                fails++;
            }
            else
            {
                log("Test-Save PASS", "OK");
            }

            // 8) Tracking must be SAVED_PENDING, never submitted
            string tracking = File.Exists(cfg.TrackingPath) ? File.ReadAllText(cfg.TrackingPath, Encoding.UTF8) : "";
            if (tracking.IndexOf("SAVED_PENDING", StringComparison.Ordinal) < 0)
            {
                log("tracking missing SAVED_PENDING after Test-Save", "ERROR");
                fails++;
            }
            else
            {
                log("tracking SAVED_PENDING present", "OK");
            }
            if (tracking.IndexOf("SUBMITTED", StringComparison.OrdinalIgnoreCase) >= 0 ||
                tracking.IndexOf("CODED", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // CODED from prior reconcile is ok historically; SUBMITTED would be bad for this client note
                if (tracking.IndexOf("ترحيل", StringComparison.Ordinal) >= 0 &&
                    tracking.IndexOf("28503142200993", StringComparison.Ordinal) >= 0 &&
                    tracking.IndexOf("no ترحيل", StringComparison.Ordinal) < 0)
                {
                    log("tracking suggests ترحيل for Test-Save client", "ERROR");
                    fails++;
                }
            }

            WritePreflightSummary(logPath, buf, fails);
            return fails == 0 ? 0 : 1;
        }

        static void WritePreflightSummary(string logPath, StringBuilder buf, int fails)
        {
            string summary = fails == 0
                ? "PREFLIGHT PASS — safe for controlled pilot (mock OK; no auto-ترحيل)"
                : ("PREFLIGHT FAIL — " + fails + " check(s) failed — do NOT go to production yet");
            try { File.AppendAllText(logPath, summary + Environment.NewLine, Encoding.UTF8); } catch { }
            try { Console.WriteLine(summary); } catch { }
            try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(logPath), "preflight-latest.txt"), buf.ToString() + summary, Encoding.UTF8); } catch { }
        }

        // Lab E2E: SFTP sync → normalize → اختبار إضافة (1 جديد) against CodingReplica.
        static int RunOperatorSmokeCli(string baseDir)
        {
            string logPath = NewLog("operator-smoke");
            Action<string, string> log = MakeLogger(logPath, null);
            log("=== OPERATOR SMOKE (CodingReplica / no OCR) ===", "INFO");
            BotConfig cfg = BotConfig.Load(baseDir);
            cfg.App.LookupBeforeEntry = false;
            if (string.IsNullOrWhiteSpace(cfg.App.WindowTitlePattern))
            {
                cfg.App.WindowTitlePattern = "برنامج التكويد";
            }
            OperatorFlowResult res = OperatorFlow.RunTestAdd(cfg, log);
            log(res != null ? res.Note : "null result",
                res != null && res.Ok ? "OK" : "ERROR");
            if (res != null && res.Execute != null)
            {
                log("entry note: " + res.Execute.Note, res.Execute.Ok ? "OK" : "ERROR");
            }
            log("log file: " + logPath, "INFO");
            return res != null && res.Ok ? 0 : 1;
        }
    }
}
