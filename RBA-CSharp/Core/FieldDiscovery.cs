// =============================================================================
//  FieldDiscovery.cs - classify controls, map a window, resolve selectors.
//  Ported from RBA/_shared/FieldDiscovery.ps1 (critical path for EGX Coding).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace RbaCore
{
    public class AppField
    {
        public int Index;
        public IntPtr Handle;
        public IntPtr Parent;
        public string Class;
        public string Kind;
        public string Caption;
        public int ControlId;
        public string AccName;
        public string Label;
        public string LabelHow;
        public string Section;
        public int ClassOrdinal;
        public bool Enabled;
        public bool Visible;
        public int X, Y, W, H, Right, CenterX, CenterY;
    }

    public class FieldMap
    {
        public IntPtr Window;
        public string Title;
        public string Class;
        public List<AppField> Fields;
        public List<AppField> Labels;
        public List<AppField> AllControls;
        public string CaptionSource;
    }

    public class FieldSelector
    {
        public string FieldKey;
        public string FieldLabel;
        public string Kind;
        public string Class;
        public string AccName;
        public string ControlId;
        public string Label;
        public string Caption;
        public string ClassOrdinal;
        public string Index;
        public string LabelOrdinal;
        public string SheetColumn;
        public string DatePart;
        public string SourceKey;
        public string Required;
        public string ConfirmedAt;
        public string Aliases;
        public string Screen;
        public string Shortcut;
        public string Section;
        // Form-relative geometry captured at click time. Immune to control
        // insertion (e.g. «الوظيفة») which invalidates ClassOrdinal / Index.
        public string PosX;
        public string PosY;
        public string PosW;
        public string PosH;

        public bool HasPosition
        {
            get
            {
                int w, h;
                if (!int.TryParse(PosW, out w) || w <= 0) { return false; }
                if (!int.TryParse(PosH, out h) || h <= 0) { return false; }
                int x, y;
                return int.TryParse(PosX, out x) && int.TryParse(PosY, out y);
            }
        }
    }

    public class ResolveResult
    {
        public IntPtr Handle;
        public string Tier;
        public bool Ambiguous;
        public string Note;
    }

    public static class FieldDiscovery
    {
        public static readonly string[] InputKinds = new string[] {
            "TextBox","ComboBox","CheckBox","RadioButton","DatePicker",
            "ListBox","Button","Grid","Picture","Toolbar","StatusBar"
        };

        private static readonly Dictionary<string, int> TierRank = BuildTierRank();

        private static Dictionary<string, int> BuildTierRank()
        {
            Dictionary<string, int> d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            d["Position"] = 70;
            d["AccName"] = 60;
            d["ControlId"] = 50;
            d["Label"] = 40;
            d["Caption"] = 30;
            d["ClassOrdinal"] = 20;
            d["Index"] = 10;
            d["None"] = 0;
            return d;
        }

        public static string GetControlKind(string className)
        {
            string c = className != null ? className : "";
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?(TextBox|EditBox)")) { return "TextBox"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?ComboBox")) { return "ComboBox"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?ListBox")) { return "ListBox"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?CheckBox")) { return "CheckBox"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?OptionButton")) { return "RadioButton"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?CommandButton")) { return "Button"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?Frame")) { return "Group"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?UserControl")) { return "Group"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?(MDIForm|Form)")) { return "Form"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?FileListBox")) { return "ListBox"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?DirListBox")) { return "ListBox"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?DriveListBox")) { return "ComboBox"; }
            if (Regex.IsMatch(c, "(?i)^Thunder(RT\\d+)?PictureBox")) { return "Picture"; }
            if (Regex.IsMatch(c, "(?i)(MaskEdBox|MSMaskWndClass)")) { return "TextBox"; }
            if (Regex.IsMatch(c, "(?i)(RichTextWndClass)")) { return "TextBox"; }
            if (Regex.IsMatch(c, "(?i)(MSFlexGridWndClass|MSHFlexGridWndClass|DataGridWndClass|VSFlexGrid|FlexGrid|DBGrid|TrueDBGrid|TDBGrid|FarPoint|FpSpread)")) { return "Grid"; }
            if (Regex.IsMatch(c, "(?i)(SSTab|TabDlg|SysTabControl)")) { return "Tab"; }
            if (Regex.IsMatch(c, "(?i)(^|\\.)(EDIT|TEdit|TMemo|TMaskEdit|RichEdit)")) { return "TextBox"; }
            if (Regex.IsMatch(c, "(?i)(^|\\.)(COMBOBOX|TComboBox)")) { return "ComboBox"; }
            if (Regex.IsMatch(c, "(?i)(^|\\.)(BUTTON|TButton|TBitBtn)")) { return "Button"; }
            if (Regex.IsMatch(c, "(?i)(^|\\.)(STATIC|TLabel)")) { return "Label"; }
            if (Regex.IsMatch(c, "(?i)(TGroupBox|TPanel)")) { return "Group"; }
            if (Regex.IsMatch(c, "(?i)(^|\\.)(TCheckBox)")) { return "CheckBox"; }
            if (Regex.IsMatch(c, "(?i)(LISTBOX|TListBox)")) { return "ListBox"; }
            return "Other";
        }

        public static string NormalizeKey(string text, bool loose)
        {
            if (string.IsNullOrEmpty(text)) { return ""; }
            string t = text.Trim();
            t = t.Replace("\u0640", ""); // tatweel
            t = Regex.Replace(t, "[\\u064B-\\u065F\\u0670]", ""); // harakat
            t = t.Replace("\u200E", "").Replace("\u200F", "").Replace("\u202A", "").Replace("\u202B", "").Replace("\u202C", "");
            t = t.Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ى", "ي").Replace("ة", "ه");
            if (loose)
            {
                t = Regex.Replace(t, "\\s+", "");
            }
            else
            {
                t = Regex.Replace(t, "\\s+", " ").Trim();
            }
            return t;
        }

        public static bool TitleMatch(string title, string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(title)) { return false; }
            try { if (Regex.IsMatch(title, pattern)) { return true; } } catch { }
            string t = NormalizeKey(title, true);
            string p = NormalizeKey(pattern, true);
            if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(p)) { return false; }
            return t == p || t.IndexOf(p, StringComparison.Ordinal) >= 0 || p.IndexOf(t, StringComparison.Ordinal) >= 0;
        }

        private static bool IsInputKind(string kind)
        {
            for (int i = 0; i < InputKinds.Length; i++)
            {
                if (InputKinds[i] == kind) { return true; }
            }
            return false;
        }

        private static LabelPair FindAdjacentLabel(AppField field, List<AppField> labels)
        {
            LabelPair none = new LabelPair();
            none.Label = "";
            none.How = "none";
            if (labels == null || labels.Count == 0) { return none; }

            int tolerance = Math.Max(8, (field.H / 2) + 6);
            List<Cand> candidates = new List<Cand>();
            for (int i = 0; i < labels.Count; i++)
            {
                AppField l = labels[i];
                int dy = Math.Abs(l.CenterY - field.CenterY);
                if (dy > tolerance) { continue; }
                Cand c = new Cand();
                c.Text = l.Caption;
                c.Dy = dy;
                if (l.X >= (field.Right - 4))
                {
                    c.How = "right";
                    c.Distance = l.X - field.Right;
                    candidates.Add(c);
                }
                else if (l.Right <= (field.X + 4))
                {
                    c.How = "left";
                    c.Distance = field.X - l.Right;
                    candidates.Add(c);
                }
            }

            Cand best = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Distance > 240) { continue; }
                if (best == null || candidates[i].Dy < best.Dy ||
                    (candidates[i].Dy == best.Dy && candidates[i].Distance < best.Distance))
                {
                    best = candidates[i];
                }
            }
            if (best != null)
            {
                LabelPair p = new LabelPair();
                p.Label = best.Text;
                p.How = best.How;
                return p;
            }

            AppField above = null;
            int bestAbove = int.MaxValue;
            for (int i = 0; i < labels.Count; i++)
            {
                AppField l = labels[i];
                if (l.CenterY >= field.CenterY) { continue; }
                int dist = field.CenterY - l.CenterY;
                if (dist > 46) { continue; }
                if (l.X >= (field.Right + 24) || l.Right <= (field.X - 24)) { continue; }
                if (dist < bestAbove) { bestAbove = dist; above = l; }
            }
            if (above != null)
            {
                LabelPair p = new LabelPair();
                p.Label = above.Caption;
                p.How = "above";
                return p;
            }
            return none;
        }

        private class Cand { public string Text; public string How; public int Distance; public int Dy; }
        private class LabelPair { public string Label; public string How; }

        public static FieldMap GetFieldMap(IntPtr window, bool includeInvisible)
        {
            Win32Driver.AssertHandle(window, "target window");
            List<W32WindowInfo> kids = Win32Driver.GetChildren(window);
            List<AppField> all = new List<AppField>();
            Dictionary<long, AppField> byHandle = new Dictionary<long, AppField>();
            int i = 0;
            for (int k = 0; k < kids.Count; k++)
            {
                W32WindowInfo kid = kids[k];
                i++;
                if (!includeInvisible && !kid.Visible) { continue; }
                Win32Driver.RectInfo rect = Win32Driver.GetRect(kid.Handle);
                AppField rec = new AppField();
                rec.Index = i;
                rec.Handle = kid.Handle;
                rec.Parent = Win32Interop.GetParent(kid.Handle);
                rec.Class = kid.Class;
                rec.Kind = GetControlKind(kid.Class);
                rec.Caption = kid.Text;
                try { rec.ControlId = Win32Interop.GetDlgCtrlID(kid.Handle); } catch { rec.ControlId = 0; }
                rec.AccName = "";
                rec.Label = "";
                rec.LabelHow = "";
                rec.Section = "";
                rec.Enabled = Win32Interop.IsWindowEnabled(kid.Handle);
                rec.Visible = kid.Visible;
                rec.X = rect.X; rec.Y = rect.Y; rec.W = rect.W; rec.H = rect.H;
                rec.Right = rect.Right; rec.CenterX = rect.CenterX; rec.CenterY = rect.CenterY;
                all.Add(rec);
                byHandle[(long)kid.Handle] = rec;
            }

            Dictionary<string, int> perClass = new Dictionary<string, int>();
            for (int n = 0; n < all.Count; n++)
            {
                string key = all[n].Class != null ? all[n].Class : "";
                if (!perClass.ContainsKey(key)) { perClass[key] = 0; }
                perClass[key] = perClass[key] + 1;
                all[n].ClassOrdinal = perClass[key];
            }

            for (int n = 0; n < all.Count; n++)
            {
                long p = (long)all[n].Parent;
                int guard = 0;
                while (p != 0 && guard < 12)
                {
                    guard++;
                    if (!byHandle.ContainsKey(p)) { break; }
                    AppField anc = byHandle[p];
                    if (anc.Kind == "Group" && !string.IsNullOrWhiteSpace(anc.Caption))
                    {
                        all[n].Section = anc.Caption;
                        break;
                    }
                    p = (long)anc.Parent;
                }
            }

            List<AppField> labels = new List<AppField>();
            for (int n = 0; n < all.Count; n++)
            {
                if (all[n].Kind != "Label") { continue; }
                if (string.IsNullOrWhiteSpace(all[n].Caption)) { continue; }
                labels.Add(all[n]);
            }

            List<AppField> fields = new List<AppField>();
            for (int n = 0; n < all.Count; n++)
            {
                AppField c = all[n];
                // Large VB6 Frames wrap MSFlexGrid. They are not InputKinds, but
                // lookupGrid / confirmGrid resolve by position against this list.
                bool gridSurface = c.Kind == "Group" && c.W >= 220 && c.H >= 90;
                if (!IsInputKind(c.Kind) && !gridSurface) { continue; }
                if ((c.Kind == "Button" || c.Kind == "CheckBox" || c.Kind == "RadioButton") &&
                    !string.IsNullOrWhiteSpace(c.Caption))
                {
                    c.Label = c.Caption;
                    c.LabelHow = "own caption";
                }
                else
                {
                    LabelPair pair = FindAdjacentLabel(c, labels);
                    c.Label = pair.Label;
                    c.LabelHow = pair.How;
                }
                fields.Add(c);
            }

            FieldMap map = new FieldMap();
            map.Window = window;
            map.Title = Win32Driver.GetWindowText(window);
            map.Class = Win32Driver.GetClassName(window);
            map.Fields = fields;
            map.Labels = labels;
            map.AllControls = all;
            map.CaptionSource = labels.Count > 0 ? "windows" : "none";
            return map;
        }

        public static ResolveResult ResolveField(FieldMap map, FieldSelector selector, string minTier)
        {
            ResolveResult answer = new ResolveResult();
            answer.Handle = IntPtr.Zero;
            answer.Tier = "None";
            answer.Ambiguous = false;
            answer.Note = "";

            if (map == null || map.Fields == null || selector == null) { return answer; }
            if (string.IsNullOrEmpty(minTier)) { minTier = "Index"; }
            int minRank = TierRank.ContainsKey(minTier) ? TierRank[minTier] : 10;

            List<AppField> fields = map.Fields;
            string kind = selector.Kind != null ? selector.Kind : "";
            string className = selector.Class != null ? selector.Class : "";

            // 0 Position — captured screen geometry is the only identity that survives
            // Coding inserting «الوظيفة» after الشكل القانوني.
            ResolveResult byPos = ResolveByPosition(map, selector);
            if (byPos.Handle != IntPtr.Zero) { return byPos; }

            // Operator click-map: ONLY the exact control that was picked — never
            // another field that shares the same Arabic label (e.g. two «اسم»).
            if (IsExactClickMap(selector))
            {
                return ResolveExactClickMap(fields, selector, kind, className);
            }

            List<AppField> pool = new List<AppField>();
            if (!string.IsNullOrEmpty(kind))
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    if (fields[i].Kind == kind) { pool.Add(fields[i]); }
                }
            }
            if (pool.Count == 0) { pool = fields; }

            // Typed fields with ClassOrdinal still prefer ordinal before fuzzy labels.
            bool typedField =
                string.Equals(kind, "TextBox", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "ComboBox", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "DatePicker", StringComparison.OrdinalIgnoreCase);

            int cordEarly = 0;
            int.TryParse(selector.ClassOrdinal, out cordEarly);
            if (typedField && cordEarly > 0 && !string.IsNullOrEmpty(className) &&
                TierRank["ClassOrdinal"] >= minRank)
            {
                ResolveResult exact = MatchClassOrdinal(fields, kind, className, cordEarly);
                if (exact.Handle != IntPtr.Zero) { return exact; }
            }

            // 1 AccName
            if (!string.IsNullOrEmpty(selector.AccName))
            {
                List<AppField> hit = FilterByAccName(pool, selector.AccName);
                if (hit.Count == 0) { hit = FilterByAccName(fields, selector.AccName); }
                if (hit.Count == 1)
                {
                    answer.Handle = hit[0].Handle;
                    answer.Tier = "AccName";
                    return answer;
                }
                if (hit.Count > 1) { answer.Note = "AccName matched " + hit.Count; }
            }

            // 2 ControlId
            int cid = 0;
            int.TryParse(selector.ControlId, out cid);
            if (cid != 0 && cid != 65535 && TierRank["ControlId"] >= minRank)
            {
                List<AppField> hit = new List<AppField>();
                for (int i = 0; i < pool.Count; i++)
                {
                    if (pool[i].ControlId == cid && pool[i].ControlId != (int)((long)pool[i].Handle))
                    {
                        hit.Add(pool[i]);
                    }
                }
                if (hit.Count == 1)
                {
                    answer.Handle = hit[0].Handle;
                    answer.Tier = "ControlId";
                    return answer;
                }
            }

            // 3 Label
            if (!string.IsNullOrEmpty(selector.Label) && TierRank["Label"] >= minRank)
            {
                string want = NormalizeKey(selector.Label, false);
                List<AppField> hit = new List<AppField>();
                for (int i = 0; i < pool.Count; i++)
                {
                    if (NormalizeKey(pool[i].Label, false) == want) { hit.Add(pool[i]); }
                }
                if (hit.Count == 0)
                {
                    string wantLoose = NormalizeKey(selector.Label, true);
                    for (int i = 0; i < pool.Count; i++)
                    {
                        if (NormalizeKey(pool[i].Label, true) == wantLoose) { hit.Add(pool[i]); }
                    }
                }
                if (hit.Count == 1)
                {
                    answer.Handle = hit[0].Handle;
                    answer.Tier = "Label";
                    return answer;
                }
                if (hit.Count > 1)
                {
                    int ord = 0;
                    int.TryParse(selector.LabelOrdinal, out ord);
                    if (ord >= 1 && ord <= hit.Count)
                    {
                        hit.Sort(CompareByReadingOrder);
                        answer.Handle = hit[ord - 1].Handle;
                        answer.Tier = "Label";
                        answer.Note = "ordinal " + ord;
                        return answer;
                    }
                    answer.Ambiguous = true;
                    answer.Note = "caption shared by " + hit.Count;
                }
            }

            // 4 Caption — buttons only (TextBox/Combo Caption is unreliable / polluted).
            bool typedFieldCap =
                string.Equals(kind, "TextBox", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "ComboBox", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "DatePicker", StringComparison.OrdinalIgnoreCase);
            if (!typedFieldCap && !string.IsNullOrEmpty(selector.Caption) && TierRank["Caption"] >= minRank)
            {
                string want = NormalizeKey(selector.Caption, false);
                List<AppField> hit = new List<AppField>();
                for (int i = 0; i < pool.Count; i++)
                {
                    if (NormalizeKey(pool[i].Caption, false) == want) { hit.Add(pool[i]); }
                }
                if (hit.Count == 1)
                {
                    answer.Handle = hit[0].Handle;
                    answer.Tier = "Caption";
                    return answer;
                }
            }

            // 5 ClassOrdinal
            int cord = 0;
            int.TryParse(selector.ClassOrdinal, out cord);
            if (cord > 0 && !string.IsNullOrEmpty(className) && TierRank["ClassOrdinal"] >= minRank)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    if (fields[i].Class == className && fields[i].ClassOrdinal == cord)
                    {
                        if (string.IsNullOrEmpty(kind) || fields[i].Kind == kind)
                        {
                            answer.Handle = fields[i].Handle;
                            answer.Tier = "ClassOrdinal";
                            answer.Note = "control " + cord + " of class " + className;
                            return answer;
                        }
                    }
                }
            }

            // 6 Index
            int idx = 0;
            int.TryParse(selector.Index, out idx);
            if (idx > 0 && TierRank["Index"] >= minRank)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    if (fields[i].Index == idx)
                    {
                        if (string.IsNullOrEmpty(kind) || fields[i].Kind == kind)
                        {
                            answer.Handle = fields[i].Handle;
                            answer.Tier = "Index";
                            answer.Note = "creation order";
                            return answer;
                        }
                    }
                }
            }

            return answer;
        }

        // Form-relative origin of the mapped window (screen coords of its top-left).
        public static bool TryGetFormOrigin(FieldMap map, out int originX, out int originY)
        {
            originX = 0;
            originY = 0;
            if (map == null || map.Window == IntPtr.Zero) { return false; }
            if (!Win32Interop.IsWindow(map.Window)) { return false; }
            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(map.Window);
                originX = r.X;
                originY = r.Y;
                return true;
            }
            catch { return false; }
        }

        public static FieldSelector StampPosition(FieldSelector selector, IntPtr formRoot, IntPtr control)
        {
            if (selector == null) { return selector; }
            if (formRoot == IntPtr.Zero || control == IntPtr.Zero) { return selector; }
            try
            {
                Win32Driver.RectInfo form = Win32Driver.GetRect(formRoot);
                Win32Driver.RectInfo ctl = Win32Driver.GetRect(control);
                if (ctl.W <= 0 || ctl.H <= 0) { return selector; }
                selector.PosX = (ctl.X - form.X).ToString(CultureInfo.InvariantCulture);
                selector.PosY = (ctl.Y - form.Y).ToString(CultureInfo.InvariantCulture);
                selector.PosW = ctl.W.ToString(CultureInfo.InvariantCulture);
                selector.PosH = ctl.H.ToString(CultureInfo.InvariantCulture);
            }
            catch { }
            return selector;
        }

        // Windowless VB6 buttons have no HWND — store the operator click as a small hit box.
        public static FieldSelector StampClickPoint(
            FieldSelector selector, IntPtr formRoot, int screenX, int screenY)
        {
            if (selector == null || formRoot == IntPtr.Zero) { return selector; }
            try
            {
                Win32Driver.RectInfo form = Win32Driver.GetRect(formRoot);
                selector.PosX = (screenX - form.X - 8).ToString(CultureInfo.InvariantCulture);
                selector.PosY = (screenY - form.Y - 8).ToString(CultureInfo.InvariantCulture);
                selector.PosW = "16";
                selector.PosH = "16";
            }
            catch { }
            return selector;
        }

        // Match the control whose form-relative box is closest to the captured one.
        // Requires a clear winner so a shifted layout fails loudly instead of guessing.
        public static ResolveResult ResolveByPosition(FieldMap map, FieldSelector selector)
        {
            return ResolveByPosition(map, selector, false);
        }

        // ignoreKindAndClass: VB6 grids are often captured as Frame/Form while the
        // live HWND is MSFlexGrid (or the reverse). Used by lookupGrid / Export.
        public static ResolveResult ResolveByPosition(FieldMap map, FieldSelector selector, bool ignoreKindAndClass)
        {
            ResolveResult miss = new ResolveResult();
            miss.Handle = IntPtr.Zero;
            miss.Tier = "None";
            miss.Note = "position miss";
            if (map == null || selector == null) { return miss; }
            if (!selector.HasPosition) { return miss; }

            ResolveResult hit = ResolveByPositionIn(map.Fields, map, selector, ignoreKindAndClass);
            if (hit.Handle != IntPtr.Zero) { return hit; }

            // Frames / windowless FlexGrids live in AllControls, not Fields.
            if (map.AllControls != null)
            {
                hit = ResolveByPositionIn(map.AllControls, map, selector, true);
                if (hit.Handle != IntPtr.Zero)
                {
                    hit.Note = (hit.Note != null ? hit.Note + " " : "") + "(AllControls)";
                    return hit;
                }
            }
            return miss;
        }

        private static ResolveResult ResolveByPositionIn(
            List<AppField> pool, FieldMap map, FieldSelector selector, bool ignoreKindAndClass)
        {
            ResolveResult miss = new ResolveResult();
            miss.Handle = IntPtr.Zero;
            miss.Tier = "None";
            miss.Note = "position miss";
            if (pool == null || pool.Count == 0) { return miss; }

            int originX, originY;
            if (!TryGetFormOrigin(map, out originX, out originY)) { return miss; }

            int wantX = int.Parse(selector.PosX, CultureInfo.InvariantCulture);
            int wantY = int.Parse(selector.PosY, CultureInfo.InvariantCulture);
            int wantW = int.Parse(selector.PosW, CultureInfo.InvariantCulture);
            int wantH = int.Parse(selector.PosH, CultureInfo.InvariantCulture);
            int wantCx = wantX + wantW / 2;
            int wantCy = wantY + wantH / 2;
            bool largeSurface = wantW >= 200 || wantH >= 120;

            string kind = selector.Kind != null ? selector.Kind : "";
            string className = selector.Class != null ? selector.Class : "";

            AppField best = null;
            AppField second = null;
            int bestDist = int.MaxValue;
            int secondDist = int.MaxValue;

            for (int i = 0; i < pool.Count; i++)
            {
                AppField f = pool[i];
                if (!f.Visible) { continue; }
                if (!ignoreKindAndClass)
                {
                    if (!string.IsNullOrEmpty(kind) && f.Kind != kind) { continue; }
                    if (!string.IsNullOrEmpty(className) &&
                        !string.Equals(f.Class, className, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                int cx = (f.X - originX) + f.W / 2;
                int cy = (f.Y - originY) + f.H / 2;
                int dx = cx - wantCx;
                int dy = cy - wantCy;
                int dist = (dx * dx) + (dy * dy);
                if (dist < bestDist)
                {
                    secondDist = bestDist;
                    second = best;
                    bestDist = dist;
                    best = f;
                }
                else if (dist < secondDist)
                {
                    secondDist = dist;
                    second = f;
                }
            }

            if (best == null) { return miss; }

            // Small inputs stay strict (10px). Large Frame/grid surfaces tolerate resize
            // drift — otherwise lookupGrid / confirmGrid never resolve after a layout nudge.
            int accept = 10;
            int separation = 18;
            if (largeSurface)
            {
                accept = Math.Max(28, Math.Min(wantW, wantH) / 6);
                separation = Math.Max(24, accept / 2);
            }
            if (bestDist > accept * accept)
            {
                miss.Note = "position: nearest control is " +
                    ((int)Math.Sqrt(bestDist)) + "px away (window resized?)";
                return miss;
            }
            if (second != null && secondDist < separation * separation)
            {
                if (largeSurface)
                {
                    best = PreferGridSurface(best, second);
                }
                else
                {
                    miss.Ambiguous = true;
                    miss.Note = "position: two controls overlap the captured spot";
                    return miss;
                }
            }

            ResolveResult ok = new ResolveResult();
            ok.Handle = best.Handle;
            ok.Tier = "Position";
            ok.Note = "position match (" + ((int)Math.Sqrt(bestDist)) + "px)";
            return ok;
        }

        // Frame + FlexGrid share the same click spot; prefer the real grid HWND.
        private static AppField PreferGridSurface(AppField a, AppField b)
        {
            if (a == null) { return b; }
            if (b == null) { return a; }
            int scoreA = GridSurfaceScore(a);
            int scoreB = GridSurfaceScore(b);
            if (scoreA != scoreB) { return scoreA > scoreB ? a : b; }
            long areaA = (long)a.W * a.H;
            long areaB = (long)b.W * b.H;
            return areaA >= areaB ? a : b;
        }

        private static int GridSurfaceScore(AppField f)
        {
            if (f == null) { return 0; }
            string cls = f.Class != null ? f.Class : "";
            if (string.Equals(f.Kind, "Grid", StringComparison.OrdinalIgnoreCase)) { return 4; }
            if (Regex.IsMatch(cls, "(?i)(grid|listview|MSFlex|MSHFlex|VSFlex|FlexGrid)")) { return 3; }
            if (string.Equals(f.Kind, "Group", StringComparison.OrdinalIgnoreCase)) { return 2; }
            if (string.Equals(f.Kind, "Picture", StringComparison.OrdinalIgnoreCase)) { return 1; }
            return 0;
        }

        public static ResolveResult ResolveWithIndexDelta(FieldMap map, FieldSelector selector, int delta)
        {
            ResolveResult miss = new ResolveResult();
            miss.Handle = IntPtr.Zero;
            miss.Tier = "None";
            miss.Note = "index delta miss";
            if (map == null || selector == null || delta == 0) { return miss; }
            int idx = 0;
            if (!int.TryParse(selector.Index, out idx) || idx <= 0) { return miss; }
            FieldSelector shifted = CloneSelector(selector);
            shifted.Index = (idx + delta).ToString(CultureInfo.InvariantCulture);
            // Keep ClassOrdinal cleared so ExactClickMap uses Index path.
            ResolveResult r = ResolveExactClickMap(
                map.Fields,
                shifted,
                shifted.Kind != null ? shifted.Kind : "",
                shifted.Class != null ? shifted.Class : "");
            if (r.Handle != IntPtr.Zero)
            {
                r.Note = "Index" + (delta >= 0 ? "+" : "") + delta;
            }
            return r;
        }

        public static ResolveResult ResolveWithOrdinalDelta(FieldMap map, FieldSelector selector, int delta)
        {
            ResolveResult miss = new ResolveResult();
            miss.Handle = IntPtr.Zero;
            miss.Tier = "None";
            miss.Note = "ordinal delta miss";
            if (map == null || selector == null || delta == 0) { return miss; }
            int cord = 0;
            if (!int.TryParse(selector.ClassOrdinal, out cord) || cord <= 0) { return miss; }
            FieldSelector shifted = CloneSelector(selector);
            shifted.ClassOrdinal = (cord + delta).ToString(CultureInfo.InvariantCulture);
            shifted.Index = "";
            ResolveResult r = ResolveExactClickMap(
                map.Fields,
                shifted,
                shifted.Kind != null ? shifted.Kind : "",
                shifted.Class != null ? shifted.Class : "");
            if (r.Handle != IntPtr.Zero)
            {
                r.Note = "ClassOrdinal" + (delta >= 0 ? "+" : "") + delta;
                return r;
            }
            shifted.SourceKey = "";
            shifted.ConfirmedAt = "";
            return ResolveField(map, shifted, "ClassOrdinal");
        }

        // After «الشكل القانوني» inserts «الوظيفة», ClassOrdinal click-maps go stale.
        // Resolve by Arabic label / keywords only — never trust ordinals for the rest of the form.
        public static ResolveResult ResolveStableAfterLayoutChange(FieldMap map, FieldSelector selector)
        {
            ResolveResult miss = new ResolveResult();
            miss.Handle = IntPtr.Zero;
            miss.Tier = "None";
            miss.Note = "stable resolve miss";
            if (map == null || selector == null) { return miss; }

            FieldSelector soft = CloneSelector(selector);
            soft.SourceKey = "";
            soft.ConfirmedAt = "";
            soft.ClassOrdinal = "";
            soft.Index = "";
            // Prefer catalog / field label text for matching.
            if (string.IsNullOrWhiteSpace(soft.Label))
            {
                soft.Label = !string.IsNullOrWhiteSpace(soft.FieldLabel) ? soft.FieldLabel : soft.FieldKey;
            }

            string[] tips = LabelTipsForKey(selector.FieldKey);
            if (tips != null && tips.Length > 0 &&
                (string.IsNullOrWhiteSpace(soft.Label) || soft.Label.IndexOf(tips[0], StringComparison.Ordinal) < 0))
            {
                soft.Label = tips[0];
            }

            ResolveResult byLabel = ResolveField(map, soft, "Label");
            if (byLabel.Handle != IntPtr.Zero && !FieldLooksLikeJob(map, byLabel.Handle))
            {
                byLabel.Note = "stable-label";
                return byLabel;
            }

            ResolveResult byTip = FindTextBoxByLabelTips(map, tips);
            if (byTip.Handle != IntPtr.Zero)
            {
                return byTip;
            }

            // Last resort: AccName only (still no ordinal).
            soft.Label = selector.Label;
            ResolveResult byAcc = ResolveField(map, soft, "AccName");
            if (byAcc.Handle != IntPtr.Zero && !FieldLooksLikeJob(map, byAcc.Handle))
            {
                byAcc.Note = "stable-acc";
                return byAcc;
            }

            miss.Note = "stable resolve: no label match for " + selector.FieldKey + " (ordinals ignored)";
            return miss;
        }

        public static bool FieldLooksLikeJob(FieldMap map, IntPtr hwnd)
        {
            if (map == null || map.Fields == null || hwnd == IntPtr.Zero) { return false; }
            for (int i = 0; i < map.Fields.Count; i++)
            {
                if (map.Fields[i].Handle != hwnd) { continue; }
                string lab = map.Fields[i].Label != null ? map.Fields[i].Label : "";
                if (lab.IndexOf("وظيف", StringComparison.Ordinal) >= 0) { return true; }
                return false;
            }
            return false;
        }

        public static Dictionary<long, bool> SnapshotTextBoxHandles(FieldMap map)
        {
            Dictionary<long, bool> set = new Dictionary<long, bool>();
            if (map == null || map.Fields == null) { return set; }
            for (int i = 0; i < map.Fields.Count; i++)
            {
                AppField f = map.Fields[i];
                if (f.Kind != "TextBox") { continue; }
                long key = f.Handle.ToInt64();
                if (!set.ContainsKey(key)) { set[key] = true; }
            }
            return set;
        }

        public static bool IsNewTextBox(Dictionary<long, bool> before, IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) { return false; }
            if (before == null || before.Count == 0) { return false; }
            return !before.ContainsKey(hwnd.ToInt64());
        }

        private static FieldSelector CloneSelector(FieldSelector s)
        {
            FieldSelector c = new FieldSelector();
            if (s == null) { return c; }
            c.FieldKey = s.FieldKey;
            c.FieldLabel = s.FieldLabel;
            c.Kind = s.Kind;
            c.SheetColumn = s.SheetColumn;
            c.Screen = s.Screen;
            c.Shortcut = s.Shortcut;
            c.Label = s.Label;
            c.LabelOrdinal = s.LabelOrdinal;
            c.AccName = s.AccName;
            c.ControlId = s.ControlId;
            c.Caption = s.Caption;
            c.Class = s.Class;
            c.Section = s.Section;
            c.ClassOrdinal = s.ClassOrdinal;
            c.Index = s.Index;
            c.DatePart = s.DatePart;
            c.SourceKey = s.SourceKey;
            c.Required = s.Required;
            c.ConfirmedAt = s.ConfirmedAt;
            c.Aliases = s.Aliases;
            c.PosX = s.PosX;
            c.PosY = s.PosY;
            c.PosW = s.PosW;
            c.PosH = s.PosH;
            return c;
        }

        private static string[] LabelTipsForKey(string fieldKey)
        {
            if (string.IsNullOrEmpty(fieldKey)) { return null; }
            string k = fieldKey.ToLowerInvariant();
            if (k == "subform") { return new string[] { "الشكل الفرعي", "فرعي" }; }
            if (k == "legalform") { return new string[] { "الشكل القانوني", "قانوني" }; }
            if (k == "nationalid") { return new string[] { "الرقم القومي", "قومي" }; }
            if (k == "nationality") { return new string[] { "الجنسية", "جنسي" }; }
            if (k == "gender") { return new string[] { "النوع", "جنس" }; }
            if (k == "idtype") { return new string[] { "نوع الهوية", "الهوية" }; }
            if (k == "governorate") { return new string[] { "المحافظة", "محافظ" }; }
            if (k == "country") { return new string[] { "البلد", "الدولة", "بلد" }; }
            if (k == "mobile") { return new string[] { "محمول", "ت. محمول" }; }
            if (k == "docscount") { return new string[] { "عدد المستندات", "مستندات" }; }
            if (k == "brokercode" || k == "broker") { return new string[] { "كود الفرع", "الوسيط", "الفرع" }; }
            if (k == "name") { return new string[] { "الإسم", "الاسم", "اسم" }; }
            if (k == "address") { return new string[] { "العنوان", "عنوان" }; }
            if (k == "job") { return new string[] { "الوظيفة", "وظيف" }; }
            return null;
        }

        private static ResolveResult FindTextBoxByLabelTips(FieldMap map, string[] tips)
        {
            ResolveResult miss = new ResolveResult();
            miss.Handle = IntPtr.Zero;
            miss.Note = "tip miss";
            if (map == null || map.Fields == null || tips == null || tips.Length == 0) { return miss; }

            for (int t = 0; t < tips.Length; t++)
            {
                string tip = tips[t];
                if (string.IsNullOrEmpty(tip)) { continue; }
                List<AppField> hits = new List<AppField>();
                for (int i = 0; i < map.Fields.Count; i++)
                {
                    AppField f = map.Fields[i];
                    if (f.Kind != "TextBox" && f.Kind != "ComboBox") { continue; }
                    string lab = f.Label != null ? f.Label : "";
                    if (lab.IndexOf(tip, StringComparison.Ordinal) < 0) { continue; }
                    // Do not treat الوظيفة as الشكل الفرعي (both may contain unrelated text).
                    if (lab.IndexOf("وظيف", StringComparison.Ordinal) >= 0 &&
                        tip.IndexOf("وظيف", StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }
                    hits.Add(f);
                }
                if (hits.Count == 1)
                {
                    ResolveResult ok = new ResolveResult();
                    ok.Handle = hits[0].Handle;
                    ok.Tier = "LabelTip";
                    ok.Note = "tip «" + tip + "»";
                    return ok;
                }
                if (hits.Count > 1)
                {
                    hits.Sort(CompareByReadingOrder);
                    ResolveResult ok = new ResolveResult();
                    ok.Handle = hits[0].Handle;
                    ok.Tier = "LabelTip";
                    ok.Note = "tip «" + tip + "» first of " + hits.Count;
                    return ok;
                }
            }
            return miss;
        }

        // True when the operator explicitly clicked this control in Setup.
        private static bool IsExactClickMap(FieldSelector selector)
        {
            if (selector == null) { return false; }
            bool hasClass = !string.IsNullOrEmpty(selector.Class);
            bool hasOrd = !string.IsNullOrEmpty(selector.ClassOrdinal);
            bool hasIdx = !string.IsNullOrEmpty(selector.Index);
            if (string.Equals(selector.SourceKey, "click-map", StringComparison.OrdinalIgnoreCase))
            {
                return hasClass && (hasOrd || hasIdx);
            }
            if (!string.IsNullOrEmpty(selector.ConfirmedAt) && hasClass && (hasOrd || hasIdx))
            {
                return true;
            }
            return false;
        }

        private static ResolveResult ResolveExactClickMap(
            List<AppField> fields, FieldSelector selector, string kind, string className)
        {
            ResolveResult answer = new ResolveResult();
            answer.Handle = IntPtr.Zero;
            answer.Tier = "None";
            answer.Note = "click-map miss";
            if (fields == null) { return answer; }

            // 1) Index FIRST — unique per discovery pass. Operator maps often reuse the same
            // ClassOrdinal for unrelated fields (e.g. name=17 and issueDay=17) which caused
            // mass wrong-field writes when ClassOrdinal was preferred.
            int idx = 0;
            int.TryParse(selector.Index, out idx);
            if (idx > 0)
            {
                for (int i = 0; i < fields.Count; i++)
                {
                    if (fields[i].Index != idx) { continue; }
                    if (!string.IsNullOrEmpty(kind) && fields[i].Kind != kind) { continue; }
                    if (!string.IsNullOrEmpty(className) && fields[i].Class != className) { continue; }
                    answer.Handle = fields[i].Handle;
                    answer.Tier = "Index";
                    answer.Note = "exact click-map by Index";
                    return answer;
                }
            }

            // 2) ClassOrdinal only when UNIQUE for that class/kind.
            int cord = 0;
            int.TryParse(selector.ClassOrdinal, out cord);
            if (cord > 0 && !string.IsNullOrEmpty(className))
            {
                List<AppField> hits = new List<AppField>();
                for (int i = 0; i < fields.Count; i++)
                {
                    if (fields[i].Class != className) { continue; }
                    if (fields[i].ClassOrdinal != cord) { continue; }
                    if (!string.IsNullOrEmpty(kind) && fields[i].Kind != kind) { continue; }
                    hits.Add(fields[i]);
                }
                if (hits.Count == 1)
                {
                    answer.Handle = hits[0].Handle;
                    answer.Tier = "ClassOrdinal";
                    answer.Note = "exact click-map ClassOrdinal (unique)";
                    return answer;
                }
                if (hits.Count > 1)
                {
                    answer.Ambiguous = true;
                    answer.Note = "ClassOrdinal " + cord + " ambiguous (" + hits.Count +
                        ") — need Index remap";
                    return answer;
                }
            }

            answer.Note = "exact click-map: Index/ClassOrdinal not found";
            return answer;
        }

        private static ResolveResult MatchClassOrdinal(
            List<AppField> fields, string kind, string className, int cord)
        {
            ResolveResult answer = new ResolveResult();
            answer.Handle = IntPtr.Zero;
            for (int i = 0; i < fields.Count; i++)
            {
                if (fields[i].Class != className) { continue; }
                if (fields[i].ClassOrdinal != cord) { continue; }
                if (!string.IsNullOrEmpty(kind) && fields[i].Kind != kind) { continue; }
                answer.Handle = fields[i].Handle;
                answer.Tier = "ClassOrdinal";
                answer.Note = "control " + cord + " of class " + className;
                return answer;
            }
            return answer;
        }

        private static int CompareByReadingOrder(AppField a, AppField b)
        {
            int cy = a.CenterY.CompareTo(b.CenterY);
            if (cy != 0) { return cy; }
            return b.X.CompareTo(a.X); // RTL: higher X first
        }

        private static List<AppField> FilterByAccName(List<AppField> list, string accName)
        {
            List<AppField> hit = new List<AppField>();
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].AccName == accName) { hit.Add(list[i]); }
            }
            return hit;
        }

        public static List<FieldSelector> ImportSelectors(string path)
        {
            List<FieldSelector> list = new List<FieldSelector>();
            if (!File.Exists(path)) { return list; }
            string raw = SettingsVault.ReadUtf8Auto(path);
            if (string.IsNullOrEmpty(raw)) { return list; }
            string[] lines = raw.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            if (lines.Length < 2) { return list; }
            string[] headers = ParseCsvLine(lines[0]);
            for (int r = 1; r < lines.Length; r++)
            {
                if (string.IsNullOrWhiteSpace(lines[r])) { continue; }
                string[] cols = ParseCsvLine(lines[r]);
                FieldSelector s = new FieldSelector();
                for (int c = 0; c < headers.Length && c < cols.Length; c++)
                {
                    SetSelectorProp(s, headers[c], cols[c]);
                }
                list.Add(s);
            }
            return list;
        }

        public static void SaveSelectors(string path, List<FieldSelector> selectors)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            string[] cols = new string[] {
                "FieldKey","FieldLabel","Kind","SheetColumn","Screen","Shortcut",
                "Label","LabelOrdinal","AccName","ControlId","Caption","Class","Section",
                "ClassOrdinal","Index","DatePart","SourceKey","Required","ConfirmedAt","Aliases",
                "PosX","PosY","PosW","PosH"
            };
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(string.Join(",", cols));
            for (int i = 0; i < selectors.Count; i++)
            {
                FieldSelector s = selectors[i];
                string[] vals = new string[] {
                    Csv(s.FieldKey), Csv(s.FieldLabel), Csv(s.Kind), Csv(s.SheetColumn),
                    Csv(s.Screen), Csv(s.Shortcut), Csv(s.Label), Csv(s.LabelOrdinal),
                    Csv(s.AccName), Csv(s.ControlId), Csv(s.Caption), Csv(s.Class), Csv(s.Section),
                    Csv(s.ClassOrdinal), Csv(s.Index), Csv(s.DatePart), Csv(s.SourceKey),
                    Csv(s.Required), Csv(s.ConfirmedAt), Csv(s.Aliases),
                    Csv(s.PosX), Csv(s.PosY), Csv(s.PosW), Csv(s.PosH)
                };
                sb.AppendLine(string.Join(",", vals));
            }
            SettingsVault.WriteUtf8Locked(path, sb.ToString());
        }

        private static string Csv(string v)
        {
            if (v == null) { v = ""; }
            if (v.IndexOfAny(new char[] { ',', '"', '\r', '\n' }) >= 0)
            {
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            }
            return v;
        }

        private static string[] ParseCsvLine(string line)
        {
            List<string> parts = new List<string>();
            StringBuilder cur = new StringBuilder();
            bool inQ = false;
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (inQ)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                        else { inQ = false; }
                    }
                    else { cur.Append(ch); }
                }
                else
                {
                    if (ch == '"') { inQ = true; }
                    else if (ch == ',') { parts.Add(cur.ToString()); cur.Length = 0; }
                    else { cur.Append(ch); }
                }
            }
            parts.Add(cur.ToString());
            return parts.ToArray();
        }

        private static void SetSelectorProp(FieldSelector s, string header, string value)
        {
            string h = header != null ? header.Trim().TrimStart('\ufeff') : "";
            string v = value != null ? value : "";
            if (h.Equals("FieldKey", StringComparison.OrdinalIgnoreCase)) { s.FieldKey = v; }
            else if (h.Equals("FieldLabel", StringComparison.OrdinalIgnoreCase)) { s.FieldLabel = v; }
            else if (h.Equals("Kind", StringComparison.OrdinalIgnoreCase)) { s.Kind = v; }
            else if (h.Equals("Class", StringComparison.OrdinalIgnoreCase)) { s.Class = v; }
            else if (h.Equals("AccName", StringComparison.OrdinalIgnoreCase)) { s.AccName = v; }
            else if (h.Equals("ControlId", StringComparison.OrdinalIgnoreCase)) { s.ControlId = v; }
            else if (h.Equals("Label", StringComparison.OrdinalIgnoreCase)) { s.Label = v; }
            else if (h.Equals("Caption", StringComparison.OrdinalIgnoreCase)) { s.Caption = v; }
            else if (h.Equals("ClassOrdinal", StringComparison.OrdinalIgnoreCase)) { s.ClassOrdinal = v; }
            else if (h.Equals("Index", StringComparison.OrdinalIgnoreCase)) { s.Index = v; }
            else if (h.Equals("LabelOrdinal", StringComparison.OrdinalIgnoreCase)) { s.LabelOrdinal = v; }
            else if (h.Equals("SheetColumn", StringComparison.OrdinalIgnoreCase)) { s.SheetColumn = v; }
            else if (h.Equals("DatePart", StringComparison.OrdinalIgnoreCase)) { s.DatePart = v; }
            else if (h.Equals("SourceKey", StringComparison.OrdinalIgnoreCase)) { s.SourceKey = v; }
            else if (h.Equals("Required", StringComparison.OrdinalIgnoreCase)) { s.Required = v; }
            else if (h.Equals("ConfirmedAt", StringComparison.OrdinalIgnoreCase)) { s.ConfirmedAt = v; }
            else if (h.Equals("Aliases", StringComparison.OrdinalIgnoreCase)) { s.Aliases = v; }
            else if (h.Equals("Screen", StringComparison.OrdinalIgnoreCase)) { s.Screen = v; }
            else if (h.Equals("Shortcut", StringComparison.OrdinalIgnoreCase)) { s.Shortcut = v; }
            else if (h.Equals("Section", StringComparison.OrdinalIgnoreCase)) { s.Section = v; }
            else if (h.Equals("PosX", StringComparison.OrdinalIgnoreCase)) { s.PosX = v; }
            else if (h.Equals("PosY", StringComparison.OrdinalIgnoreCase)) { s.PosY = v; }
            else if (h.Equals("PosW", StringComparison.OrdinalIgnoreCase)) { s.PosW = v; }
            else if (h.Equals("PosH", StringComparison.OrdinalIgnoreCase)) { s.PosH = v; }
        }

        public static IntPtr GetTargetAppWindow(string titlePattern, string exePath, bool autoLaunch, int timeoutSec)
        {
            IntPtr found = FindRunningTarget(titlePattern, exePath);
            if (found != IntPtr.Zero) { return found; }

            if (autoLaunch && !string.IsNullOrEmpty(exePath) && File.Exists(exePath))
            {
                Process proc = Win32Driver.StartProcess(exePath, "", Path.GetDirectoryName(exePath));
                DateTime deadline = DateTime.Now.AddSeconds(timeoutSec > 0 ? timeoutSec : 90);
                while (DateTime.Now < deadline)
                {
                    Thread.Sleep(400);
                    found = FindRunningTarget(titlePattern, exePath);
                    if (found != IntPtr.Zero) { return found; }
                    // Prefer windows owned by the process we just launched.
                    if (proc != null && !proc.HasExited)
                    {
                        found = FindBestWindowForPid(proc.Id, titlePattern);
                        if (found != IntPtr.Zero) { return found; }
                    }
                }
            }
            return IntPtr.Zero;
        }

        // Attach by STABLE identity: title text and/or Coding.exe path — never a stored HWND.
        public static IntPtr FindRunningTarget(string titlePattern, string exePath)
        {
            List<W32WindowInfo> wins = Win32Driver.GetAppWindows(".");
            string exeName = "";
            if (!string.IsNullOrEmpty(exePath))
            {
                exeName = Path.GetFileNameWithoutExtension(exePath);
            }

            IntPtr bestTitle = IntPtr.Zero;
            int bestTitleArea = 0;
            IntPtr bestExe = IntPtr.Zero;
            int bestExeArea = 0;

            for (int i = 0; i < wins.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(wins[i].Title)) { continue; }
                // Never attach to the RPA host itself (title contains «تكويد» too).
                if (IsRpaHostTitle(wins[i].Title)) { continue; }
                Win32Driver.RectInfo r = Win32Driver.GetRect(wins[i].Handle);
                int area = r.W * r.H;
                if (area < 200 * 100) { continue; }

                bool titleHit = !string.IsNullOrEmpty(titlePattern) && TitleMatch(wins[i].Title, titlePattern);
                bool exeHit = false;
                if (!string.IsNullOrEmpty(exeName))
                {
                    try
                    {
                        Process p = Process.GetProcessById(wins[i].ProcessId);
                        if (string.Equals(p.ProcessName, exeName, StringComparison.OrdinalIgnoreCase))
                        {
                            exeHit = true;
                        }
                    }
                    catch { }
                }
                // CodingReplica / Thunder frame without configured exe still matches title.
                if (!exeHit && string.IsNullOrEmpty(exePath) &&
                    string.Equals(wins[i].Class, "ThunderRT6MDIForm", StringComparison.OrdinalIgnoreCase) &&
                    (string.IsNullOrEmpty(titlePattern) || titleHit))
                {
                    exeHit = true;
                }

                if (titleHit && area > bestTitleArea) { bestTitleArea = area; bestTitle = wins[i].Handle; }
                if (exeHit && area > bestExeArea) { bestExeArea = area; bestExe = wins[i].Handle; }
            }

            // Prefer title+exe agreement, else title, else exe.
            if (bestTitle != IntPtr.Zero && bestExe != IntPtr.Zero && bestTitle == bestExe) { return bestTitle; }
            if (bestTitle != IntPtr.Zero) { return bestTitle; }
            if (bestExe != IntPtr.Zero) { return bestExe; }
            return IntPtr.Zero;
        }

        private static bool IsRpaHostTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) { return false; }
            if (title.IndexOf("RPA V1", StringComparison.OrdinalIgnoreCase) >= 0) { return true; }
            if (title.IndexOf("روبوت تكويد", StringComparison.Ordinal) >= 0) { return true; }
            if (title.IndexOf("RBA", StringComparison.OrdinalIgnoreCase) >= 0 &&
                title.IndexOf("Coding", StringComparison.OrdinalIgnoreCase) < 0 &&
                title.IndexOf("تكويد", StringComparison.Ordinal) < 0)
            {
                return true;
            }
            return false;
        }

        private static IntPtr FindBestWindowForPid(int pid, string titlePattern)
        {
            List<W32WindowInfo> wins = Win32Driver.GetAppWindows(".");
            IntPtr best = IntPtr.Zero;
            int bestArea = 0;
            for (int i = 0; i < wins.Count; i++)
            {
                if (wins[i].ProcessId != pid) { continue; }
                if (string.IsNullOrWhiteSpace(wins[i].Title)) { continue; }
                if (IsRpaHostTitle(wins[i].Title)) { continue; }
                if (!string.IsNullOrEmpty(titlePattern) && !TitleMatch(wins[i].Title, titlePattern)) { continue; }
                Win32Driver.RectInfo r = Win32Driver.GetRect(wins[i].Handle);
                int area = r.W * r.H;
                if (area > bestArea) { bestArea = area; best = wins[i].Handle; }
            }
            return best;
        }

        public static IntPtr GetScanRoot(IntPtr appWindow, string childPattern, bool preferInnerForm)
        {
            if (appWindow == IntPtr.Zero || !Win32Interop.IsWindow(appWindow)) { return IntPtr.Zero; }
            if (preferInnerForm)
            {
                List<W32WindowInfo> forms = Win32Driver.GetFormWindows(appWindow, 4);
                for (int i = forms.Count - 1; i >= 0; i--)
                {
                    if (!forms[i].Visible) { continue; }
                    if (!string.IsNullOrEmpty(childPattern) && !TitleMatch(forms[i].Text, childPattern) &&
                        !TitleMatch(forms[i].Class, childPattern))
                    {
                        continue;
                    }
                    List<W32WindowInfo> kids = Win32Driver.GetChildren(forms[i].Handle);
                    if (kids.Count > 0) { return forms[i].Handle; }
                }
                if (forms.Count > 0) { return forms[forms.Count - 1].Handle; }
            }
            return appWindow;
        }
    }
}
