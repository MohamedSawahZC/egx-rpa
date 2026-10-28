// =============================================================================
//  UiControlPicker.cs - identify menu items / buttons under a screen point.
//  Identity is Caption + Class + ClassOrdinal (never HWND — survives reopen).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace RbaCore
{
    public enum UiPickKind
    {
        MenuItem = 0,
        Button = 1,
        Control = 2,
        CheckBox = 3
    }

    public class UiPickResult
    {
        public bool Ok;
        public bool Cancelled;
        public string Note;
        public UiPickKind Kind;
        public string ControlKind;
        public string Caption;
        public string Class;
        public string ClassOrdinal;
        public string ControlId;
        public string AccName;
        public string Label;
        public string Index;
        // Form-relative geometry — the identity that survives layout changes.
        public IntPtr FormRoot;
        public IntPtr Control;
        public FieldSelector AsSelector;
        // True when WindowFromPoint hit the form (VB6 windowless CommandButton).
        public bool WindowlessClick;
        public int ClickScreenX;
        public int ClickScreenY;
    }

    public static class UiControlPicker
    {
        // Resolve what sits under a desktop point, preferring menu captions then buttons.
        public static UiPickResult IdentifyAtPoint(POINT screenPt, IntPtr preferredApp, UiPickKind prefer)
        {
            UiPickResult r = new UiPickResult();

            if (prefer == UiPickKind.MenuItem)
            {
                string menuCaption = FindMenuCaptionAtPoint(preferredApp, screenPt);
                if (string.IsNullOrEmpty(menuCaption))
                {
                    menuCaption = FindAccessibleNameAtPoint(screenPt);
                }
                if (!string.IsNullOrEmpty(menuCaption))
                {
                    r.Ok = true;
                    r.Kind = UiPickKind.MenuItem;
                    r.Caption = CleanMenuCaption(menuCaption);
                    r.Note = "menu item by caption";
                    return r;
                }
                r.Note = "menu item not found under cursor";
                return r;
            }

            IntPtr hit = Win32Interop.WindowFromPoint(screenPt);
            if (hit == IntPtr.Zero || !Win32Interop.IsWindow(hit))
            {
                r.Note = "nothing under cursor";
                return r;
            }
            r.ClickScreenX = screenPt.X;
            r.ClickScreenY = screenPt.Y;

            IntPtr target;
            if (prefer == UiPickKind.Button)
            {
                target = PreferButtonAncestor(hit);
            }
            else if (prefer == UiPickKind.CheckBox)
            {
                target = PreferCheckBoxAncestor(hit);
            }
            else
            {
                target = PreferInputAncestor(hit);
            }

            string cls = Win32Driver.GetClassName(target);
            string caption = Win32Driver.GetWindowText(target);
            if (string.IsNullOrWhiteSpace(caption))
            {
                caption = Win32Interop.GetWindowTextValue(target);
            }

            string kind = FieldDiscovery.GetControlKind(cls);
            bool looksCheck = kind == "CheckBox" ||
                cls.IndexOf("CheckBox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cls.IndexOf("Check", StringComparison.OrdinalIgnoreCase) >= 0;
            bool looksButton = !looksCheck && (kind == "Button" ||
                cls.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0 ||
                cls.IndexOf("TBitBtn", StringComparison.OrdinalIgnoreCase) >= 0);

            if (prefer == UiPickKind.CheckBox && !looksCheck)
            {
                r.Note = "انقر مربع اختيار (CheckBox) — حصلت على " + cls + " / " + kind;
                return r;
            }

            if (prefer == UiPickKind.Button && !looksButton)
            {
                IntPtr form = FindNearestForm(hit);
                if (form == IntPtr.Zero) { form = preferredApp; }
                if (form == IntPtr.Zero) { form = Win32Interop.GetAncestor(hit, Win32Interop.GA_ROOT); }
                r.Ok = true;
                r.Kind = UiPickKind.Button;
                r.ControlKind = "Button";
                r.FormRoot = form;
                r.Control = form;
                r.Caption = FindAccessibleNameAtPoint(screenPt);
                r.Class = "";
                r.ClassOrdinal = "";
                r.Index = "";
                r.WindowlessClick = true;
                r.ClickScreenX = screenPt.X;
                r.ClickScreenY = screenPt.Y;
                r.Note = "windowless button at click " + screenPt.X + "," + screenPt.Y +
                    (string.IsNullOrWhiteSpace(r.Caption) ? "" : " «" + r.Caption + "»");
                r.AsSelector = BuildNavSelector(r);
                return r;
            }

            IntPtr root = preferredApp;
            if (root == IntPtr.Zero || !Win32Interop.IsWindow(root))
            {
                root = Win32Interop.GetAncestor(target, Win32Interop.GA_ROOT);
            }

            // Prefer the nearest form — then take ClassOrdinal/Index from GetFieldMap
            // so pick identity MATCHES resolve (1-based ordinals, same child walk).
            IntPtr ordinalRoot = FindNearestForm(target);
            if (ordinalRoot == IntPtr.Zero) { ordinalRoot = root; }

            AppField mapped = FindInFieldMap(ordinalRoot, target);
            if (mapped == null && ordinalRoot != root)
            {
                mapped = FindInFieldMap(root, target);
            }

            int ctrlId = 0;
            try { ctrlId = Win32Interop.GetDlgCtrlID(target); } catch { }

            r.Ok = true;
            r.FormRoot = ordinalRoot;
            r.Control = target;
            if (looksCheck) { r.Kind = UiPickKind.CheckBox; }
            else if (looksButton) { r.Kind = UiPickKind.Button; }
            else { r.Kind = UiPickKind.Control; }
            r.ControlKind = string.IsNullOrEmpty(kind)
                ? (looksCheck ? "CheckBox" : (looksButton ? "Button" : "Control"))
                : kind;
            r.Class = mapped != null ? mapped.Class : cls;
            r.ControlId = mapped != null && mapped.ControlId > 0
                ? mapped.ControlId.ToString()
                : (ctrlId > 0 ? ctrlId.ToString() : "");

            if (mapped != null)
            {
                r.ClassOrdinal = mapped.ClassOrdinal.ToString(); // 1-based — same as ResolveField
                r.Label = !string.IsNullOrWhiteSpace(mapped.Label) ? mapped.Label.Trim() : "";
                // Keep stable captions for buttons AND checkboxes (e.g. «ملفات»).
                // NEVER store live TextBox/Combo values as Caption — that poisons resolve.
                if (looksButton || looksCheck || kind == "Button" || kind == "CheckBox")
                {
                    r.Caption = !string.IsNullOrWhiteSpace(mapped.Caption) ? mapped.Caption.Trim() : "";
                    if (string.IsNullOrWhiteSpace(r.Caption) && !string.IsNullOrWhiteSpace(caption))
                    {
                        r.Caption = caption.Trim();
                    }
                }
                else
                {
                    r.Caption = "";
                }
                r.Note = "mapped via FieldDiscovery ClassOrdinal=" + r.ClassOrdinal + " Index=" + mapped.Index;
                r.AsSelector = BuildNavSelector(r);
                r.Index = mapped.Index.ToString();
                return r;
            }

            // Fallback only if map miss (should be rare).
            int ordinal = ComputeClassOrdinal(ordinalRoot, target, cls);
            r.ClassOrdinal = (ordinal + 1).ToString(); // convert 0-based walk → 1-based
            r.Caption = (looksButton || looksCheck) ? (caption != null ? caption.Trim() : "") : "";
            r.Label = FindNearbyLabelText(ordinalRoot, target);
            r.Index = "";
            r.Note = looksCheck ? "checkbox fallback" : (looksButton ? "button fallback" : "control fallback");
            r.AsSelector = BuildNavSelector(r);
            return r;
        }

        private static AppField FindInFieldMap(IntPtr root, IntPtr target)
        {
            if (root == IntPtr.Zero || target == IntPtr.Zero) { return null; }
            try
            {
                FieldMap map = FieldDiscovery.GetFieldMap(root, false);
                if (map == null) { return null; }
                if (map.Fields != null)
                {
                    for (int i = 0; i < map.Fields.Count; i++)
                    {
                        if (map.Fields[i].Handle == target) { return map.Fields[i]; }
                    }
                }
                if (map.AllControls != null)
                {
                    for (int i = 0; i < map.AllControls.Count; i++)
                    {
                        if (map.AllControls[i].Handle == target) { return map.AllControls[i]; }
                    }
                }
            }
            catch { }
            return null;
        }

        public static FieldSelector BuildNavSelector(UiPickResult pick)
        {
            return BuildFieldSelector(pick, "navNewClient", "إدخال بيانات العملاء", "", "", "");
        }

        public static FieldSelector BuildFieldSelector(
            UiPickResult pick,
            string fieldKey,
            string fieldLabel,
            string sheetColumn,
            string datePart,
            string screen)
        {
            FieldSelector s = new FieldSelector();
            s.FieldKey = fieldKey;
            s.FieldLabel = string.IsNullOrEmpty(fieldLabel)
                ? (string.IsNullOrEmpty(pick.Label) ? fieldKey : pick.Label)
                : fieldLabel;
            s.Kind = string.IsNullOrEmpty(pick.ControlKind)
                ? (pick.Kind == UiPickKind.Button ? "Button"
                    : (pick.Kind == UiPickKind.CheckBox ? "CheckBox" : "TextBox"))
                : pick.ControlKind;
            s.Class = pick.Class;
            // Buttons + checkboxes keep stable caption (e.g. «ملفات»); inputs must NOT.
            bool keepCaption =
                string.Equals(s.Kind, "Button", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Kind, "CheckBox", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(s.Kind, "RadioButton", StringComparison.OrdinalIgnoreCase);
            s.Caption = keepCaption ? (pick.Caption ?? "") : "";
            // Do not store adjacent Label for resolve — duplicates like two «اسم»
            // must not steal the click. FieldLabel keeps the catalog Arabic name for UI.
            s.Label = "";
            s.ClassOrdinal = pick.ClassOrdinal;
            s.Index = pick.Index;
            s.ControlId = keepCaption ? pick.ControlId : ""; // ControlId often unstable for VB6 edits
            s.AccName = "";
            s.SheetColumn = sheetColumn != null ? sheetColumn : "";
            s.DatePart = datePart != null ? datePart : "";
            s.Screen = screen != null ? screen : "";
            s.ConfirmedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            s.SourceKey = "click-map";
            // Pull-codes grid/button must replay the exact operator click, not the HWND centre.
            bool pullClickPoint =
                string.Equals(fieldKey, PullCodes.GridKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fieldKey, PullCodes.SearchButtonKey, StringComparison.OrdinalIgnoreCase);
            if (pick.WindowlessClick || pullClickPoint)
            {
                FieldDiscovery.StampClickPoint(s, pick.FormRoot, pick.ClickScreenX, pick.ClickScreenY);
            }
            else
            {
                FieldDiscovery.StampPosition(s, pick.FormRoot, pick.Control);
            }
            return s;
        }

        private static IntPtr PreferCheckBoxAncestor(IntPtr start)
        {
            IntPtr h = start;
            IntPtr best = start;
            for (int depth = 0; depth < 12 && h != IntPtr.Zero; depth++)
            {
                string kind = FieldDiscovery.GetControlKind(Win32Driver.GetClassName(h));
                string cls = Win32Driver.GetClassName(h);
                if (kind == "CheckBox" ||
                    cls.IndexOf("CheckBox", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return h;
                }
                best = h;
                h = Win32Interop.GetParent(h);
            }
            return best;
        }

        private static IntPtr PreferInputAncestor(IntPtr start)
        {
            IntPtr h = start;
            IntPtr best = start;
            for (int i = 0; i < 8 && h != IntPtr.Zero; i++)
            {
                string cls = Win32Driver.GetClassName(h);
                string kind = FieldDiscovery.GetControlKind(cls);
                if (kind == "TextBox" || kind == "ComboBox" || kind == "CheckBox" ||
                    kind == "RadioButton" || kind == "DatePicker" || kind == "ListBox" ||
                    kind == "Button")
                {
                    return h;
                }
                if (cls.IndexOf("ThunderRT6MDIForm", StringComparison.OrdinalIgnoreCase) >= 0) { break; }
                if (cls.IndexOf("ThunderRT6Form", StringComparison.OrdinalIgnoreCase) >= 0) { break; }
                best = h;
                h = Win32Interop.GetParent(h);
            }
            return best;
        }

        private static IntPtr FindNearestForm(IntPtr start)
        {
            IntPtr h = start;
            for (int i = 0; i < 12 && h != IntPtr.Zero; i++)
            {
                string cls = Win32Driver.GetClassName(h);
                if (cls.IndexOf("ThunderRT6Form", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    cls.IndexOf("MDIForm", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return h;
                }
                if (cls.IndexOf("ThunderRT6MDIForm", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return h;
                }
                h = Win32Interop.GetParent(h);
            }
            return IntPtr.Zero;
        }

        // Best-effort adjacent label for operator feedback (not required for resolve).
        private static string FindNearbyLabelText(IntPtr root, IntPtr target)
        {
            if (root == IntPtr.Zero || target == IntPtr.Zero) { return ""; }
            try
            {
                FieldMap map = FieldDiscovery.GetFieldMap(root, false);
                if (map == null || map.Fields == null) { return ""; }
                for (int i = 0; i < map.Fields.Count; i++)
                {
                    AppField f = map.Fields[i];
                    if (f.Handle != target) { continue; }
                    if (!string.IsNullOrWhiteSpace(f.Label)) { return f.Label.Trim(); }
                    if (!string.IsNullOrWhiteSpace(f.Caption)) { return f.Caption.Trim(); }
                }
            }
            catch { }
            return "";
        }

        private static IntPtr PreferButtonAncestor(IntPtr start)
        {
            IntPtr h = start;
            for (int i = 0; i < 6 && h != IntPtr.Zero; i++)
            {
                string cls = Win32Driver.GetClassName(h);
                string kind = FieldDiscovery.GetControlKind(cls);
                if (kind == "Button") { return h; }
                if (cls.IndexOf("ThunderRT6MDIForm", StringComparison.OrdinalIgnoreCase) >= 0) { break; }
                if (cls.IndexOf("ThunderRT6Form", StringComparison.OrdinalIgnoreCase) >= 0) { break; }
                h = Win32Interop.GetParent(h);
            }
            return start;
        }

        private static int ComputeClassOrdinal(IntPtr root, IntPtr target, string cls)
        {
            if (root == IntPtr.Zero || target == IntPtr.Zero || string.IsNullOrEmpty(cls)) { return 0; }
            List<IntPtr> kids = Win32Interop.EnumChildWindowsList(root);
            int ord = 0;
            for (int i = 0; i < kids.Count; i++)
            {
                if (!Win32Interop.IsWindowVisible(kids[i])) { continue; }
                if (!string.Equals(Win32Driver.GetClassName(kids[i]), cls, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (kids[i] == target) { return ord; }
                ord++;
            }
            return 0;
        }

        private static string FindMenuCaptionAtPoint(IntPtr frame, POINT pt)
        {
            if (frame != IntPtr.Zero && Win32Interop.IsWindow(frame))
            {
                string fromBar = HitTestMenu(frame, Win32Interop.GetMenu(frame), pt);
                if (!string.IsNullOrEmpty(fromBar)) { return fromBar; }

                // Opened submenu under the frame menu.
                IntPtr bar = Win32Interop.GetMenu(frame);
                if (bar != IntPtr.Zero)
                {
                    int n = Win32Interop.GetMenuItemCount(bar);
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr sub = Win32Interop.GetSubMenu(bar, i);
                        if (sub == IntPtr.Zero) { continue; }
                        string leaf = HitTestMenu(frame, sub, pt);
                        if (!string.IsNullOrEmpty(leaf)) { return leaf; }
                    }
                }
            }

            // Visible popup menu windows (#32768).
            List<IntPtr> tops = Win32Interop.EnumWindowsList();
            for (int i = 0; i < tops.Count; i++)
            {
                if (!Win32Interop.IsWindowVisible(tops[i])) { continue; }
                string cls = Win32Driver.GetClassName(tops[i]);
                if (cls != "#32768") { continue; }
                IntPtr hMenu = Win32Interop.SendMessage(tops[i], Win32Interop.MN_GETHMENU, IntPtr.Zero, IntPtr.Zero);
                if (hMenu == IntPtr.Zero) { continue; }
                string text = HitTestMenu(tops[i], hMenu, pt);
                if (!string.IsNullOrEmpty(text)) { return text; }
            }
            return "";
        }

        private static string HitTestMenu(IntPtr hwnd, IntPtr hMenu, POINT pt)
        {
            if (hwnd == IntPtr.Zero || hMenu == IntPtr.Zero) { return ""; }
            int pos = Win32Interop.MenuItemFromPoint(hwnd, hMenu, pt);
            if (pos >= 0)
            {
                string byPoint = GetMenuItemText(hMenu, (uint)pos);
                if (!string.IsNullOrEmpty(byPoint)) { return byPoint; }
            }

            // Fallback: walk item rectangles (more reliable on some VB6 MDI bars).
            int count = Win32Interop.GetMenuItemCount(hMenu);
            for (int i = 0; i < count; i++)
            {
                RECT rc;
                if (!Win32Interop.GetMenuItemRect(hwnd, hMenu, (uint)i, out rc)) { continue; }
                if (pt.X >= rc.Left && pt.X <= rc.Right && pt.Y >= rc.Top && pt.Y <= rc.Bottom)
                {
                    return GetMenuItemText(hMenu, (uint)i);
                }
            }
            return "";
        }

        private static string GetMenuItemText(IntPtr hMenu, uint pos)
        {
            StringBuilder sb = new StringBuilder(256);
            int n = Win32Interop.GetMenuString(hMenu, pos, sb, sb.Capacity, Win32Interop.MF_BYPOSITION);
            if (n <= 0) { return ""; }
            string t = sb.ToString();
            int tab = t.IndexOf('\t');
            if (tab >= 0) { t = t.Substring(0, tab); }
            return CleanMenuCaption(t);
        }

        private static string CleanMenuCaption(string raw)
        {
            if (raw == null) { return ""; }
            string t = raw.Replace("&", "").Trim();
            int tab = t.IndexOf('\t');
            if (tab >= 0) { t = t.Substring(0, tab).Trim(); }
            return t;
        }

        // MSAA fallback for menus / toolbar items without a reliable HMENU hit.
        private static string FindAccessibleNameAtPoint(POINT pt)
        {
            try
            {
                object acc;
                object child;
                int hr = Win32Interop.AccessibleObjectFromPoint(pt, out acc, out child);
                if (hr != 0 || acc == null) { return ""; }

                object nameObj = InvokeAcc(acc, "get_accName", child);
                string name = nameObj != null ? nameObj.ToString() : "";
                object roleObj = InvokeAcc(acc, "get_accRole", child);
                int role = 0;
                if (roleObj is int) { role = (int)roleObj; }
                else if (roleObj != null) { int.TryParse(roleObj.ToString(), out role); }

                // ROLE_SYSTEM_MENUITEM=12, MENUPOPUP=11, PUSHBUTTON=43, BUTTONDROPDOWN=56
                if (role == 12 || role == 11 || role == 43 || role == 56 ||
                    (!string.IsNullOrWhiteSpace(name) && name.Length < 80))
                {
                    return CleanMenuCaption(name);
                }
            }
            catch { }
            return "";
        }

        private static object InvokeAcc(object acc, string method, object child)
        {
            try
            {
                object[] args = new object[] { child == null ? 0 : child };
                return acc.GetType().InvokeMember(
                    method,
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                    null, acc, args);
            }
            catch
            {
                try
                {
                    return acc.GetType().InvokeMember(
                        method,
                        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                        null, acc, new object[] { child == null ? 0 : child });
                }
                catch { return null; }
            }
        }
    }
}
