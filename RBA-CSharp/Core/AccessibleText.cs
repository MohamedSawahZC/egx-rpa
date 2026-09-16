// =============================================================================
//  AccessibleText.cs - read text out of windowless controls through MSAA.
//
//  VB6 grids (MSFlexGrid and friends) draw their cells; there is no child HWND
//  and no WM_GETTEXT to read. MSAA is the only Win32-level way to see the rows,
//  which is what the «الإستعلام للتعديل» duplicate check needs.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace RbaCore
{
    public static class AccessibleText
    {
        // Guards against pathological trees; a result grid never needs more.
        private const int MaxDepth = 8;
        private const int MaxNodes = 4000;
        private const int MaxSimpleChildren = 800;

        // All visible strings inside a window, flattened. Empty when MSAA is unavailable.
        public static List<string> Harvest(IntPtr window)
        {
            List<string> texts = HarvestId(window, Win32Interop.OBJID_CLIENT);
            if (texts.Count == 0)
            {
                // OBJID_WINDOW is 0. The constant named OBJID_WINDOW in Win32Interop
                // is actually OBJID_NATIVEOM (0xFFFFFFF0) — try both.
                texts = HarvestId(window, 0);
            }
            if (texts.Count == 0)
            {
                texts = HarvestId(window, Win32Interop.OBJID_WINDOW);
            }
            return texts;
        }

        // Every descendant HWND as well — FlexGrid often sits under a Frame.
        public static List<string> HarvestDeep(IntPtr window)
        {
            List<string> texts = Harvest(window);
            if (window == IntPtr.Zero || !Win32Interop.IsWindow(window)) { return texts; }

            List<IntPtr> kids = Win32Interop.EnumChildWindowsList(window);
            for (int i = 0; i < kids.Count; i++)
            {
                Merge(texts, Harvest(kids[i]));
                string wt = Win32Interop.GetWindowTextValue(kids[i]);
                if (!string.IsNullOrWhiteSpace(wt)) { texts.Add(wt.Trim()); }
            }
            return texts;
        }

        // FlexGrid often reports accChildCount=0 while cells still answer as simple ids.
        public static List<string> HarvestForcedIds(IntPtr window, int maxId, Action<string, string> log)
        {
            List<string> texts = new List<string>();
            if (window == IntPtr.Zero || !Win32Interop.IsWindow(window)) { return texts; }
            if (maxId < 20) { maxId = 20; }
            if (maxId > 400) { maxId = 400; }

            uint[] oids = new uint[] { Win32Interop.OBJID_CLIENT, 0, Win32Interop.OBJID_WINDOW };
            for (int u = 0; u < oids.Length; u++)
            {
                object acc = FromWindow(window, oids[u]);
                if (acc == null) { continue; }
                int reported = ReadChildCount(acc);
                if (log != null)
                {
                    log("msaa: forced-id object=0x" + oids[u].ToString("X8") +
                        " reportedChildren=" + reported, "INFO");
                }
                Append(texts, SafeStr(Invoke(acc, "get_accName", 0)));
                Append(texts, SafeStr(Invoke(acc, "get_accValue", 0)));
                object focus = Invoke(acc, "get_accFocus", null);
                if (focus != null && !(focus is int && (int)focus == 0))
                {
                    Append(texts, SafeStr(Invoke(acc, "get_accName", focus)));
                    Append(texts, SafeStr(Invoke(acc, "get_accValue", focus)));
                    if (log != null)
                    {
                        log("msaa: accFocus=" + focus + " name=«" +
                            OneDumpLine(SafeStr(Invoke(acc, "get_accName", focus))) + "»", "INFO");
                    }
                }

                int hit = 0;
                for (int id = 1; id <= maxId; id++)
                {
                    string name = SafeStr(Invoke(acc, "get_accName", id));
                    string value = SafeStr(Invoke(acc, "get_accValue", id));
                    string desc = SafeStr(Invoke(acc, "get_accDescription", id));
                    if (name.Length == 0 && value.Length == 0 && desc.Length == 0) { continue; }
                    hit++;
                    if (log != null && hit <= 80)
                    {
                        log("msaa: #" + id + " name=«" + OneDumpLine(name) +
                            "» value=«" + OneDumpLine(value) +
                            "» desc=«" + OneDumpLine(desc) + "»", "INFO");
                    }
                    Append(texts, name);
                    Append(texts, value);
                    Append(texts, desc);
                }
                if (log != null)
                {
                    log("msaa: forced-id hits=" + hit + " of " + maxId, "INFO");
                }
                if (texts.Count > 0) { return texts; }
            }
            return texts;
        }

        // Cell under the cursor — windowless VB6 grids expose text here only.
        public static List<string> HarvestAtPoint(int x, int y)
        {
            List<string> texts = new List<string>();
            try
            {
                POINT pt = new POINT(x, y);
                object acc;
                object child;
                int hr = Win32Interop.AccessibleObjectFromPoint(pt, out acc, out child);
                if (hr != 0 || acc == null) { return texts; }

                object id = child == null ? 0 : child;
                Append(texts, Invoke(acc, "get_accName", id));
                Append(texts, Invoke(acc, "get_accValue", id));
                Append(texts, Invoke(acc, "get_accDescription", id));

                int budget = 120;
                Collect(acc, 0, texts, ref budget);
            }
            catch { }
            return texts;
        }

        // Walk a mapped Frame/grid rectangle so at least one cell is sampled.
        public static List<string> HarvestRegion(int x, int y, int w, int h)
        {
            List<string> texts = new List<string>();
            if (w < 8 || h < 8) { return texts; }

            int stepX = 56;
            int stepY = 22;
            int startY = y + Math.Min(28, Math.Max(8, h / 8));
            int endX = x + w - 8;
            int endY = y + h - 8;
            if (endX <= x) { endX = x + w; }
            if (endY <= startY) { endY = y + h; }

            for (int py = startY; py <= endY; py += stepY)
            {
                for (int px = x + 12; px <= endX; px += stepX)
                {
                    Merge(texts, HarvestAtPoint(px, py));
                }
            }
            return texts;
        }

        public static void Merge(List<string> dest, List<string> extra)
        {
            if (dest == null || extra == null) { return; }
            for (int i = 0; i < extra.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(extra[i])) { dest.Add(extra[i]); }
            }
        }

        private static List<string> HarvestId(IntPtr window, uint objectId)
        {
            List<string> texts = new List<string>();
            if (window == IntPtr.Zero || !Win32Interop.IsWindow(window)) { return texts; }

            object root = FromWindow(window, objectId);
            if (root == null) { return texts; }

            int budget = MaxNodes;
            Collect(root, 0, texts, ref budget);
            return texts;
        }

        // True when any harvested string contains the needle (digits compared bare).
        public static bool Contains(List<string> texts, string needle)
        {
            return FirstMatch(texts, needle) != null;
        }

        // The harvested string that carries the needle, for the log. Null when none does.
        // A result row packs several values in one cell ("ذكر / رقم قومى / 285…"), so a
        // substring match on the normalised text is what actually finds the client.
        public static string FirstMatch(List<string> texts, string needle)
        {
            if (texts == null || string.IsNullOrWhiteSpace(needle)) { return null; }
            string want = Normalize(needle);
            if (want.Length == 0) { return null; }
            for (int i = 0; i < texts.Count; i++)
            {
                string have = Normalize(texts[i]);
                if (have.Length == 0) { continue; }
                if (have.IndexOf(want, StringComparison.Ordinal) >= 0) { return texts[i]; }
            }
            return null;
        }

        // Removes everything a control already showed before the search ran, so the
        // term echoed back by the search box can never be mistaken for a result row.
        public static void Exclude(List<string> texts, List<string> unwanted)
        {
            if (texts == null || unwanted == null) { return; }
            for (int i = texts.Count - 1; i >= 0; i--)
            {
                string have = Normalize(texts[i]);
                for (int j = 0; j < unwanted.Count; j++)
                {
                    if (have == Normalize(unwanted[j])) { texts.RemoveAt(i); break; }
                }
            }
        }

        // Strip separators so "285-0314-2200993" still matches "28503142200993", and
        // fold Arabic spelling / digit variants so «إبراهيم» matches «ابراهيم».
        private static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            string folded = ArabicText.Normalize(value);
            StringBuilder sb = new StringBuilder(folded.Length);
            for (int i = 0; i < folded.Length; i++)
            {
                char c = folded[i];
                if (char.IsWhiteSpace(c)) { continue; }
                if (c == '-' || c == '_' || c == '/' || c == '\\' || c == '.') { continue; }
                if (c >= '\u0660' && c <= '\u0669') { c = (char)('0' + (c - '\u0660')); }
                else if (c >= '\u06F0' && c <= '\u06F9') { c = (char)('0' + (c - '\u06F0')); }
                sb.Append(c);
            }
            return sb.ToString();
        }

        private static object FromWindow(IntPtr window)
        {
            return FromWindow(window, Win32Interop.OBJID_CLIENT);
        }

        private static object FromWindow(IntPtr window, uint objectId)
        {
            try
            {
                object acc;
                Guid iid = Win32Interop.IID_IAccessible;
                int hr = Win32Interop.AccessibleObjectFromWindow(window, objectId, ref iid, out acc);
                return hr == 0 ? acc : null;
            }
            catch { return null; }
        }

        private static int ReadChildCount(object acc)
        {
            object countObj = Invoke(acc, "get_accChildCount", null);
            if (countObj is int) { return (int)countObj; }
            if (countObj is short) { return (int)((short)countObj); }
            countObj = Invoke(acc, "get_accChildCount", 0);
            if (countObj is int) { return (int)countObj; }
            if (countObj is short) { return (int)((short)countObj); }
            return 0;
        }

        private static void Collect(object acc, int depth, List<string> texts, ref int budget)
        {
            if (acc == null || depth > MaxDepth || budget <= 0) { return; }
            budget--;

            Append(texts, Invoke(acc, "get_accName", 0));
            Append(texts, Invoke(acc, "get_accValue", 0));
            Append(texts, Invoke(acc, "get_accDescription", 0));

            int count = ReadChildCount(acc);
            if (count <= 0) { return; }

            // MSFlexGrid exposes cells as integer child ids and often returns
            // E_NOTIMPL from AccessibleChildren — walk the ids first.
            int simple = count > MaxSimpleChildren ? MaxSimpleChildren : count;
            for (int id = 1; id <= simple && budget > 0; id++)
            {
                budget--;
                Append(texts, Invoke(acc, "get_accName", id));
                Append(texts, Invoke(acc, "get_accValue", id));
                Append(texts, Invoke(acc, "get_accDescription", id));
            }

            int walk = count > budget ? budget : count;
            object[] slots = new object[walk];
            int got;
            try
            {
                if (Win32Interop.AccessibleChildren(acc, 0, walk, slots, out got) != 0) { return; }
            }
            catch { return; }

            for (int i = 0; i < got && budget > 0; i++)
            {
                object child = slots[i];
                if (child == null) { continue; }
                if (child is int) { continue; }
                Collect(child, depth + 1, texts, ref budget);
            }
        }

        private static void Append(List<string> texts, object value)
        {
            if (value == null) { return; }
            string t = value.ToString();
            if (string.IsNullOrWhiteSpace(t)) { return; }
            texts.Add(t.Trim());
        }

        // Diagnostic dump of the MSAA tree — used by Setup «تصدير تشخيص الجدول».
        public static void DumpWindow(IntPtr window, StringBuilder sb, string title, int maxLines)
        {
            if (sb == null) { return; }
            if (maxLines < 20) { maxLines = 20; }
            sb.AppendLine("--- MSAA " + (title != null ? title : "window") + " hwnd=" + window + " ---");
            if (window == IntPtr.Zero || !Win32Interop.IsWindow(window))
            {
                sb.AppendLine("(dead handle)");
                return;
            }

            DumpOneId(window, sb, "OBJID_CLIENT", Win32Interop.OBJID_CLIENT, maxLines);
            DumpOneId(window, sb, "OBJID_WINDOW(0)", 0, maxLines);
            DumpOneId(window, sb, "OBJID_NATIVEOM", Win32Interop.OBJID_WINDOW, maxLines);

            List<string> harvested = HarvestDeep(window);
            sb.AppendLine("HarvestDeep count=" + harvested.Count);
            int show = harvested.Count < 80 ? harvested.Count : 80;
            for (int i = 0; i < show; i++)
            {
                sb.AppendLine("  [" + (i + 1) + "] " + OneDumpLine(harvested[i]));
            }
            if (harvested.Count > show)
            {
                sb.AppendLine("  … +" + (harvested.Count - show) + " more");
            }
        }

        private static void DumpOneId(IntPtr window, StringBuilder sb, string label, uint objectId, int maxLines)
        {
            object root = FromWindow(window, objectId);
            if (root == null)
            {
                sb.AppendLine(label + ": (no IAccessible)");
                return;
            }
            int budget = maxLines;
            sb.AppendLine(label + ":");
            DumpCollect(root, 0, sb, ref budget);
        }

        private static void DumpCollect(object acc, int depth, StringBuilder sb, ref int budget)
        {
            if (acc == null || depth > 5 || budget <= 0) { return; }
            budget--;

            string pad = new string(' ', depth * 2);
            string name = SafeStr(Invoke(acc, "get_accName", 0));
            string value = SafeStr(Invoke(acc, "get_accValue", 0));
            string desc = SafeStr(Invoke(acc, "get_accDescription", 0));
            object roleObj = Invoke(acc, "get_accRole", 0);
            string role = roleObj != null ? roleObj.ToString() : "";
            int kids = ReadChildCount(acc);
            sb.AppendLine(pad + "name=«" + OneDumpLine(name) + "» value=«" + OneDumpLine(value) +
                "» desc=«" + OneDumpLine(desc) + "» role=" + role + " children=" + kids);

            int simple = kids > 40 ? 40 : kids;
            for (int id = 1; id <= simple && budget > 0; id++)
            {
                budget--;
                string cn = SafeStr(Invoke(acc, "get_accName", id));
                string cv = SafeStr(Invoke(acc, "get_accValue", id));
                if (cn.Length == 0 && cv.Length == 0) { continue; }
                sb.AppendLine(pad + "  #" + id + " name=«" + OneDumpLine(cn) + "» value=«" + OneDumpLine(cv) + "»");
            }

            if (kids <= 0 || budget <= 0) { return; }
            int walk = kids > 30 ? 30 : kids;
            object[] slots = new object[walk];
            int got;
            try
            {
                if (Win32Interop.AccessibleChildren(acc, 0, walk, slots, out got) != 0) { return; }
            }
            catch { return; }

            for (int i = 0; i < got && budget > 0; i++)
            {
                object child = slots[i];
                if (child == null || child is int) { continue; }
                DumpCollect(child, depth + 1, sb, ref budget);
            }
        }

        private static string SafeStr(object value)
        {
            if (value == null) { return ""; }
            string t = value.ToString();
            return t != null ? t.Trim() : "";
        }

        private static string OneDumpLine(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            string t = value.Replace("\r", " ").Replace("\n", " / ").Replace("\t", " ").Trim();
            return t.Length > 200 ? t.Substring(0, 200) + "…" : t;
        }

        // Windowless VB6 CommandButtons respond to accDoDefaultAction at the click point.
        public static bool DoDefaultActionAtPoint(int x, int y, Action<string, string> log)
        {
            try
            {
                POINT pt = new POINT(x, y);
                object acc;
                object child;
                int hr = Win32Interop.AccessibleObjectFromPoint(pt, out acc, out child);
                if (hr != 0 || acc == null) { return false; }

                object nameObj = Invoke(acc, "get_accName", child == null ? 0 : child);
                string name = nameObj != null ? nameObj.ToString() : "";
                Invoke(acc, "accDoDefaultAction", child == null ? 0 : child);
                if (log != null)
                {
                    log("post-save: MSAA default-action at " + x + "," + y +
                        (name.Length > 0 ? " «" + name + "»" : ""), "OK");
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Walk the form's MSAA tree and click the first node whose name is إكسل / Excel / …
        public static bool ClickNamed(IntPtr window, string[] names, Action<string, string> log)
        {
            if (window == IntPtr.Zero || names == null || names.Length == 0) { return false; }
            object root = FromWindow(window);
            if (root == null) { return false; }
            int budget = MaxNodes;
            return ClickNamedWalk(root, 0, names, log, ref budget);
        }

        private static bool ClickNamedWalk(
            object acc, int depth, string[] names, Action<string, string> log, ref int budget)
        {
            if (acc == null || depth > MaxDepth || budget <= 0) { return false; }
            budget--;

            if (TryInvokeIfName(acc, 0, names, log)) { return true; }

            int count = 0;
            object countObj = Invoke(acc, "get_accChildCount", null);
            if (countObj is int) { count = (int)countObj; }
            if (count <= 0) { return false; }
            if (count > budget) { count = budget; }

            object[] slots = new object[count];
            int got;
            try
            {
                if (Win32Interop.AccessibleChildren(acc, 0, count, slots, out got) != 0) { return false; }
            }
            catch { return false; }

            for (int i = 0; i < got && budget > 0; i++)
            {
                object child = slots[i];
                if (child == null) { continue; }
                if (child is int)
                {
                    budget--;
                    if (TryInvokeIfName(acc, child, names, log)) { return true; }
                    continue;
                }
                if (ClickNamedWalk(child, depth + 1, names, log, ref budget)) { return true; }
            }
            return false;
        }

        private static bool TryInvokeIfName(
            object acc, object child, string[] names, Action<string, string> log)
        {
            object nameObj = Invoke(acc, "get_accName", child == null ? 0 : child);
            string name = nameObj != null ? nameObj.ToString().Trim() : "";
            if (name.Length == 0) { return false; }
            for (int i = 0; i < names.Length; i++)
            {
                if (string.IsNullOrEmpty(names[i])) { continue; }
                if (name.IndexOf(names[i], StringComparison.OrdinalIgnoreCase) < 0 &&
                    !ArabicText.Contains(name, names[i]))
                {
                    continue;
                }
                Invoke(acc, "accDoDefaultAction", child == null ? 0 : child);
                if (log != null)
                {
                    log("post-save: MSAA clicked «" + name + "»", "OK");
                }
                return true;
            }
            return false;
        }

        private static object Invoke(object acc, string member, object child)
        {
            object[] args = child == null ? new object[0] : new object[] { child };
            try
            {
                return acc.GetType().InvokeMember(
                    member,
                    BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
                    null, acc, args);
            }
            catch
            {
                try
                {
                    return acc.GetType().InvokeMember(
                        member,
                        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                        null, acc, args);
                }
                catch { return null; }
            }
        }
    }
}
