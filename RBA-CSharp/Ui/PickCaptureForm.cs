// =============================================================================
//  PickCaptureForm.cs - color-picker style: click any UI target outside this form.
//  Captures menu caption or button identity; Esc cancels.
// =============================================================================
using System;
using System.Drawing;
using System.Windows.Forms;
using RbaCore;

namespace RbaUi
{
    public class PickCaptureForm : Form
    {
        private readonly IntPtr _preferredApp;
        private readonly UiPickKind _prefer;
        private readonly Label _lbl;
        private Timer _timer;
        private bool _armed;
        private bool _prevDown;
        private UiPickResult _result;

        public UiPickResult Result
        {
            get { return _result; }
        }

        public PickCaptureForm(string instruction, IntPtr preferredApp, UiPickKind prefer)
        {
            _preferredApp = preferredApp;
            _prefer = prefer;
            _result = new UiPickResult();
            _result.Cancelled = true;

            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            ClientSize = new Size(520, 88);
            BackColor = Color.FromArgb(15, 23, 32);
            Cursor = Cursors.Cross;
            Text = "التقاط عنصر الواجهة";
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = false;

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Left + (wa.Width - Width) / 2, wa.Top + 24);

            _lbl = new Label();
            _lbl.Dock = DockStyle.Fill;
            _lbl.Padding = new Padding(16, 12, 16, 12);
            _lbl.ForeColor = Color.White;
            _lbl.Font = new Font("Segoe UI Semibold", 10f);
            _lbl.TextAlign = ContentAlignment.MiddleCenter;
            _lbl.Text = instruction + "\n(Esc للإلغاء — يُحفظ بالاسم وليس برقم النافذة)";
            Controls.Add(_lbl);

            KeyPreview = true;
            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    _result = new UiPickResult();
                    _result.Cancelled = true;
                    _result.Note = "cancelled";
                    DialogResult = DialogResult.Cancel;
                    Close();
                }
            };

            _timer = new Timer();
            _timer.Interval = 40;
            _timer.Tick += OnTick;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Wait until the button that opened us is fully released.
            _armed = false;
            _prevDown = true;
            _timer.Start();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (_timer != null) { _timer.Stop(); _timer.Dispose(); _timer = null; }
            base.OnFormClosed(e);
        }

        private void OnTick(object sender, EventArgs e)
        {
            bool down = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_LBUTTON) & 0x8000) != 0;
            bool esc = (Win32Interop.GetAsyncKeyState(Win32Interop.VK_ESCAPE) & 0x8000) != 0;
            if (esc)
            {
                _timer.Stop();
                _result = new UiPickResult();
                _result.Cancelled = true;
                _result.Note = "cancelled";
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }

            if (!_armed)
            {
                if (!down) { _armed = true; _prevDown = false; }
                return;
            }

            // Capture on button release so the click lands on the target, not our form.
            if (_prevDown && !down)
            {
                POINT pt;
                if (!Win32Interop.GetCursorPos(out pt))
                {
                    _prevDown = down;
                    return;
                }

                // Ignore clicks on this picker banner.
                if (Bounds.Contains(pt.X, pt.Y))
                {
                    _prevDown = down;
                    return;
                }

                _timer.Stop();
                UiPickResult hit = UiControlPicker.IdentifyAtPoint(pt, _preferredApp, _prefer);
                if (!hit.Ok)
                {
                    _lbl.Text = (string.IsNullOrEmpty(hit.Note) ? "لم يُعرف العنصر" : hit.Note)
                        + " — أعد النقر أو Esc للإلغاء";
                    _timer.Start();
                    _prevDown = false;
                    return;
                }

                _result = hit;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }
            _prevDown = down;
        }

        public static UiPickResult Run(IWin32Window owner, string instruction, IntPtr preferredApp, UiPickKind prefer)
        {
            using (PickCaptureForm f = new PickCaptureForm(instruction, preferredApp, prefer))
            {
                f.ShowDialog(owner);
                return f.Result;
            }
        }
    }
}
