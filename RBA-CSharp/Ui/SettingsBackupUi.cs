// =============================================================================
//  SettingsBackupUi.cs - locked settings backup dialogs (same admin lock).
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using RbaCore;

namespace RbaUi
{
    public static class SettingsBackupUi
    {
        private static string _lastDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public static MappingBundleResult PickAndRead(IWin32Window owner)
        {
            if (!SettingsLock.ConfirmAdmin(owner, "أدخل حساب المسؤول لفتح نسخة الإعدادات المقفلة."))
            {
                return Fail("cancelled");
            }

            OpenFileDialog d = new OpenFileDialog();
            d.Title = "استيراد نسخة إعدادات مقفلة";
            d.Filter = SettingsVault.FileFilter;
            d.InitialDirectory = _lastDir;
            d.CheckFileExists = true;
            if (d.ShowDialog(owner) != DialogResult.OK) { return Fail("cancelled"); }
            _lastDir = Path.GetDirectoryName(d.FileName);

            try
            {
                string json;
                if (SettingsVault.LooksLocked(d.FileName))
                {
                    json = SettingsVault.ReadLocked(d.FileName, SettingsLock.BackupPassphrase());
                }
                else
                {
                    json = File.ReadAllText(d.FileName, System.Text.Encoding.UTF8);
                }
                MappingBundleResult b = MappingBundle.ImportJson(json);
                if (b != null && b.Ok) { b.SourcePath = d.FileName; }
                return b;
            }
            catch (Exception ex)
            {
                MappingBundleResult r = new MappingBundleResult();
                r.Note = "تعذر فتح الملف المقفَل: " + ex.Message;
                return r;
            }
        }

        public static bool SaveLocked(
            IWin32Window owner,
            BotConfig cfg,
            List<FieldSelector> selectors,
            string confirmedTitle,
            string confirmedScreen)
        {
            if (!SettingsLock.ConfirmAdmin(owner, "أدخل حساب المسؤول لحفظ نسخة إعدادات مقفلة."))
            {
                return false;
            }

            SaveFileDialog d = new SaveFileDialog();
            d.Title = "تصدير نسخة إعدادات مقفلة";
            d.Filter = SettingsVault.FileFilter;
            d.DefaultExt = "rpas";
            d.FileName = "RPA-settings-" + Environment.MachineName + "-" +
                         DateTime.Now.ToString("yyyyMMdd-HHmm") + ".rpas";
            d.InitialDirectory = _lastDir;
            d.OverwritePrompt = true;
            if (d.ShowDialog(owner) != DialogResult.OK) { return false; }
            _lastDir = Path.GetDirectoryName(d.FileName);

            string dest = d.FileName;
            if (string.Equals(Path.GetExtension(dest), ".json", StringComparison.OrdinalIgnoreCase))
            {
                dest = Path.ChangeExtension(dest, ".rpas");
            }

            string json = MappingBundle.ExportToString(cfg, selectors, confirmedTitle, confirmedScreen);
            SettingsVault.WriteLocked(dest, json, SettingsLock.BackupPassphrase());
            MessageBox.Show(owner,
                "تم حفظ نسخة مقفلة:\n" + dest +
                "\n\nالملف مشفّر. فتحه يحتاج نفس مستخدم/كلمة مرور قفل الإعدادات.",
                "نسخة مقفلة",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return true;
        }

        private static MappingBundleResult Fail(string note)
        {
            MappingBundleResult r = new MappingBundleResult();
            r.Note = note;
            return r;
        }
    }
}
