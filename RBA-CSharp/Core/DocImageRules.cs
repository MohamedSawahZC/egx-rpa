// =============================================================================
//  DocImageRules.cs - EGX / Coding document image gates for Test-Save validation.
//  Size <= 500 KB, DPI between 100 and 300 (inclusive).
// =============================================================================
using System;
using System.Drawing;
using System.IO;

namespace RbaCore
{
    public static class DocImageRules
    {
        public const long MaxBytes = 500L * 1024L;
        public const float MinDpi = 100f;
        public const float MaxDpi = 300f;

        public static bool TryValidateFile(string path, out string note)
        {
            note = "";
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                note = "ملف المستند غير موجود";
                return false;
            }

            string ext = Path.GetExtension(path);
            if (!string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                // Soft preference: Coding accepts JPG best; still allow others but warn size/DPI.
            }

            FileInfo fi = new FileInfo(path);
            if (fi.Length > MaxBytes)
            {
                note = Path.GetFileName(path) + ": الحجم " +
                    (fi.Length / 1024.0).ToString("0") + " KB أكبر من الحد 500 KB";
                return false;
            }

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Image img = Image.FromStream(fs, false, false))
                {
                    float hDpi = img.HorizontalResolution;
                    float vDpi = img.VerticalResolution;
                    if (hDpi < MinDpi || hDpi > MaxDpi || vDpi < MinDpi || vDpi > MaxDpi)
                    {
                        note = Path.GetFileName(path) + ": الدقة " +
                            hDpi.ToString("0") + "x" + vDpi.ToString("0") +
                            " DPI خارج النطاق 100–300";
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                note = Path.GetFileName(path) + ": تعذر قراءة الصورة (" + ex.Message + ")";
                return false;
            }

            return true;
        }

        public static bool TryValidateFolder(string folder, out string note)
        {
            note = "";
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            {
                note = "لم يتم فحص المستندات (مجلد غير موجود)";
                return false;
            }

            System.Collections.Generic.List<string> imgs = DocumentAttach.ListImages(folder);
            if (imgs.Count == 0)
            {
                note = "لا توجد صور مستندات في مجلد الهوية";
                return false;
            }
            if (imgs.Count == 1)
            {
                note = "صورة واحدة فقط في مجلد الهوية (يفضّل وجه وظهر على الأقل)";
                // Soft-ok: still validate the single file; caller treats as warn via note.
            }

            for (int i = 0; i < imgs.Count; i++)
            {
                string one;
                if (!TryValidateFile(imgs[i], out one))
                {
                    note = one;
                    return false;
                }
            }

            note = imgs.Count + " صورة من مجلد الهوية (≤500KB، DPI 100–300)";
            return true;
        }
    }
}
