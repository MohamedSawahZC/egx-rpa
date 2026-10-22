// =============================================================================
//  MappingBundle.cs - import/export FULL settings backup:
//  paths, app names/titles/shortcuts/menu, window+size, settings, selectors.
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace RbaCore
{
    public class MappingBundleResult
    {
        public bool Ok;
        public string Note;
        public int BundleVersion;
        public string ExportedAt;
        public string ExportedOn;
        public string ExportedBy;
        public string SourcePath;
        public string SheetPath;
        public string ScansRoot;
        public string ExportFolder;
        public string StampImagePath;
        public string SignatureImagePath;
        public string Signature2ImagePath;
        public int? DocImageMaxWidth;
        public string SftpLocalRoot;
        public string SftpHost;
        public int? SftpPort;
        public string SftpUser;
        public string SftpPassword;
        public string SftpRemoteInbox;
        public bool? SftpOfflineMode;
        public string ExePath;
        public string Arguments;
        public string WorkingDir;
        public string ChildWindow;
        public string WindowTitle;
        public string EntryWindowTitle;
        public string EntryWindowClass;
        public string EntryShortcut;
        public string EntryScreen;
        public string[] MenuPath;
        public string LookupWindowTitle;
        public string[] LookupMenuPath;
        public bool? LookupBeforeEntry;
        public bool? CloseEntryAfterSave;
        public string ConfirmWindowTitle;
        public string[] ConfirmMenuPath;
        public string PeriodWindowTitle;
        public string[] PeriodMenuPath;
        public string PullCodesWindowTitle;
        public string[] PullCodesMenuPath;
        public int? LaunchTimeoutSec;
        public bool? AutoLaunch;
        public WindowLayoutConfig Windows;
        public BotSettings Settings;
        public List<FieldSelector> Selectors = new List<FieldSelector>();
    }

    public static class MappingBundle
    {
        // v2 = full settings + window position/size + menu + entry titles.
        public const int SupportedVersion = 2;

        public static MappingBundleResult ImportFile(string path)
        {
            MappingBundleResult r = new MappingBundleResult();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                r.Note = "file not found";
                return r;
            }
            string raw = File.ReadAllText(path, Encoding.UTF8);
            if (!string.IsNullOrEmpty(raw) && raw[0] == '\ufeff') { raw = raw.Substring(1); }
            MappingBundleResult parsed = ImportJson(raw);
            if (parsed != null) { parsed.SourcePath = path; }
            return parsed;
        }

        public static MappingBundleResult ImportJson(string json)
        {
            MappingBundleResult r = new MappingBundleResult();
            if (string.IsNullOrWhiteSpace(json))
            {
                r.Note = "empty json";
                return r;
            }
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;
                object root = ser.DeserializeObject(json);
                if (root is object[])
                {
                    r.Selectors = ParseSelectorArray((object[])root);
                    r.Ok = r.Selectors.Count > 0;
                    r.Note = r.Ok ? ("imported " + r.Selectors.Count + " selectors (array)") : "no selectors in array";
                    return r;
                }

                Dictionary<string, object> bag = AsDict(root);
                if (bag == null)
                {
                    r.Note = "unsupported json root";
                    return r;
                }

                r.BundleVersion = ToInt(Get(bag, "bundleVersion"), 1);
                r.ExportedAt = ToStr(Get(bag, "exportedAt"));
                r.ExportedOn = ToStr(Get(bag, "exportedOn"));
                r.ExportedBy = ToStr(Get(bag, "exportedBy"));

                Dictionary<string, object> paths = AsDict(Get(bag, "paths"));
                if (paths != null)
                {
                    r.SheetPath = ToStr(Get(paths, "sheetPath"));
                    r.ScansRoot = ToStr(Get(paths, "scansRoot"));
                    r.ExportFolder = ToStr(Get(paths, "exportFolder"));
                    r.StampImagePath = ToStr(Get(paths, "stampImagePath"));
                    r.SignatureImagePath = ToStr(Get(paths, "signatureImagePath"));
                    r.Signature2ImagePath = ToStr(Get(paths, "signature2ImagePath"));
                    if (paths.ContainsKey("docImageMaxWidth") && paths["docImageMaxWidth"] != null)
                    {
                        r.DocImageMaxWidth = ToInt(paths["docImageMaxWidth"], 1100);
                    }
                    r.SftpLocalRoot = ToStr(Get(paths, "sftpLocalRoot"));
                    r.SftpHost = ToStr(Get(paths, "sftpHost"));
                    if (paths.ContainsKey("sftpPort") && paths["sftpPort"] != null)
                    {
                        r.SftpPort = ToInt(paths["sftpPort"], 22);
                    }
                    r.SftpUser = ToStr(Get(paths, "sftpUser"));
                    r.SftpPassword = ToStr(Get(paths, "sftpPassword"));
                    r.SftpRemoteInbox = ToStr(Get(paths, "sftpRemoteInbox"));
                    if (paths.ContainsKey("sftpOfflineMode") && paths["sftpOfflineMode"] != null)
                    {
                        r.SftpOfflineMode = ToBool(paths["sftpOfflineMode"]);
                    }
                }

                Dictionary<string, object> app = AsDict(Get(bag, "app"));
                if (app != null)
                {
                    r.ExePath = ToStr(Get(app, "exePath"));
                    r.Arguments = ToStr(Get(app, "arguments"));
                    r.WorkingDir = ToStr(Get(app, "workingDir"));
                    r.ChildWindow = ToStr(Get(app, "childWindow"));
                    r.EntryWindowTitle = ToStr(Get(app, "entryWindowTitle"));
                    r.EntryWindowClass = ToStr(Get(app, "entryWindowClass"));
                    r.EntryShortcut = ToStr(Get(app, "entryShortcut"));
                    string wtp = ToStr(Get(app, "windowTitlePattern"));
                    if (!string.IsNullOrEmpty(wtp)) { r.WindowTitle = wtp; }
                    if (app.ContainsKey("autoLaunch") && app["autoLaunch"] != null)
                    {
                        r.AutoLaunch = ToBool(app["autoLaunch"]);
                    }
                    if (app.ContainsKey("launchTimeoutSec") && app["launchTimeoutSec"] != null)
                    {
                        r.LaunchTimeoutSec = ToInt(app["launchTimeoutSec"], 90);
                    }
                    string[] mp = ToStringArray(Get(app, "menuPath"));
                    if (mp != null && mp.Length > 0) { r.MenuPath = mp; }
                    r.LookupWindowTitle = ToStr(Get(app, "lookupWindowTitle"));
                    string[] lp = ToStringArray(Get(app, "lookupMenuPath"));
                    if (lp != null && lp.Length > 0) { r.LookupMenuPath = lp; }
                    if (app.ContainsKey("lookupBeforeEntry") && app["lookupBeforeEntry"] != null)
                    {
                        r.LookupBeforeEntry = ToBool(app["lookupBeforeEntry"]);
                    }
                    if (app.ContainsKey("closeEntryAfterSave") && app["closeEntryAfterSave"] != null)
                    {
                        r.CloseEntryAfterSave = ToBool(app["closeEntryAfterSave"]);
                    }
                    r.ConfirmWindowTitle = ToStr(Get(app, "confirmWindowTitle"));
                    string[] cp = ToStringArray(Get(app, "confirmMenuPath"));
                    if (cp != null && cp.Length > 0) { r.ConfirmMenuPath = cp; }
                    r.PeriodWindowTitle = ToStr(Get(app, "periodWindowTitle"));
                    string[] pp = ToStringArray(Get(app, "periodMenuPath"));
                    if (pp != null && pp.Length > 0) { r.PeriodMenuPath = pp; }
                    r.PullCodesWindowTitle = ToStr(Get(app, "pullCodesWindowTitle"));
                    string[] pk = ToStringArray(Get(app, "pullCodesMenuPath"));
                    if (pk != null && pk.Length > 0) { r.PullCodesMenuPath = pk; }
                }

                Dictionary<string, object> against = AsDict(Get(bag, "confirmedAgainst"));
                if (against != null)
                {
                    if (string.IsNullOrEmpty(r.WindowTitle))
                    {
                        r.WindowTitle = ToStr(Get(against, "windowTitle"));
                    }
                    r.EntryScreen = ToStr(Get(against, "screen"));
                    if (string.IsNullOrEmpty(r.EntryWindowTitle) && !string.IsNullOrEmpty(r.EntryScreen))
                    {
                        r.EntryWindowTitle = r.EntryScreen;
                    }
                }

                r.Windows = ParseWindows(AsDict(Get(bag, "windows")));
                r.Settings = ParseSettings(AsDict(Get(bag, "settings")));

                object selObj = Get(bag, "selectors");
                if (selObj is object[])
                {
                    r.Selectors = ParseSelectorArray((object[])selObj);
                }
                else if (selObj is ArrayList)
                {
                    ArrayList al = (ArrayList)selObj;
                    object[] arr = new object[al.Count];
                    al.CopyTo(arr);
                    r.Selectors = ParseSelectorArray(arr);
                }

                bool hasSettings =
                    !string.IsNullOrEmpty(r.SheetPath) ||
                    !string.IsNullOrEmpty(r.ScansRoot) ||
                    !string.IsNullOrEmpty(r.ExePath) ||
                    (r.Windows != null && (
                        (r.Windows.MainApp != null && r.Windows.MainApp.IsValid) ||
                        (r.Windows.EntryForm != null && r.Windows.EntryForm.IsValid))) ||
                    r.Selectors.Count > 0;

                if (!hasSettings)
                {
                    r.Note = "الملف فارغ — لا إعدادات ولا ربط حقول";
                    return r;
                }

                r.Ok = true;
                r.Note = "imported settings"
                    + (r.Selectors.Count > 0 ? (" + " + r.Selectors.Count + " fields") : "")
                    + (string.IsNullOrEmpty(r.ExportedAt) ? "" : (" — exported " + r.ExportedAt))
                    + (string.IsNullOrEmpty(r.ExportedOn) ? "" : (" @" + r.ExportedOn));
                if (r.BundleVersion > SupportedVersion)
                {
                    r.Note += " (bundle v" + r.BundleVersion + " newer — partial read)";
                }
                return r;
            }
            catch (Exception ex)
            {
                r.Note = "invalid json: " + ex.Message;
                return r;
            }
        }

        // Apply every imported setting onto live BotConfig (paths, names, windows, settings).
        public static void ApplyToConfig(BotConfig cfg, MappingBundleResult b)
        {
            if (cfg == null || b == null) { return; }
            if (!string.IsNullOrEmpty(b.SheetPath)) { cfg.SheetPath = b.SheetPath; }
            if (!string.IsNullOrEmpty(b.ScansRoot)) { cfg.ScansRoot = b.ScansRoot; }
            if (!string.IsNullOrEmpty(b.ExportFolder)) { cfg.ExportFolder = b.ExportFolder; }
            if (!string.IsNullOrEmpty(b.StampImagePath)) { cfg.StampImagePath = b.StampImagePath; }
            if (!string.IsNullOrEmpty(b.SignatureImagePath)) { cfg.SignatureImagePath = b.SignatureImagePath; }
            if (!string.IsNullOrEmpty(b.Signature2ImagePath)) { cfg.Signature2ImagePath = b.Signature2ImagePath; }
            if (b.DocImageMaxWidth.HasValue && b.DocImageMaxWidth.Value >= 200)
            {
                cfg.DocImageMaxWidth = b.DocImageMaxWidth.Value;
            }
            if (!string.IsNullOrEmpty(b.SftpLocalRoot)) { cfg.SftpLocalRoot = b.SftpLocalRoot; }
            if (!string.IsNullOrEmpty(b.SftpHost)) { cfg.SftpHost = b.SftpHost; }
            if (b.SftpPort.HasValue && b.SftpPort.Value > 0) { cfg.SftpPort = b.SftpPort.Value; }
            if (!string.IsNullOrEmpty(b.SftpUser)) { cfg.SftpUser = b.SftpUser; }
            if (b.SftpPassword != null && b.SftpPassword.Length > 0) { cfg.SftpPassword = b.SftpPassword; }
            if (!string.IsNullOrEmpty(b.SftpRemoteInbox)) { cfg.SftpRemoteInbox = b.SftpRemoteInbox; }
            if (b.SftpOfflineMode.HasValue) { cfg.SftpOfflineMode = b.SftpOfflineMode.Value; }
            if (!string.IsNullOrEmpty(b.ExePath)) { cfg.App.ExePath = b.ExePath; }
            if (!string.IsNullOrEmpty(b.Arguments)) { cfg.App.Arguments = b.Arguments; }
            if (!string.IsNullOrEmpty(b.WorkingDir)) { cfg.App.WorkingDir = b.WorkingDir; }
            if (!string.IsNullOrEmpty(b.ChildWindow)) { cfg.App.ChildWindow = b.ChildWindow; }
            if (!string.IsNullOrEmpty(b.WindowTitle)) { cfg.App.WindowTitlePattern = b.WindowTitle; }
            if (!string.IsNullOrEmpty(b.EntryWindowTitle)) { cfg.App.EntryWindowTitle = b.EntryWindowTitle; }
            else if (!string.IsNullOrEmpty(b.EntryScreen)) { cfg.App.EntryWindowTitle = b.EntryScreen; }
            if (!string.IsNullOrEmpty(b.EntryWindowClass)) { cfg.App.EntryWindowClass = b.EntryWindowClass; }
            if (!string.IsNullOrEmpty(b.EntryShortcut)) { cfg.App.EntryShortcut = b.EntryShortcut; }
            if (b.MenuPath != null && b.MenuPath.Length >= 1) { cfg.App.MenuPath = b.MenuPath; }
            if (!string.IsNullOrEmpty(b.LookupWindowTitle)) { cfg.App.LookupWindowTitle = b.LookupWindowTitle; }
            if (b.LookupMenuPath != null && b.LookupMenuPath.Length >= 2) { cfg.App.LookupMenuPath = b.LookupMenuPath; }
            if (b.LookupBeforeEntry.HasValue) { cfg.App.LookupBeforeEntry = b.LookupBeforeEntry.Value; }
            if (b.CloseEntryAfterSave.HasValue) { cfg.App.CloseEntryAfterSave = b.CloseEntryAfterSave.Value; }
            if (!string.IsNullOrEmpty(b.ConfirmWindowTitle)) { cfg.App.ConfirmWindowTitle = b.ConfirmWindowTitle; }
            if (b.ConfirmMenuPath != null && b.ConfirmMenuPath.Length >= 2) { cfg.App.ConfirmMenuPath = b.ConfirmMenuPath; }
            if (!string.IsNullOrEmpty(b.PeriodWindowTitle)) { cfg.App.PeriodWindowTitle = b.PeriodWindowTitle; }
            if (b.PeriodMenuPath != null && b.PeriodMenuPath.Length >= 2) { cfg.App.PeriodMenuPath = b.PeriodMenuPath; }
            if (!string.IsNullOrEmpty(b.PullCodesWindowTitle)) { cfg.App.PullCodesWindowTitle = b.PullCodesWindowTitle; }
            if (b.PullCodesMenuPath != null && b.PullCodesMenuPath.Length >= 2) { cfg.App.PullCodesMenuPath = b.PullCodesMenuPath; }
            if (b.AutoLaunch.HasValue) { cfg.App.AutoLaunch = b.AutoLaunch.Value; }
            if (b.LaunchTimeoutSec.HasValue && b.LaunchTimeoutSec.Value > 0)
            {
                cfg.App.LaunchTimeoutSec = b.LaunchTimeoutSec.Value;
            }
            if (b.Windows != null)
            {
                cfg.Windows = b.Windows;
            }
            if (b.Settings != null)
            {
                cfg.Settings = b.Settings;
            }
        }

        public static void ExportFile(
            string path,
            BotConfig cfg,
            List<FieldSelector> selectors,
            string confirmedTitle,
            string confirmedScreen)
        {
            File.WriteAllText(path, ExportToString(cfg, selectors, confirmedTitle, confirmedScreen), new UTF8Encoding(false));
        }

        public static string ExportToString(
            BotConfig cfg,
            List<FieldSelector> selectors,
            string confirmedTitle,
            string confirmedScreen)
        {
            if (selectors == null) { selectors = new List<FieldSelector>(); }
            if (cfg == null) { cfg = new BotConfig(); }
            if (cfg.App == null) { cfg.App = new BotAppConfig(); }
            if (cfg.Windows == null) { cfg.Windows = new WindowLayoutConfig(); }
            if (cfg.Settings == null) { cfg.Settings = new BotSettings(); }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"bundleVersion\": " + SupportedVersion + ",");
            sb.AppendLine("  \"exportedAt\": " + Q(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + ",");
            sb.AppendLine("  \"exportedOn\": " + Q(Environment.MachineName) + ",");
            sb.AppendLine("  \"exportedBy\": " + Q(Environment.UserName) + ",");

            // --- All paths ---
            sb.AppendLine("  \"paths\": {");
            sb.AppendLine("    \"sheetPath\": " + Q(cfg.SheetPath) + ",");
            sb.AppendLine("    \"scansRoot\": " + Q(cfg.ScansRoot) + ",");
            sb.AppendLine("    \"exportFolder\": " + Q(cfg.ExportFolder) + ",");
            sb.AppendLine("    \"stampImagePath\": " + Q(cfg.StampImagePath) + ",");
            sb.AppendLine("    \"signatureImagePath\": " + Q(cfg.SignatureImagePath) + ",");
            sb.AppendLine("    \"signature2ImagePath\": " + Q(cfg.Signature2ImagePath) + ",");
            sb.AppendLine("    \"docImageMaxWidth\": " + cfg.DocImageMaxWidth + ",");
            sb.AppendLine("    \"sftpLocalRoot\": " + Q(cfg.SftpLocalRoot) + ",");
            sb.AppendLine("    \"sftpHost\": " + Q(cfg.SftpHost) + ",");
            sb.AppendLine("    \"sftpPort\": " + cfg.SftpPort + ",");
            sb.AppendLine("    \"sftpUser\": " + Q(cfg.SftpUser) + ",");
            sb.AppendLine("    \"sftpPassword\": " + Q(cfg.SftpPassword) + ",");
            sb.AppendLine("    \"sftpRemoteInbox\": " + Q(cfg.SftpRemoteInbox) + ",");
            sb.AppendLine("    \"sftpOfflineMode\": " + (cfg.SftpOfflineMode ? "true" : "false"));
            sb.AppendLine("  },");

            // --- App names / titles / shortcuts / menu ---
            sb.AppendLine("  \"app\": {");
            sb.AppendLine("    \"exePath\": " + Q(cfg.App.ExePath) + ",");
            sb.AppendLine("    \"arguments\": " + Q(cfg.App.Arguments) + ",");
            sb.AppendLine("    \"workingDir\": " + Q(cfg.App.WorkingDir) + ",");
            sb.AppendLine("    \"childWindow\": " + Q(cfg.App.ChildWindow) + ",");
            sb.AppendLine("    \"windowTitlePattern\": " + Q(cfg.App.WindowTitlePattern) + ",");
            sb.AppendLine("    \"entryWindowTitle\": " + Q(cfg.App.EntryWindowTitle) + ",");
            sb.AppendLine("    \"entryWindowClass\": " + Q(cfg.App.EntryWindowClass) + ",");
            sb.AppendLine("    \"entryShortcut\": " + Q(cfg.App.EntryShortcut) + ",");
            sb.AppendLine("    \"autoLaunch\": " + (cfg.App.AutoLaunch ? "true" : "false") + ",");
            sb.AppendLine("    \"launchTimeoutSec\": " + cfg.App.LaunchTimeoutSec + ",");
            sb.Append("    \"menuPath\": [");
            if (cfg.App.MenuPath != null)
            {
                for (int i = 0; i < cfg.App.MenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(cfg.App.MenuPath[i]));
                }
            }
            sb.AppendLine("],");
            sb.AppendLine("    \"lookupBeforeEntry\": " + (cfg.App.LookupBeforeEntry ? "true" : "false") + ",");
            sb.AppendLine("    \"closeEntryAfterSave\": " + (cfg.App.CloseEntryAfterSave ? "true" : "false") + ",");
            sb.AppendLine("    \"lookupWindowTitle\": " + Q(cfg.App.LookupWindowTitle) + ",");
            sb.Append("    \"lookupMenuPath\": [");
            if (cfg.App.LookupMenuPath != null)
            {
                for (int i = 0; i < cfg.App.LookupMenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(cfg.App.LookupMenuPath[i]));
                }
            }
            sb.AppendLine("],");
            sb.AppendLine("    \"confirmWindowTitle\": " + Q(cfg.App.ConfirmWindowTitle) + ",");
            sb.Append("    \"confirmMenuPath\": [");
            if (cfg.App.ConfirmMenuPath != null)
            {
                for (int i = 0; i < cfg.App.ConfirmMenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(cfg.App.ConfirmMenuPath[i]));
                }
            }
            sb.AppendLine("],");
            sb.AppendLine("    \"periodWindowTitle\": " + Q(cfg.App.PeriodWindowTitle) + ",");
            sb.Append("    \"periodMenuPath\": [");
            if (cfg.App.PeriodMenuPath != null)
            {
                for (int i = 0; i < cfg.App.PeriodMenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(cfg.App.PeriodMenuPath[i]));
                }
            }
            sb.AppendLine("],");
            sb.AppendLine("    \"pullCodesWindowTitle\": " + Q(cfg.App.PullCodesWindowTitle) + ",");
            sb.Append("    \"pullCodesMenuPath\": [");
            if (cfg.App.PullCodesMenuPath != null)
            {
                for (int i = 0; i < cfg.App.PullCodesMenuPath.Length; i++)
                {
                    if (i > 0) { sb.Append(", "); }
                    sb.Append(Q(cfg.App.PullCodesMenuPath[i]));
                }
            }
            sb.AppendLine("]");
            sb.AppendLine("  },");

            // --- Window position + size (critical for stable field maps) ---
            sb.AppendLine("  \"windows\": {");
            sb.AppendLine("    \"mainApp\": " + BoundsJson(cfg.Windows.MainApp) + ",");
            sb.AppendLine("    \"entryForm\": " + BoundsJson(cfg.Windows.EntryForm) + ",");
            sb.AppendLine("    \"mostanad\": " + BoundsJson(cfg.Windows.Mostanad) + ",");
            sb.AppendLine("    \"lookupScreen\": " + BoundsJson(cfg.Windows.LookupScreen) + ",");
            sb.AppendLine("    \"confirmScreen\": " + BoundsJson(cfg.Windows.ConfirmScreen) + ",");
            sb.AppendLine("    \"periodScreen\": " + BoundsJson(cfg.Windows.PeriodScreen) + ",");
            sb.AppendLine("    \"pullCodesScreen\": " + BoundsJson(cfg.Windows.PullCodesScreen));
            sb.AppendLine("  },");

            // --- Runtime settings ---
            BotSettings st = cfg.Settings;
            sb.AppendLine("  \"settings\": {");
            sb.AppendLine("    \"loopEnabled\": " + (st.LoopEnabled ? "true" : "false") + ",");
            sb.AppendLine("    \"loopIntervalMinutes\": " + st.LoopIntervalMinutes + ",");
            sb.AppendLine("    \"maxIterations\": " + st.MaxIterations + ",");
            sb.AppendLine("    \"maxRetries\": " + st.MaxRetries + ",");
            sb.AppendLine("    \"retryDelaySeconds\": " + st.RetryDelaySeconds + ",");
            sb.AppendLine("    \"stopOnError\": " + (st.StopOnError ? "true" : "false") + ",");
            sb.AppendLine("    \"doValidate\": " + (st.DoValidate ? "true" : "false") + ",");
            sb.AppendLine("    \"doReconcile\": " + (st.DoReconcile ? "true" : "false") + ",");
            sb.AppendLine("    \"doWriteBack\": " + (st.DoWriteBack ? "true" : "false") + ",");
            sb.AppendLine("    \"checkCoded\": " + (st.CheckCoded ? "true" : "false") + ",");
            sb.AppendLine("    \"checkRejected\": " + (st.CheckRejected ? "true" : "false") + ",");
            sb.AppendLine("    \"daysBack\": " + st.DaysBack + ",");
            sb.AppendLine("    \"detailedActionLog\": " + (st.DetailedActionLog ? "true" : "false") + ",");
            sb.AppendLine("    \"postSaveConfirmExport\": " + (st.PostSaveConfirmExport ? "true" : "false") + ",");
            sb.AppendLine("    \"exportDateRangeYearToDate\": " + (st.ExportDateRangeYearToDate ? "true" : "false") + ",");
            sb.AppendLine("    \"dailyExportEnabled\": " + (st.DailyExportEnabled ? "true" : "false") + ",");
            sb.AppendLine("    \"dailyExportHour\": " + st.DailyExportHour + ",");
            sb.AppendLine("    \"dailyExportMinute\": " + st.DailyExportMinute + ",");
            sb.AppendLine("    \"dailyExportPm\": " + (st.DailyExportPm ? "true" : "false") + ",");
            sb.AppendLine("    \"fillStepMs\": " + st.FillStepMs + ",");
            sb.AppendLine("    \"fillSettleMs\": " + st.FillSettleMs + ",");
            sb.AppendLine("    \"betweenClientsMs\": " + st.BetweenClientsMs + ",");
            sb.AppendLine("    \"afterSaveMs\": " + st.AfterSaveMs);
            sb.AppendLine("  },");

            sb.AppendLine("  \"confirmedAgainst\": {");
            sb.AppendLine("    \"windowTitle\": " + Q(confirmedTitle) + ",");
            sb.AppendLine("    \"screen\": " + Q(confirmedScreen) + ",");
            sb.AppendLine("    \"fieldCount\": " + selectors.Count);
            sb.AppendLine("  },");

            sb.AppendLine("  \"selectors\": [");
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                sb.Append("    {");
                sb.Append("\"FieldKey\":").Append(Q(s.FieldKey)).Append(",");
                sb.Append("\"FieldLabel\":").Append(Q(s.FieldLabel)).Append(",");
                sb.Append("\"Kind\":").Append(Q(s.Kind)).Append(",");
                sb.Append("\"SheetColumn\":").Append(Q(s.SheetColumn)).Append(",");
                sb.Append("\"Screen\":").Append(Q(s.Screen)).Append(",");
                sb.Append("\"Shortcut\":").Append(Q(s.Shortcut)).Append(",");
                sb.Append("\"Label\":").Append(Q(s.Label)).Append(",");
                sb.Append("\"LabelOrdinal\":").Append(Q(s.LabelOrdinal)).Append(",");
                sb.Append("\"AccName\":").Append(Q(s.AccName)).Append(",");
                sb.Append("\"ControlId\":").Append(Q(s.ControlId)).Append(",");
                sb.Append("\"Caption\":").Append(Q(s.Caption)).Append(",");
                sb.Append("\"Class\":").Append(Q(s.Class)).Append(",");
                sb.Append("\"Section\":").Append(Q(s.Section)).Append(",");
                sb.Append("\"ClassOrdinal\":").Append(Q(s.ClassOrdinal)).Append(",");
                sb.Append("\"Index\":").Append(Q(s.Index)).Append(",");
                sb.Append("\"DatePart\":").Append(Q(s.DatePart)).Append(",");
                sb.Append("\"SourceKey\":").Append(Q(s.SourceKey)).Append(",");
                sb.Append("\"Required\":").Append(Q(s.Required)).Append(",");
                sb.Append("\"ConfirmedAt\":").Append(Q(s.ConfirmedAt)).Append(",");
                sb.Append("\"Aliases\":").Append(Q(s.Aliases)).Append(",");
                sb.Append("\"PosX\":").Append(Q(s.PosX)).Append(",");
                sb.Append("\"PosY\":").Append(Q(s.PosY)).Append(",");
                sb.Append("\"PosW\":").Append(Q(s.PosW)).Append(",");
                sb.Append("\"PosH\":").Append(Q(s.PosH));
                sb.Append("}");
                if (i < selectors.Count - 1) { sb.Append(","); }
                sb.AppendLine();
            }
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private static string BoundsJson(WindowBounds b)
        {
            if (b == null) { b = new WindowBounds(); }
            return "{ \"x\": " + b.X + ", \"y\": " + b.Y + ", \"w\": " + b.W + ", \"h\": " + b.H + " }";
        }

        private static WindowLayoutConfig ParseWindows(Dictionary<string, object> win)
        {
            WindowLayoutConfig cfg = new WindowLayoutConfig();
            if (win == null) { return cfg; }
            cfg.MainApp = ParseBounds(AsDict(Get(win, "mainApp")));
            cfg.EntryForm = ParseBounds(AsDict(Get(win, "entryForm")));
            cfg.Mostanad = ParseBounds(AsDict(Get(win, "mostanad")));
            cfg.LookupScreen = ParseBounds(AsDict(Get(win, "lookupScreen")));
            cfg.ConfirmScreen = ParseBounds(AsDict(Get(win, "confirmScreen")));
            cfg.PeriodScreen = ParseBounds(AsDict(Get(win, "periodScreen")));
            cfg.PullCodesScreen = ParseBounds(AsDict(Get(win, "pullCodesScreen")));
            return cfg;
        }

        private static WindowBounds ParseBounds(Dictionary<string, object> d)
        {
            WindowBounds b = new WindowBounds();
            if (d == null) { return b; }
            b.X = ToInt(Get(d, "x"), 0);
            b.Y = ToInt(Get(d, "y"), 0);
            b.W = ToInt(Get(d, "w"), 0);
            b.H = ToInt(Get(d, "h"), 0);
            return b;
        }

        private static BotSettings ParseSettings(Dictionary<string, object> d)
        {
            if (d == null) { return null; }
            BotSettings s = new BotSettings();
            if (d.ContainsKey("loopEnabled")) { s.LoopEnabled = ToBool(d["loopEnabled"]); }
            if (d.ContainsKey("doValidate")) { s.DoValidate = ToBool(d["doValidate"]); }
            if (d.ContainsKey("doReconcile")) { s.DoReconcile = ToBool(d["doReconcile"]); }
            if (d.ContainsKey("doWriteBack")) { s.DoWriteBack = ToBool(d["doWriteBack"]); }
            if (d.ContainsKey("stopOnError")) { s.StopOnError = ToBool(d["stopOnError"]); }
            if (d.ContainsKey("checkCoded")) { s.CheckCoded = ToBool(d["checkCoded"]); }
            if (d.ContainsKey("checkRejected")) { s.CheckRejected = ToBool(d["checkRejected"]); }
            if (d.ContainsKey("loopIntervalMinutes")) { s.LoopIntervalMinutes = ToInt(d["loopIntervalMinutes"], s.LoopIntervalMinutes); }
            if (d.ContainsKey("maxIterations")) { s.MaxIterations = ToInt(d["maxIterations"], s.MaxIterations); }
            if (d.ContainsKey("maxRetries")) { s.MaxRetries = ToInt(d["maxRetries"], s.MaxRetries); }
            if (d.ContainsKey("retryDelaySeconds")) { s.RetryDelaySeconds = ToInt(d["retryDelaySeconds"], s.RetryDelaySeconds); }
            if (d.ContainsKey("daysBack")) { s.DaysBack = ToInt(d["daysBack"], s.DaysBack); }
            if (d.ContainsKey("detailedActionLog")) { s.DetailedActionLog = ToBool(d["detailedActionLog"]); }
            if (d.ContainsKey("postSaveConfirmExport")) { s.PostSaveConfirmExport = ToBool(d["postSaveConfirmExport"]); }
            if (d.ContainsKey("exportDateRangeYearToDate")) { s.ExportDateRangeYearToDate = ToBool(d["exportDateRangeYearToDate"]); }
            if (d.ContainsKey("dailyExportEnabled")) { s.DailyExportEnabled = ToBool(d["dailyExportEnabled"]); }
            if (d.ContainsKey("dailyExportHour")) { s.DailyExportHour = ToInt(d["dailyExportHour"], s.DailyExportHour); }
            if (d.ContainsKey("dailyExportMinute")) { s.DailyExportMinute = ToInt(d["dailyExportMinute"], s.DailyExportMinute); }
            if (d.ContainsKey("dailyExportPm")) { s.DailyExportPm = ToBool(d["dailyExportPm"]); }
            if (d.ContainsKey("fillStepMs")) { s.FillStepMs = ToInt(d["fillStepMs"], s.FillStepMs); }
            if (d.ContainsKey("fillSettleMs")) { s.FillSettleMs = ToInt(d["fillSettleMs"], s.FillSettleMs); }
            if (d.ContainsKey("betweenClientsMs")) { s.BetweenClientsMs = ToInt(d["betweenClientsMs"], s.BetweenClientsMs); }
            if (d.ContainsKey("afterSaveMs")) { s.AfterSaveMs = ToInt(d["afterSaveMs"], s.AfterSaveMs); }
            ActionPace.Normalize(s);
            return s;
        }

        private static string[] ToStringArray(object o)
        {
            if (o == null) { return null; }
            if (o is object[])
            {
                object[] arr = (object[])o;
                List<string> list = new List<string>();
                for (int i = 0; i < arr.Length; i++) { list.Add(ToStr(arr[i])); }
                return list.ToArray();
            }
            if (o is ArrayList)
            {
                ArrayList al = (ArrayList)o;
                string[] arr = new string[al.Count];
                for (int i = 0; i < al.Count; i++) { arr[i] = ToStr(al[i]); }
                return arr;
            }
            return null;
        }

        private static List<FieldSelector> ParseSelectorArray(object[] arr)
        {
            List<FieldSelector> list = new List<FieldSelector>();
            if (arr == null) { return list; }
            for (int i = 0; i < arr.Length; i++)
            {
                Dictionary<string, object> d = AsDict(arr[i]);
                if (d == null) { continue; }
                FieldSelector s = new FieldSelector();
                s.FieldKey = Pick(d, "FieldKey", "fieldKey");
                s.FieldLabel = Pick(d, "FieldLabel", "fieldLabel");
                s.Kind = Pick(d, "Kind", "kind");
                s.SheetColumn = Pick(d, "SheetColumn", "sheetColumn");
                s.Screen = Pick(d, "Screen", "screen");
                s.Shortcut = Pick(d, "Shortcut", "shortcut");
                s.Label = Pick(d, "Label", "label");
                s.LabelOrdinal = Pick(d, "LabelOrdinal", "labelOrdinal");
                s.AccName = Pick(d, "AccName", "accName");
                s.ControlId = Pick(d, "ControlId", "controlId");
                s.Caption = Pick(d, "Caption", "caption");
                s.Class = Pick(d, "Class", "class");
                s.Section = Pick(d, "Section", "section");
                s.ClassOrdinal = Pick(d, "ClassOrdinal", "classOrdinal");
                s.Index = Pick(d, "Index", "index");
                s.DatePart = Pick(d, "DatePart", "datePart");
                s.SourceKey = Pick(d, "SourceKey", "sourceKey");
                s.Required = Pick(d, "Required", "required");
                s.ConfirmedAt = Pick(d, "ConfirmedAt", "confirmedAt");
                s.Aliases = Pick(d, "Aliases", "aliases");
                s.PosX = Pick(d, "PosX", "posX");
                s.PosY = Pick(d, "PosY", "posY");
                s.PosW = Pick(d, "PosW", "posW");
                s.PosH = Pick(d, "PosH", "posH");
                if (string.IsNullOrWhiteSpace(s.FieldKey) && string.IsNullOrWhiteSpace(s.Class)) { continue; }
                list.Add(s);
            }
            return list;
        }

        private static string Pick(Dictionary<string, object> d, params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                object v = Get(d, keys[i]);
                if (v != null) { return ToStr(v); }
            }
            return "";
        }

        private static Dictionary<string, object> AsDict(object o)
        {
            if (o == null) { return null; }
            Dictionary<string, object> d = o as Dictionary<string, object>;
            if (d != null) { return d; }
            IDictionary id = o as IDictionary;
            if (id == null) { return null; }
            Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry e in id)
            {
                if (e.Key != null) { copy[e.Key.ToString()] = e.Value; }
            }
            return copy;
        }

        private static object Get(Dictionary<string, object> d, string key)
        {
            if (d == null || key == null) { return null; }
            object v;
            if (d.TryGetValue(key, out v)) { return v; }
            foreach (KeyValuePair<string, object> kv in d)
            {
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) { return kv.Value; }
            }
            return null;
        }

        private static string ToStr(object o)
        {
            if (o == null) { return ""; }
            return Convert.ToString(o, CultureInfo.InvariantCulture);
        }

        private static int ToInt(object o, int def)
        {
            if (o == null) { return def; }
            int n;
            if (int.TryParse(ToStr(o), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) { return n; }
            return def;
        }

        private static bool ToBool(object o)
        {
            if (o is bool) { return (bool)o; }
            string s = ToStr(o);
            return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) || s == "1";
        }

        private static string Q(string s)
        {
            if (s == null) { s = ""; }
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n") + "\"";
        }
    }
}
