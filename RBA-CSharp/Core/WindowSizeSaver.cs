// =============================================================================
//  WindowSizeSaver.cs - open a Coding screen, capture size/position, persist.
//  Click-maps are only valid against the size they were captured on; reopening
//  at a different size (especially period/export) breaks Position hits.
// =============================================================================
using System;
using System.Collections.Generic;

namespace RbaCore
{
    public static class WindowSizeSaver
    {
        public const string SlotEntry = "entry";
        public const string SlotLookup = "lookup";
        public const string SlotConfirm = "confirm";
        public const string SlotPeriod = "period";
        public const string SlotPullCodes = "pullcodes";

        public static string ArabicName(string slot)
        {
            if (string.Equals(slot, SlotEntry, StringComparison.OrdinalIgnoreCase))
            {
                return "بيانات العملاء";
            }
            if (string.Equals(slot, SlotLookup, StringComparison.OrdinalIgnoreCase))
            {
                return "الإستعلام للتعديل";
            }
            if (string.Equals(slot, SlotConfirm, StringComparison.OrdinalIgnoreCase))
            {
                return "تأكيد بيانات العملاء";
            }
            if (string.Equals(slot, SlotPeriod, StringComparison.OrdinalIgnoreCase))
            {
                return "عمليات التكويد خلال فترة (التصدير)";
            }
            if (string.Equals(slot, SlotPullCodes, StringComparison.OrdinalIgnoreCase))
            {
                return "سحب الأكواد";
            }
            return slot;
        }

        // Find the screen if it is already open; otherwise open it from the menu.
        public static IntPtr FindOrOpen(
            IntPtr appWindow,
            BotConfig cfg,
            List<FieldSelector> selectors,
            string slot,
            int settleMs,
            Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            if (appWindow == IntPtr.Zero || !Win32Interop.IsWindow(appWindow) || cfg == null)
            {
                return IntPtr.Zero;
            }
            if (settleMs <= 0) { settleMs = 500; }
            WindowLayout.Active = cfg.Windows;
            SanitizePostMenus(cfg);

            if (string.Equals(slot, SlotEntry, StringComparison.OrdinalIgnoreCase))
            {
                return OpenEntry(appWindow, cfg, settleMs, log);
            }
            if (string.Equals(slot, SlotLookup, StringComparison.OrdinalIgnoreCase))
            {
                IntPtr open = ClientLookup.FindOpenScreen(appWindow, cfg.App, selectors);
                if (open != IntPtr.Zero) { return open; }
                return ClientLookup.EnsureScreen(appWindow, cfg.App, selectors, settleMs, log);
            }
            if (string.Equals(slot, SlotConfirm, StringComparison.OrdinalIgnoreCase))
            {
                IntPtr open = CodingPostSave.FindOpenConfirm(appWindow, cfg.App, selectors);
                if (open != IntPtr.Zero) { return open; }
                return CodingPostSave.EnsureConfirmScreen(appWindow, cfg.App, selectors, settleMs, log);
            }
            if (string.Equals(slot, SlotPeriod, StringComparison.OrdinalIgnoreCase))
            {
                IntPtr open = CodingPostSave.FindOpenPeriod(appWindow, cfg.App, selectors);
                if (open != IntPtr.Zero) { return open; }
                return CodingPostSave.EnsurePeriodScreen(appWindow, cfg.App, selectors, settleMs, log);
            }
            if (string.Equals(slot, SlotPullCodes, StringComparison.OrdinalIgnoreCase))
            {
                IntPtr open = CodingPostSave.FindOpenPullCodes(appWindow, cfg.App, selectors);
                if (open != IntPtr.Zero) { return open; }
                return CodingPostSave.EnsurePullCodesScreen(appWindow, cfg.App, selectors, settleMs, log);
            }
            return IntPtr.Zero;
        }

        public static WindowBounds GetSaved(BotConfig cfg, string slot)
        {
            if (cfg == null || cfg.Windows == null) { return null; }
            if (string.Equals(slot, SlotEntry, StringComparison.OrdinalIgnoreCase)) { return cfg.Windows.EntryForm; }
            if (string.Equals(slot, SlotLookup, StringComparison.OrdinalIgnoreCase)) { return cfg.Windows.LookupScreen; }
            if (string.Equals(slot, SlotConfirm, StringComparison.OrdinalIgnoreCase)) { return cfg.Windows.ConfirmScreen; }
            if (string.Equals(slot, SlotPeriod, StringComparison.OrdinalIgnoreCase)) { return cfg.Windows.PeriodScreen; }
            if (string.Equals(slot, SlotPullCodes, StringComparison.OrdinalIgnoreCase)) { return cfg.Windows.PullCodesScreen; }
            return null;
        }

        public static void ApplySaved(IntPtr hwnd, BotConfig cfg, string slot, Action<string, string> log)
        {
            WindowBounds b = GetSaved(cfg, slot);
            if (hwnd == IntPtr.Zero || b == null || !b.IsValid) { return; }
            WindowLayout.Apply(hwnd, b, log);
        }

        public static bool Save(
            BotConfig cfg,
            IntPtr appWindow,
            IntPtr hwnd,
            string slot,
            Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            WindowBounds b = WindowLayout.Capture(hwnd);
            if (!b.IsValid)
            {
                log("window size save failed — invalid bounds for " + slot, "WARN");
                return false;
            }
            if (cfg.Windows == null) { cfg.Windows = new WindowLayoutConfig(); }

            if (string.Equals(slot, SlotEntry, StringComparison.OrdinalIgnoreCase))
            {
                cfg.Windows.EntryForm = b;
            }
            else if (string.Equals(slot, SlotLookup, StringComparison.OrdinalIgnoreCase))
            {
                cfg.Windows.LookupScreen = b;
            }
            else if (string.Equals(slot, SlotConfirm, StringComparison.OrdinalIgnoreCase))
            {
                cfg.Windows.ConfirmScreen = b;
            }
            else if (string.Equals(slot, SlotPeriod, StringComparison.OrdinalIgnoreCase))
            {
                cfg.Windows.PeriodScreen = b;
            }
            else if (string.Equals(slot, SlotPullCodes, StringComparison.OrdinalIgnoreCase))
            {
                cfg.Windows.PullCodesScreen = b;
            }
            else
            {
                return false;
            }

            if (appWindow != IntPtr.Zero)
            {
                WindowBounds main = WindowLayout.Capture(appWindow);
                if (main.IsValid) { cfg.Windows.MainApp = main; }
            }

            cfg.SaveBotConfig();
            WindowLayout.Active = cfg.Windows;
            log("saved «" + ArabicName(slot) + "» size " + b.ToShort(), "OK");
            return true;
        }

        private static IntPtr OpenEntry(
            IntPtr appWindow, BotConfig cfg, int settleMs, Action<string, string> log)
        {
            string title = "بيانات العملاء";
            string cls = "ThunderRT6FormDC";
            string shortcut = "^{F2}";
            string child = null;
            string[] menu = null;
            if (cfg.App != null)
            {
                if (!string.IsNullOrWhiteSpace(cfg.App.EntryWindowTitle)) { title = cfg.App.EntryWindowTitle.Trim(); }
                if (!string.IsNullOrWhiteSpace(cfg.App.EntryWindowClass)) { cls = cfg.App.EntryWindowClass.Trim(); }
                if (!string.IsNullOrWhiteSpace(cfg.App.EntryShortcut)) { shortcut = cfg.App.EntryShortcut.Trim(); }
                child = cfg.App.ChildWindow;
                menu = cfg.App.MenuPath;
            }

            IntPtr already = ClientEntry.FindEntryForm(appWindow, title, cls);
            if (already != IntPtr.Zero) { return already; }

            log("opening «" + title + "» to capture size", "INFO");
            return ClientEntry.GetEntryScanRoot(
                appWindow, title, cls, child, shortcut, null, menu, true, settleMs);
        }

        private static void SanitizePostMenus(BotConfig cfg)
        {
            if (cfg == null || cfg.App == null) { return; }
            cfg.App.ConfirmMenuPath = CodingPostSave.SanitizeMenuPath(cfg.App.ConfirmMenuPath, "confirm");
            cfg.App.PeriodMenuPath = CodingPostSave.SanitizeMenuPath(cfg.App.PeriodMenuPath, "period");
            cfg.App.PullCodesMenuPath = PullCodes.MenuPath(cfg.App);
        }
    }
}
