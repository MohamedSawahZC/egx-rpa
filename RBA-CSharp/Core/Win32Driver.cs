// =============================================================================
//  Win32Driver.cs - high-level window actions for the compiled RBA engine.
//  Ported from RBA/_shared/Win32Driver.ps1 (actions + enumeration helpers).
//  Unicode only via SendMessageW. BM_CLICK is posted by default (modal-safe).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace RbaCore
{
    public class W32WindowInfo
    {
        public IntPtr Handle;
        public string Class;
        public string Text;
        public string Title;
        public bool Visible;
        public int ProcessId;
    }

    public class W32ActionResult
    {
        public bool Ok;
        public string Expected;
        public string Actual;
        public string Note;
    }

    public static class Win32Driver
    {
        public static void AssertHandle(IntPtr handle, string what)
        {
            if (handle == IntPtr.Zero || !Win32Interop.IsWindow(handle))
            {
                throw new Exception("dead window handle for " + what);
            }
        }

        public static string GetClassName(IntPtr handle)
        {
            return Win32Interop.GetClassName(handle);
        }

        public static string GetWindowText(IntPtr handle)
        {
            return Win32Interop.GetWindowTextValue(handle);
        }

        public static string GetWindowTitle(IntPtr handle)
        {
            return Win32Interop.GetWindowTitle(handle);
        }

        public static int GetProcessId(IntPtr handle)
        {
            return Win32Interop.GetWindowProcessId(handle);
        }

        public static RectInfo GetRect(IntPtr handle)
        {
            RECT r;
            Win32Interop.GetWindowRect(handle, out r);
            RectInfo info = new RectInfo();
            info.X = r.Left;
            info.Y = r.Top;
            info.W = r.Right - r.Left;
            info.H = r.Bottom - r.Top;
            info.Right = r.Right;
            info.Bottom = r.Bottom;
            info.CenterX = (r.Left + r.Right) / 2;
            info.CenterY = (r.Top + r.Bottom) / 2;
            return info;
        }

        public class RectInfo
        {
            public int X, Y, W, H, Right, Bottom, CenterX, CenterY;
        }

        public static List<W32WindowInfo> GetChildren(IntPtr parent)
        {
            List<W32WindowInfo> list = new List<W32WindowInfo>();
            List<IntPtr> handles = Win32Interop.EnumChildWindowsList(parent);
            for (int i = 0; i < handles.Count; i++)
            {
                IntPtr h = handles[i];
                W32WindowInfo w = new W32WindowInfo();
                w.Handle = h;
                w.Class = GetClassName(h);
                w.Text = GetWindowText(h);
                w.Visible = Win32Interop.IsWindowVisible(h);
                list.Add(w);
            }
            return list;
        }

        public static List<W32WindowInfo> GetDirectChildren(IntPtr parent)
        {
            List<W32WindowInfo> list = new List<W32WindowInfo>();
            IntPtr h = Win32Interop.GetWindow(parent, Win32Interop.GW_CHILD);
            int guard = 0;
            while (h != IntPtr.Zero && guard < 5000)
            {
                guard++;
                W32WindowInfo w = new W32WindowInfo();
                w.Handle = h;
                w.Class = GetClassName(h);
                w.Text = GetWindowText(h);
                w.Visible = Win32Interop.IsWindowVisible(h);
                list.Add(w);
                h = Win32Interop.GetWindow(h, Win32Interop.GW_HWNDNEXT);
            }
            return list;
        }

        public static List<W32WindowInfo> GetAppWindows(string titleLike)
        {
            List<W32WindowInfo> list = new List<W32WindowInfo>();
            List<IntPtr> tops = Win32Interop.EnumWindowsList();
            for (int i = 0; i < tops.Count; i++)
            {
                IntPtr h = tops[i];
                if (!Win32Interop.IsWindowVisible(h)) { continue; }
                string t = GetWindowTitle(h);
                if (string.IsNullOrEmpty(t)) { continue; }
                bool match = false;
                try { match = Regex.IsMatch(t, titleLike); }
                catch { match = t.IndexOf(titleLike, StringComparison.OrdinalIgnoreCase) >= 0; }
                if (!match) { continue; }
                W32WindowInfo w = new W32WindowInfo();
                w.Handle = h;
                w.Title = t;
                w.Class = GetClassName(h);
                w.Text = t;
                w.Visible = true;
                w.ProcessId = GetProcessId(h);
                list.Add(w);
            }
            return list;
        }

        public static IntPtr FindTopWindow(string titleLike, int timeoutMs)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            do
            {
                List<W32WindowInfo> wins = GetAppWindows(titleLike);
                if (wins.Count > 0) { return wins[0].Handle; }
                Thread.Sleep(200);
            } while (DateTime.UtcNow < deadline);
            return IntPtr.Zero;
        }

        // Forms inside an MDI / top window (VB6 ThunderRT6FormDC, dialogs, etc.).
        public static List<W32WindowInfo> GetFormWindows(IntPtr window, int maxDepth)
        {
            return GetFormWindows(window, maxDepth, false);
        }

        // includeHidden: VB6 MDI children stay alive after «الإستعلام» covers them.
        public static List<W32WindowInfo> GetFormWindows(IntPtr window, int maxDepth, bool includeHidden)
        {
            List<W32WindowInfo> forms = new List<W32WindowInfo>();
            CollectForms(window, 0, maxDepth, forms, includeHidden);
            return forms;
        }

        private static void CollectForms(
            IntPtr parent, int depth, int maxDepth, List<W32WindowInfo> forms, bool includeHidden)
        {
            if (depth > maxDepth) { return; }
            List<W32WindowInfo> kids = GetDirectChildren(parent);
            for (int i = 0; i < kids.Count; i++)
            {
                W32WindowInfo k = kids[i];
                string c = k.Class != null ? k.Class : "";
                bool isForm = false;
                if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?FormDC")) { isForm = true; }
                else if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?MDIForm")) { isForm = true; }
                else if (c == "#32770") { isForm = true; }
                else if (c.StartsWith("TForm", StringComparison.OrdinalIgnoreCase)) { isForm = true; }
                // Never treat bare MDIClient as an entry form (it has no KYC fields).
                else if (string.Equals(c, "MDIClient", StringComparison.OrdinalIgnoreCase)) { isForm = false; }
                else if (c.IndexOf("WindowsForms10.Window", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    int style = Win32Interop.GetWindowLong(k.Handle, Win32Interop.GWL_STYLE);
                    if ((style & Win32Interop.WS_CAPTION) != 0) { isForm = true; }
                }

                if (isForm && (includeHidden || k.Visible))
                {
                    forms.Add(k);
                }
                CollectForms(k.Handle, depth + 1, maxDepth, forms, includeHidden);
            }
        }

        public static void SetForeground(IntPtr handle)
        {
            AssertHandle(handle, "foreground target");
            Win32Interop.ShowWindow(handle, Win32Interop.SW_RESTORE);
            Win32Interop.SetForegroundWindow(handle);
            Win32Interop.BringWindowToTop(handle);
        }

        // VB6 MDI: GetForegroundWindow is always the frame. Activate the child so
        // F-keys land on الإستعلام, not the empty MDI frame.
        public static void ActivateMdiChild(IntPtr frame, IntPtr child)
        {
            if (child == IntPtr.Zero || !Win32Interop.IsWindow(child)) { return; }
            IntPtr client = FindChildByClass(frame, "MDIClient", 4);
            if (client != IntPtr.Zero)
            {
                Win32Interop.SendMessage(client, Win32Interop.WM_MDIACTIVATE, child, IntPtr.Zero);
            }
            try { SetForeground(frame); } catch { }
            Win32Interop.BringWindowToTop(child);
            Win32Interop.ShowWindow(child, Win32Interop.SW_RESTORE);
            try { Win32Interop.SetFocus(child); } catch { }
        }

        public static IntPtr FindChildByClass(IntPtr root, string className, int maxDepth)
        {
            if (root == IntPtr.Zero || string.IsNullOrEmpty(className)) { return IntPtr.Zero; }
            if (maxDepth < 1) { maxDepth = 1; }
            return FindChildByClassWalk(root, className, 0, maxDepth);
        }

        private static IntPtr FindChildByClassWalk(IntPtr parent, string className, int depth, int maxDepth)
        {
            if (depth > maxDepth) { return IntPtr.Zero; }
            List<W32WindowInfo> kids = GetDirectChildren(parent);
            for (int i = 0; i < kids.Count; i++)
            {
                string c = kids[i].Class != null ? kids[i].Class : "";
                if (string.Equals(c, className, StringComparison.OrdinalIgnoreCase))
                {
                    return kids[i].Handle;
                }
            }
            for (int i = 0; i < kids.Count; i++)
            {
                IntPtr nested = FindChildByClassWalk(kids[i].Handle, className, depth + 1, maxDepth);
                if (nested != IntPtr.Zero) { return nested; }
            }
            return IntPtr.Zero;
        }

        public static W32ActionResult SetText(IntPtr handle, string text)
        {
            if (text == null) { text = ""; }
            AssertHandle(handle, "edit field");
            Win32Interop.SendMessage(handle, Win32Interop.WM_SETTEXT, IntPtr.Zero, text);
            Thread.Sleep(40);
            string actual = GetWindowText(handle);
            W32ActionResult r = new W32ActionResult();
            r.Ok = (actual == text);
            r.Expected = text;
            r.Actual = actual;
            return r;
        }

        public static void Click(IntPtr handle, int settleMs, bool sync)
        {
            AssertHandle(handle, "button");
            if (sync)
            {
                Win32Interop.SendMessage(handle, Win32Interop.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                Win32Interop.PostMessage(handle, Win32Interop.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
            }
            Thread.Sleep(settleMs);
        }

        public static void Click(IntPtr handle)
        {
            Click(handle, 200, false);
        }

        // Physical mouse click at control center (VB6 often ignores BM_CLICK for dialogs).
        public static void ClickPhysical(IntPtr handle, int settleMs)
        {
            AssertHandle(handle, "physical click");
            try { SetForeground(handle); } catch { }
            Thread.Sleep(80);
            RectInfo rect = GetRect(handle);
            if (rect.W <= 0 || rect.H <= 0)
            {
                Click(handle, settleMs, false);
                return;
            }
            ClickScreen(rect.CenterX, rect.CenterY, settleMs);
        }

        // Physical double-click (focus + select) — used for late-appearing VB6 fields like «الوظيفة».
        public static void DoubleClickPhysical(IntPtr handle, int settleMs)
        {
            AssertHandle(handle, "physical double-click");
            try { SetForeground(handle); } catch { }
            Thread.Sleep(80);
            RectInfo rect = GetRect(handle);
            if (rect.W <= 0 || rect.H <= 0)
            {
                Click(handle, 80, false);
                Thread.Sleep(60);
                Click(handle, settleMs, false);
                return;
            }
            int x = rect.CenterX;
            int y = rect.CenterY;
            Win32Interop.SetCursorPos(x, y);
            Thread.Sleep(40);
            // First click
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(60);
            // Second click (double-click)
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(settleMs);
        }

        // Physical double-click at a point — list rows have no HWND of their own.
        public static void DoubleClickScreen(int x, int y, int settleMs)
        {
            Win32Interop.SetCursorPos(x, y);
            Thread.Sleep(40);
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(25);
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(50);
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(25);
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(settleMs);
        }

        // Physical screen click (menu bar / popup items have no useful child HWND).
        public static void ClickScreen(int x, int y, int settleMs)
        {
            Win32Interop.SetCursorPos(x, y);
            Thread.Sleep(40);
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            Win32Interop.mouse_event(Win32Interop.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(settleMs);
        }

        public static int ToSignedInt32(IntPtr result)
        {
            long low = ((long)result.ToInt64()) & 0xFFFFFFFFL;
            if (low >= 0x80000000L) { return (int)(low - 0x100000000L); }
            return (int)low;
        }

        public static W32ActionResult SelectComboItem(IntPtr handle, string value)
        {
            W32ActionResult r = new W32ActionResult();
            if (string.IsNullOrWhiteSpace(value))
            {
                r.Ok = true;
                r.Note = "skipped (empty)";
                return r;
            }
            AssertHandle(handle, "combo box");

            string[] candidates = ComboCandidates(value);
            int idx = -1;
            string matched = null;
            for (int c = 0; c < candidates.Length && idx < 0; c++)
            {
                string tryVal = candidates[c];
                idx = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.CB_FINDSTRINGEXACT, (IntPtr)(-1), tryVal));
                if (idx >= 0) { matched = tryVal; break; }
                idx = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.CB_FINDSTRING, (IntPtr)(-1), tryVal));
                if (idx >= 0) { matched = tryVal; break; }
            }
            if (idx < 0)
            {
                idx = FindComboContains(handle, value);
                if (idx >= 0) { matched = value; }
            }
            if (idx < 0)
            {
                r.Ok = false;
                r.Note = "item not in list: " + value;
                return r;
            }
            Win32Interop.SendMessage(handle, Win32Interop.CB_SETCURSEL, (IntPtr)idx, IntPtr.Zero);
            Thread.Sleep(60);
            int now = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.CB_GETCURSEL, IntPtr.Zero, IntPtr.Zero));
            r.Ok = (now == idx);
            r.Note = r.Ok
                ? ("index " + idx + (matched != null && matched != value ? (" via '" + matched + "'") : ""))
                : ("CB_SETCURSEL did not stick (got " + now + ")");
            return r;
        }

        // Sheet labels often differ slightly from Coding / replica combo captions.
        private static string[] ComboCandidates(string value)
        {
            List<string> list = new List<string>();
            list.Add(value);
            string v = value.Trim();
            if (v.IndexOf("قوم", StringComparison.Ordinal) >= 0)
            {
                list.Add("بطاقة رقم قومي");
                list.Add("رقم قومي");
                list.Add("رقم قومى");
            }
            if (v == "فرد" || v.IndexOf("طبيعي", StringComparison.Ordinal) >= 0)
            {
                list.Add("طبيعي");
                list.Add("فرد");
            }
            if (v == "مصر" || v.IndexOf("مصر", StringComparison.Ordinal) >= 0)
            {
                list.Add("مصر");
                list.Add("القاهرة");
            }
            return list.ToArray();
        }

        private static int FindComboContains(IntPtr handle, string value)
        {
            if (string.IsNullOrEmpty(value)) { return -1; }
            int count = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.CB_GETCOUNT, IntPtr.Zero, IntPtr.Zero));
            for (int i = 0; i < count; i++)
            {
                int len = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.CB_GETLBTEXTLEN, (IntPtr)i, IntPtr.Zero));
                if (len <= 0) { continue; }
                System.Text.StringBuilder sb = new System.Text.StringBuilder(len + 2);
                Win32Interop.SendMessage(handle, Win32Interop.CB_GETLBTEXT, (IntPtr)i, sb);
                string item = sb.ToString();
                if (item.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    value.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return i;
                }
            }
            return -1;
        }

        // Top-level windows owned by a process (includes empty titles — common for VB6 pickers).
        public static List<W32WindowInfo> GetProcessTopWindows(int processId)
        {
            List<W32WindowInfo> list = new List<W32WindowInfo>();
            if (processId <= 0) { return list; }
            List<IntPtr> tops = Win32Interop.EnumWindowsList();
            for (int i = 0; i < tops.Count; i++)
            {
                IntPtr h = tops[i];
                if (!Win32Interop.IsWindowVisible(h)) { continue; }
                if (GetProcessId(h) != processId) { continue; }
                W32WindowInfo w = new W32WindowInfo();
                w.Handle = h;
                w.Title = GetWindowTitle(h);
                w.Class = GetClassName(h);
                w.Text = w.Title;
                w.Visible = true;
                w.ProcessId = processId;
                list.Add(w);
            }
            return list;
        }

        public static W32ActionResult SelectListBoxItem(IntPtr handle, string value)
        {
            W32ActionResult r = new W32ActionResult();
            if (string.IsNullOrWhiteSpace(value))
            {
                r.Ok = true;
                r.Note = "skipped (empty)";
                return r;
            }
            AssertHandle(handle, "list box");
            string want = value.Trim();
            int idx = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.LB_FINDSTRINGEXACT, (IntPtr)(-1), want));
            if (idx < 0)
            {
                idx = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.LB_FINDSTRING, (IntPtr)(-1), want));
            }
            if (idx < 0)
            {
                // File names sometimes appear without extension, or with brackets in DirList.
                int count = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero));
                string wantNoExt = Path.GetFileNameWithoutExtension(want);
                for (int i = 0; i < count && idx < 0; i++)
                {
                    int len = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.LB_GETTEXTLEN, (IntPtr)i, IntPtr.Zero));
                    if (len <= 0) { continue; }
                    System.Text.StringBuilder sb = new System.Text.StringBuilder(len + 2);
                    Win32Interop.SendMessage(handle, Win32Interop.LB_GETTEXT, (IntPtr)i, sb);
                    string item = sb.ToString().Trim();
                    if (string.Equals(item, want, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(item, wantNoExt, StringComparison.OrdinalIgnoreCase) ||
                        item.IndexOf(want, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        (!string.IsNullOrEmpty(wantNoExt) && item.IndexOf(wantNoExt, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        idx = i;
                    }
                }
            }
            if (idx < 0)
            {
                r.Ok = false;
                r.Note = "item not in list: " + value;
                return r;
            }
            Win32Interop.SendMessage(handle, Win32Interop.LB_SETTOPINDEX, (IntPtr)idx, IntPtr.Zero);
            Win32Interop.SendMessage(handle, Win32Interop.LB_SETCURSEL, (IntPtr)idx, IntPtr.Zero);
            Thread.Sleep(60);
            int now = ToSignedInt32(Win32Interop.SendMessage(handle, Win32Interop.LB_GETCURSEL, IntPtr.Zero, IntPtr.Zero));
            r.Ok = (now == idx);
            r.Note = r.Ok ? ("index " + idx) : ("LB_SETCURSEL did not stick (got " + now + ")");
            return r;
        }

        public static bool GetCheckState(IntPtr handle)
        {
            return ((int)Win32Interop.SendMessage(handle, Win32Interop.BM_GETCHECK, IntPtr.Zero, IntPtr.Zero)) == 1;
        }

        public static bool SetCheck(IntPtr handle, bool checkedState)
        {
            if (GetCheckState(handle) != checkedState)
            {
                Click(handle, 150, true);
            }
            return GetCheckState(handle) == checkedState;
        }

        public static void Highlight(IntPtr handle, int flashes)
        {
            RectInfo rect = GetRect(handle);
            if (rect.W <= 0 || rect.H <= 0) { return; }
            // Lightweight flash via brief foreground focus; full overlay is UI-side.
            try { SetForeground(handle); } catch { }
            for (int i = 0; i < flashes; i++)
            {
                Thread.Sleep(120);
            }
        }

        // Kept as a thin wrapper — ConfirmDialog owns the caption/id rules.
        public static IntPtr WaitDialog(int ownerProcessId, int timeoutMs, bool clickOk)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                IntPtr h = ConfirmDialog.FindModal(ownerProcessId, IntPtr.Zero);
                if (h != IntPtr.Zero)
                {
                    if (clickOk)
                    {
                        string clicked;
                        ConfirmDialog.ClickAffirmative(h, out clicked);
                    }
                    return h;
                }
                Thread.Sleep(150);
            }
            return IntPtr.Zero;
        }

        public static void SendShortcut(IntPtr handle, string keys, int settleMs)
        {
            AssertHandle(handle, "shortcut target");
            CodingHealthState health = CodingHealth.Probe(handle, 1500);
            if (health != CodingHealthState.Ok)
            {
                throw new InvalidOperationException(
                    "shortcut aborted — " + CodingHealth.Describe(health));
            }
            SetForeground(handle);
            Thread.Sleep(80);

            // Prefer non-blocking F-keys (SendKeys.SendWait can hang forever on a frozen UI).
            int vk;
            if (TryMapFunctionKey(keys, out vk))
            {
                try { SetForeground(handle); } catch { }
                TapVirtualKey(vk);
                Thread.Sleep(Math.Max(0, settleMs));
                return;
            }

            Exception err = null;
            Thread worker = new Thread(delegate()
            {
                try { System.Windows.Forms.SendKeys.SendWait(keys); }
                catch (Exception ex) { err = ex; }
            });
            worker.IsBackground = true;
            try { worker.SetApartmentState(ApartmentState.STA); } catch { }
            worker.Start();
            if (!worker.Join(CodingHealth.ShortcutTimeoutMs))
            {
                throw new TimeoutException(
                    "SendKeys timed out after " + CodingHealth.ShortcutTimeoutMs +
                    "ms (Coding may be frozen) keys=" + keys);
            }
            if (err != null) { throw err; }
            Thread.Sleep(Math.Max(0, settleMs));
        }

        private static bool TryMapFunctionKey(string keys, out int vk)
        {
            vk = 0;
            if (string.IsNullOrWhiteSpace(keys)) { return false; }
            string k = keys.Trim().ToUpperInvariant();
            if (k == "{F2}") { vk = 0x71; return true; }
            if (k == "{F6}") { vk = 0x75; return true; }
            if (k == "{F9}") { vk = 0x78; return true; }
            if (k == "{ENTER}" || k == "~") { vk = 0x0D; return true; }
            if (k == "{ESC}" || k == "{ESCAPE}") { vk = 0x1B; return true; }
            return false;
        }

        // VB6 MDI often ignores SendKeys; PostMessage lands on the HWND we name.
        public static void PostVirtualKey(IntPtr handle, int vk)
        {
            if (handle == IntPtr.Zero || !Win32Interop.IsWindow(handle)) { return; }
            uint scan = Win32Interop.MapVirtualKey((uint)vk, Win32Interop.MAPVK_VK_TO_VSC);
            int down = 1 | ((int)scan << 16);
            int up = down | (1 << 30) | (1 << 31);
            IntPtr w = (IntPtr)vk;
            Win32Interop.PostMessage(handle, Win32Interop.WM_KEYDOWN, w, (IntPtr)down);
            Win32Interop.PostMessage(handle, Win32Interop.WM_KEYUP, w, (IntPtr)up);
        }

        // Hardware tap after SetForeground — last resort when PostMessage is ignored.
        public static void TapVirtualKey(int vk)
        {
            byte b = (byte)vk;
            Win32Interop.keybd_event(b, 0, 0, UIntPtr.Zero);
            Thread.Sleep(30);
            Win32Interop.keybd_event(b, 0, Win32Interop.KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        public static Process StartProcess(string exePath, string arguments, string workingDir)
        {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = exePath;
            if (!string.IsNullOrEmpty(arguments)) { psi.Arguments = arguments; }
            if (!string.IsNullOrEmpty(workingDir) && System.IO.Directory.Exists(workingDir))
            {
                psi.WorkingDirectory = workingDir;
            }
            else
            {
                psi.WorkingDirectory = System.IO.Path.GetDirectoryName(exePath);
            }
            psi.UseShellExecute = true;
            return Process.Start(psi);
        }
    }
}
