// =============================================================================
//  Vb6FileBrowser.cs - drive / directory / file navigation inside «المستند».
//
//  VB6 exposes Drive, Path and FileName as properties, NOT as window text, so
//  WM_SETTEXT on a DirListBox changes nothing and the file list keeps showing
//  the folder the dialog opened on. The only reliable way in is the one a human
//  uses: pick the drive, then walk the tree by double-clicking one folder at a
//  time, then click the file. Every step is verified against the live list.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace RbaCore
{
    public class Vb6BrowserControls
    {
        public IntPtr Drive = IntPtr.Zero;
        public IntPtr Dir = IntPtr.Zero;
        public IntPtr Files = IntPtr.Zero;

        public bool HasBrowser
        {
            get { return Files != IntPtr.Zero || (Dir != IntPtr.Zero && Drive != IntPtr.Zero); }
        }
    }

    public static class Vb6FileBrowser
    {
        private const int StepSettleMs = 220;
        private const int ListRefreshMs = 900;
        private const int MaxRetriesPerLevel = 2;
        private const int MaxClimbs = 2;
        // Docs folders hold one National-Id directory per client; Coding lists them all.
        private const int MaxListItems = 100000;
        private const int LargeListFingerprintCap = 80;

        public static Vb6BrowserControls Locate(IntPtr picker)
        {
            return Locate(picker, null);
        }

        // Class names identify the three lists on a stock VB6 dialog. When a build renames
        // or reskins them, the operator's own capture (Setup → قائمة المجلدات / الملفات)
        // wins: guessing the wrong list is what sends the walk into the wrong folder.
        public static Vb6BrowserControls Locate(IntPtr picker, List<FieldSelector> selectors)
        {
            Vb6BrowserControls c = new Vb6BrowserControls();
            if (picker == IntPtr.Zero || !Win32Interop.IsWindow(picker)) { return c; }

            List<W32WindowInfo> kids = Win32Driver.GetChildren(picker);
            for (int i = 0; i < kids.Count; i++)
            {
                string cls = kids[i].Class != null ? kids[i].Class : "";
                if (Regex.IsMatch(cls, "(?i)DriveListBox")) { if (c.Drive == IntPtr.Zero) { c.Drive = kids[i].Handle; } }
                else if (Regex.IsMatch(cls, "(?i)DirListBox")) { if (c.Dir == IntPtr.Zero) { c.Dir = kids[i].Handle; } }
                else if (Regex.IsMatch(cls, "(?i)FileListBox")) { if (c.Files == IntPtr.Zero) { c.Files = kids[i].Handle; } }
            }

            if (selectors == null) { return c; }

            FieldMap map = FieldDiscovery.GetFieldMap(picker, false);
            IntPtr dir = Resolve(map, selectors, FieldCatalog.DocDirListKey);
            IntPtr files = Resolve(map, selectors, FieldCatalog.DocPathKey);
            IntPtr drive = Resolve(map, selectors, FieldCatalog.DocDriveListKey);
            if (dir != IntPtr.Zero) { c.Dir = dir; }
            if (drive != IntPtr.Zero) { c.Drive = drive; }
            // The captured file list must really be a list; operators sometimes click the
            // name textbox instead, and writing the walk against it would go nowhere.
            if (files != IntPtr.Zero && IsListBox(files)) { c.Files = files; }
            return c;
        }

        private static IntPtr Resolve(FieldMap map, List<FieldSelector> selectors, string fieldKey)
        {
            if (map == null || selectors == null) { return IntPtr.Zero; }
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                if (s == null || string.IsNullOrWhiteSpace(s.Class)) { continue; }
                if (!string.Equals(s.FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase)) { continue; }
                return FieldDiscovery.ResolveField(map, s, "Index").Handle;
            }
            return IntPtr.Zero;
        }

        private static bool IsListBox(IntPtr handle)
        {
            string cls = Win32Driver.GetClassName(handle);
            return cls != null && Regex.IsMatch(cls, "(?i)(listbox|list)");
        }

        // Everything the picker contains, so one run tells us why navigation stalled
        // instead of another round of guessing.
        public static void DumpControls(IntPtr picker, Action<string, string> log)
        {
            if (log == null || picker == IntPtr.Zero) { return; }

            List<W32WindowInfo> kids = Win32Driver.GetChildren(picker);
            StringBuilder sb = new StringBuilder();
            sb.Append("vb6-browser: picker '").Append(Win32Driver.GetWindowTitle(picker))
              .Append("' class=").Append(Win32Driver.GetClassName(picker))
              .Append(" children=").Append(kids.Count);
            for (int i = 0; i < kids.Count && i < 40; i++)
            {
                if (!kids[i].Visible) { continue; }
                Win32Driver.RectInfo r = Win32Driver.GetRect(kids[i].Handle);
                sb.Append(" | ").Append(kids[i].Class)
                  .Append(" @").Append(r.X).Append(",").Append(r.Y)
                  .Append(" ").Append(r.W).Append("x").Append(r.H);
            }
            log(sb.ToString(), "INFO");
        }

        // Full snapshot of «المستند»: children, drive, folder tip, folder rows, file rows.
        // Called on every failed attach so the run log shows exactly what Coding displayed.
        public static void DumpState(IntPtr picker, List<FieldSelector> selectors, Action<string, string> log)
        {
            if (log == null || picker == IntPtr.Zero || !Win32Interop.IsWindow(picker)) { return; }

            try
            {
                string title = Win32Driver.GetWindowTitle(picker) ?? "";
                string cls = Win32Driver.GetClassName(picker) ?? "";
                Win32Driver.RectInfo wr = Win32Driver.GetRect(picker);
                log("مستند-DUMP: window title=«" + title + "» class=" + cls +
                    " hwnd=" + picker.ToInt64() +
                    " @" + wr.X + "," + wr.Y + " " + wr.W + "x" + wr.H, "WARN");

                List<W32WindowInfo> kids = Win32Driver.GetChildren(picker);
                log("مستند-DUMP: children=" + kids.Count, "WARN");
                int listed = 0;
                for (int i = 0; i < kids.Count && listed < 40; i++)
                {
                    if (!kids[i].Visible) { continue; }
                    listed++;
                    Win32Driver.RectInfo r = Win32Driver.GetRect(kids[i].Handle);
                    string text = kids[i].Text != null ? kids[i].Text.Trim() : "";
                    if (text.Length > 60) { text = text.Substring(0, 60) + "…"; }
                    log("مستند-DUMP:   [" + listed + "] " + kids[i].Class +
                        " @" + r.X + "," + r.Y + " " + r.W + "x" + r.H +
                        (text.Length > 0 ? " text=«" + text + "»" : ""), "WARN");
                }

                Vb6BrowserControls c = Locate(picker, selectors);
                if (c.Drive != IntPtr.Zero)
                {
                    string driveText = Win32Driver.GetWindowText(c.Drive) ?? "";
                    log("مستند-DUMP: drive=«" + driveText + "» items=" +
                        Describe(ReadComboItems(c.Drive)), "WARN");
                }
                else
                {
                    log("مستند-DUMP: drive list NOT found", "WARN");
                }

                if (c.Dir != IntPtr.Zero)
                {
                    List<string> dirs = ReadItems(c.Dir);
                    log("مستند-DUMP: folder tip=«" + DescribeCurrent(OpenTip(c.Dir)) +
                        "» rows=" + dirs.Count, "WARN");
                    log("مستند-DUMP: folders = " + Describe(dirs), "WARN");
                }
                else
                {
                    log("مستند-DUMP: folder list NOT found", "WARN");
                }

                if (c.Files != IntPtr.Zero)
                {
                    List<string> files = ReadItems(c.Files);
                    int sel = Win32Driver.ToSignedInt32(
                        Win32Interop.SendMessage(c.Files, Win32Interop.LB_GETCURSEL, IntPtr.Zero, IntPtr.Zero));
                    string selName = sel >= 0 ? ReadItem(c.Files, sel) : "(none)";
                    log("مستند-DUMP: files rows=" + files.Count + " selected=«" + selName + "»", "WARN");
                    log("مستند-DUMP: files = " + Describe(files), "WARN");
                }
                else
                {
                    log("مستند-DUMP: file list NOT found", "WARN");
                }
            }
            catch (Exception ex)
            {
                log("مستند-DUMP: failed — " + ex.Message, "WARN");
            }
        }

        private static List<string> ReadComboItems(IntPtr combo)
        {
            List<string> items = new List<string>();
            if (combo == IntPtr.Zero) { return items; }
            try
            {
                int count = Win32Driver.ToSignedInt32(
                    Win32Interop.SendMessage(combo, Win32Interop.CB_GETCOUNT, IntPtr.Zero, IntPtr.Zero));
                if (count <= 0 || count > 100) { return items; }
                for (int i = 0; i < count; i++)
                {
                    int len = Win32Driver.ToSignedInt32(
                        Win32Interop.SendMessage(combo, Win32Interop.CB_GETLBTEXTLEN, (IntPtr)i, IntPtr.Zero));
                    if (len <= 0 || len > 260) { continue; }
                    StringBuilder sb = new StringBuilder(len + 2);
                    Win32Interop.SendMessage(combo, Win32Interop.CB_GETLBTEXT, (IntPtr)i, sb);
                    items.Add(sb.ToString());
                }
            }
            catch { }
            return items;
        }

        // Navigate to the file's folder and select it. Returns false without
        // clicking anything destructive when the file never shows up.
        public static bool SelectFile(IntPtr picker, string filePath, Action<string, string> log)
        {
            return SelectFile(picker, filePath, null, log);
        }

        public static bool SelectFile(
            IntPtr picker, string filePath, List<FieldSelector> selectors, Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }

            string folder = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(fileName)) { return false; }

            Vb6BrowserControls c = Locate(picker, selectors);
            if (c.Files == IntPtr.Zero || c.Dir == IntPtr.Zero)
            {
                log("vb6-browser: " + (c.Files == IntPtr.Zero ? "no file list" : "no folder list") +
                    " found — التقط «قائمة المجلدات» و«قائمة الملفات» من الإعدادات", "WARN");
                DumpControls(picker, log);
                return false;
            }

            try { Win32Driver.SetForeground(picker); } catch { }

            string tip = OpenTip(c.Dir);
            log("vb6-browser: state tip=«" + DescribeCurrent(tip) + "» want=«" + folder +
                "» file=«" + fileName + "» files=" + Describe(ReadItems(c.Files)), "INFO");

            if (!ReadyToPick(c, folder, fileName))
            {
                DumpControls(picker, log);
                log("vb6-browser: folder list = " + Describe(ReadItems(c.Dir)), "INFO");
                OpenDrive(c, folder, log);
                NavigateFromState(c, folder, fileName, log);
            }

            // After navigation, NID may be listed on the parent with an empty file square —
            // one forced enter before giving up.
            if (!ReadyToPick(c, folder, fileName) &&
                DirListContainsLeaf(c.Dir, LeafName(folder)) &&
                FindItem(c.Files, fileName) < 0)
            {
                log("vb6-browser: final force-open «" + LeafName(folder) + "»", "INFO");
                OpenChildByName(c, LeafName(folder), log, true);
            }

            if (!ReadyToPick(c, folder, fileName) && !InFolder(c.Dir, folder))
            {
                log("vb6-browser: still outside «" + folder + "» — current folder: " +
                    DescribeCurrent(OpenTip(c.Dir, folder)), "WARN");
                DumpState(picker, selectors, log);
                return false;
            }

            // The folder list can finish a hop before the file list repaints - wait for
            // the white file square to actually show rows before clicking anything.
            if (!WaitForFileList(c, fileName, log))
            {
                log("vb6-browser: file list empty/missing «" + fileName + "» — showing: " +
                    Describe(ReadItems(c.Files)), "WARN");
                DumpState(picker, selectors, log);
                return false;
            }

            int idx = FindItem(c.Files, fileName);
            if (idx < 0)
            {
                log("vb6-browser: «" + fileName + "» not in the list — showing: " +
                    Describe(ReadItems(c.Files)), "WARN");
                DumpState(picker, selectors, log);
                return false;
            }

            if (!SelectFileRow(c.Files, idx, fileName, log))
            {
                log("vb6-browser: could not select «" + fileName + "» in the file list", "WARN");
                DumpState(picker, selectors, log);
                return false;
            }

            log("vb6-browser: selected «" + fileName + "» in " + folder, "OK");
            return true;
        }

        // FileListBox only lists files of the OPEN Path. Presence of the image + NID in
        // the folder strip is the only safe "already there" signal — never trust tip text
        // alone (parent with NID listed as a child looks the same but files stay empty).
        private static bool ReadyToPick(Vb6BrowserControls c, string folder, string fileName)
        {
            if (FindItem(c.Files, fileName) < 0) { return false; }

            string nid = LeafName(folder);
            if (nid.Length > 0 && DirListContainsLeaf(c.Dir, nid)) { return true; }

            return InFolder(c.Dir, folder);
        }

        private static bool DirListContainsLeaf(IntPtr dir, string leaf)
        {
            if (string.IsNullOrEmpty(leaf)) { return false; }
            List<string> items = ReadItems(dir);
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(LeafName(Trim(items[i])), leaf, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private static void NavigateFromState(
            Vb6BrowserControls c, string folder, string fileName, Action<string, string> log)
        {
            if (c.Dir == IntPtr.Zero) { return; }
            if (ReadyToPick(c, folder, fileName)) { return; }

            string nid = LeafName(folder);
            string docs = "";
            try { docs = Path.GetDirectoryName(folder) ?? ""; } catch { docs = ""; }
            string docsLeaf = LeafName(docs);

            log("vb6-browser: navigate tip=«" + OpenTip(c.Dir, folder) + "» want=«" + folder + "»", "INFO");

            // NID in the strip + empty file square = sitting on the PARENT. Force enter.
            if (nid.Length > 0 && DirListContainsLeaf(c.Dir, nid) && FindItem(c.Files, fileName) < 0)
            {
                log("vb6-browser: «" + nid + "» listed but files empty — opening it", "INFO");
                if (OpenChildByName(c, nid, log, true) && ReadyToPick(c, folder, fileName)) { return; }
            }

            if (nid.Length > 0 && OpenChildByName(c, nid, log, false))
            {
                if (ReadyToPick(c, folder, fileName)) { return; }
            }

            for (int climb = 0; climb < 6; climb++)
            {
                if (ReadyToPick(c, folder, fileName)) { return; }

                string tip = OpenTip(c.Dir, folder);
                if (tip.Length == 0 || IsDriveRoot(tip)) { break; }

                if (nid.Length > 0 && DirListContainsLeaf(c.Dir, nid) && FindItem(c.Files, fileName) < 0)
                {
                    if (OpenChildByName(c, nid, log, true) && ReadyToPick(c, folder, fileName)) { return; }
                }

                if (nid.Length > 0 && OpenChildByName(c, nid, log, false))
                {
                    if (ReadyToPick(c, folder, fileName)) { return; }
                }

                if (docsLeaf.Length > 0 && OpenAncestorByName(c, docsLeaf, docs, log))
                {
                    WaitListRefresh(c);
                    if (nid.Length > 0 && OpenChildByName(c, nid, log, true))
                    {
                        if (ReadyToPick(c, folder, fileName)) { return; }
                    }
                    continue;
                }

                log("vb6-browser: leaving «" + LeafName(tip) + "» → up one level", "INFO");
                if (!ClimbOneLevel(c, log)) { break; }
                WaitListRefresh(c);
            }

            if (ReadyToPick(c, folder, fileName)) { return; }

            log("vb6-browser: walking from " + DescribeCurrent(OpenTip(c.Dir, folder)) +
                " to " + folder, "INFO");
            WalkTo(c, folder, fileName, log);
        }

        // Tip = longest PREFIX of the wanted path matching DirList rows from index 0.
        // Never "last bare name" — that made tip=Windows on the C:\ root and looped.
        private static string OpenTip(IntPtr dir)
        {
            return OpenTip(dir, null);
        }

        private static string OpenTip(IntPtr dir, string wantFolder)
        {
            if (dir == IntPtr.Zero) { return ""; }
            List<string> items = ReadItems(dir);
            if (items.Count == 0) { return CurrentDirPath(dir); }

            string deep = DeepestPathRow(items);
            if (deep.Length > 0 && !IsDriveRoot(deep)) { return deep; }

            if (!string.IsNullOrEmpty(wantFolder))
            {
                int depth = ConsecutiveChainDepth(items, PathChain(wantFolder));
                if (depth >= 0 && depth < items.Count) { return Trim(items[depth]); }
            }

            if (deep.Length > 0) { return deep; }
            return CurrentDirPath(dir);
        }

        private static int ConsecutiveChainDepth(List<string> items, List<string> chain)
        {
            if (items == null || chain == null || items.Count == 0 || chain.Count == 0) { return -1; }

            int depth = -1;
            int n = items.Count < chain.Count ? items.Count : chain.Count;
            for (int i = 0; i < n; i++)
            {
                string row = Trim(items[i]);
                string want = Trim(chain[i]);
                bool ok = SamePath(row, want) ||
                          string.Equals(LeafName(row), LeafName(want), StringComparison.OrdinalIgnoreCase);
                if (i == 0 && IsDriveRoot(row) && IsDriveRoot(want)) { ok = true; }
                if (!ok) { break; }
                depth = i;
            }
            return depth;
        }

        private static bool IsDriveRoot(string path)
        {
            string t = Trim(path);
            if (t.Length == 0) { return false; }
            if (t.Length <= 3 && t.IndexOf(':') >= 0) { return true; }
            try
            {
                string root = Path.GetPathRoot(t);
                return root != null && SamePath(root, t);
            }
            catch { return false; }
        }

        private static bool OpenChildByName(Vb6BrowserControls c, string name, Action<string, string> log)
        {
            return OpenChildByName(c, name, log, false);
        }

        // forceEnter: click even when tip text equals name — parent lists NID as child
        // without Path having entered it (file square empty).
        private static bool OpenChildByName(
            Vb6BrowserControls c, string name, Action<string, string> log, bool forceEnter)
        {
            if (string.IsNullOrEmpty(name)) { return false; }

            if (!forceEnter && IsReallyInsideName(c, name))
            {
                log("vb6-browser: «" + name + "» already open — skip click", "INFO");
                return true;
            }

            List<string> items = ReadItems(c.Dir);
            for (int i = 0; i < items.Count; i++)
            {
                string item = Trim(items[i]);
                if (!string.Equals(LeafName(item), name, StringComparison.OrdinalIgnoreCase)) { continue; }
                if (!forceEnter && !IsListedChild(items, i)) { continue; }

                log("vb6-browser: open child «" + name + "» → " + item +
                    (forceEnter ? " (force)" : "") + " row=" + i + "/" + items.Count, "INFO");
                TypePrefixIntoList(c.Dir, name, log);
                string beforeFiles = ListFingerprint(c.Files);
                string beforeDirs = ListFingerprint(c.Dir);
                if (!OpenFolder(c, item, "child «" + name + "»", log)) { return false; }
                WaitListRefresh(c);
                if (IsReallyInsideName(c, name)) { return true; }
                if (ListFingerprint(c.Files) != beforeFiles || ListFingerprint(c.Dir) != beforeDirs)
                {
                    return true;
                }
            }

            // Thousands of sibling National-Id folders: type the digits so VB6 jumps.
            log("vb6-browser: «" + name + "» not visible in " + items.Count +
                " folder rows — type-ahead + rescan", "INFO");
            TypePrefixIntoList(c.Dir, name, log);
            items = ReadItems(c.Dir);
            for (int i = 0; i < items.Count; i++)
            {
                string item = Trim(items[i]);
                if (!string.Equals(LeafName(item), name, StringComparison.OrdinalIgnoreCase)) { continue; }
                return OpenFolder(c, item, "child «" + name + "» after scroll", log);
            }
            return false;
        }

        private static bool IsReallyInsideName(Vb6BrowserControls c, string name)
        {
            if (c == null || c.Dir == IntPtr.Zero || string.IsNullOrEmpty(name)) { return false; }
            if (!string.Equals(LeafName(OpenTip(c.Dir)), name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            // National-Id tip with an empty file list = still on the parent.
            if (c.Files != IntPtr.Zero && ItemCount(c.Files) == 0 && LooksLikeNationalId(name))
            {
                return false;
            }
            return true;
        }

        private static bool LooksLikeNationalId(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length < 10) { return false; }
            for (int i = 0; i < name.Length; i++)
            {
                if (!char.IsDigit(name[i])) { return false; }
            }
            return true;
        }

        // Double-click an ancestor row (Docs) so sibling National-Id folders appear.
        private static bool OpenAncestorByName(
            Vb6BrowserControls c, string name, string fullPath, Action<string, string> log)
        {
            if (string.IsNullOrEmpty(name)) { return false; }
            if (InFolder(c.Dir, fullPath) ||
                string.Equals(LeafName(OpenTip(c.Dir)), name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            List<string> items = ReadItems(c.Dir);
            for (int i = 0; i < items.Count; i++)
            {
                if (IsListedChild(items, i)) { continue; }
                string item = Trim(items[i]);
                bool nameHit = string.Equals(LeafName(item), name, StringComparison.OrdinalIgnoreCase);
                bool pathHit = fullPath.Length > 0 && SamePath(item, fullPath);
                if (!nameHit && !pathHit) { continue; }
                log("vb6-browser: open ancestor «" + name + "» → " + item, "INFO");
                return OpenFolder(c, item, "ancestor «" + name + "»", log);
            }
            return false;
        }

        // Click the parent of the open tip. With Coding's bare-name strip, path-style
        // rows may only be "C:\" — climb by clicking the previous chain row instead.
        private static bool ClimbOneLevel(Vb6BrowserControls c, Action<string, string> log)
        {
            List<string> items = ReadItems(c.Dir);
            if (items.Count >= 2)
            {
                // Prefer second-to-last row when the list is a bare path chain.
                string parent = Trim(items[items.Count - 2]);
                if (parent.Length > 0 && !IsDriveRoot(Trim(items[items.Count - 1])))
                {
                    return OpenFolder(c, parent, "up → " + parent, log);
                }
            }

            List<string> paths = new List<string>();
            for (int i = 0; i < items.Count; i++)
            {
                string item = Trim(items[i]);
                if (LooksLikePath(item)) { paths.Add(item); }
            }
            if (paths.Count == 0) { return ClimbToRoot(c, log); }

            paths.Sort(delegate(string a, string b)
            {
                if (SamePath(a, b)) { return 0; }
                if (IsAncestorOrSelf(a, b)) { return -1; }
                if (IsAncestorOrSelf(b, a)) { return 1; }
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });

            if (paths.Count < 2) { return ClimbToRoot(c, log); }
            string pathParent = paths[paths.Count - 2];
            return OpenFolder(c, pathParent, "up → " + pathParent, log);
        }

        // Poll until the wanted file appears (or any files show up then we re-check).
        private static bool WaitForFileList(Vb6BrowserControls c, string fileName, Action<string, string> log)
        {
            DateTime until = DateTime.Now.AddMilliseconds(8000);
            bool typed = false;
            while (DateTime.Now < until)
            {
                if (FindItem(c.Files, fileName) >= 0) { return true; }
                if (!typed && ItemCount(c.Files) > 20)
                {
                    typed = true;
                    TypePrefixIntoList(c.Files, fileName, log);
                }
                Thread.Sleep(160);
            }
            return FindItem(c.Files, fileName) >= 0;
        }

        // Coding only enables موافق after a real selection in the FileListBox. Drive the
        // white square with every channel VB6 accepts, and refuse success until GETCURSEL
        // points at the wanted row.
        private static bool SelectFileRow(IntPtr files, int index, string fileName, Action<string, string> log)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                FocusList(files);
                if (attempt == 0)
                {
                    TypePrefixIntoList(files, fileName, log);
                    int jumped = FindItem(files, fileName);
                    if (jumped >= 0) { index = jumped; }
                }
                BringRowIntoView(files, index, log);
                Win32Interop.SendMessage(files, Win32Interop.LB_SETTOPINDEX, (IntPtr)index, IntPtr.Zero);
                Win32Interop.SendMessage(files, Win32Interop.LB_SETCURSEL, (IntPtr)index, IntPtr.Zero);
                NotifyParent(files, Win32Interop.LBN_SELCHANGE);
                Thread.Sleep(80);

                if (attempt == 0) { MouseClickItem(files, index, false, log); }
                else if (attempt == 1) { MessageClick(files, index); }
                else if (attempt == 2) { MouseClickItem(files, index, true, log); }
                else { MessageDoubleClick(files, index); }

                Win32Interop.SendMessage(files, Win32Interop.LB_SETCURSEL, (IntPtr)index, IntPtr.Zero);
                NotifyParent(files, Win32Interop.LBN_SELCHANGE);
                Thread.Sleep(StepSettleMs);

                int sel = Win32Driver.ToSignedInt32(
                    Win32Interop.SendMessage(files, Win32Interop.LB_GETCURSEL, IntPtr.Zero, IntPtr.Zero));
                if (sel == index)
                {
                    log("vb6-browser: file row " + index + " selected («" + fileName + "»)", "INFO");
                    return true;
                }

                // Some Thunder builds leave GETCURSEL at -1 even when FileName is set;
                // a matching LB_GETTEXT on the selection (or a forced re-read) is enough.
                if (sel >= 0 && string.Equals(ReadItem(files, sel).Trim(), fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        // Some dialogs accept the file on double-click instead of a موافق button.
        public static void DoubleClickSelectedFile(IntPtr picker, string fileName)
        {
            Vb6BrowserControls c = Locate(picker);
            if (c.Files == IntPtr.Zero) { return; }
            int idx = FindItem(c.Files, fileName);
            if (idx < 0) { return; }
            if (!MouseClickItem(c.Files, idx, true, null)) { MessageDoubleClick(c.Files, idx); }
        }

        // First rows of a list, for the run log: enough to tell what the dialog shows.
        private static string Describe(List<string> items)
        {
            if (items == null || items.Count == 0) { return "(فارغة)"; }
            StringBuilder sb = new StringBuilder();
            int show = items.Count < 12 ? items.Count : 12;
            for (int i = 0; i < show; i++)
            {
                if (i > 0) { sb.Append(" | "); }
                sb.Append(items[i]);
            }
            if (items.Count > show) { sb.Append(" | … +").Append(items.Count - show); }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- drive

        private static void OpenDrive(Vb6BrowserControls c, string folder, Action<string, string> log)
        {
            if (c.Drive == IntPtr.Zero) { return; }

            string root = Path.GetPathRoot(folder);
            if (string.IsNullOrEmpty(root)) { return; }
            string letter = root.TrimEnd('\\', '/');
            if (letter.Length < 2) { return; }

            // The first folder row is always the drive root, which makes it a far more
            // reliable "where am I" reading than the list selection.
            if (SameDrive(CurrentDrive(c.Dir), letter)) { return; }

            W32ActionResult sel = Win32Driver.SelectComboItem(c.Drive, letter.ToLower(CultureInfo.InvariantCulture));
            if (!sel.Ok) { sel = Win32Driver.SelectComboItem(c.Drive, letter); }
            NotifyParent(c.Drive, Win32Interop.CBN_SELCHANGE);
            Thread.Sleep(400);
            log("vb6-browser: drive → " + letter + " (" + sel.Note + ")", "INFO");
        }

        private static bool SameDrive(string path, string letter)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(letter)) { return false; }
            return path.StartsWith(letter, StringComparison.OrdinalIgnoreCase);
        }

        // Row 0 of a DirListBox is the root of the open drive, whatever is selected.
        private static string CurrentDrive(IntPtr dir)
        {
            List<string> items = ReadItems(dir);
            if (items.Count == 0) { return ""; }
            string first = items[0].Trim();
            return LooksLikePath(first) ? first : "";
        }

        // ------------------------------------------------------------ directory

        // Walks the folder chain one double-click at a time. Progress is tracked by the
        // DEPTH already opened, and every hop must go deeper than the last one, so the
        // walk can only move forward: the old version compared the next hop with the
        // previous one only, which let an unreadable selection bounce the dialog between
        // two folders forever instead of reaching the end of the path.
        private static void WalkTo(
            Vb6BrowserControls c, string folder, string fileName, Action<string, string> log)
        {
            if (c.Dir == IntPtr.Zero) { return; }

            List<string> chain = PathChain(folder);
            if (chain.Count == 0) { return; }

            int last = chain.Count - 1;
            int depth = -1;      // deepest chain level confirmed open (full-path match)
            int nameDepth = -1;  // deepest level reached by folder NAME instead of path
            int retries = 0;     // unconfirmed hops we allowed at the current level
            int climbs = 0;      // times we went back to the top of the listed tree

            int budget = (chain.Count * (MaxRetriesPerLevel + 1)) + MaxClimbs + 4;
            for (int step = 0; step < budget; step++)
            {
                if (InFolder(c.Dir, folder)) { return; }

                // Promote depth from whatever the list tip already proves open.
                for (int i = chain.Count - 1; i > depth; i--)
                {
                    if (InFolder(c.Dir, chain[i])) { depth = i; break; }
                }
                if (depth >= last) { return; }

                // Stop early only when the file shows up in a folder that can be the
                // wanted one: another client's folder may hold the same file name.
                if (FindItem(c.Files, fileName) >= 0 && InFolder(c.Dir, folder)) { return; }

                int level = DeepestListedIndex(c.Dir, chain, depth);
                string target = level >= 0 ? chain[level] : null;
                bool byName = false;

                if (target == null)
                {
                    // The list does not show our paths at all - a different drive letter or
                    // share for the same folder. Descend by FOLDER NAME instead, which is
                    // what actually identifies the client folder (it is the National Id).
                    target = DeepestListedByName(c.Dir, chain, nameDepth, out level);
                    byName = target != null;
                }

                if (target == null && climbs < MaxClimbs && ClimbToRoot(c, log))
                {
                    // The dialog sits in a leaf folder - another client's, typically -
                    // so nothing on screen leads anywhere. Go back to the top of the
                    // listed tree, where the client folder becomes reachable again.
                    climbs++;
                    WaitListRefresh(c);
                    continue;
                }

                if (target == null)
                {
                    // Nothing deeper is on screen. Either the previous hop silently did
                    // nothing (retry it) or the folder really is not there (give up).
                    if (depth >= 0 && retries < MaxRetriesPerLevel)
                    {
                        retries++;
                        log("vb6-browser: retrying step " + (depth + 1) + "/" + (last + 1) +
                            " → " + chain[depth], "WARN");
                        if (!OpenFolder(c, chain[depth], depth, last, log)) { return; }
                        WaitListRefresh(c);
                        continue;
                    }
                    log("vb6-browser: «" + PathChainAt(chain, depth + 1) +
                        "» is not listed — stopped at " + DescribeCurrent(CurrentDirPath(c.Dir)) +
                        " on the way to " + folder, "WARN");
                    return;
                }

                if (byName)
                {
                    log("vb6-browser: «" + LeafName(chain[level]) + "» matched by name → " + target, "INFO");
                }
                if (!OpenFolder(c, target, level, last, log)) { return; }
                WaitListRefresh(c);

                // Only a folder the list tip agrees we are inside counts as progress.
                if (InFolder(c.Dir, chain[level]) || InFolder(c.Dir, target))
                {
                    if (byName) { nameDepth = level; } else { depth = level; }
                    retries = 0;
                    continue;
                }

                retries++;
                if (retries > MaxRetriesPerLevel)
                {
                    log("vb6-browser: could not settle inside «" + target + "» — stopped at " +
                        DescribeCurrent(CurrentDirPath(c.Dir)), "WARN");
                    return;
                }
            }

            log("vb6-browser: too many steps before reaching «" + folder + "»", "WARN");
        }

        // Row 0 of the folder list is the top of the tree the dialog shows. Opening it
        // is one click that undoes any amount of wandering, and it never depends on the
        // configured path matching what the dialog displays.
        private static bool ClimbToRoot(Vb6BrowserControls c, Action<string, string> log)
        {
            List<string> items = ReadItems(c.Dir);
            if (items.Count == 0) { return false; }

            string root = Trim(items[0]);
            if (root.Length == 0 || InFolder(c.Dir, root)) { return false; }

            return OpenFolder(c, root, "back to the top → " + root, log);
        }

        // The dialog is inside the wanted folder when the tip matches by full path or by
        // National-Id leaf. Coding often shows only "C:\" as a path row and the rest as
        // bare names — OpenTip already resolves that to the last segment.
        private static bool InFolder(IntPtr dir, string folder)
        {
            if (dir == IntPtr.Zero || string.IsNullOrEmpty(folder)) { return false; }

            List<string> items = ReadItems(dir);
            if (items.Count == 0) { return false; }

            string want = Trim(folder);
            string open = OpenTip(dir, folder);
            if (open.Length == 0) { return false; }

            if (SamePath(open, want)) { return true; }
            if (string.Equals(LeafName(open), LeafName(want), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        // Longest path-style row is the folder the dialog has open; bare names are never
        // candidates because Coding lists children without a drive prefix.
        private static string DeepestPathRow(List<string> items)
        {
            string best = "";
            for (int i = 0; i < items.Count; i++)
            {
                string item = Trim(items[i]);
                if (!LooksLikePath(item)) { continue; }
                if (best.Length == 0 || IsAncestorOrSelf(best, item)) { best = item; }
            }
            return best;
        }

        // Deepest chain segment whose NAME appears as a subfolder of the open folder.
        // Only listed children qualify: an ancestor row carries the same name and
        // clicking it would walk back up the tree.
        private static string DeepestListedByName(
            IntPtr dir, List<string> chain, int after, out int level)
        {
            level = -1;
            List<string> items = ReadItems(dir);
            if (items.Count == 0) { return null; }

            for (int i = chain.Count - 1; i > after && i > 0; i--)
            {
                string leaf = LeafName(chain[i]);
                if (leaf.Length == 0) { continue; }
                for (int k = 0; k < items.Count; k++)
                {
                    string item = Trim(items[k]);
                    if (item.Length == 0 || !IsListedChild(items, k)) { continue; }
                    if (!string.Equals(LeafName(item), leaf, StringComparison.OrdinalIgnoreCase)) { continue; }
                    level = i;
                    return item;
                }
            }
            return null;
        }

        // Bare names are always children. Path rows are children only when they sit
        // directly under the deepest path row (the open folder).
        private static bool IsListedChild(List<string> items, int index)
        {
            string self = Trim(items[index]);
            if (self.Length == 0) { return false; }
            if (!LooksLikePath(self)) { return true; }

            string open = DeepestPathRow(items);
            if (open.Length == 0) { return false; }
            return IsDirectChild(open, self);
        }

        // Kept for hop confirmation: after a click we only need the target to still be
        // findable / the open tip to agree - full InFolder is reserved for "arrived".
        private static bool CouldBeInFolder(IntPtr dir, string folder)
        {
            if (dir == IntPtr.Zero || string.IsNullOrEmpty(folder)) { return true; }
            if (InFolder(dir, folder)) { return true; }

            // Mid-hop: the row we just clicked may still be selected as a child before
            // the list finishes rebuilding around it as the new open folder.
            return FindDirItem(dir, folder) >= 0 && FindDirItemIsChild(dir, folder);
        }

        private static bool FindDirItemIsChild(IntPtr dir, string folder)
        {
            List<string> items = ReadItems(dir);
            int idx = FindDirItem(dir, folder);
            if (idx < 0) { return false; }
            return IsListedChild(items, idx);
        }

        // Biggest safe hop: deepest chain entry that is listed as a CHILD of the open
        // folder. Ancestor rows matching an earlier chain segment must not be clicked.
        private static int DeepestListedIndex(IntPtr dir, List<string> chain, int after)
        {
            List<string> items = ReadItems(dir);
            for (int i = chain.Count - 1; i > after; i--)
            {
                int idx = FindDirItem(dir, chain[i]);
                if (idx < 0) { continue; }
                if (IsListedChild(items, idx)) { return i; }
            }
            return -1;
        }

        private static int ChainIndexOf(List<string> chain, string path)
        {
            if (string.IsNullOrEmpty(path)) { return -1; }
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                if (SamePath(chain[i], path)) { return i; }
            }
            return -1;
        }

        private static string PathChainAt(List<string> chain, int index)
        {
            if (chain == null || index < 0 || index >= chain.Count) { return "(نهاية المسار)"; }
            return chain[index];
        }

        private static bool OpenFolder(
            Vb6BrowserControls c, string target, int level, int last, Action<string, string> log)
        {
            return OpenFolder(c, target, "step " + (level + 1) + "/" + (last + 1) + " → " + target, log);
        }

        private static bool OpenFolder(
            Vb6BrowserControls c, string target, string step, Action<string, string> log)
        {
            int idx = FindDirItem(c.Dir, target);
            if (idx < 0)
            {
                log("vb6-browser: folder «" + target + "» not listed — stopped at " +
                    DescribeCurrent(CurrentDirPath(c.Dir)), "WARN");
                return false;
            }

            // Posted messages beat a physical double-click here: the mouse route needs the
            // picker to be foreground and the row to be on screen, and when either is off
            // the click lands on whatever window happens to sit under those coordinates.
            // Drive selection already worked purely by messages, folders now do the same.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                idx = FindDirItem(c.Dir, target);
                if (idx < 0)
                {
                    log("vb6-browser: «" + target + "» disappeared from the list", "WARN");
                    return false;
                }

                string before = ListFingerprint(c.Dir);
                if (attempt == 0) { TypePrefixIntoList(c.Dir, LeafName(target), log); }
                idx = FindDirItem(c.Dir, target);
                if (idx < 0) { continue; }
                BringRowIntoView(c.Dir, idx, log);
                string how = SendItemClick(c.Dir, idx, attempt, log);
                if (how == null) { continue; }
                Thread.Sleep(StepSettleMs);
                WaitListRefresh(c);

                if (DirOpened(c.Dir, target, before))
                {
                    log("vb6-browser: opened " + step + " (" + how + ")", "INFO");
                    return true;
                }

                // The dialog moved, just not where we asked. Trying another click on a
                // list that has already been rebuilt is how the walk used to wander off:
                // hand control back so the next hop is picked from the live list.
                if (ListFingerprint(c.Dir) != before)
                {
                    log("vb6-browser: " + step + " landed elsewhere (" + how + ") — " +
                        DescribeCurrent(CurrentDirPath(c.Dir)), "WARN");
                    return true;
                }

                log("vb6-browser: " + step + " ignored the " + how, "WARN");
            }

            log("vb6-browser: " + step + " did not change the folder list", "WARN");
            return false;
        }

        // One activation attempt. A real mouse double-click on the row is what the folder
        // list reacts to most faithfully, so it goes first; the synthetic routes only
        // cover builds or screen states where the pointer cannot reach the row.
        private static string SendItemClick(IntPtr list, int index, int attempt, Action<string, string> log)
        {
            if (attempt == 0) { return MouseClickItem(list, index, true, log) ? "mouse double-click" : null; }
            if (attempt == 1) { return MessageDoubleClick(list, index) ? "message double-click" : null; }

            // Last resort: tell the form itself, the way the runtime would.
            Win32Interop.SendMessage(list, Win32Interop.LB_SETCURSEL, (IntPtr)index, IntPtr.Zero);
            NotifyParent(list, Win32Interop.LBN_DBLCLK);
            return "parent notification";
        }

        // The selection is unreadable on some builds, but a folder change always
        // rebuilds the list - a different fingerprint is proof the hop landed.
        private static bool DirOpened(IntPtr dir, string target, string before)
        {
            string current = CurrentDirPath(dir);
            if (!string.IsNullOrEmpty(current))
            {
                if (SamePath(current, target)) { return true; }
                // Coding may select "C:\Users" after a click on the bare row "Users".
                if (string.Equals(LeafName(current), LeafName(target), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            string after = ListFingerprint(dir);
            if (after == before) { return false; }
            return FindDirItem(dir, target) >= 0 || InFolder(dir, target);
        }

        private static string ListFingerprint(IntPtr list)
        {
            int n = ItemCount(list);
            if (n <= 0) { return ""; }
            int sel = Win32Driver.ToSignedInt32(
                Win32Interop.SendMessage(list, Win32Interop.LB_GETCURSEL, IntPtr.Zero, IntPtr.Zero));
            if (n > LargeListFingerprintCap)
            {
                return "n=" + n + "|sel=" + sel + "|" + ReadItem(list, 0) + "|" +
                    ReadItem(list, n / 2) + "|" + ReadItem(list, n - 1);
            }

            List<string> items = ReadItems(list);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < items.Count; i++) { sb.Append(items[i]).Append('|'); }
            return sb.ToString();
        }

        private static bool IsAncestorOrSelf(string ancestor, string path)
        {
            if (SamePath(ancestor, path)) { return true; }
            string a = Trim(ancestor);
            string p = Trim(path);
            if (a.Length == 0 || p.Length <= a.Length) { return false; }
            if (!p.StartsWith(a, StringComparison.OrdinalIgnoreCase)) { return false; }
            return p[a.Length] == '\\' || a.EndsWith("\\", StringComparison.Ordinal);
        }

        private static bool IsDirectChild(string parent, string path)
        {
            if (!IsAncestorOrSelf(parent, path) || SamePath(parent, path)) { return false; }
            string rest = Trim(path).Substring(Trim(parent).Length).Trim('\\');
            return rest.Length > 0 && rest.IndexOf('\\') < 0;
        }

        private static string DescribeCurrent(string current)
        {
            return string.IsNullOrEmpty(current) ? "(المجلد الحالي غير معروف)" : current;
        }

        // "C:\a\b\c" → ["C:\", "C:\a", "C:\a\b", "C:\a\b\c"]
        private static List<string> PathChain(string folder)
        {
            List<string> chain = new List<string>();
            if (string.IsNullOrEmpty(folder)) { return chain; }

            string root = Path.GetPathRoot(folder);
            if (string.IsNullOrEmpty(root)) { return chain; }
            chain.Add(Trim(root));

            string rest = folder.Substring(root.Length).Trim('\\', '/');
            if (rest.Length == 0) { return chain; }

            string[] parts = rest.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            string acc = Trim(root);
            for (int i = 0; i < parts.Length; i++)
            {
                // The root already ends with a separator ("C:\"); appending another one
                // produced "C:\\Users", which matched no row in the folder list - the
                // walk could then only ever recognise the drive root itself.
                acc = acc.EndsWith("\\", StringComparison.Ordinal) ? acc + parts[i] : acc + "\\" + parts[i];
                chain.Add(acc);
            }
            return chain;
        }

        // Coding's DirListBox lists the open folder as a path ("C:\") and its children
        // as bare names ("Users"). Exact path match alone therefore misses every hop
        // after the drive root - which is exactly what the live logs showed.
        private static int FindDirItem(IntPtr list, string target)
        {
            string wanted = Trim(target);
            if (wanted.Length == 0) { return -1; }

            int exact = FindStringExact(list, wanted);
            if (exact >= 0) { return exact; }
            string wantLeaf = LeafName(wanted);
            if (wantLeaf.Length > 0)
            {
                exact = FindStringExact(list, wantLeaf);
                if (exact >= 0) { return exact; }
            }

            List<string> items = ReadItems(list);

            for (int i = 0; i < items.Count; i++)
            {
                if (SamePath(items[i], wanted)) { return i; }
            }

            string leaf = LeafName(wanted);
            if (leaf.Length == 0) { return -1; }

            int bare = -1;
            int pathChild = -1;
            for (int i = 0; i < items.Count; i++)
            {
                string item = Trim(items[i]);
                if (item.Length == 0) { continue; }
                if (!string.Equals(LeafName(item), leaf, StringComparison.OrdinalIgnoreCase)) { continue; }

                // Bare child names are unique among siblings at this level.
                if (!LooksLikePath(item))
                {
                    if (bare < 0) { bare = i; }
                    continue;
                }

                // Full-path rows: only a listed child (not an ancestor) may match by leaf,
                // so "Users" inside "C:\Users\foo" does not reopen "C:\Users".
                if (IsListedChild(items, i) && pathChild < 0) { pathChild = i; }
            }

            if (bare >= 0) { return bare; }
            if (pathChild >= 0) { return pathChild; }
            return -1;
        }

        private static bool ListHoldsPaths(List<string> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (LooksLikePath(items[i])) { return true; }
            }
            return false;
        }

        private static string CurrentDirPath(IntPtr dir)
        {
            if (dir == IntPtr.Zero) { return ""; }
            int sel = Win32Driver.ToSignedInt32(
                Win32Interop.SendMessage(dir, Win32Interop.LB_GETCURSEL, IntPtr.Zero, IntPtr.Zero));
            if (sel < 0) { return ""; }
            string text = ReadItem(dir, sel);
            return LooksLikePath(text) ? Trim(text) : "";
        }

        private static bool LooksLikePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) { return false; }
            return value.IndexOf(':') >= 0 || value.IndexOf('\\') >= 0;
        }

        // ----------------------------------------------------------- list utils

        private static int FindItem(IntPtr list, string value)
        {
            if (list == IntPtr.Zero || string.IsNullOrEmpty(value)) { return -1; }

            int exact = FindStringExact(list, value.Trim());
            if (exact >= 0) { return exact; }
            string leaf = LeafName(value);
            if (leaf.Length > 0 && !string.Equals(leaf, value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                exact = FindStringExact(list, leaf);
                if (exact >= 0) { return exact; }
            }

            List<string> items = ReadItems(list);
            string want = value.Trim();
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].Trim(), want, StringComparison.OrdinalIgnoreCase)) { return i; }
            }
            for (int i = 0; i < items.Count; i++)
            {
                if (string.Equals(LeafName(items[i]), want, StringComparison.OrdinalIgnoreCase)) { return i; }
            }
            // Coding sometimes lists "name.JPG" while the sheet has "name.jpg".
            string wantNoExt = StripExtension(want);
            if (wantNoExt.Length > 0 && wantNoExt.Length < want.Length)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (string.Equals(StripExtension(LeafName(items[i])), wantNoExt, StringComparison.OrdinalIgnoreCase))
                    {
                        return i;
                    }
                }
            }
            return -1;
        }

        private static string StripExtension(string name)
        {
            if (string.IsNullOrEmpty(name)) { return ""; }
            try { return Path.GetFileNameWithoutExtension(name) ?? name; }
            catch { return name; }
        }

        private static List<string> ReadItems(IntPtr list)
        {
            List<string> items = new List<string>();
            if (list == IntPtr.Zero || !Win32Interop.IsWindow(list)) { return items; }

            int count = Win32Driver.ToSignedInt32(
                Win32Interop.SendMessage(list, Win32Interop.LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero));
            if (count <= 0 || count > MaxListItems) { return items; }

            for (int i = 0; i < count; i++)
            {
                items.Add(ReadItem(list, i));
            }
            return items;
        }

        private static string ReadItem(IntPtr list, int index)
        {
            try
            {
                int len = Win32Driver.ToSignedInt32(
                    Win32Interop.SendMessage(list, Win32Interop.LB_GETTEXTLEN, (IntPtr)index, IntPtr.Zero));
                if (len <= 0 || len > 4096) { return ""; }
                StringBuilder sb = new StringBuilder(len + 2);
                Win32Interop.SendMessage(list, Win32Interop.LB_GETTEXT, (IntPtr)index, sb);
                return sb.ToString();
            }
            catch { return ""; }
        }

        // A full mouse sequence delivered straight to the list. VB6 reads the clicked row
        // out of the message coordinates, so the row is scrolled into view first, but no
        // cursor is moved and no window has to be foreground - nothing else can receive it.
        private static bool MessageDoubleClick(IntPtr list, int index)
        {
            POINT p;
            if (!ItemClientPoint(list, index, out p)) { return false; }

            IntPtr lp = MakePoint(p);
            IntPtr down = (IntPtr)Win32Interop.MK_LBUTTON;

            // Posted, not sent: a list captures the mouse on button-down, and blocking our
            // thread inside that handler while it waits for the button-up would deadlock.
            FocusList(list);
            Win32Interop.SendMessage(list, Win32Interop.LB_SETCURSEL, (IntPtr)index, IntPtr.Zero);
            Win32Interop.PostMessage(list, Win32Interop.WM_LBUTTONDOWN, down, lp);
            Win32Interop.PostMessage(list, Win32Interop.WM_LBUTTONUP, IntPtr.Zero, lp);
            Win32Interop.PostMessage(list, Win32Interop.WM_LBUTTONDBLCLK, down, lp);
            Win32Interop.PostMessage(list, Win32Interop.WM_LBUTTONUP, IntPtr.Zero, lp);
            Thread.Sleep(120);
            return true;
        }

        private static bool MessageClick(IntPtr list, int index)
        {
            POINT p;
            if (!ItemClientPoint(list, index, out p)) { return false; }

            IntPtr lp = MakePoint(p);
            FocusList(list);
            Win32Interop.SendMessage(list, Win32Interop.LB_SETCURSEL, (IntPtr)index, IntPtr.Zero);
            Win32Interop.PostMessage(list, Win32Interop.WM_LBUTTONDOWN, (IntPtr)Win32Interop.MK_LBUTTON, lp);
            Win32Interop.PostMessage(list, Win32Interop.WM_LBUTTONUP, IntPtr.Zero, lp);
            Thread.Sleep(120);
            return true;
        }

        private static IntPtr MakePoint(POINT p)
        {
            return (IntPtr)(((p.Y & 0xFFFF) << 16) | (p.X & 0xFFFF));
        }

        // VB6 only reacts to input on the control that owns the focus, and SetFocus is
        // refused across threads unless the input queues are attached first.
        private static void FocusList(IntPtr list)
        {
            uint self = Win32Interop.GetCurrentThreadId();
            uint owner = Win32Interop.GetWindowThreadProcessId(list, IntPtr.Zero);
            bool attached = false;
            try
            {
                if (owner != 0 && owner != self)
                {
                    attached = Win32Interop.AttachThreadInput(self, owner, true);
                }
                Win32Interop.SetFocus(list);
            }
            catch { }
            finally
            {
                if (attached) { try { Win32Interop.AttachThreadInput(self, owner, false); } catch { } }
            }
        }

        // A real pointer click on the row - the input VB6 trusts most. Every reason a
        // synthetic click used to land on the wrong thing is checked away first:
        // the dialog is brought to the front, the row is scrolled into view, the list
        // itself confirms which row that point belongs to, and the window under the
        // pointer must be the list. Anything unexpected aborts instead of clicking.
        private static bool MouseClickItem(IntPtr list, int index, bool doubleClick, Action<string, string> log)
        {
            if (list == IntPtr.Zero || index < 0) { return false; }
            if (log == null) { log = delegate(string m, string l) { }; }

            if (!Activate(list))
            {
                log("vb6-browser: picker window would not come to the front", "WARN");
                return false;
            }

            POINT client;
            if (!ItemClientPoint(list, index, out client))
            {
                log("vb6-browser: row " + index + " is not on screen", "WARN");
                return false;
            }

            // The list decides which row owns that point - no arithmetic to get wrong.
            int hit = Win32Driver.ToSignedInt32(Win32Interop.SendMessage(
                list, Win32Interop.LB_ITEMFROMPOINT, IntPtr.Zero, MakePoint(client))) & 0xFFFF;
            if (hit != index)
            {
                log("vb6-browser: point maps to row " + hit + " not " + index, "WARN");
                return false;
            }

            POINT screen = client;
            if (!Win32Interop.ClientToScreen(list, ref screen)) { return false; }

            IntPtr under = Win32Interop.WindowFromPoint(screen);
            if (under != list && !IsSameOrChild(list, under) && !IsSameOrChild(under, list))
            {
                // Still click: Coding's file square often reports a sibling/parent under the
                // pointer, and skipping the click left FileName empty («اختر المستند أولاً»).
                if (log != null)
                {
                    log("vb6-browser: click through overlay hwnd=" + under.ToInt64() + " on list", "WARN");
                }
            }

            POINT restore;
            bool saved = Win32Interop.GetCursorPos(out restore);
            if (doubleClick) { Win32Driver.DoubleClickScreen(screen.X, screen.Y, StepSettleMs); }
            else { Win32Driver.ClickScreen(screen.X, screen.Y, StepSettleMs); }
            if (saved) { try { Win32Interop.SetCursorPos(restore.X, restore.Y); } catch { } }
            return true;
        }

        private static bool IsSameOrChild(IntPtr ancestor, IntPtr hwnd)
        {
            if (ancestor == IntPtr.Zero || hwnd == IntPtr.Zero) { return false; }
            IntPtr cur = hwnd;
            for (int i = 0; i < 8 && cur != IntPtr.Zero; i++)
            {
                if (cur == ancestor) { return true; }
                cur = Win32Interop.GetParent(cur);
            }
            return false;
        }

        // Foreground the dialog that owns the list, and wait for it: the first click on
        // an inactive window is swallowed by the activation, so a double-click would
        // arrive as a single one and the folder would never open.
        private static bool Activate(IntPtr list)
        {
            IntPtr top = Win32Interop.GetAncestor(list, Win32Interop.GA_ROOT);
            if (top == IntPtr.Zero) { top = list; }
            if (Win32Interop.GetForegroundWindow() == top) { return true; }

            for (int i = 0; i < 6; i++)
            {
                try { Win32Driver.SetForeground(top); } catch { }
                Thread.Sleep(80);
                if (Win32Interop.GetForegroundWindow() == top) { return true; }
            }
            return false;
        }

        // Centre of a row in client coordinates, scrolling it into view when needed:
        // a rectangle outside the client area means the row is not on screen, and its
        // coordinates would point at a neighbouring control.
        private static bool ItemClientPoint(IntPtr list, int index, out POINT p)
        {
            if (MeasureItem(list, index, out p)) { return true; }
            BringRowIntoView(list, index, null);
            return MeasureItem(list, index, out p);
        }

        // Scroll the list until GETITEMRECT lands inside the client area. Needed when
        // docs\ holds thousands of National-Id folders and the wanted row is far below.
        private static bool BringRowIntoView(IntPtr list, int index, Action<string, string> log)
        {
            if (list == IntPtr.Zero || index < 0) { return false; }

            int count = ItemCount(list);
            if (count <= 0 || index >= count) { return false; }

            int top = index - 2;
            if (top < 0) { top = 0; }

            for (int pass = 0; pass < 5; pass++)
            {
                Win32Interop.SendMessage(list, Win32Interop.LB_SETTOPINDEX, (IntPtr)(pass == 0 ? top : index), IntPtr.Zero);
                Thread.Sleep(90 + pass * 40);
                POINT dummy;
                if (MeasureItem(list, index, out dummy))
                {
                    if (log != null && index > 8)
                    {
                        log("vb6-browser: scrolled list to row " + index + "/" + count, "INFO");
                    }
                    return true;
                }
            }
            return false;
        }

        // VB6 listboxes jump to the first row that matches typed characters — the
        // same way an operator types a National Id instead of scrolling the wheel.
        private static void TypePrefixIntoList(IntPtr list, string prefix, Action<string, string> log)
        {
            if (list == IntPtr.Zero || string.IsNullOrEmpty(prefix)) { return; }

            string keys = LeafName(prefix);
            if (keys.Length == 0) { keys = prefix.Trim(); }
            if (keys.Length == 0) { return; }
            if (keys.Length > 24) { keys = keys.Substring(0, 24); }

            FocusList(list);
            for (int i = 0; i < keys.Length; i++)
            {
                IntPtr ch = (IntPtr)keys[i];
                Win32Interop.PostMessage(list, Win32Interop.WM_CHAR, ch, IntPtr.Zero);
                Thread.Sleep(35);
            }
            Thread.Sleep(120);
            if (log != null)
            {
                log("vb6-browser: type-ahead «" + keys + "»", "INFO");
            }
        }

        private static int FindStringExact(IntPtr list, string value)
        {
            if (list == IntPtr.Zero || string.IsNullOrEmpty(value)) { return -1; }
            int idx = Win32Driver.ToSignedInt32(
                Win32Interop.SendMessage(list, Win32Interop.LB_FINDSTRINGEXACT, (IntPtr)(-1), value));
            return idx >= 0 ? idx : -1;
        }

        private static bool MeasureItem(IntPtr list, int index, out POINT p)
        {
            p = new POINT();

            RECT rc = new RECT();
            IntPtr got = Win32Interop.SendMessage(list, Win32Interop.LB_GETITEMRECT, (IntPtr)index, ref rc);
            if (Win32Driver.ToSignedInt32(got) < 0) { return false; }
            if (rc.Right <= rc.Left || rc.Bottom <= rc.Top) { return false; }

            RECT client;
            if (!Win32Interop.GetClientRect(list, out client)) { return false; }

            int x = rc.Left + (rc.Right - rc.Left) / 2;
            int y = rc.Top + (rc.Bottom - rc.Top) / 2;
            if (x < client.Left || x >= client.Right) { return false; }
            if (y < client.Top || y >= client.Bottom) { return false; }

            p.X = x;
            p.Y = y;
            return true;
        }

        private static void NotifyParent(IntPtr control, int code)
        {
            try
            {
                IntPtr parent = Win32Interop.GetParent(control);
                if (parent == IntPtr.Zero) { return; }
                int id = Win32Interop.GetDlgCtrlID(control);
                IntPtr wParam = (IntPtr)((code << 16) | (id & 0xFFFF));
                Win32Interop.SendMessage(parent, Win32Interop.WM_COMMAND, wParam, control);
            }
            catch { }
        }

        // Both lists are rebuilt on every folder change. Watching the folder list too
        // keeps the walk fast through empty folders, where the file count never moves.
        private static void WaitListRefresh(Vb6BrowserControls c)
        {
            int filesBefore = ItemCount(c.Files);
            int dirsBefore = ItemCount(c.Dir);

            DateTime until = DateTime.Now.AddMilliseconds(ListRefreshMs);
            while (DateTime.Now < until)
            {
                Thread.Sleep(70);
                if (ItemCount(c.Files) != filesBefore) { return; }
                if (ItemCount(c.Dir) != dirsBefore) { return; }
            }
        }

        private static int ItemCount(IntPtr list)
        {
            if (list == IntPtr.Zero) { return -1; }
            return Win32Driver.ToSignedInt32(
                Win32Interop.SendMessage(list, Win32Interop.LB_GETCOUNT, IntPtr.Zero, IntPtr.Zero));
        }

        // ----------------------------------------------------------- path utils

        private static bool SamePath(string a, string b)
        {
            return string.Equals(Trim(a), Trim(b), StringComparison.OrdinalIgnoreCase);
        }

        private static string Trim(string path)
        {
            if (string.IsNullOrEmpty(path)) { return ""; }
            string p = path.Trim();
            if (p.Length > 3) { p = p.TrimEnd('\\', '/'); }
            return p;
        }

        private static string LeafName(string path)
        {
            if (string.IsNullOrEmpty(path)) { return ""; }
            string p = Trim(path);
            int cut = p.LastIndexOfAny(new char[] { '\\', '/' });
            return cut >= 0 ? p.Substring(cut + 1) : p;
        }
    }
}
