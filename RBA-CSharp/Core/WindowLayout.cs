// =============================================================================
//  WindowLayout.cs - save/restore Coding / entry / المستند window bounds.
//  Critical anti-hallucination: keep the same size used during field click-map
//  so ClassOrdinal / Index resolution stays stable across runs.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace RbaCore
{
    public class WindowBounds
    {
        public int X;
        public int Y;
        public int W;
        public int H;

        public bool IsValid
        {
            get { return W >= 80 && H >= 60; }
        }

        public string ToShort()
        {
            return X + "," + Y + " " + W + "x" + H;
        }
    }

    public class WindowLayoutConfig
    {
        public WindowBounds MainApp = new WindowBounds();
        public WindowBounds EntryForm = new WindowBounds();
        public WindowBounds Mostanad = new WindowBounds();
        // «الإستعلام للتعديل» — its search box is position-mapped like entry fields.
        public WindowBounds LookupScreen = new WindowBounds();
        // Confirm-before-coding grid screen (F2 / F9).
        public WindowBounds ConfirmScreen = new WindowBounds();
        // «عمليات التكويد خلال فترة» date + export screen.
        public WindowBounds PeriodScreen = new WindowBounds();
        public WindowBounds PullCodesScreen = new WindowBounds();
    }

    public static class WindowLayout
    {
        // Active layout for the current fill/docs session (set by EntryTest / ClientEntry).
        public static WindowLayoutConfig Active;

        public static WindowBounds Capture(IntPtr hwnd)
        {
            WindowBounds b = new WindowBounds();
            if (hwnd == IntPtr.Zero || !Win32Interop.IsWindow(hwnd)) { return b; }
            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(hwnd);
                b.X = r.X;
                b.Y = r.Y;
                b.W = r.W;
                b.H = r.H;
            }
            catch { }
            return b;
        }

        public static bool Apply(IntPtr hwnd, WindowBounds bounds, Action<string, string> log)
        {
            if (hwnd == IntPtr.Zero || bounds == null || !bounds.IsValid) { return false; }
            if (!Win32Interop.IsWindow(hwnd)) { return false; }
            if (log == null) { log = delegate(string m, string l) { }; }

            try
            {
                // Restore if maximized/minimized so MoveWindow sticks.
                Win32Interop.ShowWindow(hwnd, Win32Interop.SW_RESTORE);
                Thread.Sleep(80);

                int x = bounds.X;
                int y = bounds.Y;
                IntPtr parent = Win32Interop.GetParent(hwnd);
                if (parent != IntPtr.Zero && Win32Interop.IsWindow(parent))
                {
                    string pCls = Win32Driver.GetClassName(parent) ?? "";
                    bool mdi = pCls.IndexOf("MDIClient", StringComparison.OrdinalIgnoreCase) >= 0;
                    int style = Win32Interop.GetWindowLong(hwnd, -16);
                    bool child = (style & 0x40000000) != 0;
                    if (mdi || child)
                    {
                        POINT pt = new POINT(bounds.X, bounds.Y);
                        Win32Interop.ScreenToClient(parent, ref pt);
                        x = pt.X;
                        y = pt.Y;
                    }
                }

                bool ok = Win32Interop.SetWindowPos(
                    hwnd, Win32Interop.HWND_TOP,
                    x, y, bounds.W, bounds.H,
                    Win32Interop.SWP_NOZORDER | Win32Interop.SWP_SHOWWINDOW);
                if (!ok)
                {
                    ok = Win32Interop.MoveWindow(hwnd, x, y, bounds.W, bounds.H, true);
                }
                Thread.Sleep(150);
                try { Win32Driver.SetForeground(hwnd); } catch { }

                WindowBounds now = Capture(hwnd);
                log("window layout applied: want " + bounds.ToShort() +
                    " now " + now.ToShort() + " ok=" + ok, ok ? "OK" : "WARN");
                return ok;
            }
            catch (Exception ex)
            {
                log("window layout apply failed: " + ex.Message, "WARN");
                return false;
            }
        }

        public static void ApplyAll(
            IntPtr mainApp,
            IntPtr entryForm,
            IntPtr mostanad,
            WindowLayoutConfig layout,
            Action<string, string> log)
        {
            if (layout == null) { return; }
            if (mainApp != IntPtr.Zero && layout.MainApp != null && layout.MainApp.IsValid)
            {
                Apply(mainApp, layout.MainApp, log);
            }
            if (entryForm != IntPtr.Zero && layout.EntryForm != null && layout.EntryForm.IsValid)
            {
                Apply(entryForm, layout.EntryForm, log);
            }
            if (mostanad != IntPtr.Zero && layout.Mostanad != null && layout.Mostanad.IsValid)
            {
                Apply(mostanad, layout.Mostanad, log);
            }
        }

        public static WindowLayoutConfig CaptureFromLive(
            IntPtr mainApp,
            IntPtr entryForm,
            IntPtr mostanad)
        {
            return CaptureFromLive(mainApp, entryForm, mostanad, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        public static WindowLayoutConfig CaptureFromLive(
            IntPtr mainApp,
            IntPtr entryForm,
            IntPtr mostanad,
            IntPtr lookupScreen)
        {
            return CaptureFromLive(mainApp, entryForm, mostanad, lookupScreen, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        }

        public static WindowLayoutConfig CaptureFromLive(
            IntPtr mainApp,
            IntPtr entryForm,
            IntPtr mostanad,
            IntPtr lookupScreen,
            IntPtr confirmScreen,
            IntPtr periodScreen)
        {
            return CaptureFromLive(mainApp, entryForm, mostanad, lookupScreen, confirmScreen, periodScreen, IntPtr.Zero);
        }

        public static WindowLayoutConfig CaptureFromLive(
            IntPtr mainApp,
            IntPtr entryForm,
            IntPtr mostanad,
            IntPtr lookupScreen,
            IntPtr confirmScreen,
            IntPtr periodScreen,
            IntPtr pullCodesScreen)
        {
            WindowLayoutConfig cfg = new WindowLayoutConfig();
            cfg.MainApp = Capture(mainApp);
            cfg.EntryForm = Capture(entryForm);
            cfg.Mostanad = Capture(mostanad);
            cfg.LookupScreen = Capture(lookupScreen);
            cfg.ConfirmScreen = Capture(confirmScreen);
            cfg.PeriodScreen = Capture(periodScreen);
            cfg.PullCodesScreen = Capture(pullCodesScreen);
            return cfg;
        }

        // When a screen is not open during capture, keep the last saved bounds
        // so a later reopen still uses the mapped size.
        public static void KeepPreviousIfMissing(WindowLayoutConfig live, WindowLayoutConfig previous)
        {
            if (live == null || previous == null) { return; }
            if (!IsValid(live.MainApp) && IsValid(previous.MainApp)) { live.MainApp = previous.MainApp; }
            if (!IsValid(live.EntryForm) && IsValid(previous.EntryForm)) { live.EntryForm = previous.EntryForm; }
            if (!IsValid(live.Mostanad) && IsValid(previous.Mostanad)) { live.Mostanad = previous.Mostanad; }
            if (!IsValid(live.LookupScreen) && IsValid(previous.LookupScreen)) { live.LookupScreen = previous.LookupScreen; }
            if (!IsValid(live.ConfirmScreen) && IsValid(previous.ConfirmScreen)) { live.ConfirmScreen = previous.ConfirmScreen; }
            if (!IsValid(live.PeriodScreen) && IsValid(previous.PeriodScreen)) { live.PeriodScreen = previous.PeriodScreen; }
            if (!IsValid(live.PullCodesScreen) && IsValid(previous.PullCodesScreen)) { live.PullCodesScreen = previous.PullCodesScreen; }
        }

        private static bool IsValid(WindowBounds b)
        {
            return b != null && b.IsValid;
        }

        // Values that must NEVER be typed into KYC fields (anti-hallucination).
        public static bool LooksLikeFilePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return false; }
            string v = value.Trim();
            if (v.IndexOf(":\\", StringComparison.Ordinal) >= 0) { return true; }
            if (v.IndexOf("\\\\", StringComparison.Ordinal) >= 0) { return true; }
            if (Regex.IsMatch(v, "(?i)\\.(jpg|jpeg|png|tif|tiff|bmp|pdf|doc|docx)$")) { return true; }
            if (v.IndexOf("\\Docs\\", StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            if (v.IndexOf("/Docs/", StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            if (v.IndexOf("Sample\\Docs", StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            return false;
        }

        public static bool IsKycFieldKey(string fieldKey)
        {
            if (string.IsNullOrEmpty(fieldKey)) { return false; }
            return Regex.IsMatch(fieldKey,
                "^(broker|name|address|job|mobile|email|landline|national|gender|legal|subForm|idType|gov|country|dob|issue|expiry|docsCount)",
                RegexOptions.IgnoreCase);
        }

        // File-browser controls must never receive KYC text (or vice-versa).
        public static bool IsFileBrowserClass(string className)
        {
            if (string.IsNullOrEmpty(className)) { return false; }
            return className.IndexOf("DriveList", StringComparison.OrdinalIgnoreCase) >= 0
                || className.IndexOf("DirList", StringComparison.OrdinalIgnoreCase) >= 0
                || className.IndexOf("FileList", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static IntPtr FindMostanadIfOpen(int processId)
        {
            List<IntPtr> tops = Win32Interop.EnumWindowsList();
            for (int i = 0; i < tops.Count; i++)
            {
                IntPtr h = tops[i];
                if (!Win32Interop.IsWindowVisible(h)) { continue; }
                if (processId > 0 && Win32Driver.GetProcessId(h) != processId) { continue; }
                string title = Win32Driver.GetWindowTitle(h) ?? "";
                if (title.IndexOf("المستند", StringComparison.Ordinal) >= 0 ||
                    title.IndexOf("مستند", StringComparison.Ordinal) >= 0)
                {
                    return h;
                }
            }
            return IntPtr.Zero;
        }
    }
}
