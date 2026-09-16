// =============================================================================
//  CodingHealth.cs - detect Coding closed / hung; optional one-shot reattach.
// =============================================================================
using System;
using System.Threading;

namespace RbaCore
{
    public enum CodingHealthState
    {
        Ok = 0,
        Closed = 1,
        Hung = 2
    }

    public static class CodingHealth
    {
        public const int DefaultProbeMs = 2000;
        public const int ShortcutTimeoutMs = 8000;

        public static CodingHealthState Probe(IntPtr hwnd)
        {
            return Probe(hwnd, DefaultProbeMs);
        }

        public static CodingHealthState Probe(IntPtr hwnd, int timeoutMs)
        {
            if (hwnd == IntPtr.Zero || !Win32Interop.IsWindow(hwnd))
            {
                return CodingHealthState.Closed;
            }
            try
            {
                if (Win32Interop.IsHungAppWindow(hwnd))
                {
                    return CodingHealthState.Hung;
                }
            }
            catch { }

            IntPtr result;
            IntPtr sent = Win32Interop.SendMessageTimeout(
                hwnd,
                Win32Interop.WM_NULL,
                IntPtr.Zero,
                IntPtr.Zero,
                Win32Interop.SMTO_ABORTIFHUNG | Win32Interop.SMTO_BLOCK,
                (uint)Math.Max(250, timeoutMs),
                out result);
            if (sent == IntPtr.Zero)
            {
                // Timeout / hung / destroyed while probing.
                if (!Win32Interop.IsWindow(hwnd)) { return CodingHealthState.Closed; }
                return CodingHealthState.Hung;
            }
            return CodingHealthState.Ok;
        }

        public static string Describe(CodingHealthState state)
        {
            if (state == CodingHealthState.Closed) { return "نافذة التكويد أُغلقت"; }
            if (state == CodingHealthState.Hung) { return "نافذة التكويد لا تستجيب (تجمّد)"; }
            return "OK";
        }

        // Returns true when app is usable. On Closed, tries one reattach via config.
        public static bool EnsureAlive(
            ref IntPtr app,
            BotConfig cfg,
            Action<string, string> log,
            out string why,
            bool tryReattach)
        {
            why = "";
            CodingHealthState state = Probe(app);
            if (state == CodingHealthState.Ok) { return true; }

            if (state == CodingHealthState.Hung)
            {
                why = Describe(state);
                if (log != null) { log("coding-health: " + why, "ERROR"); }
                return false;
            }

            // Closed.
            why = Describe(CodingHealthState.Closed);
            if (!tryReattach || cfg == null || cfg.App == null)
            {
                if (log != null) { log("coding-health: " + why, "ERROR"); }
                return false;
            }

            if (log != null) { log("coding-health: window gone — trying reattach once", "WARN"); }
            IntPtr again = FieldDiscovery.GetTargetAppWindow(
                cfg.App.WindowTitlePattern,
                cfg.App.ExePath,
                cfg.App.AutoLaunch,
                Math.Min(15, Math.Max(5, cfg.App.LaunchTimeoutSec)));
            if (again == IntPtr.Zero)
            {
                again = FieldDiscovery.GetTargetAppWindow("برنامج التكويد", "", false, 5);
            }
            if (again == IntPtr.Zero)
            {
                again = FieldDiscovery.GetTargetAppWindow("تكويد", "", false, 5);
            }
            if (again == IntPtr.Zero || Probe(again) != CodingHealthState.Ok)
            {
                if (log != null) { log("coding-health: reattach failed — " + why, "ERROR"); }
                return false;
            }
            app = again;
            why = "";
            if (log != null)
            {
                log("coding-health: reattached hwnd=" + app + " «" +
                    Win32Driver.GetWindowTitle(app) + "»", "OK");
            }
            return true;
        }

        public static bool IsAlive(IntPtr hwnd)
        {
            return Probe(hwnd) == CodingHealthState.Ok;
        }
    }
}
