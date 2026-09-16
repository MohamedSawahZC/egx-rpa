// =============================================================================
//  LookupGridProbe.cs - operator dump of «الإستعلام للتعديل» result table.
//
//  VB6 MSFlexGrid cells are painted, not child windows, so the live run often
//  sees an empty MSAA tree. This probe writes every HWND, MSAA node, mapped
//  rectangle sample, Select-All clipboard, and a screenshot so the table can
//  be inspected without guessing.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace RbaCore
{
    public class LookupGridProbeResult
    {
        public bool Ok;
        public string TextPath = "";
        public string ImagePath = "";
        public string Note = "";
        public string ClipboardText = "";
    }

    public static class LookupGridProbe
    {
        public static LookupGridProbeResult Run(
            IntPtr screen,
            UiPickResult click,
            string logsDir,
            Action<string, string> log)
        {
            LookupGridProbeResult result = new LookupGridProbeResult();
            if (log == null) { log = delegate(string m, string l) { }; }

            if (screen == IntPtr.Zero || !Win32Interop.IsWindow(screen))
            {
                result.Note = "lookup screen handle is dead";
                return result;
            }

            string dir = logsDir;
            if (string.IsNullOrWhiteSpace(dir)) { dir = Path.GetTempPath(); }
            try
            {
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
            }
            catch (Exception ex)
            {
                result.Note = "cannot create logs folder: " + ex.Message;
                return result;
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            result.TextPath = Path.Combine(dir, "lookup-grid-probe_" + stamp + ".txt");
            result.ImagePath = Path.Combine(dir, "lookup-grid-probe_" + stamp + ".png");

            StringBuilder sb = new StringBuilder(16 * 1024);
            sb.AppendLine("RPA-V1 lookup grid probe");
            sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Machine: " + Environment.MachineName);
            sb.AppendLine();

            DumpHwnd(sb, "LOOKUP SCREEN", screen);

            IntPtr clicked = IntPtr.Zero;
            int clickX = 0;
            int clickY = 0;
            if (click != null)
            {
                clicked = click.Control;
                clickX = click.ClickScreenX;
                clickY = click.ClickScreenY;
                sb.AppendLine("CLICK");
                sb.AppendLine("  screen=" + clickX + "," + clickY);
                sb.AppendLine("  windowless=" + click.WindowlessClick);
                sb.AppendLine("  class=" + (click.Class != null ? click.Class : ""));
                sb.AppendLine("  kind=" + (click.ControlKind != null ? click.ControlKind : ""));
                sb.AppendLine("  caption=«" + (click.Caption != null ? click.Caption : "") + "»");
                sb.AppendLine("  accName=«" + (click.AccName != null ? click.AccName : "") + "»");
                sb.AppendLine("  ordinal=" + (click.ClassOrdinal != null ? click.ClassOrdinal : "") +
                    " index=" + (click.Index != null ? click.Index : ""));
                sb.AppendLine();
                if (clicked != IntPtr.Zero) { DumpHwnd(sb, "CLICKED CONTROL", clicked); }
            }

            sb.AppendLine("=== CHILD WINDOWS ===");
            List<W32WindowInfo> kids = Win32Driver.GetChildren(screen);
            sb.AppendLine("count=" + kids.Count);
            for (int i = 0; i < kids.Count; i++)
            {
                W32WindowInfo k = kids[i];
                string cls = k.Class != null ? k.Class : "";
                string kind = FieldDiscovery.GetControlKind(cls);
                string wt = "";
                int w = 0, h = 0, x = 0, y = 0;
                try
                {
                    Win32Driver.RectInfo r = Win32Driver.GetRect(k.Handle);
                    x = r.X; y = r.Y; w = r.W; h = r.H;
                    wt = Win32Driver.GetWindowText(k.Handle);
                }
                catch { }
                sb.AppendLine("  [" + (i + 1) + "] " + kind + " " + cls +
                    " vis=" + k.Visible + " @" + x + "," + y + " " + w + "x" + h +
                    " text=«" + OneLine(wt) + "» hwnd=" + k.Handle);
            }
            sb.AppendLine();

            IntPtr flex = FlexGridSurface.Find(screen, clicked);
            if (flex != IntPtr.Zero)
            {
                DumpHwnd(sb, "MSHFLEXGRID", flex);
                AccessibleText.DumpWindow(flex, sb, "flexgrid", 120);
                sb.AppendLine();
                string flexClip = FlexGridSurface.CopyCells(flex, screen, log);
                sb.AppendLine("=== FLEXGRID COM + VISIBLE COPY ===");
                sb.AppendLine(string.IsNullOrWhiteSpace(flexClip) ? "(empty)" : flexClip);
                sb.AppendLine();
                string comOnly = FlexGridSurface.ReadComCells(flex, log);
                sb.AppendLine("=== FLEXGRID COM TEXTMATRIX ===");
                sb.AppendLine(string.IsNullOrWhiteSpace(comOnly) ? "(empty)" : comOnly);
                sb.AppendLine();
            }

            AccessibleText.DumpWindow(screen, sb, "screen", 120);
            sb.AppendLine();
            if (clicked != IntPtr.Zero && clicked != screen)
            {
                AccessibleText.DumpWindow(clicked, sb, "clicked", 120);
                sb.AppendLine();
            }

            if (clickX != 0 || clickY != 0)
            {
                sb.AppendLine("=== MSAA AT CLICK POINT ===");
                List<string> at = AccessibleText.HarvestAtPoint(clickX, clickY);
                sb.AppendLine("count=" + at.Count);
                for (int i = 0; i < at.Count && i < 40; i++)
                {
                    sb.AppendLine("  [" + (i + 1) + "] " + OneLine(at[i]));
                }
                sb.AppendLine();

                try
                {
                    Win32Driver.RectInfo form = Win32Driver.GetRect(screen);
                    int rx = clickX - form.X - 80;
                    int ry = clickY - form.Y - 40;
                    if (rx < 0) { rx = 0; }
                    if (ry < 0) { ry = 0; }
                    List<string> region = AccessibleText.HarvestRegion(form.X + rx, form.Y + ry, 240, 160);
                    sb.AppendLine("=== MSAA REGION AROUND CLICK (240x160) ===");
                    sb.AppendLine("count=" + region.Count);
                    for (int i = 0; i < region.Count && i < 60; i++)
                    {
                        sb.AppendLine("  [" + (i + 1) + "] " + OneLine(region[i]));
                    }
                    sb.AppendLine();
                }
                catch { }
            }

            string clipCell = CopyFromTable(screen, clicked, clickX, clickY, false, log);
            string clipAll = CopyFromTable(screen, clicked, clickX, clickY, true, log);
            result.ClipboardText = !string.IsNullOrWhiteSpace(clipAll) ? clipAll : clipCell;

            sb.AppendLine("=== CLIPBOARD AFTER CLICK + Ctrl+C ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(clipCell) ? "(empty)" : clipCell);
            sb.AppendLine();
            sb.AppendLine("=== CLIPBOARD AFTER CLICK + Ctrl+A + Ctrl+C ===");
            sb.AppendLine(string.IsNullOrWhiteSpace(clipAll) ? "(empty)" : clipAll);
            sb.AppendLine();

            try
            {
                CaptureWindowPng(screen, result.ImagePath);
                sb.AppendLine("Screenshot: " + result.ImagePath);
            }
            catch (Exception ex)
            {
                sb.AppendLine("Screenshot failed: " + ex.Message);
                result.ImagePath = "";
            }

            try
            {
                File.WriteAllText(result.TextPath, sb.ToString(), new UTF8Encoding(true));
                result.Ok = true;
                result.Note = "wrote " + result.TextPath;
                log("lookup-probe: " + result.Note, "OK");
            }
            catch (Exception ex)
            {
                result.Note = "write failed: " + ex.Message;
                log("lookup-probe: " + result.Note, "ERROR");
            }
            return result;
        }

        private static void DumpHwnd(StringBuilder sb, string title, IntPtr hwnd)
        {
            sb.AppendLine("=== " + title + " ===");
            sb.AppendLine("hwnd=" + hwnd);
            try
            {
                sb.AppendLine("title=«" + Win32Driver.GetWindowTitle(hwnd) + "»");
                sb.AppendLine("class=" + Win32Driver.GetClassName(hwnd));
                sb.AppendLine("kind=" + FieldDiscovery.GetControlKind(Win32Driver.GetClassName(hwnd)));
                Win32Driver.RectInfo r = Win32Driver.GetRect(hwnd);
                sb.AppendLine("rect=" + r.X + "," + r.Y + " " + r.W + "x" + r.H);
                sb.AppendLine("wm_gettext=«" + OneLine(Win32Driver.GetWindowText(hwnd)) + "»");
            }
            catch (Exception ex)
            {
                sb.AppendLine("error: " + ex.Message);
            }
            sb.AppendLine();
        }

        private static string CopyFromTable(
            IntPtr screen, IntPtr clicked, int clickX, int clickY, bool selectAll,
            Action<string, string> log)
        {
            try
            {
                try { Win32Driver.SetForeground(screen); } catch { }
                Thread.Sleep(80);
                if (clickX != 0 || clickY != 0)
                {
                    Win32Driver.ClickScreen(clickX, clickY, 160);
                }
                else if (clicked != IntPtr.Zero)
                {
                    Win32Driver.ClickPhysical(clicked, 160);
                }
                if (selectAll)
                {
                    try { Win32Driver.SendShortcut(screen, "^a", 180); } catch { }
                }
                Win32Driver.SendShortcut(screen, "^c", 280);
                string clip = Clipboard.GetText();
                return clip != null ? clip.Trim() : "";
            }
            catch (Exception ex)
            {
                if (log != null) { log("lookup-probe: clipboard " + ex.Message, "WARN"); }
                return "";
            }
        }

        private static void CaptureWindowPng(IntPtr hwnd, string path)
        {
            Win32Driver.RectInfo r = Win32Driver.GetRect(hwnd);
            if (r.W < 8 || r.H < 8) { return; }
            using (Bitmap bmp = new Bitmap(r.W, r.H))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(r.X, r.Y, 0, 0, new Size(r.W, r.H));
                }
                bmp.Save(path, ImageFormat.Png);
            }
        }

        private static string OneLine(string value)
        {
            if (string.IsNullOrEmpty(value)) { return ""; }
            string t = value.Replace("\r", " ").Replace("\n", " / ").Replace("\t", " ").Trim();
            return t.Length > 180 ? t.Substring(0, 180) + "…" : t;
        }
    }
}
