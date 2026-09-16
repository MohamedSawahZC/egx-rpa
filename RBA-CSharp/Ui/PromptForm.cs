// =============================================================================
//  PromptForm.cs - single-line RTL input dialog used by Setup actions.
// =============================================================================
using System;
using System.Drawing;
using System.Windows.Forms;

namespace RbaUi
{
    public class PromptForm : Form
    {
        private readonly TextBox _input;

        private PromptForm(string title, string question, string initial)
        {
            Text = title;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(430, 130);

            Label prompt = new Label();
            prompt.Text = question;
            prompt.SetBounds(14, 14, 400, 22);
            prompt.ForeColor = UiTheme.Ink;
            Controls.Add(prompt);

            _input = new TextBox();
            _input.SetBounds(14, 42, 400, 26);
            _input.Text = initial ?? "";
            Controls.Add(_input);

            Button ok = UiTheme.MakeButton("موافق", true);
            ok.SetBounds(14, 84, 100, 32);
            ok.DialogResult = DialogResult.OK;
            Controls.Add(ok);

            Button cancel = UiTheme.MakeButton("إلغاء", false);
            cancel.SetBounds(124, 84, 100, 32);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        // Returns "" when the operator cancels.
        public static string Ask(IWin32Window owner, string title, string question, string initial)
        {
            using (PromptForm f = new PromptForm(title, question, initial))
            {
                if (f.ShowDialog(owner) != DialogResult.OK) { return ""; }
                return f._input.Text.Trim();
            }
        }
    }
}
