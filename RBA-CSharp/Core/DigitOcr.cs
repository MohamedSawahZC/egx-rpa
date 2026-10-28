// =============================================================================
//  DigitOcr.cs - read painted MSHFlexGrid digits (كود 12) from a window bitmap.
//
//  MSAA / NativeOM / clipboard are empty on this VB6 grid. PrintWindow often
//  misses owner-drawn cells; CopyFromScreen of the highlighted row is the
//  source of كود 12.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RbaCore
{
    public static class DigitOcr
    {
        private static List<DigitTemplate> _templates;

        public static string Harvest(IntPtr flex, IntPtr screen, Action<string, string> log)
        {
            return Harvest(flex, screen, log, true);
        }

        // clickRow=false when the caller already clicked the Setup point (سحب column).
        public static string Harvest(IntPtr flex, IntPtr screen, Action<string, string> log, bool clickRow)
        {
            try
            {
                IntPtr top = screen != IntPtr.Zero ? screen : flex;
                if (top != IntPtr.Zero) { Win32Driver.SetForeground(top); Thread.Sleep(80); }
            }
            catch { }

            // Selection paint often appears only after the data row is clicked.
            // Never use the table-centre formula on «سحب الأكواد» — Setup point is required.
            if (clickRow)
            {
                string title = "";
                try { title = Win32Driver.GetWindowTitle(screen != IntPtr.Zero ? screen : flex) ?? ""; }
                catch { title = ""; }
                if (ArabicText.Contains(title, "سحب"))
                {
                    if (log != null)
                    {
                        log("ocr: skip formula row-click on «سحب الأكواد» — use Setup point", "INFO");
                    }
                }
                else
                {
                    FlexGridSurface.ClickFirstDataRow(flex, screen, log);
                }
            }

            StringBuilder all = new StringBuilder();
            Bitmap fromScreen = CaptureUntilHighlight(flex, screen, log);
            if (fromScreen != null)
            {
                SaveDebug(fromScreen, "ocr-screen.png", log);
                AppendBitmap(all, fromScreen, log, "screen");
                fromScreen.Dispose();
                if (HasTwelve(all.ToString()) || HasUnified(all.ToString()))
                {
                    return Finish(all, log);
                }
            }

            IntPtr form = screen;
            if (form == IntPtr.Zero || !Win32Interop.IsWindow(form)) { form = flex; }
            Bitmap printed = TryPrint(form);
            if (printed != null)
            {
                Bitmap crop = CropToFlex(printed, form, flex);
                printed.Dispose();
                if (crop != null)
                {
                    if (log != null)
                    {
                        log("ocr: PrintWindow " + crop.Width + "x" + crop.Height, "INFO");
                    }
                    SaveDebug(crop, "ocr-print.png", log);
                    AppendBitmap(all, crop, log, "print");
                    crop.Dispose();
                }
            }
            return Finish(all, log);
        }

        // Offline probe: same pipeline as the live FlexGrid capture.
        public static string ReadBitmap(Bitmap bmp, Action<string, string> log)
        {
            StringBuilder all = new StringBuilder();
            AppendBitmap(all, bmp, log, "file");
            return Finish(all, log);
        }

        public static void DebugBand(Bitmap bmp, Action<string, string> log)
        {
            if (bmp == null || log == null) { return; }
            EnsureTemplates();
            Bitmap band = ExtractHighlight(bmp);
            if (band == null) { band = ExtractDataBand(bmp); }
            if (band == null)
            {
                log("debug: no band", "WARN");
                return;
            }
            log("debug: band " + band.Width + "x" + band.Height, "INFO");
            SampleColors(band, log);
            EnsureTemplates();
            log("debug: templates=" + _templates.Count, "INFO");
            bool[][] ink = ToContrastInk(band);
            List<Blob> blobs = Connected(ink);
            MergeHalves(blobs);
            log("debug: contrast blobs=" + blobs.Count, "INFO");
            blobs.Sort(CompareBlob);
            int shown = blobs.Count < 80 ? blobs.Count : 80;
            for (int i = 0; i < shown; i++)
            {
                Blob b = blobs[i];
                char d;
                double s = BestScore(b, out d);
                log("debug: blob[" + i + "] x=" + b.X + " y=" + b.Y +
                    " " + b.W + "x" + b.H + " px=" + b.Pixels +
                    " score=" + s.ToString("0.00") + " → " +
                    (d == '\0' ? "-" : d.ToString()), "INFO");
            }
            band.Dispose();
        }

        private static void SampleColors(Bitmap bmp, Action<string, string> log)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            int[] xs = new int[] { 8, w / 6, w / 3, w / 2, (2 * w) / 3, w - 12 };
            int[] ys = new int[] { 2, h / 3, (2 * h) / 3, h - 3 };
            for (int i = 0; i < xs.Length; i++)
            {
                for (int j = 0; j < ys.Length; j++)
                {
                    int x = xs[i];
                    int y = ys[j];
                    if (x < 0 || y < 0 || x >= w || y >= h) { continue; }
                    Color c = bmp.GetPixel(x, y);
                    log("debug: px@" + x + "," + y + " R" + c.R + " G" + c.G + " B" + c.B, "INFO");
                }
            }
        }

        private static double BestScore(Blob b, out char digit)
        {
            digit = '\0';
            if (b == null) { return 0; }
            double best = 0;
            for (int t = 0; t < _templates.Count; t++)
            {
                double s = Score(b, _templates[t]);
                if (s > best)
                {
                    best = s;
                    digit = _templates[t].Digit;
                }
            }
            return best;
        }

        public static bool HasUnified(string text)
        {
            string nin;
            string code12;
            FlexGridSurface.ParseCodes(text, "", out nin, out code12);
            return nin.Length >= 6 && nin.Length <= 9;
        }

        public static bool HasTwelve(string text)
        {
            string dummyNin;
            string code12;
            FlexGridSurface.ParseCodes(text, "", out dummyNin, out code12);
            return code12.Length == 12;
        }

        private static string Finish(StringBuilder all, Action<string, string> log)
        {
            string text = all != null ? all.ToString() : "";
            if (log != null)
            {
                string preview = text.Trim();
                if (preview.Length > 160) { preview = preview.Substring(0, 160) + "..."; }
                log("ocr: " + (preview.Length == 0 ? "(empty)" : "«" + preview + "»"),
                    HasTwelve(text) || HasUnified(text) ? "OK" : "INFO");
            }
            return text;
        }

        private static Bitmap CaptureUntilHighlight(
            IntPtr flex, IntPtr screen, Action<string, string> log)
        {
            Bitmap last = null;
            for (int i = 0; i < 8; i++)
            {
                if (last != null) { last.Dispose(); last = null; }
                last = CaptureScreen(flex, screen, i == 0 ? log : null);
                if (last != null && HasHighlight(last)) { return last; }
                Thread.Sleep(180);
            }
            return last;
        }

        private static bool HasHighlight(Bitmap bmp)
        {
            Bitmap band = ExtractHighlight(bmp);
            if (band == null) { return false; }
            band.Dispose();
            return true;
        }

        private static void AppendBitmap(
            StringBuilder all, Bitmap bmp, Action<string, string> log, string tag)
        {
            if (bmp == null) { return; }
            EnsureTemplates();
            Bitmap band = ExtractHighlight(bmp);
            bool highlighted = band != null;
            if (band == null) { band = ExtractDataBand(bmp); }
            if (!highlighted && IsBlueSelection(band))
            {
                highlighted = true;
                if (log != null) { log("ocr: " + tag + " band is blue selection — contrast ink", "INFO"); }
            }
            if (band != null)
            {
                if (log != null)
                {
                    log("ocr: " + tag + " band " + band.Width + "x" + band.Height +
                        (highlighted ? " highlight" : " data"), "INFO");
                }
                SaveDebug(band, "ocr-band-" + tag + ".png", log);
                // Only 1:1 band (+ single invert for white-on-blue) joins the harvest.
                // Scale2 / double-invert invent digit soup that poisoned majority vote.
                ReadBand(all, band, log, tag, highlighted);
                Bitmap inv = Invert(band);
                if (inv != null)
                {
                    ReadBand(all, inv, log, tag + "-inv", highlighted);
                    // Debug-only: log zoomed passes without appending to the harvest.
                    StringBuilder discard = new StringBuilder();
                    Bitmap zoomInv = Scale2(inv);
                    inv.Dispose();
                    if (zoomInv != null)
                    {
                        ReadBand(discard, zoomInv, log, tag + "-inv2", highlighted);
                        zoomInv.Dispose();
                    }
                    Bitmap zoom = Scale2(band);
                    if (zoom != null)
                    {
                        ReadBand(discard, zoom, log, tag + "-x2", highlighted);
                        zoom.Dispose();
                    }
                }
                band.Dispose();
            }
            else if (log != null)
            {
                log("ocr: " + tag + " no readable data band", "INFO");
            }
        }

        private static void ReadBand(
            StringBuilder all, Bitmap band, Action<string, string> log, string tag)
        {
            ReadBand(all, band, log, tag, true);
        }

        private static void ReadBand(
            StringBuilder all, Bitmap band, Action<string, string> log, string tag, bool highlighted)
        {
            ReadBandCore(all, band, log, tag, highlighted);
            if (band != null && band.Width >= 180)
            {
                ReadBandSlice(all, band, 0.16, 0.50, log, tag + "-colA", highlighted);
                ReadBandSlice(all, band, 0.28, 0.62, log, tag + "-colB", highlighted);
                ReadBandSlice(all, band, 0.40, 0.74, log, tag + "-colC", highlighted);
                ReadBandSlice(all, band, 0.22, 0.40, log, tag + "-ninL", highlighted);
                ReadBandSlice(all, band, 0.30, 0.48, log, tag + "-ninR", highlighted);
            }
        }

        private static void ReadBandSlice(
            StringBuilder all, Bitmap band, double x0, double x1,
            Action<string, string> log, string tag, bool highlighted)
        {
            if (band == null) { return; }
            int left = (int)Math.Round(band.Width * x0);
            int right = (int)Math.Round(band.Width * x1);
            if (left < 0) { left = 0; }
            if (right > band.Width) { right = band.Width; }
            int w = right - left;
            if (w < 36) { return; }
            try
            {
                using (Bitmap slice = band.Clone(
                    new Rectangle(left, 0, w, band.Height), PixelFormat.Format32bppArgb))
                {
                    ReadBandCore(all, slice, log, tag, highlighted);
                }
            }
            catch { }
        }

        private static void ReadBandCore(
            StringBuilder all, Bitmap band, Action<string, string> log, string tag, bool highlighted)
        {
            if (highlighted)
            {
                bool[][] contrast = ToContrastInk(band);
                string rows = ReadDigitRows(contrast);
                if (!string.IsNullOrEmpty(rows))
                {
                    if (log != null) { log("ocr: " + tag + "-rows «" + rows + "»", "INFO"); }
                    all.Append('\t').Append(rows);
                }
                AppendLogged(all, contrast, log, tag + "-contrast");
                AppendLogged(all, ToInk(band, false), log, tag + "-light");
                AppendLogged(all, ToInk(band, true), log, tag + "-dark");
                AppendLogged(all, ToBrightInk(band, 200), log, tag + "-bright");
            }
            else
            {
                // Grey selection (not Windows blue): contrast still separates ink from paper.
                bool[][] contrast = ToContrastInk(band);
                string rows = ReadDigitRows(contrast);
                if (!string.IsNullOrEmpty(rows))
                {
                    if (log != null) { log("ocr: " + tag + "-rows «" + rows + "»", "INFO"); }
                    all.Append('\t').Append(rows);
                }
                AppendLogged(all, contrast, log, tag + "-contrast");
                AppendLogged(all, ToInk(band, true), log, tag + "-dark");
                AppendLogged(all, ToInk(band, false), log, tag + "-light");
                AppendLogged(all, ToBrightInk(band, 200), log, tag + "-bright");
            }
        }

        private static string ReadDigitRows(bool[][] ink)
        {
            if (ink == null) { return ""; }
            int blobs;
            int digits;
            StringBuilder sb = new StringBuilder();
            AppendRead(sb, ink, out blobs, out digits);
            return sb.ToString();
        }

        private static void AppendLogged(
            StringBuilder all, bool[][] ink, Action<string, string> log, string tag)
        {
            int blobs;
            int digits;
            string text = ReadInkStats(ink, out blobs, out digits);
            if (log != null && (blobs > 0 || digits > 0 || !string.IsNullOrEmpty(text)))
            {
                log("ocr: " + tag + " blobs=" + blobs + " digits=" + digits +
                    " «" + text + "»", "INFO");
            }
            if (!string.IsNullOrEmpty(text)) { all.Append('\t').Append(text); }
        }

        private static Bitmap CaptureScreen(IntPtr flex, IntPtr screen, Action<string, string> log)
        {
            int x, y, w, h;
            if (!VisibleBand(flex, screen, out x, out y, out w, out h)) { return null; }
            try
            {
                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(x, y, 0, 0, new Size(w, h));
                }
                if (log != null)
                {
                    log("ocr: CopyFromScreen @" + x + "," + y + " " + w + "x" + h, "INFO");
                }
                return bmp;
            }
            catch (Exception ex)
            {
                if (log != null) { log("ocr: CopyFromScreen failed — " + ex.Message, "WARN"); }
                return null;
            }
        }

        private static void SaveDebug(Bitmap bmp, string fileName, Action<string, string> log)
        {
            if (bmp == null || string.IsNullOrEmpty(fileName)) { return; }
            try
            {
                string dir = ActionLog.LogsDir;
                if (string.IsNullOrEmpty(dir)) { dir = ActionLog.BesideExeLogsDir(); }
                if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
                string path = Path.Combine(dir, fileName);
                bmp.Save(path, ImageFormat.Png);
                if (log != null) { log("ocr: saved " + path, "INFO"); }
            }
            catch { }
        }

        private static Bitmap Scale2(Bitmap src)
        {
            if (src == null) { return null; }
            Bitmap dst = new Bitmap(src.Width * 2, src.Height * 2, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.DrawImage(src, 0, 0, dst.Width, dst.Height);
            }
            return dst;
        }

        private static Bitmap ExtractHighlight(Bitmap bmp)
        {
            if (bmp == null) { return null; }
            int w = bmp.Width;
            int h = bmp.Height;
            int[] score = new int[h];
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] raw = new byte[stride * h];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    int n = 0;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + (x * 4);
                        int b = raw[i];
                        int g = raw[i + 1];
                        int r = raw[i + 2];
                        bool blue = b > 90 && b > r + 12 && b > g + 8;
                        bool yellow = r > 160 && g > 140 && b < 120;
                        if (blue || yellow) { n++; }
                    }
                    score[y] = n;
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }

            int need = Math.Max(8, w / 16);
            int bestA = -1, bestB = -1, bestN = 0;
            int a = -1;
            for (int y = 0; y <= h; y++)
            {
                bool hit = y < h && score[y] >= need;
                if (hit && a < 0) { a = y; }
                if ((!hit || y == h) && a >= 0)
                {
                    int b = y - 1;
                    int n = b - a + 1;
                    if (n >= 10 && n > bestN) { bestN = n; bestA = a; bestB = b; }
                    a = -1;
                }
            }
            if (bestA < 0) { return null; }
            // Do not pad into the header — extra gray rows wreck ink thresholds.
            int bh = bestB - bestA + 1;
            if (bh < 12) { return null; }
            try
            {
                return bmp.Clone(new Rectangle(0, bestA, w, bh), PixelFormat.Format32bppArgb);
            }
            catch { return null; }
        }

        // Windows selection blue on the inquiry row (0,120,215). ExtractHighlight
        // can miss a short band; still run white-on-blue contrast on that capture.
        private static bool IsBlueSelection(Bitmap bmp)
        {
            if (bmp == null || bmp.Width < 20 || bmp.Height < 10) { return false; }
            int w = bmp.Width;
            int h = bmp.Height;
            int stepX = Math.Max(1, w / 40);
            int stepY = Math.Max(1, h / 12);
            int blue = 0;
            int n = 0;
            for (int y = 0; y < h; y += stepY)
            {
                for (int x = 0; x < w; x += stepX)
                {
                    Color c = bmp.GetPixel(x, y);
                    n++;
                    if (c.B > 90 && c.B > c.R + 12 && c.B > c.G + 8) { blue++; }
                }
            }
            return n > 0 && blue * 3 >= n;
        }

        // First painted data row when the selection color is not detected.
        private static Bitmap ExtractDataBand(Bitmap bmp)
        {
            if (bmp == null || bmp.Height < 36) { return null; }
            int y = Math.Min(26, Math.Max(18, bmp.Height / 10));
            int h = Math.Min(44, bmp.Height - y);
            if (h < 16) { return null; }
            try
            {
                return bmp.Clone(new Rectangle(0, y, bmp.Width, h), PixelFormat.Format32bppArgb);
            }
            catch { return null; }
        }

        private static Bitmap TryPrint(IntPtr hwnd)
        {
            try
            {
                Win32Driver.RectInfo r = Win32Driver.GetRect(hwnd);
                if (r.W < 20 || r.H < 20 || r.W > 4000 || r.H > 4000) { return null; }
                Bitmap bmp = new Bitmap(r.W, r.H, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    IntPtr hdc = g.GetHdc();
                    bool ok = Win32Interop.PrintWindow(hwnd, hdc, Win32Interop.PW_RENDERFULLCONTENT);
                    if (!ok) { ok = Win32Interop.PrintWindow(hwnd, hdc, 0); }
                    g.ReleaseHdc(hdc);
                    if (!ok)
                    {
                        bmp.Dispose();
                        return null;
                    }
                }
                return bmp;
            }
            catch { return null; }
        }

        private static Bitmap CropToFlex(Bitmap formBmp, IntPtr form, IntPtr flex)
        {
            if (flex == IntPtr.Zero || !Win32Interop.IsWindow(flex) || formBmp == null)
            {
                return formBmp != null ? (Bitmap)formBmp.Clone() : null;
            }
            try
            {
                Win32Driver.RectInfo f = Win32Driver.GetRect(form);
                Win32Driver.RectInfo g = Win32Driver.GetRect(flex);
                int x = g.X - f.X;
                int y = g.Y - f.Y;
                int w = g.W;
                int h = g.H;
                if (x < 0) { w += x; x = 0; }
                if (y < 0) { h += y; y = 0; }
                if (x + w > formBmp.Width) { w = formBmp.Width - x; }
                if (y + h > formBmp.Height) { h = formBmp.Height - y; }
                if (w < 20 || h < 20) { return (Bitmap)formBmp.Clone(); }
                Rectangle rect = new Rectangle(x, y, w, h);
                return formBmp.Clone(rect, PixelFormat.Format32bppArgb);
            }
            catch { return (Bitmap)formBmp.Clone(); }
        }

        private static bool VisibleBand(
            IntPtr flex, IntPtr screen, out int x, out int y, out int w, out int h)
        {
            x = 0; y = 0; w = 0; h = 0;
            try
            {
                Win32Driver.RectInfo g = Win32Driver.GetRect(flex);
                Win32Driver.RectInfo f = (screen != IntPtr.Zero && Win32Interop.IsWindow(screen))
                    ? Win32Driver.GetRect(screen) : g;
                int x1 = Math.Max(g.X, f.X + 4);
                int y1 = Math.Max(g.Y, f.Y + 4);
                int x2 = Math.Min(g.X + g.W, f.X + f.W - 4);
                int y2 = Math.Min(g.Y + g.H, f.Y + f.H - 4);
                w = x2 - x1;
                h = y2 - y1;
                x = x1;
                y = y1;
                return w >= 20 && h >= 20;
            }
            catch { return false; }
        }

        private static bool IsBlank(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            int stepX = Math.Max(1, w / 40);
            int stepY = Math.Max(1, h / 40);
            int ink = 0;
            int n = 0;
            for (int yy = 0; yy < h; yy += stepY)
            {
                for (int xx = 0; xx < w; xx += stepX)
                {
                    Color c = bmp.GetPixel(xx, yy);
                    n++;
                    if (c.R < 245 || c.G < 245 || c.B < 245) { ink++; }
                }
            }
            return n > 0 && ink * 50 < n;
        }

        private static string ReadPolarities(Bitmap bmp)
        {
            StringBuilder all = new StringBuilder();
            AppendRead(all, ToInk(bmp, true));
            all.Append('\t');
            AppendRead(all, ToInk(bmp, false));
            return all.ToString();
        }

        private static string ReadInk(bool[][] ink)
        {
            int blobs;
            int digits;
            return ReadInkStats(ink, out blobs, out digits);
        }

        private static string ReadInkStats(bool[][] ink, out int blobs, out int digits)
        {
            blobs = 0;
            digits = 0;
            if (ink == null) { return ""; }
            StringBuilder sb = new StringBuilder();
            AppendRead(sb, ink, out blobs, out digits);
            string cols = ReadColumns(ink);
            if (!string.IsNullOrEmpty(cols) && sb.ToString().IndexOf(cols, StringComparison.Ordinal) < 0)
            {
                sb.Append('\t').Append(cols);
            }
            return sb.ToString();
        }

        // White glyphs on Windows selection blue (0,120,215). Bright AA counts
        // as ink so 3 stays whole; solid blue (gray < 110) stays as the gap.
        private static bool[][] ToContrastInk(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] raw = new byte[stride * h];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                bool[][] ink = new bool[h][];
                for (int y = 0; y < h; y++)
                {
                    ink[y] = new bool[w];
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + (x * 4);
                        int b = raw[i];
                        int g = raw[i + 1];
                        int r = raw[i + 2];
                        bool bgBlue = b > 80 && b >= r + 10 && b >= g + 8 && r < 190;
                        bool bgYellow = r > 160 && g > 140 && b < 120;
                        ink[y][x] = !bgBlue && !bgYellow;
                    }
                }
                return ink;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static string ReadColumns(bool[][] ink)
        {
            if (ink == null || ink.Length < 5) { return ""; }
            int h = ink.Length;
            int w = ink[0].Length;
            int[] col = new int[w];
            for (int x = 0; x < w; x++)
            {
                int n = 0;
                for (int y = 0; y < h; y++)
                {
                    if (ink[y][x]) { n++; }
                }
                col[x] = n;
            }
            int need = Math.Max(3, h / 6);
            StringBuilder sb = new StringBuilder();
            int a = -1;
            for (int x = 0; x <= w; x++)
            {
                bool hit = x < w && col[x] >= need;
                if (hit && a < 0) { a = x; }
                if ((!hit || x == w) && a >= 0)
                {
                    int b = x - 1;
                    int bw = b - a + 1;
                    if (bw >= 2 && bw <= 44)
                    {
                        Blob blob = CropColumn(ink, a, b);
                        char d = blob != null ? Classify(blob) : '\0';
                        if (d != '\0') { sb.Append(d); }
                    }
                    a = -1;
                }
            }
            return sb.ToString();
        }

        private static Blob CropColumn(bool[][] ink, int x0, int x1)
        {
            int h = ink.Length;
            int minY = h, maxY = -1;
            int pixels = 0;
            for (int y = 0; y < h; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (!ink[y][x]) { continue; }
                    pixels++;
                    if (y < minY) { minY = y; }
                    if (y > maxY) { maxY = y; }
                }
            }
            if (maxY < minY || pixels < 4) { return null; }
            Blob b = new Blob();
            b.X = x0;
            b.Y = minY;
            b.W = x1 - x0 + 1;
            b.H = maxY - minY + 1;
            b.Pixels = pixels;
            b.Ink = new bool[b.H][];
            for (int y = 0; y < b.H; y++)
            {
                b.Ink[y] = new bool[b.W];
                for (int x = 0; x < b.W; x++)
                {
                    b.Ink[y][x] = ink[minY + y][x0 + x];
                }
            }
            return b;
        }

        private static bool[][] ToBrightInk(Bitmap bmp, int minGray)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] raw = new byte[stride * h];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                bool[][] ink = new bool[h][];
                for (int y = 0; y < h; y++)
                {
                    ink[y] = new bool[w];
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + (x * 4);
                        int g = (raw[i + 2] * 30 + raw[i + 1] * 59 + raw[i] * 11) / 100;
                        ink[y][x] = g >= minGray;
                    }
                }
                return ink;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static void AppendRead(StringBuilder sb, bool[][] ink)
        {
            int blobs;
            int digits;
            AppendRead(sb, ink, out blobs, out digits);
        }

        private static void AppendRead(
            StringBuilder sb, bool[][] ink, out int blobCount, out int digitCount)
        {
            blobCount = 0;
            digitCount = 0;
            if (ink == null) { return; }
            List<Blob> blobs = Connected(ink);
            MergeHalves(blobs);
            blobCount = blobs.Count;
            if (blobs.Count == 0) { return; }
            blobs.Sort(CompareBlob);
            int rowY = blobs[0].Y + (blobs[0].H / 2);
            int lastEnd = -1;
            StringBuilder row = new StringBuilder();
            for (int i = 0; i < blobs.Count; i++)
            {
                Blob b = blobs[i];
                int cy = b.Y + (b.H / 2);
                if (Math.Abs(cy - rowY) > Math.Max(10, b.H))
                {
                    sb.Append(row.ToString()).Append('\t');
                    row.Length = 0;
                    rowY = cy;
                    lastEnd = -1;
                }
                char d = Classify(b);
                if (d == '\0') { continue; }
                if (lastEnd >= 0 && b.X - lastEnd >= 6 && b.X - lastEnd <= 16)
                {
                    char mid = ClassifyGap(ink, lastEnd, b.X, rowY);
                    if (mid != '\0')
                    {
                        row.Append(mid);
                        digitCount++;
                    }
                }
                row.Append(d);
                digitCount++;
                lastEnd = b.X + b.W;
            }
            sb.Append(row.ToString());
        }

        private static char ClassifyGap(bool[][] ink, int x0, int x1, int rowY)
        {
            if (ink == null || x1 - x0 < 3) { return '\0'; }
            int h = ink.Length;
            int y0 = Math.Max(0, rowY - 7);
            int y1 = Math.Min(h - 1, rowY + 7);
            Blob gap = CropBand(ink, x0, x1 - 1, y0, y1);
            if (gap == null) { return '\0'; }
            char d = Classify(gap);
            if (d != '\0' && d != '7') { return d; }
            Blob fat = Dilate(gap);
            if (fat != null)
            {
                d = Classify(fat);
                if (d != '\0' && d != '7') { return d; }
            }
            // Broken 3 on this FlexGrid looks like a 7 after AA is stripped.
            if (x1 - x0 >= 6 && x1 - x0 <= 10 && gap.Pixels >= 6) { return '3'; }
            return d == '7' ? '3' : '\0';
        }

        private static Blob Dilate(Blob src)
        {
            if (src == null) { return null; }
            int w = src.W + 2;
            int h = src.H + 2;
            Blob b = new Blob();
            b.X = src.X - 1;
            b.Y = src.Y - 1;
            b.W = w;
            b.H = h;
            b.Ink = new bool[h][];
            int px = 0;
            for (int y = 0; y < h; y++)
            {
                b.Ink[y] = new bool[w];
                for (int x = 0; x < w; x++)
                {
                    bool hit = false;
                    for (int dy = -1; dy <= 1 && !hit; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !hit; dx++)
                        {
                            int sy = y - 1 + dy;
                            int sx = x - 1 + dx;
                            if (sx >= 0 && sy >= 0 && sx < src.W && sy < src.H)
                            {
                                hit = src.Ink[sy][sx];
                            }
                        }
                    }
                    b.Ink[y][x] = hit;
                    if (hit) { px++; }
                }
            }
            b.Pixels = px;
            return b;
        }

        private static Blob CropBand(bool[][] ink, int x0, int x1, int y0, int y1)
        {
            if (ink == null) { return null; }
            int h = ink.Length;
            int w = ink[0].Length;
            x0 = Math.Max(0, x0);
            x1 = Math.Min(w - 1, x1);
            y0 = Math.Max(0, y0);
            y1 = Math.Min(h - 1, y1);
            if (x1 < x0 || y1 < y0) { return null; }
            int minX = x1, maxX = x0, minY = y1, maxY = y0, pixels = 0;
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (!ink[y][x]) { continue; }
                    pixels++;
                    if (x < minX) { minX = x; }
                    if (x > maxX) { maxX = x; }
                    if (y < minY) { minY = y; }
                    if (y > maxY) { maxY = y; }
                }
            }
            if (pixels < 2 || maxX < minX) { return null; }
            Blob b = new Blob();
            b.X = minX;
            b.Y = minY;
            b.W = maxX - minX + 1;
            b.H = maxY - minY + 1;
            b.Pixels = pixels;
            b.Ink = new bool[b.H][];
            for (int y = 0; y < b.H; y++)
            {
                b.Ink[y] = new bool[b.W];
                for (int x = 0; x < b.W; x++)
                {
                    b.Ink[y][x] = ink[minY + y][minX + x];
                }
            }
            return b;
        }

        private static int CompareBlob(Blob a, Blob b)
        {
            int ay = a.Y + (a.H / 2);
            int by = b.Y + (b.H / 2);
            int dy = ay - by;
            if (Math.Abs(dy) > 6) { return dy; }
            return a.X - b.X;
        }

        private static char Classify(Blob b)
        {
            if (b == null || b.W < 2 || b.H < 6) { return '\0'; }
            if (b.W > 8 || b.H > 12) { return '\0'; }
            if (b.Pixels < 4) { return '\0'; }

            if (b.W <= 2 && b.H >= 6) { return '1'; }

            char best = '\0';
            double bestScore = 0.22;
            for (int t = 0; t < _templates.Count; t++)
            {
                double s = Score(b, _templates[t]);
                if (s > bestScore)
                {
                    bestScore = s;
                    best = _templates[t].Digit;
                }
            }
            if (best != '\0') { return best; }
            return FeatureDigit(b);
        }

        private static Bitmap Invert(Bitmap src)
        {
            if (src == null) { return null; }
            int w = src.Width;
            int h = src.Height;
            Bitmap dst = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData sData = src.LockBits(
                new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData dData = dst.LockBits(
                new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int srcStride = sData.Stride;
                int dstStride = dData.Stride;
                byte[] srcRaw = new byte[srcStride * h];
                byte[] dstRaw = new byte[dstStride * h];
                Marshal.Copy(sData.Scan0, srcRaw, 0, srcRaw.Length);
                for (int y = 0; y < h; y++)
                {
                    int sRow = y * srcStride;
                    int dRow = y * dstStride;
                    for (int x = 0; x < w; x++)
                    {
                        int si = sRow + (x * 4);
                        int di = dRow + (x * 4);
                        dstRaw[di] = (byte)(255 - srcRaw[si]);
                        dstRaw[di + 1] = (byte)(255 - srcRaw[si + 1]);
                        dstRaw[di + 2] = (byte)(255 - srcRaw[si + 2]);
                        dstRaw[di + 3] = srcRaw[si + 3];
                    }
                }
                Marshal.Copy(dstRaw, 0, dData.Scan0, dstRaw.Length);
            }
            finally
            {
                src.UnlockBits(sData);
                dst.UnlockBits(dData);
            }
            return dst;
        }

        private static double Score(Blob b, DigitTemplate t)
        {
            int tw = t.W;
            int th = t.H;
            if (tw < 2 || th < 2) { return 0; }
            int tmplInk = 0;
            int hit = 0;
            int blobInk = 0;
            for (int y = 0; y < th; y++)
            {
                int sy = y * b.H / th;
                if (sy >= b.H) { sy = b.H - 1; }
                for (int x = 0; x < tw; x++)
                {
                    int sx = x * b.W / tw;
                    if (sx >= b.W) { sx = b.W - 1; }
                    bool ink = b.Ink[sy][sx];
                    bool tmpl = t.Ink[y][x];
                    if (ink) { blobInk++; }
                    if (!tmpl) { continue; }
                    tmplInk++;
                    if (ink) { hit++; }
                }
            }
            if (tmplInk == 0) { return 0; }
            double rec = (double)hit / (double)tmplInk;
            double prec = blobInk > 0 ? (double)hit / (double)blobInk : 0;
            if (rec + prec <= 0) { return 0; }
            return 2.0 * rec * prec / (rec + prec);
        }

        private static bool[][] ToInk(Bitmap bmp, bool darkOnLight)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] raw = new byte[stride * h];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                int[] hist = new int[256];
                byte[] gray = new byte[w * h];
                for (int y = 0; y < h; y++)
                {
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + (x * 4);
                        int g = (raw[i + 2] * 30 + raw[i + 1] * 59 + raw[i] * 11) / 100;
                        gray[y * w + x] = (byte)g;
                        hist[g]++;
                    }
                }
                int cut = Otsu(hist, w * h);
                if (cut < 16 || cut > 240) { cut = 160; }
                bool[][] ink = new bool[h][];
                for (int y = 0; y < h; y++)
                {
                    ink[y] = new bool[w];
                    for (int x = 0; x < w; x++)
                    {
                        int g = gray[y * w + x];
                        ink[y][x] = darkOnLight ? (g < cut) : (g > cut);
                    }
                }
                return ink;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private static int Otsu(int[] hist, int total)
        {
            double sum = 0;
            for (int i = 0; i < 256; i++) { sum += i * hist[i]; }
            double sumB = 0;
            int wB = 0;
            double max = 0;
            int cut = 128;
            for (int t = 0; t < 256; t++)
            {
                wB += hist[t];
                if (wB == 0) { continue; }
                int wF = total - wB;
                if (wF == 0) { break; }
                sumB += t * hist[t];
                double mB = sumB / wB;
                double mF = (sum - sumB) / wF;
                double between = (double)wB * (double)wF * (mB - mF) * (mB - mF);
                if (between > max) { max = between; cut = t; }
            }
            return cut;
        }

        private static List<Blob> Connected(bool[][] ink)
        {
            List<Blob> list = new List<Blob>();
            int h = ink.Length;
            int w = ink[0].Length;
            bool[][] seen = new bool[h][];
            for (int y = 0; y < h; y++) { seen[y] = new bool[w]; }

            int[] stack = new int[w * h * 2];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (!ink[y][x] || seen[y][x]) { continue; }
                    int minX = x, maxX = x, minY = y, maxY = y, pixels = 0;
                    int sp = 0;
                    stack[sp++] = x;
                    stack[sp++] = y;
                    seen[y][x] = true;
                    while (sp > 0)
                    {
                        int cy = stack[--sp];
                        int cx = stack[--sp];
                        pixels++;
                        if (cx < minX) { minX = cx; }
                        if (cx > maxX) { maxX = cx; }
                        if (cy < minY) { minY = cy; }
                        if (cy > maxY) { maxY = cy; }
                        TryPush(ink, seen, stack, ref sp, w, h, cx - 1, cy);
                        TryPush(ink, seen, stack, ref sp, w, h, cx + 1, cy);
                        TryPush(ink, seen, stack, ref sp, w, h, cx, cy - 1);
                        TryPush(ink, seen, stack, ref sp, w, h, cx, cy + 1);
                    }
                    int bw = maxX - minX + 1;
                    int bh = maxY - minY + 1;
                    if (bh < 5 || bh > 48 || bw < 2 || bw > 48) { continue; }
                    if (pixels < 4) { continue; }
                    Blob b = new Blob();
                    b.X = minX;
                    b.Y = minY;
                    b.W = bw;
                    b.H = bh;
                    b.Pixels = pixels;
                    b.Ink = new bool[bh][];
                    for (int yy = 0; yy < bh; yy++)
                    {
                        b.Ink[yy] = new bool[bw];
                        for (int xx = 0; xx < bw; xx++)
                        {
                            b.Ink[yy][xx] = ink[minY + yy][minX + xx];
                        }
                    }
                    list.Add(b);
                    if (list.Count >= 400) { return list; }
                }
            }
            return list;
        }

        private static void TryPush(
            bool[][] ink, bool[][] seen, int[] stack, ref int sp, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) { return; }
            if (!ink[y][x] || seen[y][x]) { return; }
            seen[y][x] = true;
            stack[sp++] = x;
            stack[sp++] = y;
        }

        // 4-connected splits 0/8/9; glue touching halves, not neighbouring digits.
        private static void MergeHalves(List<Blob> blobs)
        {
            if (blobs == null || blobs.Count < 2) { return; }
            blobs.Sort(CompareBlob);
            for (int i = 0; i < blobs.Count - 1; i++)
            {
                Blob a = blobs[i];
                Blob b = blobs[i + 1];
                int gap = b.X - (a.X + a.W);
                if (gap > 1 || gap < -1) { continue; }
                int ay1 = a.Y;
                int ay2 = a.Y + a.H;
                int by1 = b.Y;
                int by2 = b.Y + b.H;
                int overlap = Math.Min(ay2, by2) - Math.Max(ay1, by1);
                if (overlap < Math.Min(a.H, b.H) / 2) { continue; }
                int x0 = Math.Min(a.X, b.X);
                int y0 = Math.Min(a.Y, b.Y);
                int x1 = Math.Max(a.X + a.W, b.X + b.W);
                int y1 = Math.Max(a.Y + a.H, b.Y + b.H);
                int nw = x1 - x0;
                int nh = y1 - y0;
                if (nw > 10 || nh > 16) { continue; }
                Blob m = new Blob();
                m.X = x0;
                m.Y = y0;
                m.W = nw;
                m.H = nh;
                m.Ink = new bool[nh][];
                int px = 0;
                for (int y = 0; y < nh; y++)
                {
                    m.Ink[y] = new bool[nw];
                    for (int x = 0; x < nw; x++)
                    {
                        int ax = x0 + x - a.X;
                        int ay = y0 + y - a.Y;
                        int bx = x0 + x - b.X;
                        int by = y0 + y - b.Y;
                        bool hit = false;
                        if (ax >= 0 && ay >= 0 && ax < a.W && ay < a.H) { hit = a.Ink[ay][ax]; }
                        if (!hit && bx >= 0 && by >= 0 && bx < b.W && by < b.H) { hit = b.Ink[by][bx]; }
                        m.Ink[y][x] = hit;
                        if (hit) { px++; }
                    }
                }
                m.Pixels = px;
                blobs[i] = m;
                blobs.RemoveAt(i + 1);
                i--;
            }
        }

        private static char FeatureDigit(Blob b)
        {
            if (b == null || b.W < 2 || b.H < 5 || b.W > 12 || b.H > 16) { return '\0'; }
            if (b.W <= 2 && b.H >= 6) { return '1'; }

            int holes = CountHoles(b);
            double dens = (double)b.Pixels / (double)(b.W * b.H);
            int top = RowInk(b, 0, Math.Max(1, b.H / 3));
            int mid = RowInk(b, b.H / 3, (2 * b.H) / 3);
            int bot = RowInk(b, (2 * b.H) / 3, b.H);
            int left = ColInk(b, 0, Math.Max(1, b.W / 3));
            int right = ColInk(b, (2 * b.W) / 3, b.W);
            int maxRow = Math.Max(top, Math.Max(mid, bot));
            if (maxRow == 0) { return '\0'; }

            if (holes >= 2) { return '8'; }
            if (holes == 1)
            {
                if (dens > 0.55 && top >= mid && bot >= mid) { return '0'; }
                if (bot > top + 1 && mid >= top) { return '6'; }
                if (top > bot + 1) { return '9'; }
                if (right > left) { return '4'; }
                return '0';
            }

            if (top > mid && top > bot && dens < 0.42) { return '7'; }
            if (mid >= top && mid >= bot && left > right) { return '5'; }
            if (bot >= top && mid <= top) { return '2'; }
            if (mid >= Math.Min(top, bot) && right >= left) { return '3'; }
            return '\0';
        }

        private static int RowInk(Blob b, int y0, int y1)
        {
            int n = 0;
            for (int y = y0; y < y1 && y < b.H; y++)
            {
                for (int x = 0; x < b.W; x++)
                {
                    if (b.Ink[y][x]) { n++; }
                }
            }
            return n;
        }

        private static int ColInk(Blob b, int x0, int x1)
        {
            int n = 0;
            for (int y = 0; y < b.H; y++)
            {
                for (int x = x0; x < x1 && x < b.W; x++)
                {
                    if (b.Ink[y][x]) { n++; }
                }
            }
            return n;
        }

        private static int CountHoles(Blob b)
        {
            int h = b.H + 2;
            int w = b.W + 2;
            bool[][] seen = new bool[h][];
            for (int y = 0; y < h; y++) { seen[y] = new bool[w]; }
            int holes = 0;
            int[] stack = new int[w * h * 2];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool ink = (x > 0 && y > 0 && x <= b.W && y <= b.H) && b.Ink[y - 1][x - 1];
                    if (ink || seen[y][x]) { continue; }
                    int sp = 0;
                    stack[sp++] = x;
                    stack[sp++] = y;
                    seen[y][x] = true;
                    bool edge = false;
                    while (sp > 0)
                    {
                        int cy = stack[--sp];
                        int cx = stack[--sp];
                        if (cx == 0 || cy == 0 || cx == w - 1 || cy == h - 1) { edge = true; }
                        TryHole(b, seen, stack, ref sp, w, h, cx - 1, cy);
                        TryHole(b, seen, stack, ref sp, w, h, cx + 1, cy);
                        TryHole(b, seen, stack, ref sp, w, h, cx, cy - 1);
                        TryHole(b, seen, stack, ref sp, w, h, cx, cy + 1);
                    }
                    if (!edge) { holes++; }
                }
            }
            return holes;
        }

        private static void TryHole(
            Blob b, bool[][] seen, int[] stack, ref int sp, int w, int h, int x, int y)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || seen[y][x]) { return; }
            bool ink = (x > 0 && y > 0 && x <= b.W && y <= b.H) && b.Ink[y - 1][x - 1];
            if (ink) { return; }
            seen[y][x] = true;
            stack[sp++] = x;
            stack[sp++] = y;
        }

        private static void EnsureTemplates()
        {
            if (_templates != null) { return; }
            _templates = new List<DigitTemplate>();
            string[] fonts = new string[] { "Tahoma", "Microsoft Sans Serif", "MS Sans Serif", "Arial", "Segoe UI" };
            float[] sizes = new float[] { 7f, 8f, 9f, 10f, 11f, 12f, 14f };
            FontStyle[] styles = new FontStyle[] { FontStyle.Regular, FontStyle.Bold };
            for (int f = 0; f < fonts.Length; f++)
            {
                for (int s = 0; s < sizes.Length; s++)
                {
                    for (int st = 0; st < styles.Length; st++)
                    {
                        for (char d = '0'; d <= '9'; d++)
                        {
                            DigitTemplate t = RenderTemplate(d, fonts[f], sizes[s], styles[st]);
                            if (t != null) { _templates.Add(t); }
                        }
                    }
                }
            }
        }

        private static DigitTemplate RenderTemplate(
            char digit, string fontName, float px, FontStyle style)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(40, 48, PixelFormat.Format32bppArgb))
                using (Graphics g = Graphics.FromImage(bmp))
                using (Font font = new Font(fontName, px, style, GraphicsUnit.Pixel))
                {
                    g.Clear(Color.White);
                    g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
                    g.DrawString(digit.ToString(), font, Brushes.Black, 2, 2);
                    bool[][] ink = ToInkCut(bmp, 160);
                    int minX = 40, minY = 48, maxX = -1, maxY = -1;
                    for (int y = 0; y < 48; y++)
                    {
                        for (int x = 0; x < 40; x++)
                        {
                            if (!ink[y][x]) { continue; }
                            if (x < minX) { minX = x; }
                            if (x > maxX) { maxX = x; }
                            if (y < minY) { minY = y; }
                            if (y > maxY) { maxY = y; }
                        }
                    }
                    if (maxX < minX) { return null; }
                    DigitTemplate t = new DigitTemplate();
                    t.Digit = digit;
                    t.W = maxX - minX + 1;
                    t.H = maxY - minY + 1;
                    t.Ink = new bool[t.H][];
                    for (int y = 0; y < t.H; y++)
                    {
                        t.Ink[y] = new bool[t.W];
                        for (int x = 0; x < t.W; x++)
                        {
                            t.Ink[y][x] = ink[minY + y][minX + x];
                        }
                    }
                    return t;
                }
            }
            catch { return null; }
        }

        private static bool[][] ToInkCut(Bitmap bmp, int cut)
        {
            int w = bmp.Width;
            int h = bmp.Height;
            BitmapData data = bmp.LockBits(
                new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] raw = new byte[stride * h];
                Marshal.Copy(data.Scan0, raw, 0, raw.Length);
                bool[][] ink = new bool[h][];
                for (int y = 0; y < h; y++)
                {
                    ink[y] = new bool[w];
                    int row = y * stride;
                    for (int x = 0; x < w; x++)
                    {
                        int i = row + (x * 4);
                        int g = (raw[i + 2] * 30 + raw[i + 1] * 59 + raw[i] * 11) / 100;
                        ink[y][x] = g < cut;
                    }
                }
                return ink;
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        private class DigitTemplate
        {
            public char Digit;
            public int W;
            public int H;
            public bool[][] Ink;
        }

        private class Blob
        {
            public int X;
            public int Y;
            public int W;
            public int H;
            public int Pixels;
            public bool[][] Ink;
        }
    }
}
