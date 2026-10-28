// =============================================================================
//  BotConfig.cs - load bot-config.json + settings.json + paths.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RbaCore
{
    public class BotAppConfig
    {
        public string ExePath = "";
        public string Arguments = "";
        public string WorkingDir = "";
        public string WindowTitlePattern = "";
        public string ChildWindow = "";
        public string EntryWindowTitle = "";
        public string EntryWindowClass = "";
        public bool AutoLaunch = false;
        public int LaunchTimeoutSec = 90;
        public string EntryShortcut = "^{F2}";
        // Two-click MDI menu path: parent then leaf (e.g. التكويد → إدخال بيانات العملاء).
        public string[] MenuPath = new string[] { "التكويد", "إدخال بيانات العملاء" };
        // Inquiry/OCR path retired for production — operator classifies manually.
        public bool LookupBeforeEntry = false;
        public string LookupWindowTitle = "الإستعلام للتعديل";
        public string[] LookupMenuPath = new string[] { "التكويد", "الإستعلام للتعديل" };
        // Close «بيانات العملاء» after a successful save so the next client starts clean.
        public bool CloseEntryAfterSave = true;
        // Post-save: «نظام تكويد العملاء» → confirm then period export.
        public string[] ConfirmMenuPath = new string[]
        {
            "نظام تكويد العملاء", "تأكيد بيانات العملاء قبل إرسالها للتكويد"
        };
        public string ConfirmWindowTitle = "تأكيد بيانات العملاء قبل إرسالها للتكويد";
        public string[] PeriodMenuPath = new string[]
        {
            "نظام تكويد العملاء", "عمليات التكويد خلال فترة"
        };
        public string PeriodWindowTitle = "عمليات التكويد خلال فترة";
        // Same parent as add-customer («التكويد») → «سحب الأكواد» when lookup finds the client.
        public string[] PullCodesMenuPath = new string[] { "التكويد", "سحب الأكواد" };
        public string PullCodesWindowTitle = "سحب الأكواد";
    }

    public class BotSettings
    {
        public bool LoopEnabled = false;
        public int LoopIntervalMinutes = 5;
        public int MaxIterations = 0;
        public int MaxRetries = 2;
        public int RetryDelaySeconds = 2;
        public bool StopOnError = false;
        public bool DoValidate = true;
        public bool DoReconcile = false;
        public bool DoWriteBack = true;
        public bool CheckCoded = false;
        public bool CheckRejected = false;
        public int DaysBack = 30;
        // When true: write every action/issue into logs\session_*.log for sharing/debug.
        public bool DetailedActionLog = true;
        // Confirm/export are NOT after each save — they run once a day at DailyExport*.
        public bool PostSaveConfirmExport = false;
        public bool ExportDateRangeYearToDate = false;
        // Production export: once per calendar day at the configured clock time.
        public bool DailyExportEnabled = true;
        public int DailyExportHour = 5;
        public int DailyExportMinute = 0;
        public bool DailyExportPm = true;
        // Fill pace (ms). Defaults match ActionPace so existing installs stay unchanged.
        public int FillStepMs = 700;
        public int FillSettleMs = 550;
        public int BetweenClientsMs = 1100;
        public int AfterSaveMs = 1300;
    }

    public class BotConfig
    {
        public string BaseDir;
        public string ConfigDir;
        public string StateDir;
        public string LogsDir;
        public string SheetPath = "";
        public string ScansRoot = "";
        public string ExportFolder = "";
        public int DocImageMaxWidth = 1100;
        // Local SFTP lab root (inbox\kyc_export.csv + inbox\docs\{NID}\).
        public string SftpLocalRoot = @"C:\RPA-SFTP";
        // Production SFTP defaults (override in UI / bot-config when needed).
        public const string DefaultSftpHost = "10.10.95.250";
        public const string DefaultSftpUser = "RPA";
        public const string DefaultSftpPassword = "P@$$WORD";
        public string SftpHost = DefaultSftpHost;
        public int SftpPort = 22;
        public string SftpUser = DefaultSftpUser;
        // Optional password for protected OpenSSH/sftp hosts (stored in bot-config).
        public string SftpPassword = "";
        public string SftpRemoteInbox = "kyc-imports";
        // When true, never call remote SFTP — use SftpLocalRoot\inbox (or last SftpCache).
        public bool SftpOfflineMode = false;
        public BotAppConfig App = new BotAppConfig();
        public BotSettings Settings = new BotSettings();
        // Coding / entry / المستند sizes captured in Setup — restore before fill to keep maps stable.
        public WindowLayoutConfig Windows = new WindowLayoutConfig();
        public string SelectorsPath;
        public string TrackingPath;
        public string RunsPath;
        public Dictionary<string, string> ColumnMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static BotConfig Load(string baseDir)
        {
            BotConfig cfg = new BotConfig();
            cfg.BaseDir = baseDir;
            cfg.ConfigDir = Path.Combine(baseDir, "config");
            cfg.StateDir = Path.Combine(baseDir, "state");
            cfg.LogsDir = ActionLog.BesideExeLogsDir();
            cfg.SelectorsPath = Path.Combine(cfg.ConfigDir, "selectors.csv");
            cfg.TrackingPath = Path.Combine(cfg.StateDir, "tracking.csv");
            cfg.RunsPath = Path.Combine(cfg.StateDir, "runs.csv");

            string botCfgPath = Path.Combine(cfg.ConfigDir, "bot-config.json");
            if (File.Exists(botCfgPath))
            {
                try { ApplyBotConfigJson(cfg, SettingsVault.ReadUtf8Auto(botCfgPath)); }
                catch { }
            }

            string settingsPath = Path.Combine(cfg.ConfigDir, "settings.json");
            if (File.Exists(settingsPath))
            {
                try { ApplySettingsJson(cfg.Settings, SettingsVault.ReadUtf8Auto(settingsPath)); }
                catch { }
            }

            if (string.IsNullOrEmpty(cfg.ExportFolder))
            {
                cfg.ExportFolder = @"C:\ClientsFilesExcel";
            }
            ActionPace.Apply(cfg.Settings);
            return cfg;
        }

        public void SaveSettings()
        {
            if (!Directory.Exists(ConfigDir)) { Directory.CreateDirectory(ConfigDir); }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("    \"loopEnabled\":  " + (Settings.LoopEnabled ? "true" : "false") + ",");
            sb.AppendLine("    \"loopIntervalMinutes\":  " + Settings.LoopIntervalMinutes + ",");
            sb.AppendLine("    \"maxIterations\":  " + Settings.MaxIterations + ",");
            sb.AppendLine("    \"maxRetries\":  " + Settings.MaxRetries + ",");
            sb.AppendLine("    \"retryDelaySeconds\":  " + Settings.RetryDelaySeconds + ",");
            sb.AppendLine("    \"stopOnError\":  " + (Settings.StopOnError ? "true" : "false") + ",");
            sb.AppendLine("    \"doValidate\":  " + (Settings.DoValidate ? "true" : "false") + ",");
            sb.AppendLine("    \"doReconcile\":  " + (Settings.DoReconcile ? "true" : "false") + ",");
            sb.AppendLine("    \"doWriteBack\":  " + (Settings.DoWriteBack ? "true" : "false") + ",");
            sb.AppendLine("    \"checkCoded\":  " + (Settings.CheckCoded ? "true" : "false") + ",");
            sb.AppendLine("    \"checkRejected\":  " + (Settings.CheckRejected ? "true" : "false") + ",");
            sb.AppendLine("    \"daysBack\":  " + Settings.DaysBack + ",");
            sb.AppendLine("    \"detailedActionLog\":  " + (Settings.DetailedActionLog ? "true" : "false") + ",");
            sb.AppendLine("    \"postSaveConfirmExport\":  " + (Settings.PostSaveConfirmExport ? "true" : "false") + ",");
            sb.AppendLine("    \"exportDateRangeYearToDate\":  " + (Settings.ExportDateRangeYearToDate ? "true" : "false") + ",");
            sb.AppendLine("    \"dailyExportEnabled\":  " + (Settings.DailyExportEnabled ? "true" : "false") + ",");
            sb.AppendLine("    \"dailyExportHour\":  " + Settings.DailyExportHour + ",");
            sb.AppendLine("    \"dailyExportMinute\":  " + Settings.DailyExportMinute + ",");
            sb.AppendLine("    \"dailyExportPm\":  " + (Settings.DailyExportPm ? "true" : "false") + ",");
            sb.AppendLine("    \"fillStepMs\":  " + Settings.FillStepMs + ",");
            sb.AppendLine("    \"fillSettleMs\":  " + Settings.FillSettleMs + ",");
            sb.AppendLine("    \"betweenClientsMs\":  " + Settings.BetweenClientsMs + ",");
            sb.AppendLine("    \"afterSaveMs\":  " + Settings.AfterSaveMs);
            sb.AppendLine("}");
            SettingsVault.WriteUtf8Locked(Path.Combine(ConfigDir, "settings.json"), sb.ToString());
            ActionPace.Apply(Settings);
        }

        public void SaveBotConfig()
        {
            if (!Directory.Exists(ConfigDir)) { Directory.CreateDirectory(ConfigDir); }
            // Minimal JSON writer (no System.Web dependency required at runtime if serializer missing).
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"version\": 2,");
            sb.AppendLine("  \"sheetPath\": " + Q(SheetPath) + ",");
            sb.AppendLine("  \"scansRoot\": " + Q(ScansRoot) + ",");
            sb.AppendLine("  \"exportFolder\": " + Q(ExportFolder) + ",");
            sb.AppendLine("  \"docImageMaxWidth\": " + DocImageMaxWidth + ",");
            sb.AppendLine("  \"sftpLocalRoot\": " + Q(SftpLocalRoot) + ",");
            sb.AppendLine("  \"sftpHost\": " + Q(SftpHost) + ",");
            sb.AppendLine("  \"sftpPort\": " + SftpPort + ",");
            sb.AppendLine("  \"sftpUser\": " + Q(SftpUser) + ",");
            sb.AppendLine("  \"sftpPassword\": " + Q(SftpPassword) + ",");
            sb.AppendLine("  \"sftpRemoteInbox\": " + Q(SftpRemoteInbox) + ",");
            sb.AppendLine("  \"sftpOfflineMode\": " + (SftpOfflineMode ? "true" : "false") + ",");
            sb.AppendLine("  \"app\": {");
            sb.AppendLine("    \"exePath\": " + Q(App.ExePath) + ",");
            sb.AppendLine("    \"arguments\": " + Q(App.Arguments) + ",");
            sb.AppendLine("    \"workingDir\": " + Q(App.WorkingDir) + ",");
            sb.AppendLine("    \"windowTitlePattern\": " + Q(App.WindowTitlePattern) + ",");
            sb.AppendLine("    \"childWindow\": " + Q(App.ChildWindow) + ",");
            sb.AppendLine("    \"entryWindowTitle\": " + Q(App.EntryWindowTitle) + ",");
            sb.AppendLine("    \"entryWindowClass\": " + Q(App.EntryWindowClass) + ",");
            sb.AppendLine("    \"autoLaunch\": " + (App.AutoLaunch ? "true" : "false") + ",");
            sb.AppendLine("    \"launchTimeoutSec\": " + App.LaunchTimeoutSec + ",");
            sb.AppendLine("    \"entryShortcut\": " + Q(App.EntryShortcut) + ",");
            sb.Append("    \"menuPath\": [");
            if (App.MenuPath != null)
            {
                for (int i = 0; i < App.MenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(App.MenuPath[i]));
                }
            }
            sb.AppendLine("],");
            sb.AppendLine("    \"lookupBeforeEntry\": " + (App.LookupBeforeEntry ? "true" : "false") + ",");
            sb.AppendLine("    \"closeEntryAfterSave\": " + (App.CloseEntryAfterSave ? "true" : "false") + ",");
            sb.AppendLine("    \"lookupWindowTitle\": " + Q(App.LookupWindowTitle) + ",");
            sb.Append("    \"lookupMenuPath\": [");
            if (App.LookupMenuPath != null)
            {
                for (int i = 0; i < App.LookupMenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(App.LookupMenuPath[i]));
                }
            }
            sb.AppendLine("],");
            sb.AppendLine("    \"confirmWindowTitle\": " + Q(App.ConfirmWindowTitle) + ",");
            sb.Append("    \"confirmMenuPath\": [");
            if (App.ConfirmMenuPath != null)
            {
                for (int i = 0; i < App.ConfirmMenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(App.ConfirmMenuPath[i]));
                }
            }
            sb.AppendLine("],");
            sb.AppendLine("    \"periodWindowTitle\": " + Q(App.PeriodWindowTitle) + ",");
            sb.Append("    \"periodMenuPath\": [");
            if (App.PeriodMenuPath != null)
            {
                for (int i = 0; i < App.PeriodMenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(App.PeriodMenuPath[i]));
                }
            }
            sb.AppendLine("],");
            sb.AppendLine("    \"pullCodesWindowTitle\": " + Q(App.PullCodesWindowTitle) + ",");
            sb.Append("    \"pullCodesMenuPath\": [");
            if (App.PullCodesMenuPath != null)
            {
                for (int i = 0; i < App.PullCodesMenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(App.PullCodesMenuPath[i]));
                }
            }
            sb.AppendLine("]");
            sb.AppendLine("  },");
            sb.AppendLine("  \"windows\": {");
            sb.AppendLine("    \"mainApp\": " + BoundsJson(Windows != null ? Windows.MainApp : null) + ",");
            sb.AppendLine("    \"entryForm\": " + BoundsJson(Windows != null ? Windows.EntryForm : null) + ",");
            sb.AppendLine("    \"mostanad\": " + BoundsJson(Windows != null ? Windows.Mostanad : null) + ",");
            sb.AppendLine("    \"lookupScreen\": " + BoundsJson(Windows != null ? Windows.LookupScreen : null) + ",");
            sb.AppendLine("    \"confirmScreen\": " + BoundsJson(Windows != null ? Windows.ConfirmScreen : null) + ",");
            sb.AppendLine("    \"periodScreen\": " + BoundsJson(Windows != null ? Windows.PeriodScreen : null) + ",");
            sb.AppendLine("    \"pullCodesScreen\": " + BoundsJson(Windows != null ? Windows.PullCodesScreen : null));
            sb.AppendLine("  }");
            sb.AppendLine("}");
            SettingsVault.WriteUtf8Locked(Path.Combine(ConfigDir, "bot-config.json"), sb.ToString());
        }

        private static string BoundsJson(WindowBounds b)
        {
            if (b == null) { b = new WindowBounds(); }
            return "{ \"x\": " + b.X + ", \"y\": " + b.Y + ", \"w\": " + b.W + ", \"h\": " + b.H + " }";
        }

        private static string Q(string s)
        {
            if (s == null) { s = ""; }
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static void ApplyBotConfigJson(BotConfig cfg, string json)
        {
            cfg.SheetPath = ExtractString(json, "sheetPath", cfg.SheetPath);
            cfg.ScansRoot = ExtractString(json, "scansRoot", cfg.ScansRoot);
            cfg.ExportFolder = ExtractString(json, "exportFolder", cfg.ExportFolder);
            int imgW = ExtractInt(json, "docImageMaxWidth", cfg.DocImageMaxWidth);
            if (imgW >= 200 && imgW <= 4000) { cfg.DocImageMaxWidth = imgW; }
            cfg.SftpLocalRoot = ExtractString(json, "sftpLocalRoot", cfg.SftpLocalRoot);
            cfg.SftpHost = ExtractString(json, "sftpHost", cfg.SftpHost);
            cfg.SftpUser = ExtractString(json, "sftpUser", cfg.SftpUser);
            bool passwordKeyPresent = json.IndexOf("\"sftpPassword\"", StringComparison.OrdinalIgnoreCase) >= 0;
            cfg.SftpPassword = ExtractString(json, "sftpPassword", cfg.SftpPassword);
            cfg.SftpRemoteInbox = ExtractString(json, "sftpRemoteInbox", cfg.SftpRemoteInbox);
            string off = ExtractRaw(json, "sftpOfflineMode");
            if (!string.IsNullOrEmpty(off))
            {
                cfg.SftpOfflineMode = off.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            int sftpPort = ExtractInt(json, "sftpPort", cfg.SftpPort);
            if (sftpPort > 0) { cfg.SftpPort = sftpPort; }
            if (string.IsNullOrWhiteSpace(cfg.SftpHost)) { cfg.SftpHost = DefaultSftpHost; }
            if (string.IsNullOrWhiteSpace(cfg.SftpUser)) { cfg.SftpUser = DefaultSftpUser; }
            if (IsPlaceholderSftpPassword(cfg.SftpPassword))
            {
                cfg.SftpPassword = "";
            }
            cfg.App.ExePath = ExtractString(json, "exePath", cfg.App.ExePath);
            cfg.App.Arguments = ExtractString(json, "arguments", cfg.App.Arguments);
            cfg.App.WorkingDir = ExtractString(json, "workingDir", cfg.App.WorkingDir);
            cfg.App.WindowTitlePattern = ExtractString(json, "windowTitlePattern", cfg.App.WindowTitlePattern);
            cfg.App.ChildWindow = ExtractString(json, "childWindow", cfg.App.ChildWindow);
            cfg.App.EntryWindowTitle = ExtractString(json, "entryWindowTitle", cfg.App.EntryWindowTitle);
            cfg.App.EntryWindowClass = ExtractString(json, "entryWindowClass", cfg.App.EntryWindowClass);
            cfg.App.EntryShortcut = ExtractString(json, "entryShortcut", cfg.App.EntryShortcut);
            string[] mp = ExtractStringArray(json, "menuPath");
            if (mp != null && mp.Length >= 2) { cfg.App.MenuPath = mp; }
            cfg.App.LookupWindowTitle = ExtractString(json, "lookupWindowTitle", cfg.App.LookupWindowTitle);
            string[] lp = ExtractStringArray(json, "lookupMenuPath");
            if (lp != null && lp.Length >= 2) { cfg.App.LookupMenuPath = lp; }
            string lb = ExtractRaw(json, "lookupBeforeEntry");
            if (!string.IsNullOrEmpty(lb)) { cfg.App.LookupBeforeEntry = lb.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0; }
            string ce = ExtractRaw(json, "closeEntryAfterSave");
            if (!string.IsNullOrEmpty(ce)) { cfg.App.CloseEntryAfterSave = ce.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0; }
            cfg.App.ConfirmWindowTitle = ExtractString(json, "confirmWindowTitle", cfg.App.ConfirmWindowTitle);
            string[] cp = ExtractStringArray(json, "confirmMenuPath");
            if (cp != null && cp.Length >= 2) { cfg.App.ConfirmMenuPath = cp; }
            cfg.App.PeriodWindowTitle = ExtractString(json, "periodWindowTitle", cfg.App.PeriodWindowTitle);
            string[] pp = ExtractStringArray(json, "periodMenuPath");
            if (pp != null && pp.Length >= 2) { cfg.App.PeriodMenuPath = pp; }
            cfg.App.PullCodesWindowTitle = ExtractString(json, "pullCodesWindowTitle", cfg.App.PullCodesWindowTitle);
            string[] pk = ExtractStringArray(json, "pullCodesMenuPath");
            if (pk != null && pk.Length >= 2) { cfg.App.PullCodesMenuPath = pk; }
            string al = ExtractRaw(json, "autoLaunch");
            if (!string.IsNullOrEmpty(al)) { cfg.App.AutoLaunch = al.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0; }
            string to = ExtractRaw(json, "launchTimeoutSec");
            int n;
            if (int.TryParse(to, out n)) { cfg.App.LaunchTimeoutSec = n; }

            if (cfg.Windows == null) { cfg.Windows = new WindowLayoutConfig(); }
            cfg.Windows.MainApp = ExtractBounds(json, "mainApp");
            cfg.Windows.EntryForm = ExtractBounds(json, "entryForm");
            cfg.Windows.Mostanad = ExtractBounds(json, "mostanad");
            cfg.Windows.LookupScreen = ExtractBounds(json, "lookupScreen");
            cfg.Windows.ConfirmScreen = ExtractBounds(json, "confirmScreen");
            cfg.Windows.PeriodScreen = ExtractBounds(json, "periodScreen");
            cfg.Windows.PullCodesScreen = ExtractBounds(json, "pullCodesScreen");
        }

        private static WindowBounds ExtractBounds(string json, string name)
        {
            WindowBounds b = new WindowBounds();
            Match m = Regex.Match(
                json,
                "\"" + name + "\"\\s*:\\s*\\{(?<body>[^}]*)\\}",
                RegexOptions.IgnoreCase);
            if (!m.Success) { return b; }
            string body = m.Groups["body"].Value;
            b.X = ExtractInt(body, "x", 0);
            b.Y = ExtractInt(body, "y", 0);
            b.W = ExtractInt(body, "w", 0);
            b.H = ExtractInt(body, "h", 0);
            return b;
        }

        private static string[] ExtractStringArray(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[(?<v>[^\\]]*)\\]", RegexOptions.IgnoreCase);
            if (!m.Success) { return null; }
            List<string> items = new List<string>();
            foreach (Match sm in Regex.Matches(m.Groups["v"].Value, "\"(?<s>(\\\\.|[^\"\\\\])*)\""))
            {
                items.Add(Regex.Unescape(sm.Groups["s"].Value));
            }
            return items.Count > 0 ? items.ToArray() : null;
        }

        private static void ApplySettingsJson(BotSettings s, string json)
        {
            s.LoopEnabled = ExtractBool(json, "loopEnabled", s.LoopEnabled);
            s.DoValidate = ExtractBool(json, "doValidate", s.DoValidate);
            s.DoReconcile = ExtractBool(json, "doReconcile", s.DoReconcile);
            s.DoWriteBack = ExtractBool(json, "doWriteBack", s.DoWriteBack);
            s.StopOnError = ExtractBool(json, "stopOnError", s.StopOnError);
            s.CheckCoded = ExtractBool(json, "checkCoded", s.CheckCoded);
            s.CheckRejected = ExtractBool(json, "checkRejected", s.CheckRejected);
            s.LoopIntervalMinutes = ExtractInt(json, "loopIntervalMinutes", s.LoopIntervalMinutes);
            s.MaxIterations = ExtractInt(json, "maxIterations", s.MaxIterations);
            s.MaxRetries = ExtractInt(json, "maxRetries", s.MaxRetries);
            s.RetryDelaySeconds = ExtractInt(json, "retryDelaySeconds", s.RetryDelaySeconds);
            s.DaysBack = ExtractInt(json, "daysBack", s.DaysBack);
            s.DetailedActionLog = ExtractBool(json, "detailedActionLog", s.DetailedActionLog);
            s.PostSaveConfirmExport = ExtractBool(json, "postSaveConfirmExport", s.PostSaveConfirmExport);
            s.ExportDateRangeYearToDate = ExtractBool(json, "exportDateRangeYearToDate", s.ExportDateRangeYearToDate);
            s.DailyExportEnabled = ExtractBool(json, "dailyExportEnabled", s.DailyExportEnabled);
            s.DailyExportHour = ExtractInt(json, "dailyExportHour", s.DailyExportHour);
            s.DailyExportMinute = ExtractInt(json, "dailyExportMinute", s.DailyExportMinute);
            s.DailyExportPm = ExtractBool(json, "dailyExportPm", s.DailyExportPm);
            if (s.DailyExportHour < 1 || s.DailyExportHour > 12) { s.DailyExportHour = 5; }
            if (s.DailyExportMinute < 0 || s.DailyExportMinute > 59) { s.DailyExportMinute = 0; }
            s.FillStepMs = ExtractInt(json, "fillStepMs", s.FillStepMs);
            s.FillSettleMs = ExtractInt(json, "fillSettleMs", s.FillSettleMs);
            s.BetweenClientsMs = ExtractInt(json, "betweenClientsMs", s.BetweenClientsMs);
            s.AfterSaveMs = ExtractInt(json, "afterSaveMs", s.AfterSaveMs);
            ActionPace.Normalize(s);
        }

        private static string ExtractString(string json, string key, string def)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"(?<v>(\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase);
            if (!m.Success) { return def; }
            return Regex.Unescape(m.Groups["v"].Value);
        }

        public static bool IsPlaceholderSftpPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password)) { return true; }
            string t = password.Trim();
            return string.Equals(t, DefaultSftpPassword, StringComparison.Ordinal) ||
                   string.Equals(t, "P@$$WORD", StringComparison.Ordinal) ||
                   string.Equals(t, "password", StringComparison.OrdinalIgnoreCase);
        }

        private static string ExtractRaw(string json, string key)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(?<v>[^,}\\]]+)", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups["v"].Value.Trim() : "";
        }

        private static bool ExtractBool(string json, string key, bool def)
        {
            string v = ExtractRaw(json, key);
            if (string.IsNullOrEmpty(v)) { return def; }
            return v.IndexOf("true", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ExtractInt(string json, string key, int def)
        {
            string v = ExtractRaw(json, key);
            int n;
            if (int.TryParse(v, out n)) { return n; }
            return def;
        }
    }
}
