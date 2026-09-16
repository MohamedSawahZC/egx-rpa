// =============================================================================
//  DocImageCompose.cs - after SFTP pull: PNG→JPG, resize, white canvas + overlays.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace RbaCore
{
    public static class DocImageCompose
    {
        public const int DefaultMaxWidth = 1100;
        public const int DefaultDpi = 150;

        public static void PrepareTree(string docsRoot, BotConfig cfg, Action<string, string> log)
        {
            if (string.IsNullOrWhiteSpace(docsRoot) || !Directory.Exists(docsRoot)) { return; }
            if (log == null) { log = delegate(string m, string l) { }; }

            string[] dirs;
            try { dirs = Directory.GetDirectories(docsRoot); }
            catch { return; }

            int ok = 0;
            int fail = 0;
            for (int i = 0; i < dirs.Length; i++)
            {
                string name = Path.GetFileName(dirs[i]);
                if (KycSheetAdapter.DigitsOnly(name).Length < 10) { continue; }
                int n = PrepareFolder(dirs[i], cfg, log);
                if (n > 0) { ok += n; }
                else { fail++; }
            }
            log("docs compose jpg+overlays foldersOk=" + ok + " emptyOrFail=" + fail +
                " dest=" + docsRoot, ok > 0 ? "OK" : "INFO");
        }

        public static int PrepareFolder(string folder, BotConfig cfg, Action<string, string> log)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) { return 0; }
            List<string> files = ListSourceImages(folder);
            int done = 0;
            for (int i = 0; i < files.Count; i++)
            {
                if (IsOverlaySource(files[i], cfg)) { continue; }
                try
                {
                    if (ComposeOne(files[i], cfg)) { done++; }
                }
                catch (Exception ex)
                {
                    if (log != null)
                    {
                        log("docs compose failed «" + Path.GetFileName(files[i]) + "»: " + ex.Message, "WARN");
                    }
                }
            }
            return done;
        }

        private static List<string> ListSourceImages(string folder)
        {
            List<string> list = new List<string>();
            string[] files;
            try { files = Directory.GetFiles(folder); }
            catch { return list; }
            for (int i = 0; i < files.Length; i++)
            {
                string ext = Path.GetExtension(files[i]);
                if (string.IsNullOrEmpty(ext)) { continue; }
                ext = ext.ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" ||
                    ext == ".bmp" || ext == ".tif" || ext == ".tiff" || ext == ".jfif")
                {
                    list.Add(files[i]);
                }
            }
            return list;
        }

        private static bool IsOverlaySource(string path, BotConfig cfg)
        {
            if (cfg == null || string.IsNullOrEmpty(path)) { return false; }
            return SameFile(path, cfg.StampImagePath) ||
                   SameFile(path, cfg.SignatureImagePath) ||
                   SameFile(path, cfg.Signature2ImagePath);
        }

        private static bool SameFile(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) { return false; }
            try
            {
                return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static bool ComposeOne(string path, BotConfig cfg)
        {
            int maxW = cfg != null && cfg.DocImageMaxWidth > 200 ? cfg.DocImageMaxWidth : DefaultMaxWidth;
            string dest = Path.Combine(
                Path.GetDirectoryName(path),
                Path.GetFileNameWithoutExtension(path) + ".jpg");

            using (Image raw = LoadUnlocked(path))
            using (Bitmap sized = FitWidth(raw, maxW))
            using (Bitmap photo = ToBlackAndWhite(sized))
            {
                int marginX = Math.Max(48, photo.Width / 10);
                int marginTop = Math.Max(56, photo.Height / 12);
                int marginBot = Math.Max(88, photo.Height / 8);
                int canvasW = photo.Width + (marginX * 2);
                int canvasH = photo.Height + marginTop + marginBot;

                using (Bitmap canvas = new Bitmap(canvasW, canvasH, PixelFormat.Format24bppRgb))
                {
                    canvas.SetResolution(DefaultDpi, DefaultDpi);
                    using (Graphics g = Graphics.FromImage(canvas))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(photo, marginX, marginTop, photo.Width, photo.Height);

                        int overlayH = Math.Max(42, Math.Min(110, canvasH / 9));
                        DrawOverlay(g, cfg != null ? cfg.StampImagePath : "",
                            canvasW - marginX - OverlayWidth(cfg != null ? cfg.StampImagePath : "", overlayH),
                            12, overlayH);
                        DrawOverlay(g, cfg != null ? cfg.SignatureImagePath : "",
                            marginX, canvasH - overlayH - 12, overlayH);
                        DrawOverlay(g, cfg != null ? cfg.Signature2ImagePath : "",
                            canvasW - marginX - OverlayWidth(cfg != null ? cfg.Signature2ImagePath : "", overlayH),
                            canvasH - overlayH - 12, overlayH);
                    }
                    SaveJpegUnderLimit(canvas, dest);
                }
            }

            if (!string.Equals(path, dest, StringComparison.OrdinalIgnoreCase) && File.Exists(dest))
            {
                try { File.Delete(path); } catch { }
            }
            return File.Exists(dest);
        }

        private static Image LoadUnlocked(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Image tmp = Image.FromStream(fs, false, false))
            {
                return new Bitmap(tmp);
            }
        }

        private static Bitmap FitWidth(Image src, int maxW)
        {
            int w = src.Width;
            int h = src.Height;
            if (w > maxW && w > 0)
            {
                h = Math.Max(1, (int)Math.Round(h * (maxW / (double)w)));
                w = maxW;
            }
            Bitmap b = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            b.SetResolution(DefaultDpi, DefaultDpi);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, w, h);
            }
            return b;
        }

        // ID photo only — stamp/signatures stay in color on the white canvas.
        private static Bitmap ToBlackAndWhite(Bitmap src)
        {
            Bitmap dest = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            dest.SetResolution(src.HorizontalResolution, src.VerticalResolution);
            using (Graphics g = Graphics.FromImage(dest))
            using (ImageAttributes attr = new ImageAttributes())
            {
                ColorMatrix m = new ColorMatrix(new float[][]
                {
                    new float[] { 0.299f, 0.299f, 0.299f, 0, 0 },
                    new float[] { 0.587f, 0.587f, 0.587f, 0, 0 },
                    new float[] { 0.114f, 0.114f, 0.114f, 0, 0 },
                    new float[] { 0, 0, 0, 1, 0 },
                    new float[] { 0, 0, 0, 0, 1 }
                });
                attr.SetColorMatrix(m);
                g.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height),
                    0, 0, src.Width, src.Height, GraphicsUnit.Pixel, attr);
            }
            return dest;
        }

        private static int OverlayWidth(string path, int height)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || height <= 0) { return height; }
            try
            {
                using (Image img = LoadUnlocked(path))
                {
                    if (img.Height <= 0) { return height; }
                    return Math.Max(24, (int)Math.Round(img.Width * (height / (double)img.Height)));
                }
            }
            catch { return height; }
        }

        private static void DrawOverlay(Graphics g, string path, int x, int y, int height)
        {
            if (g == null || string.IsNullOrWhiteSpace(path) || !File.Exists(path) || height <= 0)
            {
                return;
            }
            try
            {
                using (Image img = LoadUnlocked(path))
                {
                    int w = OverlayWidth(path, height);
                    if (x < 0) { x = 0; }
                    if (y < 0) { y = 0; }
                    g.DrawImage(img, x, y, w, height);
                }
            }
            catch { }
        }

        private static void SaveJpegUnderLimit(Bitmap canvas, string dest)
        {
            ImageCodecInfo codec = JpegCodec();
            long quality = 82;
            string tmp = dest + ".part";
            for (int i = 0; i < 6; i++)
            {
                using (EncoderParameters ep = new EncoderParameters(1))
                {
                    ep.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    canvas.Save(tmp, codec, ep);
                }
                FileInfo fi = new FileInfo(tmp);
                if (fi.Length <= DocImageRules.MaxBytes || quality <= 55)
                {
                    break;
                }
                quality -= 8;
            }
            if (File.Exists(dest)) { File.Delete(dest); }
            File.Move(tmp, dest);
        }

        private static ImageCodecInfo JpegCodec()
        {
            ImageCodecInfo[] all = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].FormatID == ImageFormat.Jpeg.Guid) { return all[i]; }
            }
            throw new InvalidOperationException("JPEG encoder not available");
        }
    }
}
