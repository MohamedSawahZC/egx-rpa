// =============================================================================
//  ClassifyForm.cs - one client at a time: جديد / سحب / رفض + الكود الموحد.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.Windows.Forms;
using RbaCore;

namespace RbaUi
{
    public class ClassifyForm : Form
    {
        private readonly string _sheetPath;
        private readonly List<OrderedDictionary> _rows;
        private readonly Action<string, string> _log;
        private int _index;
        private Label _lblProgress;
        private Label _lblName;
        private Label _lblNid;
        private string _copyName = "";
        private string _copyNid = "";
        private Label _lblPhone;
        private Label _lblAddress;
        private Label _lblStatus;
        private TextBox _txtCode12;
        private ComboBox _cmbReject;
        private TextBox _txtRejectOther;
        private Label _lblRejectOther;
        private bool _finished;

        public int ClassifiedCount { get; private set; }
        public bool Completed { get { return _finished; } }

        public ClassifyForm(string sheetPath, List<OrderedDictionary> rows, Action<string, string> log)
        {
            _sheetPath = sheetPath;
            _rows = rows ?? new List<OrderedDictionary>();
            _log = log ?? (delegate(string m, string l) { });
            _index = 0;

            Text = "استعلام — جديد / سحب / رفض";
            RightToLeft = RightToLeft.Yes;
            // RightToLeftLayout hides ComboBox item text on WinForms.
            RightToLeftLayout = false;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(680, 610);
            UiTheme.ApplyForm(this);

            _lblProgress = MakeLabel(16, 12, 648, 24, "Segoe UI Semibold", 11f);
            _lblName = MakeLabel(16, 44, 500, 32, "Segoe UI Semibold", 15f);
            _lblNid = MakeLabel(16, 82, 500, 24, "Segoe UI", 10.5f);
            Button btnCopyName = CopyButton("نسخ الاسم", 528, 42);
            btnCopyName.Click += delegate { CopyText(_copyName, "الاسم"); };
            Button btnCopyNid = CopyButton("نسخ الرقم", 528, 80);
            btnCopyNid.Click += delegate { CopyText(_copyNid, "الرقم القومي"); };
            _lblPhone = MakeLabel(16, 108, 588, 22, "Segoe UI", 10f);
            _lblAddress = MakeLabel(16, 132, 588, 40, "Segoe UI", 9.5f);
            _lblStatus = MakeLabel(16, 176, 588, 22, "Segoe UI", 10f);
            _lblStatus.ForeColor = UiTheme.InkMuted;

            Label codeHint = MakeLabel(16, 208, 588, 20, "Segoe UI", 9f);
            codeHint.Text = "الكود الموحد — مطلوب عند اختيار «سحب» (أي عدد من الأرقام)";

            _txtCode12 = new TextBox();
            _txtCode12.SetBounds(16, 230, 588, 30);
            _txtCode12.Font = new Font("Consolas", 13f);
            _txtCode12.MaxLength = 40;
            _txtCode12.RightToLeft = RightToLeft.No;
            _txtCode12.TextAlign = HorizontalAlignment.Left;
            Controls.Add(_txtCode12);

            Label rejectHint = MakeLabel(16, 270, 648, 20, "Tahoma", 9f);
            rejectHint.Text = "سبب الرفض — مطلوب عند اختيار «رفض» (أسباب أخرى = اكتب السبب)";

            _cmbReject = new ComboBox();
            _cmbReject.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbReject.FlatStyle = FlatStyle.Flat;
            _cmbReject.RightToLeft = RightToLeft.Yes;
            _cmbReject.Font = new Font("Tahoma", 11f);
            _cmbReject.SetBounds(16, 292, 648, 32);
            _cmbReject.IntegralHeight = false;
            _cmbReject.ItemHeight = 24;
            _cmbReject.DrawMode = DrawMode.OwnerDrawFixed;
            _cmbReject.DrawItem += DrawRejectItem;
            for (int i = 0; i < OperatorDecision.RejectReasons.Length; i++)
            {
                _cmbReject.Items.Add(OperatorDecision.RejectReasons[i]);
            }
            _cmbReject.SelectedIndexChanged += delegate { ApplyRejectOtherUi(); };
            Controls.Add(_cmbReject);

            _lblRejectOther = MakeLabel(16, 328, 648, 18, "Tahoma", 9f);
            _lblRejectOther.Text = "اكتب سبب الرفض (مطلوب عند «أسباب أخرى»)";
            _lblRejectOther.ForeColor = UiTheme.InkMuted;

            _txtRejectOther = new TextBox();
            _txtRejectOther.SetBounds(16, 348, 648, 28);
            _txtRejectOther.Font = new Font("Tahoma", 10.5f);
            _txtRejectOther.MaxLength = 240;
            Controls.Add(_txtRejectOther);
            ApplyRejectOtherUi();

            Button btnNew = BigButton("جديد", 16, 392, Color.FromArgb(30, 120, 80));
            btnNew.Click += delegate { Decide(OperatorDecision.NewAr, OperatorDecision.NewEn, false, false); };

            Button btnPull = BigButton("سحب", 226, 392, Color.FromArgb(30, 90, 160));
            btnPull.Click += delegate { Decide(OperatorDecision.PullAr, OperatorDecision.PullEn, true, false); };

            Button btnReject = BigButton("رفض", 436, 392, Color.FromArgb(150, 40, 50));
            btnReject.Click += delegate { Decide(OperatorDecision.RejectedAr, OperatorDecision.RejectedEn, false, true); };

            Button btnSkip = UiTheme.MakeButton("تخطي الحالي", false);
            btnSkip.SetBounds(16, 558, 150, 34);
            btnSkip.Click += delegate { Advance(); };
            Controls.Add(btnSkip);

            Button btnDone = UiTheme.MakeButton("إنهاء التصنيف", true);
            btnDone.SetBounds(430, 558, 174, 34);
            btnDone.Click += delegate
            {
                int left = CountPending();
                if (left > 0)
                {
                    DialogResult ask = MessageBox.Show(this,
                        "باقي " + left + " عميل بلا قرار من أصل " + _rows.Count +
                        ".\nإنهاء الآن يتركهم بلا قرار.\n\nإغلاق على كده؟",
                        "إنهاء التصنيف", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (ask != DialogResult.Yes) { return; }
                    _finished = false;
                }
                else
                {
                    _finished = true;
                }
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(btnDone);

            ShowCurrent();
        }

        private Label MakeLabel(int x, int y, int w, int h, string font, float size)
        {
            Label l = new Label();
            l.SetBounds(x, y, w, h);
            l.Font = new Font(font, size);
            l.ForeColor = UiTheme.Ink;
            Controls.Add(l);
            return l;
        }

        private void DrawRejectItem(object sender, DrawItemEventArgs e)
        {
            e.DrawBackground();
            if (e.Index < 0 || _cmbReject == null) { return; }
            string text = _cmbReject.Items[e.Index] != null ? _cmbReject.Items[e.Index].ToString() : "";
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush bg = new SolidBrush(selected ? Color.FromArgb(30, 90, 160) : Color.White))
            using (SolidBrush fg = new SolidBrush(selected ? Color.White : Color.FromArgb(30, 30, 30)))
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = StringAlignment.Far;
                sf.LineAlignment = StringAlignment.Center;
                sf.FormatFlags = StringFormatFlags.DirectionRightToLeft;
                e.Graphics.FillRectangle(bg, e.Bounds);
                e.Graphics.DrawString(text, e.Font ?? _cmbReject.Font, fg, e.Bounds, sf);
            }
            e.DrawFocusRectangle();
        }

        private string SelectedRejectReason()
        {
            if (_cmbReject == null || _cmbReject.SelectedItem == null) { return ""; }
            string pick = _cmbReject.SelectedItem.ToString().Trim();
            string other = _txtRejectOther != null ? _txtRejectOther.Text : "";
            return OperatorDecision.FormatRejectReason(pick, other);
        }

        private void ApplyRejectOtherUi()
        {
            bool others = _cmbReject != null &&
                          OperatorDecision.IsOthersReject(
                              _cmbReject.SelectedItem != null ? _cmbReject.SelectedItem.ToString() : "");
            if (_txtRejectOther != null)
            {
                _txtRejectOther.Enabled = others;
                _txtRejectOther.BackColor = others ? Color.White : Color.FromArgb(240, 240, 240);
                if (!others) { _txtRejectOther.Text = ""; }
            }
            if (_lblRejectOther != null)
            {
                _lblRejectOther.Enabled = others;
            }
        }

        private void SelectRejectReason(string reason)
        {
            if (_cmbReject == null) { return; }
            string want = (reason ?? "").Trim();
            string extra = "";
            int hit = -1;
            if (OperatorDecision.IsOthersReject(want))
            {
                extra = OperatorDecision.OthersRejectDetail(want);
                for (int i = 0; i < _cmbReject.Items.Count; i++)
                {
                    string item = _cmbReject.Items[i] != null ? _cmbReject.Items[i].ToString() : "";
                    if (OperatorDecision.IsOthersReject(item)) { hit = i; break; }
                }
            }
            else if (want.Length > 0)
            {
                for (int i = 0; i < _cmbReject.Items.Count; i++)
                {
                    string item = _cmbReject.Items[i] != null ? _cmbReject.Items[i].ToString() : "";
                    if (string.Equals(item, want, StringComparison.Ordinal)) { hit = i; break; }
                }
            }
            _cmbReject.SelectedIndex = hit >= 0 ? hit : -1;
            ApplyRejectOtherUi();
            if (_txtRejectOther != null && OperatorDecision.IsOthersReject(want))
            {
                _txtRejectOther.Text = extra;
            }
        }

        private Button CopyButton(string text, int x, int y)
        {
            Button b = UiTheme.MakeButton(text, false);
            b.SetBounds(x, y, 136, 30);
            Controls.Add(b);
            return b;
        }

        private void CopyText(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                MessageBox.Show(this, "لا يوجد " + label + " للنسخ.", "نسخ",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                Clipboard.SetText(value);
                if (_lblStatus != null)
                {
                    _lblStatus.Text = "تم نسخ " + label;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "تعذر النسخ: " + ex.Message, "نسخ",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private Button BigButton(string text, int x, int y, Color back)
        {
            Button b = new Button();
            b.Text = text;
            b.SetBounds(x, y, 168, 52);
            b.FlatStyle = FlatStyle.Flat;
            b.BackColor = back;
            b.ForeColor = Color.White;
            b.Font = new Font("Segoe UI Semibold", 13f);
            Controls.Add(b);
            return b;
        }

        private int CountPending()
        {
            int n = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                if (OperatorDecision.NeedsClassify(OperatorDecision.ReadStatus(_rows[i]))) { n++; }
            }
            return n;
        }

        private int FirstPendingIndex()
        {
            for (int i = 0; i < _rows.Count; i++)
            {
                if (OperatorDecision.NeedsClassify(OperatorDecision.ReadStatus(_rows[i])))
                {
                    return i;
                }
            }
            return -1;
        }

        private void ShowCurrent()
        {
            if (_rows.Count == 0)
            {
                _lblProgress.Text = "لا يوجد عملاء";
                _lblName.Text = "";
                return;
            }
            if (_index < 0) { _index = 0; }
            if (_index >= _rows.Count) { _index = _rows.Count - 1; }

            OrderedDictionary row = _rows[_index];
            string name = CsvUtil.Get(row, "Name", "");
            string nid = CsvUtil.Get(row, "NationalId", "");
            string phone = CsvUtil.Get(row, "Mobile", "");
            string address = CsvUtil.Get(row, "Address", "");
            string status = OperatorDecision.ReadStatus(row);
            string code12 = CsvUtil.Get(row, "CODE12", "كود 12");
            if (string.IsNullOrWhiteSpace(code12))
            {
                code12 = CsvUtil.Get(row, "الكود الموحد", "NIN");
            }
            string reject = CsvUtil.Get(row, "سبب الرفض", "RejectReason");

            _lblProgress.Text = "عميل " + (_index + 1) + " من " + _rows.Count +
                "  ·  هذه الجلسة " + ClassifiedCount +
                "  ·  بلا قرار " + CountPending();
            _copyName = (name ?? "").Trim();
            _copyNid = KycSheetAdapter.DigitsOnly(nid);
            if (_copyNid.Length == 0) { _copyNid = (nid ?? "").Trim(); }
            _lblName.Text = string.IsNullOrWhiteSpace(name) ? "(بدون اسم)" : name;
            _lblNid.Text = "الرقم القومي: " + nid;
            _lblPhone.Text = "الموبايل: " + phone;
            _lblAddress.Text = "العنوان: " + (string.IsNullOrWhiteSpace(address) ? "—" : address);
            _lblStatus.Text = "الحالة الحالية: " + (string.IsNullOrWhiteSpace(status) ? "(لم يُصنَّف — اختر قراراً)" : status);
            _txtCode12.Text = code12 ?? "";
            SelectRejectReason(reject);
            _txtCode12.Focus();
        }

        private void Decide(string statusAr, string statusEn, bool requireCode12, bool requireReject)
        {
            if (_rows.Count == 0) { return; }
            OrderedDictionary row = _rows[_index];
            string nid = CsvUtil.Get(row, "NationalId", "").Trim();
            if (nid.Length == 0)
            {
                MessageBox.Show(this, "هذا الصف بلا رقم قومي — تخطّيه.", "تصنيف",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Advance();
                return;
            }

            string code12 = KycSheetAdapter.DigitsOnly(_txtCode12.Text);
            string reject = SelectedRejectReason();

            if (requireCode12)
            {
                if (code12.Length == 0)
                {
                    MessageBox.Show(this,
                        "اكتب الكود الموحد ثم سحب.",
                        "سحب", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    _txtCode12.Focus();
                    return;
                }
                SheetSync.WriteClientCodes(_sheetPath, nid, code12, code12);
                SetRow(row, "كود 12", code12);
                SetRow(row, "CODE12", code12);
                SetRow(row, "الكود الموحد", code12);
                SetRow(row, "NIN", code12);
            }

            if (requireReject)
            {
                if (reject.Length == 0)
                {
                    MessageBox.Show(this,
                        "اختَر سبب الرفض. إذا اخترت «أسباب أخرى» فاكتب السبب في الخانة ثم اضغط رفض.",
                        "رفض", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    if (_cmbReject != null && OperatorDecision.IsOthersReject(
                        _cmbReject.SelectedItem != null ? _cmbReject.SelectedItem.ToString() : ""))
                    {
                        if (_txtRejectOther != null) { _txtRejectOther.Focus(); }
                    }
                    else if (_cmbReject != null) { _cmbReject.Focus(); }
                    return;
                }
                SetRow(row, "سبب الرفض", reject);
                SetRow(row, "RejectReason", reject);
            }

            string notes = requireReject ? ("رفض: " + reject) : "تصنيف يدوي";
            SheetSync.WriteClientStatus(_sheetPath, nid, statusAr, statusEn, notes);
            SetRow(row, "الحالة", statusAr);
            SetRow(row, "Status", statusEn);

            ClassifiedCount++;
            _log("classify " + nid + " → " + statusAr +
                (requireCode12 ? (" CODE12=" + code12) : "") +
                (requireReject ? (" reject=" + reject) : ""), "OK");
            Advance();
        }

        private static void SetRow(OrderedDictionary row, string key, string value)
        {
            if (row.Contains(key)) { row[key] = value; }
            else { row.Add(key, value); }
        }

        private void Advance()
        {
            if (_index < _rows.Count - 1)
            {
                _index++;
                ShowCurrent();
                return;
            }

            int pending = FirstPendingIndex();
            if (pending >= 0)
            {
                _index = pending;
                ShowCurrent();
                MessageBox.Show(this,
                    "خلصت الشيت وفي " + CountPending() + " بلا قرار — رجوع لأول صف فاضي.",
                    "استعلام", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _finished = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
