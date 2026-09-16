// =============================================================================
//  Win32Interop.cs - the native transport layer for the RBA driver.
//
//  Ported from RBA/_shared/Win32Driver.ps1 (the "W32", "W32Sig" and "Msaa"
//  Add-Type blocks plus the window-message constants and the small safe wrappers
//  built on top of them). This is the TRANSPORT only: read a window's children,
//  ask a control its accessible name, write text, click, pick a combo entry,
//  dismiss a dialog. It knows nothing about any particular application, and it
//  does not decide which control is which field - that is FieldDiscovery's job.
//
//  Win32 messages are used instead of UI Automation because UIA could not see the
//  simulator's fields, and because window messages are Unicode-native: the wide
//  entry points (SendMessageW / GetWindowTextW / GetClassNameW) carry Arabic
//  intact.
//
//  C# 5 / .NET Framework. No string interpolation, ?. , ?? , expression-bodied
//  members, nameof or tuples.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace RbaCore
{
    // The POINT / RECT structs are kept as top-level public types (they are
    // structs, not classes) so callers across the engine can pass them to
    // WindowFromPoint / GetWindowRect / GetCursorPos without qualification.
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public POINT(int x, int y)
        {
            this.X = x;
            this.Y = y;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static class Win32Interop
    {
        // ---- enumeration callback ------------------------------------------
        // Return true to keep enumerating, false to stop. Held by the caller for
        // the duration of the (synchronous) enum call.
        public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

        // =====================================================================
        //  WINDOW MESSAGES  (WM_ / BM_ / CB_ / TCM_) and misc constants
        // =====================================================================
        public const uint WM_CLOSE         = 0x0010;
        public const uint WM_SETTEXT       = 0x000C;
        public const uint WM_GETTEXT       = 0x000D;
        public const uint WM_GETTEXTLENGTH = 0x000E;

        // Synthesised mouse messages, used only by the MouseEmulation click path.
        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_LBUTTONUP   = 0x0202;
        public const uint WM_LBUTTONDBLCLK = 0x0203;
        public const int  MK_LBUTTON     = 0x0001;

        public const uint BM_CLICK    = 0x00F5;
        public const uint BM_GETCHECK = 0x00F0;

        public const uint CB_GETCURSEL       = 0x0147;
        public const uint CB_SETCURSEL       = 0x014E;
        public const uint CB_FINDSTRING      = 0x014C;
        public const uint CB_FINDSTRINGEXACT = 0x0158;
        public const uint CB_GETCOUNT        = 0x0146;
        public const uint CB_GETLBTEXT       = 0x0148;
        public const uint CB_GETLBTEXTLEN    = 0x0149;

        // ListBox (VB6 DirListBox / FileListBox / standard LISTBOX).
        public const uint LB_GETCOUNT        = 0x018B;
        public const uint LB_GETTEXT         = 0x0189;
        public const uint LB_GETTEXTLEN      = 0x018A;
        public const uint LB_SETCURSEL       = 0x0186;
        public const uint LB_GETCURSEL       = 0x0188;
        public const uint LB_FINDSTRING      = 0x018F;
        public const uint LB_FINDSTRINGEXACT = 0x01A2;
        public const uint LB_SETTOPINDEX     = 0x0197;
        public const uint LB_GETTOPINDEX     = 0x018E;
        public const uint LB_GETITEMRECT     = 0x0198;
        public const uint LB_ITEMFROMPOINT   = 0x01A9;

        // VB6 raises Change/DblClick from these notifications, so a programmatic
        // selection must be announced to the parent form the same way the mouse does.
        public const uint WM_COMMAND    = 0x0111;
        public const int  CBN_SELCHANGE = 1;
        public const int  LBN_SELCHANGE = 1;
        public const int  LBN_DBLCLK    = 2;

        // TCM_SETCURFOCUS raises the selection-change notification (so WinForms
        // builds the page); TCM_SETCURSEL changes the visible tab WITHOUT it.
        public const uint TCM_SETCURFOCUS = 0x1330;
        public const uint TCM_SETCURSEL   = 0x130C;
        public const uint TCM_GETCURSEL   = 0x130B;

        // GetWindow direction codes: one generation at a time.
        public const uint GW_CHILD    = 5;
        public const uint GW_HWNDNEXT = 2;

        // ShowWindow command.
        public const int SW_RESTORE = 9;

        // ChildWindowFromPointEx flags.
        public const uint CWP_SKIPINVISIBLE   = 1;
        public const uint CWP_SKIPTRANSPARENT = 4;

        // Window style query - tells a form (has a title bar) from a panel.
        public const int GWL_STYLE  = -16;
        public const int WS_CAPTION = 0x00C00000;

        // =====================================================================
        //  MSAA (oleacc) constants
        // =====================================================================
        // The IAccessible IID. Kept as a field (not const - Guid cannot be const).
        public static Guid IID_IAccessible = new Guid("618736e0-3c3d-11cf-810c-00aa00389b71");
        public static Guid IID_IDispatch = new Guid("00020400-0000-0000-C000-000000000046");

        // OBJID values. Written as their unsigned 32-bit values (0xFFFFFFFC etc.).
        public const uint OBJID_CLIENT = 4294967292;
        public const uint OBJID_WINDOW = 4294967280;

        // MSAA roles this product asks about.
        public const int ROLE_GROUPING   = 20;
        public const int ROLE_STATICTEXT = 41;
        public const int ROLE_TEXT       = 42;

        // =====================================================================
        //  user32 - text messages
        // =====================================================================
        // Generic SendMessage (IntPtr lParam) via the wide entry point.
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // WM_SETTEXT overload: writes a managed string as wide chars.
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        // WM_GETTEXT overload: reads into a StringBuilder buffer (wide chars).
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, StringBuilder lParam);

        // LB_GETITEMRECT overload: the item rectangle is written back into lParam.
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW", SetLastError = true)]
        public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, ref RECT lParam);

        // PostMessage returns immediately instead of waiting for the target to
        // process the message - essential for any click that opens a modal dialog
        // this same code must then dismiss (SendMessage would deadlock).
        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "PostMessageW", SetLastError = true)]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
        public static extern int GetClassNameW(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
        public static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        // =====================================================================
        //  user32 - window state / focus
        // =====================================================================
        [DllImport("user32.dll")] public static extern bool   IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool   IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool   IsWindowEnabled(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool   IsHungAppWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SendMessageTimeout(
            IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam,
            uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

        public const uint WM_NULL = 0x0000;
        public const uint SMTO_NORMAL = 0x0000;
        public const uint SMTO_BLOCK = 0x0001;
        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public const uint SMTO_NOTIMEOUTIFNOTHUNG = 0x0008;
        [DllImport("user32.dll")] public static extern bool   SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool   ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] public static extern bool   MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll")] public static extern bool   SetWindowPos(
            IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        public static readonly IntPtr HWND_TOP = IntPtr.Zero;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);
        [DllImport("user32.dll")] public static extern bool   BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern IntPtr SetActiveWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern IntPtr GetFocus();
        [DllImport("user32.dll")] public static extern void   SwitchToThisWindow(IntPtr hWnd, bool altTab);
        [DllImport("user32.dll")] public static extern IntPtr GetDlgItem(IntPtr hDlg, int nIDDlgItem);
        public const int IDOK = 1;
        public const int IDCANCEL = 2;
        public const int IDYES = 6;
        public const int IDNO = 7;

        // =====================================================================
        //  user32 - enumeration and geometry
        // =====================================================================
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hWnd, EnumProc cb, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "FindWindowExW")]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string cls, string title);

        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] public static extern bool   GetCursorPos(out POINT p);
        [DllImport("user32.dll")] public static extern short  GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern IntPtr ChildWindowFromPointEx(IntPtr hWnd, POINT p, uint flags);
        [DllImport("user32.dll")] public static extern bool   GetWindowRect(IntPtr hWnd, out RECT r);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        // =====================================================================
        //  user32 / kernel32 - threads and process ownership
        // =====================================================================
        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        // Variant that discards the pid (pass IntPtr.Zero).
        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr pid);

        // Variant that returns the owning pid via out.
        [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

        // From the old W32Sig block.
        [DllImport("user32.dll")] public static extern int GetDlgCtrlID(IntPtr hWnd);

        // List item rectangles come back in client coordinates.
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        // Needed to reject item rectangles that sit outside the visible list area:
        // clicking those screen points would land on a neighbouring control.
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

        // =====================================================================
        //  oleacc (MSAA)
        // =====================================================================
        [DllImport("oleacc.dll")]
        public static extern int AccessibleObjectFromWindow(
            IntPtr hwnd, uint id, ref Guid iid,
            [MarshalAs(UnmanagedType.IUnknown)] out object ppvObject);

        // Enumerates the children of an accessible object - the only way to reach a
        // control that has no window handle (a VB6 windowless label, for example).
        // Each slot comes back either as another IAccessible or as a boxed int (a
        // "simple element": a child addressed only by id on its parent).
        [DllImport("oleacc.dll")]
        public static extern int AccessibleChildren(
            [MarshalAs(UnmanagedType.IUnknown)] object paccContainer,
            int iChildStart, int cChildren,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] object[] rgvarChildren,
            out int pcObtained);

        // =====================================================================
        //  SAFE WRAPPERS
        // =====================================================================

        // Get-W32WindowText: a control's text, read through WM_GETTEXTLENGTH +
        // WM_GETTEXT so it works on controls whose text GetWindowText cannot reach
        // cross-process. Returns "" when the control has no text.
        public static string GetWindowTextValue(IntPtr handle)
        {
            int len = (int)SendMessage(handle, WM_GETTEXTLENGTH, IntPtr.Zero, IntPtr.Zero);
            if (len <= 0) { return ""; }
            StringBuilder sb = new StringBuilder(len + 2);
            SendMessage(handle, WM_GETTEXT, (IntPtr)(len + 1), sb);
            return sb.ToString();
        }

        // Get-W32ClassName: the window class of a control.
        public static string GetClassName(IntPtr handle)
        {
            StringBuilder sb = new StringBuilder(256);
            GetClassNameW(handle, sb, 256);
            return sb.ToString();
        }

        // The top-of-window title (GetWindowTextW), for enumerating top-level
        // windows by caption. Distinct from GetWindowTextValue, which pulls a
        // control's text via WM_GETTEXT.
        public static string GetWindowTitle(IntPtr handle)
        {
            StringBuilder sb = new StringBuilder(512);
            GetWindowTextW(handle, sb, 512);
            return sb.ToString();
        }

        // EnumChildWindows helper: every descendant handle of a window, flat.
        // Walks the WHOLE subtree (which is what field discovery wants). The
        // callback is held in a local for the duration of the synchronous call.
        public static List<IntPtr> EnumChildWindowsList(IntPtr parent)
        {
            List<IntPtr> result = new List<IntPtr>();
            EnumProc cb = delegate(IntPtr h, IntPtr l)
            {
                result.Add(h);
                return true;
            };
            EnumChildWindows(parent, cb, IntPtr.Zero);
            GC.KeepAlive(cb);
            return result;
        }

        // EnumWindows helper: every top-level window handle on the desktop.
        public static List<IntPtr> EnumWindowsList()
        {
            List<IntPtr> result = new List<IntPtr>();
            EnumProc cb = delegate(IntPtr h, IntPtr l)
            {
                result.Add(h);
                return true;
            };
            EnumWindows(cb, IntPtr.Zero);
            GC.KeepAlive(cb);
            return result;
        }

        // Get-W32WindowPid: the process id owning a window.
        public static int GetWindowProcessId(IntPtr handle)
        {
            uint procId = 0;
            GetWindowThreadProcessId(handle, out procId);
            return (int)procId;
        }

        // ---- Win32 menus (VB6 / Coding.exe MDI frame) -------------------------
        public const uint MF_BYPOSITION = 0x00000400;
        public const uint MF_POPUP      = 0x00000010;
        public const int  SW_HIDE       = 0;
        public const int  SW_SHOW       = 5;
        public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        public const uint MOUSEEVENTF_LEFTUP   = 0x0004;

        [DllImport("user32.dll")] public static extern IntPtr GetMenu(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern int GetMenuItemCount(IntPtr hMenu);
        [DllImport("user32.dll")] public static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetMenuString(IntPtr hMenu, uint uIDItem, StringBuilder lpString, int nMaxCount, uint uFlag);
        [DllImport("user32.dll")]
        public static extern bool GetMenuItemRect(IntPtr hWnd, IntPtr hMenu, uint uItem, out RECT lprcItem);
        [DllImport("user32.dll")] public static extern int MenuItemFromPoint(IntPtr hWnd, IntPtr hMenu, POINT pt);
        // MN_GETHMENU: retrieve HMENU from a visible #32768 popup menu window.
        public const uint MN_GETHMENU = 0x01E1;
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
        [DllImport("user32.dll")]
        public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
        [DllImport("user32.dll")] public static extern uint GetMenuItemID(IntPtr hMenu, int nPos);

        [DllImport("oleacc.dll")]
        public static extern int AccessibleObjectFromPoint(
            POINT pt,
            [MarshalAs(UnmanagedType.Interface)] out object ppacc,
            out object pvarChild);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        public const uint GA_ROOT = 2;

        public const int VK_LBUTTON = 0x01;
        public const int VK_ESCAPE = 0x1B;
        public const int VK_F6 = 0x75;
        public const uint WM_KEYDOWN = 0x0100;
        public const uint WM_KEYUP = 0x0101;
        public const uint WM_CHAR = 0x0102;
        public const uint KEYEVENTF_KEYUP = 0x0002;
        public const uint WM_MDIACTIVATE = 0x0222;
        public const uint MAPVK_VK_TO_VSC = 0;

        [DllImport("user32.dll")]
        public static extern uint MapVirtualKey(uint uCode, uint uMapType);
        public const uint WM_USER = 0x0400;
        public const uint SB_GETPARTS = WM_USER + 6;
        public const uint SB_GETTEXTLENGTHW = WM_USER + 12;
        public const uint SB_GETTEXTW = WM_USER + 13;
        public const uint PW_CLIENTONLY = 0x00000001;
        public const uint PW_RENDERFULLCONTENT = 0x00000002;

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
    }
}
