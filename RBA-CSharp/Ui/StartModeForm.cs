// =============================================================================
//  StartModeForm.cs - choose استعلام or تكويد before any Coding work.
// =============================================================================
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RbaUi
{
    public enum StartMode
    {
        Cancel = 0,
        Inquire = 1,
        Code = 2
    }

    public sealed class StartModeForm : Form
    {
        public StartMode Chosen { get; private set; }

        public StartModeForm()
        {
            Chosen = StartMode.Cancel;
            Text = "ماذا تريد أن تعمل؟";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(420, 250);
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            UiTheme.ApplyForm(this);

            Label hint = new Label();
            hint.Text = "اختر المهمة. الاستعلام لا يفتح التكويد تلقائياً.";
            hint.SetBounds(16, 16, 388, 40);
            hint.ForeColor = UiTheme.InkMuted;
            Controls.Add(hint);

            Button inquire = UiTheme.MakeButton("استعلام — المرور على الشيت", true);
            inquire.SetBounds(40, 70, 340, 44);
            inquire.Click += delegate
            {
                Chosen = StartMode.Inquire;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(inquire);

            Button code = UiTheme.MakeButton("تكويد — إدخال العملاء في Coding", true);
            code.SetBounds(40, 124, 340, 44);
            code.Click += delegate
            {
                Chosen = StartMode.Code;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(code);

            Button cancel = UiTheme.MakeButton("إلغاء", false);
            cancel.SetBounds(150, 188, 120, 36);
            cancel.Click += delegate
            {
                Chosen = StartMode.Cancel;
                DialogResult = DialogResult.Cancel;
                Close();
            };
            Controls.Add(cancel);
            CancelButton = cancel;
        }
    }
}
