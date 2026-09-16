// =============================================================================
//  MenuNavigator.cs - open Coding screens by REAL menu clicks (parent → child).
//  Matches production flow: MDI frame menu (mnuCntCoding path), not title guess.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace RbaCore
{
    public class MenuNavResult
    {
        public bool Ok;
        public string How;
        public string Note;
        public string[] PathUsed;
    }

    public static class MenuNavigator
    {
        // Default paths tried in order (Arabic captions vary by site / replica).
        // Posting / confirm screen (leaf may be the long title or the short caption).
        public static readonly string[][] DefaultConfirmPaths = new string[][]
        {
            new string[] { "نظام تكويد العملاء", "تأكيد بيانات العملاء قبل إرسالها للتكويد" },
            new string[] { "التكويد", "تأكيد بيانات العملاء قبل إرسالها للتكويد" },
            new string[] { "نظام تكويد العملاء", "تأكيد بيانات العملاء" },
            new string[] { "التكويد", "تأكيد بيانات العملاء" },
            new string[] { "نظام تكويد العملاء", "\u062a\u0631\u062d\u064a\u0644" },
            new string[] { "التكويد", "\u062a\u0631\u062d\u064a\u0644" }
        };

        public static readonly string[][] DefaultPullPaths = new string[][]
        {
            new string[] { "التكويد", "سحب الأكواد" },
            new string[] { "التكويد", "سحب اكواد" },
            new string[] { "نظام تكويد العملاء", "سحب الأكواد" }
        };

        public static readonly string[][] DefaultPaths = new string[][]
        {
            new string[] { "التكويد", "إدخال بيانات العملاء" },
            new string[] { "التكويد", "ادخال بيانات العملاء" },
            new string[] { "التكويد", "إضافة بيانات العملاء" },
            new string[] { "التكويد", "اضافة بيانات العملاء" },
            new string[] { "التكويد", "بيانات العملاء (تكويد)" },
            new string[] { "التكويد", "بيانات العملاء" },
            new string[] { "نظام تكويد العملاء", "إدخال بيانات العملاء" },
            new string[] { "نظام تكويد العملاء", "ادخال بيانات العملاء" },
            new string[] { "نظام تكويد العملاء", "إضافة بيانات العملاء" }
        };

        public static MenuNavResult OpenPath(IntPtr frameWindow, string[] path, int settleMs)
        {
            MenuNavResult r = new MenuNavResult();
            if (frameWindow == IntPtr.Zero || !Win32Interop.IsWindow(frameWindow))
            {
                r.Note = "frame window dead";
                return r;
            }
            if (path == null || path.Length < 2)
            {
                r.Note = "menu path needs parent + leaf (2 clicks)";
                return r;
            }

            try { Win32Driver.SetForeground(frameWindow); } catch { }
            Thread.Sleep(120);

            IntPtr menuBar = Win32Interop.GetMenu(frameWindow);
            if (menuBar == IntPtr.Zero)
            {
                r.Note = "frame has no Win32 menu bar";
                return r;
            }

            int parentPos = FindMenuPos(menuBar, path[0]);
            if (parentPos < 0)
            {
                r.Note = "parent menu not found: " + path[0] + " (have: " + ListMenu(menuBar) + ")";
                return r;
            }

            IntPtr popup = Win32Interop.GetSubMenu(menuBar, parentPos);
            if (popup == IntPtr.Zero)
            {
                r.Note = "parent is not a popup menu: " + path[0];
                return r;
            }

            // Resolve the leaf before clicking so we never depend on title guesswork.
            IntPtr current = popup;
            int leafPos = -1;
            for (int i = 1; i < path.Length; i++)
            {
                int pos = FindMenuPos(current, path[i]);
                if (pos < 0)
                {
                    r.Note = "menu item not found: " + path[i];
                    return r;
                }
                IntPtr nested = Win32Interop.GetSubMenu(current, pos);
                if (nested != IntPtr.Zero && i < path.Length - 1)
                {
                    current = nested;
                    continue;
                }
                leafPos = pos;
                break;
            }
            if (leafPos < 0)
            {
                r.Note = "menu leaf not resolved";
                return r;
            }

            // Click 1: open the parent popup on the MDI frame (real UI).
            bool parentClicked = ClickMenuItem(frameWindow, menuBar, (uint)parentPos, Math.Max(250, settleMs / 2));
            Thread.Sleep(200);

            // Click 2: popup leaf (real UI) — fall back to WM_COMMAND with the leaf id
            // so navigation still completes when popup hit-testing fails under automation.
            bool leafClicked = ClickPopupLeaf(frameWindow, current, (uint)leafPos, settleMs);
            if (!leafClicked)
            {
                if (!PostMenuCommand(frameWindow, current, leafPos))
                {
                    r.Note = "failed to activate leaf after parent"
                        + (parentClicked ? " click" : " (parent click also missed)");
                    return r;
                }
            }

            r.Ok = true;
            r.How = leafClicked ? "MenuClicks" : (parentClicked ? "MenuParent+Command" : "MenuCommand");
            r.PathUsed = path;
            r.Note = "opened via " + string.Join(" → ", path) + " [" + r.How + "]";
            return r;
        }

        private static bool ClickPopupLeaf(IntPtr frame, IntPtr hMenu, uint pos, int settleMs)
        {
            // Visible popup menus are often #32768 windows — try those first.
            List<IntPtr> tops = Win32Interop.EnumWindowsList();
            for (int i = 0; i < tops.Count; i++)
            {
                string cls = Win32Driver.GetClassName(tops[i]);
                if (cls != "#32768") { continue; }
                if (ClickMenuItem(tops[i], hMenu, pos, settleMs)) { return true; }
            }
            if (ClickMenuItem(frame, hMenu, pos, settleMs)) { return true; }
            IntPtr fg = Win32Interop.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != frame)
            {
                return ClickMenuItem(fg, hMenu, pos, settleMs);
            }
            return false;
        }

        private static bool PostMenuCommand(IntPtr frame, IntPtr hMenu, int pos)
        {
            uint cmd = Win32Interop.GetMenuItemID(hMenu, pos);
            if (cmd == 0 || cmd == 0xFFFFFFFF)
            {
                // Fall back: caption-based id for CodingReplica / common coding leaf.
                string text = GetItemText(hMenu, (uint)pos);
                string n = Norm(text);
                if (n.IndexOf("اضافهبياناتالعملا", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("ادخالبياناتالعملا", StringComparison.Ordinal) >= 0 ||
                    n.IndexOf("بياناتالعملا", StringComparison.Ordinal) >= 0)
                {
                    cmd = 100;
                }
            }
            if (cmd == 0 || cmd == 0xFFFFFFFF) { return false; }
            Win32Interop.PostMessage(frame, 0x0111 /*WM_COMMAND*/, (IntPtr)(cmd), IntPtr.Zero);
            return true;
        }

        public static MenuNavResult OpenConfirmWithFallbacks(IntPtr frameWindow, string[] preferredPath, int settleMs)
        {
            List<string[]> tries = new List<string[]>();
            if (preferredPath != null && preferredPath.Length >= 2) { tries.Add(preferredPath); }
            for (int i = 0; i < DefaultConfirmPaths.Length; i++) { tries.Add(DefaultConfirmPaths[i]); }
            MenuNavResult last = new MenuNavResult();
            last.Note = "no confirm/posting menu path worked";
            for (int i = 0; i < tries.Count; i++)
            {
                MenuNavResult r = OpenPath(frameWindow, tries[i], settleMs);
                if (r.Ok) { return r; }
                last = r;
            }
            return last;
        }

        public static MenuNavResult OpenPullWithFallbacks(IntPtr frameWindow, string[] preferredPath, int settleMs)
        {
            List<string[]> tries = new List<string[]>();
            if (preferredPath != null && preferredPath.Length >= 2) { tries.Add(preferredPath); }
            for (int i = 0; i < DefaultPullPaths.Length; i++) { tries.Add(DefaultPullPaths[i]); }
            MenuNavResult last = new MenuNavResult();
            last.Note = "no pull-codes menu path worked";
            for (int i = 0; i < tries.Count; i++)
            {
                MenuNavResult r = OpenPath(frameWindow, tries[i], settleMs);
                if (r.Ok) { return r; }
                last = r;
            }
            return last;
        }

        public static MenuNavResult OpenWithFallbacks(IntPtr frameWindow, string[] preferredPath, int settleMs)
        {
            List<string[]> tries = new List<string[]>();
            if (preferredPath != null && preferredPath.Length >= 2) { tries.Add(preferredPath); }
            for (int i = 0; i < DefaultPaths.Length; i++) { tries.Add(DefaultPaths[i]); }

            MenuNavResult last = new MenuNavResult();
            last.Note = "no menu path worked";
            for (int i = 0; i < tries.Count; i++)
            {
                MenuNavResult r = OpenPath(frameWindow, tries[i], settleMs);
                if (r.Ok) { return r; }
                last = r;
            }
            return last;
        }

        private static string ListMenu(IntPtr hMenu)
        {
            if (hMenu == IntPtr.Zero) { return "(none)"; }
            int count = Win32Interop.GetMenuItemCount(hMenu);
            List<string> names = new List<string>();
            for (int i = 0; i < count && names.Count < 12; i++)
            {
                string text = GetItemText(hMenu, (uint)i);
                if (string.IsNullOrEmpty(text)) { continue; }
                names.Add(text);
            }
            return names.Count == 0 ? "(empty captions)" : string.Join(" | ", names.ToArray());
        }

        private static int FindMenuPos(IntPtr hMenu, string want)
        {
            if (hMenu == IntPtr.Zero || string.IsNullOrWhiteSpace(want)) { return -1; }
            string wantKey = Norm(want);
            int count = Win32Interop.GetMenuItemCount(hMenu);
            for (int i = 0; i < count; i++)
            {
                string text = GetItemText(hMenu, (uint)i);
                if (string.IsNullOrEmpty(text)) { continue; }
                string key = Norm(text);
                if (key == wantKey || key.IndexOf(wantKey, StringComparison.Ordinal) >= 0 ||
                    wantKey.IndexOf(key, StringComparison.Ordinal) >= 0)
                {
                    return i;
                }
            }
            return -1;
        }

        private static string GetItemText(IntPtr hMenu, uint pos)
        {
            StringBuilder sb = new StringBuilder(256);
            int n = Win32Interop.GetMenuString(hMenu, pos, sb, sb.Capacity, Win32Interop.MF_BYPOSITION);
            if (n <= 0) { return ""; }
            string t = sb.ToString();
            int tab = t.IndexOf('\t');
            if (tab >= 0) { t = t.Substring(0, tab); }
            return t.Trim();
        }

        private static bool ClickMenuItem(IntPtr hwnd, IntPtr hMenu, uint pos, int settleMs)
        {
            RECT rc;
            if (!Win32Interop.GetMenuItemRect(hwnd, hMenu, pos, out rc))
            {
                // Popup items sometimes need the foreground window (menu window).
                IntPtr fg = Win32Interop.GetForegroundWindow();
                if (fg == IntPtr.Zero || !Win32Interop.GetMenuItemRect(fg, hMenu, pos, out rc))
                {
                    return false;
                }
            }
            int x = (rc.Left + rc.Right) / 2;
            int y = (rc.Top + rc.Bottom) / 2;
            int w = rc.Right - rc.Left;
            int h = rc.Bottom - rc.Top;
            if (w < 4 || h < 4 || (x == 0 && y == 0)) { return false; }
            Win32Driver.ClickScreen(x, y, settleMs);
            return true;
        }

        private static string Norm(string s)
        {
            if (s == null) { return ""; }
            string t = s.Trim();
            t = t.Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا");
            t = t.Replace("ة", "ه").Replace("ى", "ي");
            t = t.Replace("إضافة", "اضافه").Replace("اضافة", "اضافه").Replace("إدخال", "ادخال");
            t = t.Replace("&", "");
            t = t.Replace(" ", "");
            return t;
        }
    }
}
