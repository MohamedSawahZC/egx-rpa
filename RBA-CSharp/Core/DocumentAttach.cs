// =============================================================================
//  DocumentAttach.cs - attach client images for Coding.exe.
//  Real flow: arm CheckBox «ملفات» once (box HWND only, not caption) →
//  click Add → window «المستند» → set path → confirm. Repeat Add+path per file.
//  Prefer JPG samples (Coding rejects PNG on some sites). Never presses ترحيل.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace RbaCore
{
    public class DocAttachResult
    {
        public bool Ok;
        public int Attached;
        public int Attempted;
        public string Note;
        public List<string> Lines = new List<string>();
    }

    public static class DocumentAttach
    {
        private static readonly string[] ImageExt = new string[] { ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".bmp" };

        // Clear any KYC TextBox on the entry form that accidentally contains a file path.
        public static int ScrubPathLeaksFromEntry(IntPtr entryWindow, Action<string, string> log)
        {
            if (log == null) { log = delegate(string m, string l) { }; }
            if (entryWindow == IntPtr.Zero || !Win32Interop.IsWindow(entryWindow)) { return 0; }
            int cleared = 0;
            try
            {
                FieldMap map = FieldDiscovery.GetFieldMap(entryWindow, false);
                if (map == null || map.Fields == null) { return 0; }
                for (int i = 0; i < map.Fields.Count; i++)
                {
                    AppField f = map.Fields[i];
                    if (f.Kind != "TextBox") { continue; }
                    if (WindowLayout.IsFileBrowserClass(f.Class)) { continue; }
                    string text = "";
                    try { text = Win32Driver.GetWindowText(f.Handle); } catch { continue; }
                    if (!WindowLayout.LooksLikeFilePath(text)) { continue; }
                    try
                    {
                        Win32Driver.SetText(f.Handle, "");
                        cleared++;
                        log("scrubbed path leak from TextBox near «" + (f.Label ?? "") + "»", "WARN");
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                log("scrub path leaks failed: " + ex.Message, "WARN");
            }
            return cleared;
        }

        public static string ResolveDocsFolder(OrderedDictionary row, string scansRoot)
        {
            string fromRow = CsvUtil.Get(row, "DocsFolder", "").Trim();
            string nid = CsvUtil.Get(row, "NationalId", "").Trim();
            string root = NormalizeScansRoot(scansRoot);

            // Explicit absolute DocsFolder on the sheet.
            if (!string.IsNullOrEmpty(fromRow) && Directory.Exists(fromRow))
            {
                return fromRow;
            }

            // Sheet DocsFolder relative to scansRoot (e.g. "28503142200993").
            if (!string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(fromRow))
            {
                string under = Path.Combine(root, fromRow);
                if (Directory.Exists(under)) { return under; }
            }

            // Operator pointed scansRoot at the client folder itself (ends with NID).
            if (!string.IsNullOrEmpty(root) && !string.IsNullOrEmpty(nid))
            {
                if (FolderLooksLikeClientDocs(root, nid)) { return root; }
                string under = Path.Combine(root, nid);
                if (Directory.Exists(under) && ListImages(under).Count > 0) { return under; }
            }

            if (!string.IsNullOrEmpty(nid))
            {
                string[] extras = ExtraDocsRoots(root);
                for (int i = 0; i < extras.Length; i++)
                {
                    string under = Path.Combine(extras[i], nid);
                    if (Directory.Exists(under) && ListImages(under).Count > 0) { return under; }
                }
            }

            // Last resort: DocsFolder name equals folder under cwd / absolute-ish.
            if (!string.IsNullOrEmpty(fromRow))
            {
                try
                {
                    string full = Path.GetFullPath(fromRow);
                    if (Directory.Exists(full)) { return full; }
                }
                catch { }
            }

            return "";
        }

        public static bool HasClientImages(OrderedDictionary row, string scansRoot)
        {
            string folder = ResolveDocsFolder(row, scansRoot);
            return ListImages(folder).Count > 0;
        }

        // Offline layouts: OfflineInbox\docs, OfflineInbox\inbox\docs, parent\inbox\docs.
        private static string[] ExtraDocsRoots(string scansRoot)
        {
            List<string> roots = new List<string>();
            string[] live = SftpInbox.EffectiveDocsRoots(SftpInbox.LastDocsRoot);
            for (int i = 0; i < live.Length; i++) { AddDocsRoot(roots, live[i]); }
            try
            {
                string cache = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "RPA-V1", "SftpCache", "docs");
                AddDocsRoot(roots, cache);
                AddDocsRoot(roots, Path.Combine(cache, "kyc-imports"));
                AddDocsRoot(roots, Path.Combine(cache, "docs"));
            }
            catch { }
            AddDocsRoot(roots, scansRoot);
            if (!string.IsNullOrEmpty(scansRoot))
            {
                AddDocsRoot(roots, Path.Combine(scansRoot, "kyc-imports"));
            }
            if (!string.IsNullOrEmpty(scansRoot))
            {
                try
                {
                    string parent = Path.GetDirectoryName(scansRoot.TrimEnd('\\', '/'));
                    AddDocsRoot(roots, Path.Combine(scansRoot, "docs"));
                    AddDocsRoot(roots, Path.Combine(scansRoot, "inbox", "docs"));
                    if (!string.IsNullOrEmpty(parent))
                    {
                        AddDocsRoot(roots, Path.Combine(parent, "docs"));
                        AddDocsRoot(roots, Path.Combine(parent, "inbox", "docs"));
                        string grand = Path.GetDirectoryName(parent.TrimEnd('\\', '/'));
                        if (!string.IsNullOrEmpty(grand))
                        {
                            AddDocsRoot(roots, Path.Combine(grand, "docs"));
                            AddDocsRoot(roots, Path.Combine(grand, "inbox", "docs"));
                        }
                    }
                }
                catch { }
            }
            return roots.ToArray();
        }

        private static void AddDocsRoot(List<string> roots, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || roots == null) { return; }
            try
            {
                if (!Directory.Exists(path)) { return; }
                string full = Path.GetFullPath(path);
                for (int i = 0; i < roots.Count; i++)
                {
                    if (string.Equals(roots[i], full, StringComparison.OrdinalIgnoreCase)) { return; }
                }
                roots.Add(full);
            }
            catch { }
        }

        // scansRoot may be a file (mis-pick) or the NID folder itself.
        private static string NormalizeScansRoot(string scansRoot)
        {
            if (string.IsNullOrWhiteSpace(scansRoot)) { return ""; }
            string p = scansRoot.Trim().Trim('"');
            try
            {
                if (File.Exists(p)) { return Path.GetDirectoryName(p) ?? ""; }
                if (Directory.Exists(p)) { return p; }
            }
            catch { }
            return p;
        }

        private static bool FolderLooksLikeClientDocs(string folder, string nid)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { return false; }
            string name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (!string.IsNullOrEmpty(nid) &&
                string.Equals(name, nid, StringComparison.OrdinalIgnoreCase))
            {
                return ListImages(folder).Count > 0;
            }
            // Any folder that already contains images when no better match exists.
            return ListImages(folder).Count > 0 &&
                   (string.IsNullOrEmpty(nid) || name.IndexOf(nid, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static List<string> ListImages(string folder)
        {
            List<string> files = new List<string>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) { return files; }
            string[] all = Directory.GetFiles(folder);
            Array.Sort(all, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < all.Length; i++)
            {
                string ext = Path.GetExtension(all[i]);
                for (int e = 0; e < ImageExt.Length; e++)
                {
                    if (string.Equals(ext, ImageExt[e], StringComparison.OrdinalIgnoreCase))
                    {
                        files.Add(all[i]);
                        break;
                    }
                }
            }
            // Prefer JPG when both JPG and PNG share the same base name.
            return PreferJpgOverPng(files);
        }

        private static List<string> PreferJpgOverPng(List<string> files)
        {
            if (files == null || files.Count == 0) { return files ?? new List<string>(); }
            Dictionary<string, bool> jpgBases = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Count; i++)
            {
                string ext = Path.GetExtension(files[i]);
                if (string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase))
                {
                    jpgBases[Path.GetFileNameWithoutExtension(files[i])] = true;
                }
            }
            List<string> kept = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                string ext = Path.GetExtension(files[i]);
                if (string.Equals(ext, ".png", StringComparison.OrdinalIgnoreCase))
                {
                    string b = Path.GetFileNameWithoutExtension(files[i]);
                    if (jpgBases.ContainsKey(b)) { continue; }
                }
                kept.Add(files[i]);
            }
            return kept;
        }

        public static DocAttachResult Attach(
            IntPtr entryWindow,
            OrderedDictionary row,
            List<FieldSelector> selectors,
            string scansRoot,
            bool watch,
            int stepMs,
            Action<string, string> log)
        {
            DocAttachResult result = new DocAttachResult();
            if (log == null) { log = delegate(string m, string l) { }; }

            string folder = ResolveDocsFolder(row, scansRoot);
            List<string> images = ListImages(folder);
            if (images.Count == 0)
            {
                result.Ok = true;
                result.Note = "no images to attach (folder empty or missing)";
                log(result.Note + (string.IsNullOrEmpty(folder) ? (" | scansRoot=" + (scansRoot ?? "")) : (": " + folder)), "WARN");
                return result;
            }

            log("docs folder: " + folder + " (" + images.Count + " image(s))", "INFO");
            log("docs flow: CheckBox ملفات مرة واحدة → إضافة ملف → مسار (لكل مرفق في المجلد)", "INFO");

            FieldMap map = FieldDiscovery.GetFieldMap(entryWindow, false);
            string[] keys = FieldCatalog.DocAddKeys;
            // Attach every image present for this NationalId folder (not a fixed 4).
            int max = images.Count;
            result.Attempted = max;
            int pid = Win32Driver.GetProcessId(entryWindow);

            string docsEvidence = FindReplicaDocsEvidence(entryWindow);
            bool replicaMode = !string.IsNullOrEmpty(docsEvidence);

            FieldSelector chkFiles = FindConcreteSelector(selectors, FieldCatalog.ChkFilesKey);
            if (chkFiles == null)
            {
                chkFiles = FindFilesCheckbox(map);
            }
            bool useMostanad = chkFiles != null;

            // Arm «ملفات» CheckBox once for the whole attach session (checkbox HWND only).
            if (useMostanad)
            {
                if (!ArmFilesCheckboxOnce(entryWindow, ref map, selectors, chkFiles, log))
                {
                    log("could not arm ملفات CheckBox — abort docs", "WARN");
                    result.Ok = false;
                    result.Note = "ملفات checkbox not armed";
                    return result;
                }
            }

            for (int i = 0; i < max; i++)
            {
                if (watch)
                {
                    Thread.Sleep(stepMs);
                }

                string fileName = Path.GetFileName(images[i]);
                log("doc slot " + (i + 1) + "/" + max + ": attach → " + fileName, "INFO");

                string slotKey = (i < keys.Length) ? keys[i] : null;

                bool attachedOk = false;
                if (useMostanad)
                {
                    attachedOk = AttachViaMostanadFlow(
                        entryWindow, ref map, selectors, slotKey, i,
                        images[i], pid, watch, stepMs, log);
                    if (!attachedOk && replicaMode)
                    {
                        attachedOk = WaitReplicaEvidenceGrow(docsEvidence, 800);
                    }
                }
                else if (replicaMode)
                {
                    attachedOk = AttachViaReplicaSlot(
                        entryWindow, map, selectors,
                        slotKey != null ? slotKey : "btnDocAdd" + (i + 1),
                        i, images[i],
                        docsEvidence, watch, stepMs, log);
                }
                else
                {
                    log("doc slot " + (i + 1) + ": map checkbox ملفات in Setup", "WARN");
                }

                if (attachedOk)
                {
                    result.Attached++;
                    result.Lines.Add("attached " + fileName);
                    log("doc slot " + (i + 1) + ": attach OK", "OK");
                }
                else
                {
                    result.Lines.Add("failed " + fileName);
                    log("doc slot " + (i + 1) + ": attach failed", "WARN");
                }
                Thread.Sleep(300);
                map = FieldDiscovery.GetFieldMap(entryWindow, false);
            }

            result.Ok = result.Attached > 0 || result.Attempted == 0;
            result.Note = "attached " + result.Attached + " of " + result.Attempted + " image(s) from " + folder;
            return result;
        }

        // Per file: click Add → wait «المستند» → set path. Checkbox already armed once.
        private static bool AttachViaMostanadFlow(
            IntPtr entryWindow,
            ref FieldMap map,
            List<FieldSelector> selectors,
            string slotKey,
            int slotIndex,
            string imagePath,
            int pid,
            bool watch,
            int stepMs,
            Action<string, string> log)
        {
            Dictionary<long, bool> beforeTops = SnapshotTopHandles();
            IntPtr fgBefore = Win32Interop.GetForegroundWindow();

            // Prefer position-based nth «إضافة» (slot 1,2,3… top→bottom) so we never skip slot 2.
            // Mapped selectors are fallback only (operator maps were often View/Delete/wrong ordinal).
            FieldSelector slotBtn = FindNthAddButton(map, slotIndex);
            if (slotBtn == null && !string.IsNullOrEmpty(slotKey))
            {
                FieldSelector mapped = FindConcreteSelector(selectors, slotKey);
                if (mapped != null && IsValidDocAddSelector(mapped))
                {
                    slotBtn = mapped;
                    log("doc slot " + (slotIndex + 1) + ": using mapped add (auto not found)", "INFO");
                }
            }
            if (slotBtn == null)
            {
                log("doc slot " + (slotIndex + 1) + ": add-file button not found", "WARN");
                return false;
            }
            ResolveResult rs = FieldDiscovery.ResolveField(map, slotBtn, "Index");
            if (rs.Handle == IntPtr.Zero)
            {
                log("doc slot " + (slotIndex + 1) + ": add-file button not found", "WARN");
                return false;
            }
            if (watch) { try { Win32Driver.Highlight(rs.Handle, 1); } catch { } Thread.Sleep(stepMs); }
            log("doc slot " + (slotIndex + 1) + ": click إضافة (Y-order slot " + (slotIndex + 1) + ")", "INFO");
            try { Win32Driver.SetForeground(entryWindow); } catch { }
            Win32Driver.ClickPhysical(rs.Handle, 450);

            IntPtr mostanad = WaitForMostanadWindow(pid, beforeTops, fgBefore, 7000, log);
            // If mapped/auto click missed المستند, try next candidate once (same slot row).
            if ((mostanad == IntPtr.Zero || !IsSafeDocPickerWindow(mostanad, entryWindow)) &&
                !string.IsNullOrEmpty(slotKey))
            {
                FieldSelector mapped = FindConcreteSelector(selectors, slotKey);
                if (mapped != null && IsValidDocAddSelector(mapped) &&
                    (slotBtn.ClassOrdinal != mapped.ClassOrdinal || slotBtn.Index != mapped.Index))
                {
                    log("doc slot " + (slotIndex + 1) + ": retry mapped add button", "WARN");
                    beforeTops = SnapshotTopHandles();
                    fgBefore = Win32Interop.GetForegroundWindow();
                    ResolveResult rs2 = FieldDiscovery.ResolveField(map, mapped, "Index");
                    if (rs2.Handle != IntPtr.Zero)
                    {
                        Win32Driver.ClickPhysical(rs2.Handle, 450);
                        mostanad = WaitForMostanadWindow(pid, beforeTops, fgBefore, 5000, log);
                    }
                }
            }
            if (mostanad == IntPtr.Zero || !IsSafeDocPickerWindow(mostanad, entryWindow))
            {
                log("doc slot " + (slotIndex + 1) + ": نافذة المستند did not appear (safe)", "WARN");
                return false;
            }

            log("نافذة المستند hwnd=" + mostanad.ToInt64() + " title='" +
                Win32Driver.GetWindowTitle(mostanad) + "'", "INFO");

            // Keep المستند at the size used during Setup click-map.
            if (WindowLayout.Active != null &&
                WindowLayout.Active.Mostanad != null &&
                WindowLayout.Active.Mostanad.IsValid)
            {
                WindowLayout.Apply(mostanad, WindowLayout.Active.Mostanad, log);
            }

            bool filled = false;
            // Real Coding «المستند» is VB6 Drive/Dir/File — select FILE then موافق.
            // Never paste a path into the entry form (was leaking into «الوظيفة»).
            if (HasVb6FileBrowserControls(mostanad))
            {
                filled = TryFillVb6FileBrowser(mostanad, imagePath, selectors, log);
            }
            else
            {
                log("المستند: لا توجد قوائم VB6 للتنقل — راجع التقاط «قائمة المجلدات»", "WARN");
                Vb6FileBrowser.DumpControls(mostanad, log);
            }
            if (!filled)
            {
                filled = TryFillMappedDocPath(mostanad, selectors, imagePath, log);
            }
            if (!filled && Win32Driver.GetClassName(mostanad) == "#32770")
            {
                filled = TryFillStandardOpenDialog(mostanad, imagePath, log);
            }
            // Clipboard paste ONLY on classic Open dialog — never on entry / المستند VB6.
            if (!filled && Win32Driver.GetClassName(mostanad) == "#32770")
            {
                filled = TryClipboardPasteAndConfirm(mostanad, imagePath, log);
            }

            DismissSelectDocumentError(pid, log);

            if (filled && Win32Interop.IsWindow(mostanad) && Win32Interop.IsWindowVisible(mostanad))
            {
                TryClickMappedDocOk(mostanad, selectors, log);
                WaitMostanadClosed(mostanad, 2500);
            }
            else if (filled)
            {
                Thread.Sleep(200);
            }
            else
            {
                // Leave the picker open and ConfirmDialog used to click موافق on it —
                // close with رجوع so the next slot / save prompt is not confused.
                log("مستند-DUMP: attach FAILED for «" + Path.GetFileName(imagePath) +
                    "» — snapshot follows", "WARN");
                Vb6FileBrowser.DumpState(mostanad, selectors, log);
                CloseMostanadWithoutAttach(mostanad, log);
            }
            return filled;
        }

        // Click the CheckBox HWND only once — never the Static caption «ملفات».
        private static bool ArmFilesCheckboxOnce(
            IntPtr entryWindow,
            ref FieldMap map,
            List<FieldSelector> selectors,
            FieldSelector chkFiles,
            Action<string, string> log)
        {
            map = FieldDiscovery.GetFieldMap(entryWindow, false);
            if (chkFiles == null)
            {
                chkFiles = FindConcreteSelector(selectors, FieldCatalog.ChkFilesKey);
            }
            if (chkFiles == null)
            {
                chkFiles = FindFilesCheckbox(map);
            }
            if (chkFiles == null)
            {
                log("checkbox ملفات not mapped — map the CheckBox only in Setup", "WARN");
                return false;
            }

            ResolveResult rr = FieldDiscovery.ResolveField(map, chkFiles, "Index");
            if (rr.Handle == IntPtr.Zero)
            {
                log("checkbox ملفات not found on screen", "WARN");
                return false;
            }

            try
            {
                if (Win32Driver.GetCheckState(rr.Handle))
                {
                    // Already armed from a previous client — toggle off/on so Coding
                    // re-enables «إضافة» for this form instance.
                    log("ملفات CheckBox already checked — toggle to re-arm docs", "INFO");
                    try { Win32Driver.SetForeground(entryWindow); } catch { }
                    Thread.Sleep(80);
                    Win32Driver.ClickPhysical(rr.Handle, 350);
                    Thread.Sleep(200);
                    try
                    {
                        if (Win32Driver.GetCheckState(rr.Handle))
                        {
                            Win32Driver.SetCheck(rr.Handle, false);
                            Thread.Sleep(150);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            try { Win32Driver.SetForeground(entryWindow); } catch { }
            Thread.Sleep(80);
            log("click ملفات CheckBox only (once) → arm docs", "INFO");
            Win32Driver.ClickPhysical(rr.Handle, 500);

            try
            {
                if (!Win32Driver.GetCheckState(rr.Handle))
                {
                    Win32Driver.SetCheck(rr.Handle, true);
                    Thread.Sleep(150);
                }
            }
            catch { }

            return true;
        }

        // Operator-mapped ComboBox/TextBox inside «المستند» that receives the file path.
        private static bool TryFillMappedDocPath(
            IntPtr mostanad,
            List<FieldSelector> selectors,
            string filePath,
            Action<string, string> log)
        {
            if (mostanad == IntPtr.Zero || !Win32Interop.IsWindow(mostanad)) { return false; }
            FieldSelector pathSel = FindConcreteSelector(selectors, FieldCatalog.DocPathKey);
            if (pathSel == null) { return false; }

            FieldMap map = FieldDiscovery.GetFieldMap(mostanad, false);
            ResolveResult rr = FieldDiscovery.ResolveField(map, pathSel, "Index");
            if (rr.Handle == IntPtr.Zero)
            {
                // Selector may have been captured against entry window; search children.
                rr = FieldDiscovery.ResolveField(FieldDiscovery.GetFieldMap(mostanad, true), pathSel, "Index");
            }
            if (rr.Handle == IntPtr.Zero)
            {
                log("docPath mapped but not found in المستند (will try auto path box)", "WARN");
                return false;
            }

            string cls = Win32Driver.GetClassName(rr.Handle);
            // Operator often click-maps DriveList by mistake — that is NOT the file.
            if (Regex.IsMatch(cls ?? "", "(?i)DriveListBox") || Regex.IsMatch(cls ?? "", "(?i)DirListBox"))
            {
                log("docPath points to Drive/Dir list — ignore; use VB6 file browser", "WARN");
                return false;
            }

            // FileListBox: select the file name (required before موافق).
            if (Regex.IsMatch(cls ?? "", "(?i)FileListBox") ||
                string.Equals(pathSel.Kind, "ListBox", StringComparison.OrdinalIgnoreCase))
            {
                string listFile = Path.GetFileName(filePath) ?? "";
                // The list only shows the folder VB6 is pointing at — walk there first.
                if (!Vb6FileBrowser.SelectFile(mostanad, filePath, selectors, log))
                {
                    log("docPath FileList: could not select " + listFile, "WARN");
                    return false;
                }
                Thread.Sleep(120);
                if (TryClickMappedDocOk(mostanad, selectors, log)) { return true; }
                if (ClickConfirmButton(mostanad))
                {
                    log("docPath FileList + confirm", "INFO");
                    return true;
                }
                try { Win32Driver.SendShortcut(mostanad, "{ENTER}", 300); } catch { }
                return true;
            }

            try { Win32Driver.SetForeground(mostanad); } catch { }
            Thread.Sleep(80);

            string folder = Path.GetDirectoryName(filePath) ?? "";
            string fileName = Path.GetFileName(filePath) ?? "";
            string kind = pathSel.Kind != null ? pathSel.Kind : FieldDiscovery.GetControlKind(Win32Driver.GetClassName(rr.Handle));

            bool setOk = false;
            // Full path first (what the operator usually types/selects).
            if (string.Equals(kind, "ComboBox", StringComparison.OrdinalIgnoreCase))
            {
                W32ActionResult combo = Win32Driver.SelectComboItem(rr.Handle, filePath);
                if (!combo.Ok) { combo = Win32Driver.SelectComboItem(rr.Handle, folder); }
                if (!combo.Ok) { combo = Win32Driver.SelectComboItem(rr.Handle, fileName); }
                if (combo.Ok)
                {
                    setOk = true;
                    log("docPath combo selected: " + combo.Note, "INFO");
                }
                // Editable dropdown: also force WM_SETTEXT with full path.
                W32ActionResult st = Win32Driver.SetText(rr.Handle, filePath);
                if (st.Ok || string.Equals(st.Actual, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    setOk = true;
                    log("docPath combo text set to full path", "INFO");
                }
            }
            else
            {
                W32ActionResult st = Win32Driver.SetText(rr.Handle, filePath);
                setOk = st.Ok || string.Equals(st.Actual, filePath, StringComparison.OrdinalIgnoreCase);
                if (setOk) { log("docPath text set to full path", "INFO"); }
            }

            if (!setOk)
            {
                // Nested Edit inside ComboBoxEx / Thunder combo.
                List<W32WindowInfo> kids = Win32Driver.GetChildren(rr.Handle);
                for (int i = 0; i < kids.Count && !setOk; i++)
                {
                    string kc = kids[i].Class != null ? kids[i].Class : "";
                    if (kc.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                    W32ActionResult st = Win32Driver.SetText(kids[i].Handle, filePath);
                    setOk = st.Ok || string.Equals(st.Actual, filePath, StringComparison.OrdinalIgnoreCase);
                    if (setOk) { log("docPath nested edit set", "INFO"); }
                }
            }

            if (!setOk)
            {
                log("docPath: could not set path on mapped control", "WARN");
                return false;
            }

            Thread.Sleep(120);
            if (TryClickMappedDocOk(mostanad, selectors, log)) { return true; }
            if (ClickConfirmButton(mostanad))
            {
                log("docPath: path set + confirm", "INFO");
                return true;
            }
            try { Win32Driver.SendShortcut(mostanad, "{ENTER}", 300); } catch { }
            log("docPath: path set + Enter", "INFO");
            return true;
        }

        private static bool WaitReplicaEvidenceGrow(string docsEvidence, int timeoutMs)
        {
            if (string.IsNullOrEmpty(docsEvidence)) { return false; }
            long evidenceLen = File.Exists(docsEvidence) ? new FileInfo(docsEvidence).Length : 0;
            DateTime until = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < until)
            {
                if (File.Exists(docsEvidence) && new FileInfo(docsEvidence).Length > evidenceLen)
                {
                    return true;
                }
                Thread.Sleep(80);
            }
            return false;
        }

        private static IntPtr WaitForMostanadWindow(
            int pid,
            Dictionary<long, bool> beforeTops,
            IntPtr fgBefore,
            int timeoutMs,
            Action<string, string> log)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < deadline)
            {
                IntPtr h = FindMostanadWindow(pid, beforeTops, fgBefore);
                if (h != IntPtr.Zero) { return h; }
                // Only accept a new picker that is clearly a file browser / Open dialog.
                h = FindNewPicker(beforeTops, pid, fgBefore);
                if (h != IntPtr.Zero && IsSafeDocPickerWindow(h, IntPtr.Zero)) { return h; }
                Thread.Sleep(120);
            }
            return IntPtr.Zero;
        }

        // Never treat the client-entry form as «المستند» (path was leaking into الوظيفة).
        private static bool IsSafeDocPickerWindow(IntPtr h, IntPtr entryWindow)
        {
            if (h == IntPtr.Zero || !Win32Interop.IsWindow(h)) { return false; }
            if (entryWindow != IntPtr.Zero && h == entryWindow) { return false; }
            string title = Win32Driver.GetWindowTitle(h) ?? "";
            string t = title.Trim();
            if (t.IndexOf("بيانات العملاء", StringComparison.Ordinal) >= 0) { return false; }
            if (t.IndexOf("تكويد العملاء", StringComparison.Ordinal) >= 0) { return false; }
            if (LooksLikeMostanadTitle(title)) { return true; }
            if (HasVb6FileBrowserControls(h)) { return true; }
            string cls = Win32Driver.GetClassName(h) ?? "";
            if (cls == "#32770" && LooksLikeOpenDialogTitle(title)) { return true; }
            return false;
        }

        private static bool IsValidDocAddSelector(FieldSelector s)
        {
            if (s == null) { return false; }
            string cls = s.Class != null ? s.Class : "";
            if (cls.IndexOf("MDIForm", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }
            if (cls.IndexOf("FormDC", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }
            if (cls.IndexOf("MDIClient", StringComparison.OrdinalIgnoreCase) >= 0) { return false; }
            // Must be a command button (caption may be empty on icon-only Add).
            if (cls.Length > 0 &&
                cls.IndexOf("CommandButton", StringComparison.OrdinalIgnoreCase) < 0 &&
                cls.IndexOf("Button", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }
            return true;
        }

        private static IntPtr FindMostanadWindow(int pid, Dictionary<long, bool> beforeTops, IntPtr fgBefore)
        {
            List<IntPtr> tops = Win32Interop.EnumWindowsList();
            for (int i = 0; i < tops.Count; i++)
            {
                IntPtr h = tops[i];
                if (!Win32Interop.IsWindowVisible(h)) { continue; }
                string title = Win32Driver.GetWindowTitle(h);
                if (!LooksLikeMostanadTitle(title)) { continue; }
                int wpid = Win32Driver.GetProcessId(h);
                if (pid > 0 && wpid != pid)
                {
                    // Still allow if brand-new and title matches المستند.
                    bool isNew = beforeTops == null || !beforeTops.ContainsKey((long)h);
                    if (!isNew) { continue; }
                }
                return h;
            }

            IntPtr fg = Win32Interop.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != fgBefore && LooksLikeMostanadTitle(Win32Driver.GetWindowTitle(fg)))
            {
                return fg;
            }

            // Child / MDI form titled المستند.
            List<W32WindowInfo> procTops = Win32Driver.GetProcessTopWindows(pid);
            for (int i = 0; i < procTops.Count; i++)
            {
                List<W32WindowInfo> forms = Win32Driver.GetFormWindows(procTops[i].Handle, 5);
                for (int f = 0; f < forms.Count; f++)
                {
                    string t = forms[f].Text != null ? forms[f].Text : forms[f].Title;
                    if (LooksLikeMostanadTitle(t) && forms[f].Visible)
                    {
                        return forms[f].Handle;
                    }
                }
            }
            return IntPtr.Zero;
        }

        private static bool LooksLikeMostanadTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) { return false; }
            string t = title.Trim();
            if (t == "المستند" || t == "مستند") { return true; }
            if (t.IndexOf("المستند", StringComparison.Ordinal) >= 0) { return true; }
            // Normalize alef / teh marbuta variants.
            string n = t.Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ة", "ه");
            if (n.IndexOf("المستند", StringComparison.Ordinal) >= 0) { return true; }
            if (n.IndexOf("مستند", StringComparison.Ordinal) >= 0 && t.Length <= 24) { return true; }
            return false;
        }

        private static void WaitMostanadClosed(IntPtr mostanad, int timeoutMs)
        {
            DateTime deadline = DateTime.Now.AddMilliseconds(timeoutMs);
            while (DateTime.Now < deadline)
            {
                if (mostanad == IntPtr.Zero || !Win32Interop.IsWindow(mostanad) ||
                    !Win32Interop.IsWindowVisible(mostanad))
                {
                    return;
                }
                Thread.Sleep(100);
            }
        }

        private static bool TryClickMappedDocOk(IntPtr mostanad, List<FieldSelector> selectors, Action<string, string> log)
        {
            if (mostanad == IntPtr.Zero || !Win32Interop.IsWindow(mostanad)) { return false; }
            FieldSelector ok = FindConcreteSelector(selectors, FieldCatalog.BtnDocOkKey);
            if (ok == null) { return false; }
            FieldMap map = FieldDiscovery.GetFieldMap(mostanad, false);
            ResolveResult rr = FieldDiscovery.ResolveField(map, ok, "Index");
            if (rr.Handle == IntPtr.Zero) { return false; }
            Win32Driver.ClickPhysical(rr.Handle, 250);
            log("clicked mapped btnDocOk", "INFO");
            return true;
        }

        private static FieldSelector FindFilesCheckbox(FieldMap map)
        {
            if (map == null || map.Fields == null) { return null; }
            // CheckBox only — never Button / Static caption next to «ملفات».
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f.Kind != "CheckBox") { continue; }
                string c = f.Caption != null ? f.Caption.Trim() : "";
                if (c == "ملفات" || c.Replace(" ", "") == "ملفات")
                {
                    FieldSelector s = new FieldSelector();
                    s.FieldKey = FieldCatalog.ChkFilesKey;
                    s.Kind = "CheckBox";
                    s.Class = f.Class;
                    s.ClassOrdinal = f.ClassOrdinal.ToString();
                    s.Index = f.Index.ToString();
                    s.Caption = f.Caption;
                    s.Section = "مستندات";
                    FieldDiscovery.StampPosition(s, map.Window, f.Handle);
                    return s;
                }
            }
            return null;
        }

        private static bool AttachViaReplicaSlot(
            IntPtr entryWindow,
            FieldMap map,
            List<FieldSelector> selectors,
            string slotKey,
            int slotIndex,
            string imagePath,
            string docsEvidence,
            bool watch,
            int stepMs,
            Action<string, string> log)
        {
            FieldSelector btn = FindSelector(selectors, slotKey);
            if (btn == null) { btn = FindNthAddButton(map, slotIndex); }
            if (btn == null)
            {
                log("doc slot " + (slotIndex + 1) + ": add button not mapped", "WARN");
                return false;
            }
            ResolveResult rr = FieldDiscovery.ResolveField(map, btn, "Index");
            if (rr.Handle == IntPtr.Zero)
            {
                log("doc slot " + (slotIndex + 1) + ": button not found", "WARN");
                return false;
            }
            if (watch)
            {
                try { Win32Driver.Highlight(rr.Handle, 2); } catch { }
                Thread.Sleep(stepMs);
            }
            long evidenceLen = File.Exists(docsEvidence) ? new FileInfo(docsEvidence).Length : 0;
            try { Win32Driver.SetForeground(entryWindow); } catch { }
            Win32Driver.ClickPhysical(rr.Handle, 400);
            DateTime until = DateTime.Now.AddMilliseconds(1500);
            while (DateTime.Now < until)
            {
                if (File.Exists(docsEvidence) && new FileInfo(docsEvidence).Length > evidenceLen)
                {
                    return true;
                }
                Thread.Sleep(80);
            }
            return false;
        }

        private static string FindReplicaDocsEvidence(IntPtr entryWindow)
        {
            try
            {
                int pid = Win32Driver.GetProcessId(entryWindow);
                Process p = Process.GetProcessById(pid);
                if (p == null || p.MainModule == null) { return ""; }
                string name = p.ProcessName != null ? p.ProcessName : "";
                if (name.IndexOf("CodingReplica", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return "";
                }
                string dir = Path.GetDirectoryName(p.MainModule.FileName);
                return Path.Combine(dir, "CodingReplica-docs.csv");
            }
            catch { }
            return "";
        }

        private static FieldSelector FindSelector(List<FieldSelector> selectors, string key)
        {
            if (selectors == null) { return null; }
            for (int i = 0; i < selectors.Count; i++)
            {
                if (string.Equals(selectors[i].FieldKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    return selectors[i];
                }
            }
            return null;
        }

        // Ignore catalog stubs that have no Class / ClassOrdinal yet.
        private static FieldSelector FindConcreteSelector(List<FieldSelector> selectors, string key)
        {
            FieldSelector s = FindSelector(selectors, key);
            if (s == null) { return null; }
            if (string.IsNullOrEmpty(s.Class) && string.IsNullOrEmpty(s.ClassOrdinal) &&
                string.IsNullOrEmpty(s.Index))
            {
                return null;
            }
            return s;
        }

        private static FieldSelector FindNthAddButton(FieldMap map, int zeroBased)
        {
            if (map == null || map.Fields == null) { return null; }
            List<AppField> adds = new List<AppField>();
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f.Kind != "Button") { continue; }
                string cls = f.Class != null ? f.Class : "";
                if (cls.IndexOf("CommandButton", StringComparison.OrdinalIgnoreCase) < 0 &&
                    cls.IndexOf("Button", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                if (cls.IndexOf("MDIForm", StringComparison.OrdinalIgnoreCase) >= 0) { continue; }
                string c = f.Caption != null ? f.Caption.Trim() : "";
                if (LooksLikeDocAddCaption(c)) { adds.Add(f); }
            }
            // Icon-only Add buttons often have empty caption — take small CommandButtons
            // stacked in the documents column (left/top area) if none matched by caption.
            if (adds.Count == 0)
            {
                for (int i = 0; i < map.Fields.Count; i++)
                {
                    AppField f = map.Fields[i];
                    if (f.Kind != "Button") { continue; }
                    string cls = f.Class != null ? f.Class : "";
                    if (cls.IndexOf("CommandButton", StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                    // Typical Add glyph buttons are small (~20–60 px).
                    if (f.W > 0 && f.W <= 70 && f.H > 0 && f.H <= 40) { adds.Add(f); }
                }
            }
            adds.Sort(CompareDocSlotOrder);
            // Prefer every 3rd small button if we collected Add/View/Del triples (إضافة/عرض/حذف).
            List<AppField> onlyAdd = PreferAddAmongTriples(adds);
            if (zeroBased < 0 || zeroBased >= onlyAdd.Count) { return null; }
            AppField hit = onlyAdd[zeroBased];
            FieldSelector s = new FieldSelector();
            s.FieldKey = "btnDocAdd" + (zeroBased + 1);
            s.Kind = "Button";
            s.Class = hit.Class;
            s.ClassOrdinal = hit.ClassOrdinal.ToString();
            s.Index = hit.Index.ToString();
            s.Caption = hit.Caption;
            s.Section = "مستندات";
            FieldDiscovery.StampPosition(s, map.Window, hit.Handle);
            return s;
        }

        // When each doc row has إضافة+عرض+حذف, keep the rightmost (RTL) or caption-matched Add.
        private static List<AppField> PreferAddAmongTriples(List<AppField> buttons)
        {
            if (buttons == null || buttons.Count == 0) { return new List<AppField>(); }
            List<AppField> captionAdds = new List<AppField>();
            for (int i = 0; i < buttons.Count; i++)
            {
                string c = buttons[i].Caption != null ? buttons[i].Caption.Trim() : "";
                if (LooksLikeDocAddCaption(c)) { captionAdds.Add(buttons[i]); }
            }
            if (captionAdds.Count > 0) { return captionAdds; }

            // Group by Y (one document slot per row). Wider band avoids splitting a row
            // and accidentally mapping index 1 → visual slot 3.
            List<AppField> picked = new List<AppField>();
            List<AppField> sorted = new List<AppField>(buttons);
            sorted.Sort(CompareDocSlotOrder);
            int i0 = 0;
            while (i0 < sorted.Count)
            {
                int y = sorted[i0].Y;
                List<AppField> row = new List<AppField>();
                while (i0 < sorted.Count && Math.Abs(sorted[i0].Y - y) <= 28)
                {
                    row.Add(sorted[i0]);
                    i0++;
                }
                // Coding docs row: حذف | عرض | إضافة — إضافة is typically the rightmost (max X).
                row.Sort(delegate(AppField a, AppField b) { return b.X.CompareTo(a.X); });
                picked.Add(row[0]);
            }
            return picked;
        }

        // Document rows stacked top→bottom (slot 1, then 2, then 3…).
        private static int CompareDocSlotOrder(AppField a, AppField b)
        {
            if (Math.Abs(a.Y - b.Y) > 20) { return a.Y.CompareTo(b.Y); }
            int byX = a.X.CompareTo(b.X);
            if (byX != 0) { return byX; }
            return a.ClassOrdinal.CompareTo(b.ClassOrdinal);
        }

        public static bool LooksLikeDocAddCaption(string caption)
        {
            // Strict: only Add/Attach — never View/Browse/Delete (those caused skipping slot 2).
            if (string.IsNullOrWhiteSpace(caption)) { return false; }
            string c = caption.Trim().Replace("&", "");
            if (c == "إضافة" || c == "اضافة" || c == "+" || c == "إرفاق" || c == "ارفاق") { return true; }
            string n = c.Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ة", "ه");
            n = n.Replace(" ", "");
            if (n == "اضافه" || n == "ارفاق") { return true; }
            if (n.StartsWith("اضافه", StringComparison.Ordinal)) { return true; }
            if (string.Equals(c, "Add", StringComparison.OrdinalIgnoreCase)) { return true; }
            if (string.Equals(c, "Attach", StringComparison.OrdinalIgnoreCase)) { return true; }
            return false;
        }

        // ---- File picker completion (Open dialog + VB6 Drive/Dir/File) --------

        private static Dictionary<long, bool> SnapshotTopHandles()
        {
            Dictionary<long, bool> set = new Dictionary<long, bool>();
            List<IntPtr> tops = Win32Interop.EnumWindowsList();
            for (int i = 0; i < tops.Count; i++)
            {
                if (!Win32Interop.IsWindowVisible(tops[i])) { continue; }
                set[(long)tops[i]] = true;
            }
            return set;
        }

        private static IntPtr FindNewPicker(Dictionary<long, bool> beforeTops, int pid, IntPtr fgBefore)
        {
            // Prefer brand-new top windows (any process — shell dialogs may differ).
            List<IntPtr> tops = Win32Interop.EnumWindowsList();
            for (int i = 0; i < tops.Count; i++)
            {
                IntPtr h = tops[i];
                if (!Win32Interop.IsWindowVisible(h)) { continue; }
                bool isNew = beforeTops == null || !beforeTops.ContainsKey((long)h);
                string cls = Win32Driver.GetClassName(h);
                string title = Win32Driver.GetWindowTitle(h);

                if (cls == "#32770" && (isNew || LooksLikeOpenDialogTitle(title)))
                {
                    return h;
                }
                if (isNew && HasVb6FileBrowserControls(h))
                {
                    return h;
                }
            }

            // Same-process picker that was already created but not in snapshot as visible.
            IntPtr same = FindPickerWindow(pid);
            if (same != IntPtr.Zero) { return same; }

            // Foreground changed to something that looks like a picker.
            IntPtr fg = Win32Interop.GetForegroundWindow();
            if (fg != IntPtr.Zero && fg != fgBefore)
            {
                string cls = Win32Driver.GetClassName(fg);
                if (cls == "#32770" || HasVb6FileBrowserControls(fg))
                {
                    return fg;
                }
            }
            return IntPtr.Zero;
        }

        private static bool TryClipboardPasteAndConfirm(IntPtr window, string filePath, Action<string, string> log)
        {
            // Hard guard: never paste a filesystem path into the client entry form.
            if (!IsSafeDocPickerWindow(window, IntPtr.Zero))
            {
                log("clipboard-paste blocked — target is not a document picker", "WARN");
                return false;
            }
            string cls = Win32Driver.GetClassName(window) ?? "";
            if (cls != "#32770")
            {
                log("clipboard-paste skipped — use VB6 FileList select instead", "INFO");
                return false;
            }
            try
            {
                try { Win32Driver.SetForeground(window); } catch { }
                Thread.Sleep(100);
                // Filename box in modern Open dialog: Alt+N / Ctrl+L then paste.
                try { System.Windows.Forms.Clipboard.SetText(filePath); } catch { return false; }
                try { Win32Driver.SendShortcut(window, "%n", 80); } catch { }
                try { Win32Driver.SendShortcut(window, "^a", 40); } catch { }
                try { Win32Driver.SendShortcut(window, "^v", 120); } catch { }
                Thread.Sleep(100);
                if (!ClickConfirmButton(window))
                {
                    // IDOK = 1 on classic dialogs
                    IntPtr ok = Win32Interop.GetDlgItem(window, Win32Interop.IDOK);
                    if (ok != IntPtr.Zero)
                    {
                        Win32Driver.Click(ok, 200, false);
                    }
                    else
                    {
                        Win32Driver.SendShortcut(window, "{ENTER}", 300);
                    }
                }
                log("clipboard-paste: path + confirm", "INFO");
                Thread.Sleep(250);
                return true;
            }
            catch (Exception ex)
            {
                log("clipboard-paste failed: " + ex.Message, "WARN");
                return false;
            }
        }

        private static IntPtr FindPickerWindow(int pid)
        {
            List<W32WindowInfo> tops = Win32Driver.GetProcessTopWindows(pid);
            for (int i = 0; i < tops.Count; i++)
            {
                IntPtr h = tops[i].Handle;
                string cls = tops[i].Class != null ? tops[i].Class : "";
                string title = tops[i].Title != null ? tops[i].Title : "";

                if (cls == "#32770" && LooksLikeOpenDialogTitle(title))
                {
                    return h;
                }
                if (HasVb6FileBrowserControls(h))
                {
                    return h;
                }
            }

            // Owned / child forms inside MDI (picker sometimes not top-level).
            for (int i = 0; i < tops.Count; i++)
            {
                List<W32WindowInfo> forms = Win32Driver.GetFormWindows(tops[i].Handle, 4);
                for (int f = 0; f < forms.Count; f++)
                {
                    if (HasVb6FileBrowserControls(forms[f].Handle))
                    {
                        return forms[f].Handle;
                    }
                    if (forms[f].Class == "#32770" && LooksLikeOpenDialogTitle(forms[f].Title ?? forms[f].Text))
                    {
                        return forms[f].Handle;
                    }
                }
            }

            // Any visible #32770 Open dialog on the desktop (last resort).
            List<IntPtr> all = Win32Interop.EnumWindowsList();
            for (int i = 0; i < all.Count; i++)
            {
                IntPtr h = all[i];
                if (!Win32Interop.IsWindowVisible(h)) { continue; }
                if (Win32Driver.GetClassName(h) != "#32770") { continue; }
                if (LooksLikeOpenDialogTitle(Win32Driver.GetWindowTitle(h)))
                {
                    return h;
                }
            }
            return IntPtr.Zero;
        }

        private static bool LooksLikeOpenDialogTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) { return true; } // untitled #32770 still may be Open
            if (title.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            if (title.IndexOf("Browse", StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            if (title.IndexOf("فتح", StringComparison.Ordinal) >= 0) { return true; }
            if (title.IndexOf("ملف", StringComparison.Ordinal) >= 0) { return true; }
            if (title.IndexOf("اختيار", StringComparison.Ordinal) >= 0) { return true; }
            if (title.IndexOf("صورة", StringComparison.Ordinal) >= 0) { return true; }
            if (title.IndexOf("مستند", StringComparison.Ordinal) >= 0) { return true; }
            if (title.IndexOf("Scan", StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            return false;
        }

        private static bool HasVb6FileBrowserControls(IntPtr root)
        {
            if (root == IntPtr.Zero) { return false; }
            List<W32WindowInfo> kids = Win32Driver.GetChildren(root);
            bool file = false, dir = false, drive = false;
            for (int i = 0; i < kids.Count; i++)
            {
                string c = kids[i].Class != null ? kids[i].Class : "";
                if (Regex.IsMatch(c, "(?i)FileListBox")) { file = true; }
                else if (Regex.IsMatch(c, "(?i)DirListBox")) { dir = true; }
                else if (Regex.IsMatch(c, "(?i)DriveListBox")) { drive = true; }
            }
            return file || (dir && drive);
        }

        private static bool TryFillStandardOpenDialog(IntPtr dialog, string filePath, Action<string, string> log)
        {
            string cls = Win32Driver.GetClassName(dialog);
            if (cls != "#32770") { return false; }

            IntPtr edit = FindFilenameEdit(dialog);
            if (edit == IntPtr.Zero)
            {
                // Still try clipboard path on bare dialog.
                return TryClipboardPasteAndConfirm(dialog, filePath, log);
            }

            try { Win32Driver.SetForeground(dialog); } catch { }
            Win32Driver.SetText(edit, filePath);
            Thread.Sleep(120);

            IntPtr ok = Win32Interop.GetDlgItem(dialog, Win32Interop.IDOK);
            if (ok != IntPtr.Zero)
            {
                Win32Driver.Click(ok, 200, false);
                log("open-dialog: path set + IDOK", "INFO");
                Thread.Sleep(200);
                return true;
            }
            if (ClickConfirmButton(dialog))
            {
                log("open-dialog: path set + confirm", "INFO");
                Thread.Sleep(200);
                return true;
            }
            try { Win32Driver.SendShortcut(dialog, "{ENTER}", 300); } catch { }
            log("open-dialog: path set + Enter", "INFO");
            Thread.Sleep(200);
            return true;
        }

        // Classic Common Item Dialog nests: ComboBoxEx32 → Edit.
        private static IntPtr FindFilenameEdit(IntPtr dialog)
        {
            List<W32WindowInfo> kids = Win32Driver.GetChildren(dialog);
            IntPtr best = IntPtr.Zero;
            for (int i = 0; i < kids.Count; i++)
            {
                if (!kids[i].Visible) { continue; }
                string kc = kids[i].Class != null ? kids[i].Class : "";
                if (kc.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    kc.IndexOf("TextBox", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Prefer deepest / last Edit (filename box is usually last).
                    best = kids[i].Handle;
                }
            }
            return best;
        }

        private static bool TryFillVb6FileBrowser(
            IntPtr picker, string filePath, List<FieldSelector> selectors, Action<string, string> log)
        {
            if (!HasVb6FileBrowserControls(picker)) { return false; }

            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName)) { return false; }

            // Drive → folder chain → file row, each step verified against the list.
            if (!Vb6FileBrowser.SelectFile(picker, filePath, selectors, log))
            {
                log("vb6-picker: SelectFile FAILED for «" + fileName + "»", "WARN");
                Vb6FileBrowser.DumpState(picker, selectors, log);
                return false;
            }

            // Confirm (موافق) — without a selected file Coding shows «اختر المستند أولاً».
            if (ClickConfirmButton(picker))
            {
                log("vb6-picker: " + fileName + " + confirm", "INFO");
                Thread.Sleep(250);
                return true;
            }

            Vb6FileBrowser.DoubleClickSelectedFile(picker, fileName);
            if (!Win32Interop.IsWindow(picker) || !Win32Interop.IsWindowVisible(picker))
            {
                log("vb6-picker: " + fileName + " accepted by double-click", "INFO");
                return true;
            }

            try { Win32Driver.SendShortcut(picker, "{ENTER}", 350); }
            catch { }
            log("vb6-picker: " + fileName + " + Enter", "INFO");
            Thread.Sleep(250);
            return true;
        }

        // Coding.exe: «من فضلك قم بإختيار المستند أولاً» after موافق without a file.
        private static void DismissSelectDocumentError(int pid, Action<string, string> log)
        {
            try
            {
                DateTime until = DateTime.Now.AddMilliseconds(900);
                while (DateTime.Now < until)
                {
                    List<IntPtr> tops = Win32Interop.EnumWindowsList();
                    for (int i = 0; i < tops.Count; i++)
                    {
                        IntPtr h = tops[i];
                        if (!Win32Interop.IsWindowVisible(h)) { continue; }
                        if (pid > 0 && Win32Driver.GetProcessId(h) != pid) { continue; }
                        string title = Win32Driver.GetWindowTitle(h) ?? "";
                        string cls = Win32Driver.GetClassName(h) ?? "";
                        if (cls != "#32770" && title.IndexOf("تكويد", StringComparison.Ordinal) < 0 &&
                            title.Length > 40)
                        {
                            continue;
                        }
                        // Read static text children for the Arabic error.
                        List<W32WindowInfo> kids = Win32Driver.GetChildren(h);
                        bool match = false;
                        for (int k = 0; k < kids.Count; k++)
                        {
                            string t = kids[k].Text != null ? kids[k].Text : "";
                            if (t.IndexOf("اختيار المستند", StringComparison.Ordinal) >= 0 ||
                                t.IndexOf("إختيار المستند", StringComparison.Ordinal) >= 0 ||
                                t.IndexOf("اختر المستند", StringComparison.Ordinal) >= 0)
                            {
                                match = true;
                                break;
                            }
                        }
                        if (!match) { continue; }
                        log("dismiss Coding error: اختر المستند أولاً", "WARN");
                        IntPtr ok = Win32Interop.GetDlgItem(h, Win32Interop.IDOK);
                        if (ok != IntPtr.Zero) { Win32Driver.Click(ok, 150, false); }
                        else { ClickConfirmButton(h); }
                        Thread.Sleep(150);
                        return;
                    }
                    Thread.Sleep(80);
                }
            }
            catch { }
        }

        private static bool ClickConfirmButton(IntPtr root)
        {
            List<W32WindowInfo> kids = Win32Driver.GetChildren(root);
            string[] prefer = new string[]
            {
                "فتح", "موافق", "تأكيد", "اختيار", "إضافة", "اضافة", "OK", "Open", "&Open", "Ok"
            };
            for (int p = 0; p < prefer.Length; p++)
            {
                for (int i = 0; i < kids.Count; i++)
                {
                    if (!kids[i].Visible) { continue; }
                    string kind = FieldDiscovery.GetControlKind(kids[i].Class);
                    if (kind != "Button") { continue; }
                    string t = kids[i].Text != null ? kids[i].Text.Trim() : "";
                    if (string.Equals(t, prefer[p], StringComparison.OrdinalIgnoreCase) ||
                        t.Replace("&", "").Equals(prefer[p].Replace("&", ""), StringComparison.OrdinalIgnoreCase))
                    {
                        // Never click Cancel / إلغاء / حذف.
                        if (LooksLikeCancel(t)) { continue; }
                        Win32Driver.Click(kids[i].Handle, 200, false);
                        return true;
                    }
                }
            }
            // Soft match: any visible button containing فتح/موافق (not cancel).
            for (int i = 0; i < kids.Count; i++)
            {
                if (!kids[i].Visible) { continue; }
                if (FieldDiscovery.GetControlKind(kids[i].Class) != "Button") { continue; }
                string t = kids[i].Text != null ? kids[i].Text.Trim() : "";
                if (LooksLikeCancel(t)) { continue; }
                if (t.IndexOf("فتح", StringComparison.Ordinal) >= 0 ||
                    t.IndexOf("موافق", StringComparison.Ordinal) >= 0 ||
                    t.IndexOf("Open", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("OK", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Win32Driver.Click(kids[i].Handle, 200, false);
                    return true;
                }
            }
            return false;
        }

        private static bool LooksLikeCancel(string caption)
        {
            if (string.IsNullOrEmpty(caption)) { return false; }
            string t = caption.Trim().Replace("&", "");
            if (t.IndexOf("إلغاء", StringComparison.Ordinal) >= 0) { return true; }
            if (t.IndexOf("الغاء", StringComparison.Ordinal) >= 0) { return true; }
            if (t.IndexOf("حذف", StringComparison.Ordinal) >= 0) { return true; }
            if (t.Equals("Cancel", StringComparison.OrdinalIgnoreCase)) { return true; }
            if (t.Equals("Close", StringComparison.OrdinalIgnoreCase)) { return true; }
            if (t.Equals("إغلاق", StringComparison.Ordinal)) { return true; }
            return false;
        }

        // Close «المستند» with رجوع / Cancel when attach failed — never موافق.
        private static void CloseMostanadWithoutAttach(IntPtr mostanad, Action<string, string> log)
        {
            if (mostanad == IntPtr.Zero || !Win32Interop.IsWindow(mostanad) ||
                !Win32Interop.IsWindowVisible(mostanad))
            {
                return;
            }

            List<W32WindowInfo> kids = Win32Driver.GetChildren(mostanad);
            string[] back = new string[] { "رجوع", "إلغاء", "الغاء", "Cancel", "Close", "إغلاق" };
            for (int p = 0; p < back.Length; p++)
            {
                for (int i = 0; i < kids.Count; i++)
                {
                    if (!kids[i].Visible) { continue; }
                    if (FieldDiscovery.GetControlKind(kids[i].Class) != "Button") { continue; }
                    string t = kids[i].Text != null ? kids[i].Text.Trim().Replace("&", "") : "";
                    if (!string.Equals(t, back[p], StringComparison.OrdinalIgnoreCase)) { continue; }
                    log("المستند: closing with «" + t + "» after failed attach", "WARN");
                    Win32Driver.Click(kids[i].Handle, 200, false);
                    WaitMostanadClosed(mostanad, 2000);
                    return;
                }
            }

            try { Win32Interop.PostMessage(mostanad, Win32Interop.WM_CLOSE, IntPtr.Zero, IntPtr.Zero); }
            catch { }
            log("المستند: posted WM_CLOSE after failed attach", "WARN");
            WaitMostanadClosed(mostanad, 1500);
        }
    }
}
