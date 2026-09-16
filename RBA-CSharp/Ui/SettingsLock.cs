// =============================================================================
//  SettingsLock.cs - admin gate for Setup / Settings so operators cannot edit maps.
// =============================================================================
using System;
using System.Drawing;
using System.Windows.Forms;
using RbaCore;

namespace RbaUi
{
    public static class SettingsLock
    {
        private const string AdminUser = "wilzyadmin";
        private const string AdminSecret = "wilzy@1234";

        private static bool _unlocked;

        public static bool IsUnlocked
        {
            get { return _unlocked; }
        }

        public static bool EnsureUnlocked(IWin32Window owner)
        {
            if (_unlocked) { return true; }
            return PromptUnlock(owner, "أدخل حساب المسؤول لتعديل الإعدادات.", true);
        }

        // Always ask again — used for locked backup export/import.
        public static bool ConfirmAdmin(IWin32Window owner, string hint)
        {
            return PromptUnlock(owner, hint, false);
        }

        // Same secret used to encrypt .rpas backups so the file stays locked off-machine.
        public static string BackupPassphrase()
        {
            return SettingsVault.DefaultPassphrase();
        }

        public static bool Accepts(string user, string password)
        {
            if (user == null) { user = ""; }
            if (password == null) { password = ""; }
            return string.Equals(user.Trim(), AdminUser, StringComparison.Ordinal) &&
                   string.Equals(password, AdminSecret, StringComparison.Ordinal);
        }

        private static bool PromptUnlock(IWin32Window owner, string hint, bool markSession)
        {
            using (SettingsLoginForm f = new SettingsLoginForm(hint))
            {
                if (f.ShowDialog(owner) != DialogResult.OK) { return false; }
                if (!Accepts(f.UserName, f.Password))
                {
                    MessageBox.Show(owner,
                        "اسم المستخدم أو كلمة المرور غير صحيحة.",
                        "قفل الإعدادات",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return false;
                }
            }
            if (markSession) { _unlocked = true; }
            return true;
        }

        private sealed class SettingsLoginForm : Form
        {
            private readonly TextBox _user;
            private readonly TextBox _pass;

            public string UserName
            {
                get { return _user != null ? _user.Text : ""; }
            }

            public string Password
            {
                get { return _pass != null ? _pass.Text : ""; }
            }

            public SettingsLoginForm()
                : this("أدخل حساب المسؤول لتعديل الإعدادات.")
            {
            }

            public SettingsLoginForm(string hintText)
            {
                Text = "قفل الإعدادات";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterParent;
                MinimizeBox = false;
                MaximizeBox = false;
                ClientSize = new Size(400, 210);
                RightToLeft = RightToLeft.Yes;
                RightToLeftLayout = true;
                UiTheme.ApplyForm(this);

                Label hint = new Label();
                hint.Text = string.IsNullOrEmpty(hintText)
                    ? "أدخل حساب المسؤول لتعديل الإعدادات."
                    : hintText;
                hint.AutoSize = false;
                hint.SetBounds(16, 10, 368, 32);
                hint.ForeColor = UiTheme.InkMuted;
                Controls.Add(hint);

                Label lu = new Label();
                lu.Text = "المستخدم";
                lu.SetBounds(16, 46, 80, 22);
                Controls.Add(lu);

                _user = new TextBox();
                _user.SetBounds(100, 44, 280, 26);
                _user.RightToLeft = RightToLeft.No;
                Controls.Add(_user);

                Label lp = new Label();
                lp.Text = "كلمة المرور";
                lp.SetBounds(16, 86, 80, 22);
                Controls.Add(lp);

                _pass = new TextBox();
                _pass.SetBounds(100, 84, 280, 26);
                _pass.UseSystemPasswordChar = true;
                _pass.RightToLeft = RightToLeft.No;
                Controls.Add(_pass);

                Button ok = UiTheme.MakeButton("دخول", true);
                ok.SetBounds(100, 140, 110, 34);
                ok.DialogResult = DialogResult.OK;
                Controls.Add(ok);

                Button cancel = UiTheme.MakeButton("إلغاء", false);
                cancel.SetBounds(220, 140, 110, 34);
                cancel.DialogResult = DialogResult.Cancel;
                Controls.Add(cancel);

                AcceptButton = ok;
                CancelButton = cancel;
            }
        }
    }
}
