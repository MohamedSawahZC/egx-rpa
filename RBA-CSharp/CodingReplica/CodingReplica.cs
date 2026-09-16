// =============================================================================
//  CodingReplica — local Coding.exe stand-in for RPA E2E tests (no OCR).
//  Main title matches «تكويد»; entry form title «بيانات العملاء».
// =============================================================================
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CodingReplica
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private readonly List<SavedClient> _clients = new List<SavedClient>();
        private EntryForm _entry;
        private PullForm _pull;
        private ConfirmForm _confirm;
        private readonly string _savePath;

        public MainForm()
        {
            Text = "برنامج التكويد - CodingReplica";
            Width = 1100;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            IsMdiContainer = true;
            RightToLeft = RightToLeft.Yes;
            RightToLeftLayout = true;

            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CodingReplica");
            Directory.CreateDirectory(dir);
            _savePath = Path.Combine(dir, "saved-clients.csv");

            // Native Win32 menu (MainMenu) so RPA MenuNavigator/GetMenu works.
            MainMenu mainMenu = new MainMenu();
            MenuItem coding = new MenuItem("التكويد");
            coding.MenuItems.Add(new MenuItem("إدخال بيانات العملاء", delegate { OpenEntry(); }));
            coding.MenuItems.Add(new MenuItem("سحب الأكواد", delegate { OpenPull(); }));
            MenuItem system = new MenuItem("نظام تكويد العملاء");
            system.MenuItems.Add(new MenuItem(
                "تأكيد بيانات العملاء قبل إرسالها للتكويد", delegate { OpenConfirm(); }));
            mainMenu.MenuItems.Add(coding);
            mainMenu.MenuItems.Add(system);
            Menu = mainMenu;

            Label hint = new Label();
            hint.Text = "CodingReplica جاهز لاختبار RPA — القوائم Win32 (التكويد / نظام تكويد العملاء)";
            hint.Dock = DockStyle.Bottom;
            hint.Height = 28;
            hint.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(hint);
        }

        public void RegisterSaved(SavedClient c)
        {
            if (c == null) { return; }
            _clients.Add(c);
            AppendCsv(c);
        }

        public IList<SavedClient> Clients { get { return _clients; } }

        private void OpenEntry()
        {
            if (_entry == null || _entry.IsDisposed)
            {
                _entry = new EntryForm(this);
                _entry.MdiParent = this;
            }
            _entry.Show();
            _entry.BringToFront();
        }

        private void OpenPull()
        {
            if (_pull == null || _pull.IsDisposed)
            {
                _pull = new PullForm(this);
                _pull.MdiParent = this;
            }
            _pull.Show();
            _pull.BringToFront();
        }

        private void OpenConfirm()
        {
            if (_confirm == null || _confirm.IsDisposed)
            {
                _confirm = new ConfirmForm(this);
                _confirm.MdiParent = this;
            }
            _confirm.RefreshGrid();
            _confirm.Show();
            _confirm.BringToFront();
        }

        private void AppendCsv(SavedClient c)
        {
            bool neu = !File.Exists(_savePath);
            using (StreamWriter w = new StreamWriter(_savePath, true, new UTF8Encoding(true)))
            {
                if (neu)
                {
                    w.WriteLine("Name,NationalId,BrokerCode,Mobile,Address,CODE12,SavedAt");
                }
                w.WriteLine(Escape(c.Name) + "," + Escape(c.NationalId) + "," + Escape(c.BrokerCode) + "," +
                    Escape(c.Mobile) + "," + Escape(c.Address) + "," + Escape(c.Code12) + "," +
                    Escape(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            }
        }

        private static string Escape(string s)
        {
            if (s == null) { s = ""; }
            if (s.IndexOfAny(new char[] { ',', '"', '\n' }) >= 0) { return "\"" + s.Replace("\"", "\"\"") + "\""; }
            return s;
        }
    }

    public class SavedClient
    {
        public string Name;
        public string NationalId;
        public string BrokerCode;
        public string Mobile;
        public string Address;
        public string Code12;
        public bool Pulled;
    }

    public class EntryForm : Form
    {
        private readonly MainForm _host;
        private TextBox _broker;
        private TextBox _name;
        private TextBox _d1, _d2, _d3;
        private TextBox _address;
        private TextBox _nat, _gender, _legal, _sub;
        private TextBox _nid;
        private TextBox _idType, _gov, _country;
        private TextBox _mobile, _docsCount, _job;
        private CheckBox _chkFiles;

        public EntryForm(MainForm host)
        {
            _host = host;
            Text = "بيانات العملاء";
            Width = 780;
            Height = 560;
            FormBorderStyle = FormBorderStyle.Sizable;
            // LTR layout so ReplicaMapper / Position stamps match control X/Y (lab).
            RightToLeft = RightToLeft.No;
            RightToLeftLayout = false;

            int y = 12;
            _broker = AddBox(this, "كود الفرع", 12, y, 52); y += 34;
            _name = AddBox(this, "الاسم", 12, y, 330); y += 34;
            _d1 = AddBox(this, "يوم", 12, y, 40);
            _d2 = AddBox(this, "شهر", 60, y, 40);
            _d3 = AddBox(this, "سنة", 108, y, 40); y += 34;
            _address = AddBox(this, "العنوان", 12, y, 330); y += 34;
            _nat = AddBox(this, "جنسية", 12, y, 52);
            _gender = AddBox(this, "نوع", 80, y, 52);
            _legal = AddBox(this, "شكل", 148, y, 52);
            _sub = AddBox(this, "فرعي", 216, y, 52); y += 34;
            _nid = AddBox(this, "قومي", 12, y, 330); y += 34;
            _idType = AddBox(this, "هوية", 12, y, 52);
            _gov = AddBox(this, "محافظة", 80, y, 52);
            _country = AddBox(this, "دولة", 148, y, 52); y += 34;
            // Widths must match ReplicaMapper buckets: mid (71-200)=mobile, code(52)=docs, wide(>200)=job.
            _mobile = AddBox(this, "موبايل", 12, y, 160); y += 34;
            _docsCount = AddBox(this, "مستندات", 12, y, 52); y += 34;
            _job = AddBox(this, "وظيفة", 12, y, 330); y += 34;

            _chkFiles = new CheckBox();
            _chkFiles.Text = "ملفات";
            _chkFiles.SetBounds(12, y, 80, 24);
            Controls.Add(_chkFiles);
            y += 36;

            Button save = new Button();
            save.Text = "حافظ";
            save.SetBounds(12, y, 90, 32);
            save.Click += delegate { SaveClient(); };
            Controls.Add(save);

            Button neu = new Button();
            neu.Text = "جديد";
            neu.SetBounds(110, y, 90, 32);
            neu.Click += delegate { ClearForm(); };
            Controls.Add(neu);

            Button cancel = new Button();
            cancel.Text = "الغ";
            cancel.SetBounds(208, y, 90, 32);
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);
        }

        private static TextBox AddBox(Control parent, string label, int x, int y, int w)
        {
            Label l = new Label();
            l.Text = label;
            l.SetBounds(x, y, 70, 20);
            parent.Controls.Add(l);
            TextBox t = new TextBox();
            t.SetBounds(x + 74, y - 2, w, 24);
            parent.Controls.Add(t);
            return t;
        }

        private void ClearForm()
        {
            foreach (Control c in Controls)
            {
                TextBox tb = c as TextBox;
                if (tb != null) { tb.Text = ""; }
            }
            if (_chkFiles != null) { _chkFiles.Checked = false; }
        }

        private void SaveClient()
        {
            if (string.IsNullOrWhiteSpace(_nid.Text) || string.IsNullOrWhiteSpace(_name.Text))
            {
                // Do not show a modal that RPA may treat as a successful Coding confirm.
                return;
            }
            SavedClient c = new SavedClient();
            c.Name = _name.Text.Trim();
            c.NationalId = Digits(_nid.Text);
            c.BrokerCode = Digits(_broker.Text);
            c.Mobile = Digits(_mobile.Text);
            c.Address = _address.Text.Trim();
            // Lab CODE12: 0 + last 11 of NID (or pad) — operator can override in pull tests via sheet.
            string nid = c.NationalId;
            if (nid.Length >= 11) { c.Code12 = "0" + nid.Substring(nid.Length - 11); }
            else { c.Code12 = ("000000000000" + nid).Substring(("000000000000" + nid).Length - 12); }
            _host.RegisterSaved(c);
            // Coding-like confirm so RPA ClientEntry can verify Save.
            MessageBox.Show(this, "تم الحفظ", "Coding",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            ClearForm();
        }

        private static string Digits(string s)
        {
            if (string.IsNullOrEmpty(s)) { return ""; }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= '0' && s[i] <= '9') { sb.Append(s[i]); }
            }
            return sb.ToString();
        }
    }

    public class PullForm : Form
    {
        private readonly MainForm _host;
        private TextBox _search;
        private DataGridView _grid;

        public PullForm(MainForm host)
        {
            _host = host;
            Text = "سحب الأكواد";
            Width = 720;
            Height = 420;
            // LTR so RPA position/click maps stay stable in lab.
            RightToLeft = RightToLeft.No;
            RightToLeftLayout = false;

            _search = new TextBox();
            _search.SetBounds(12, 12, 200, 24);
            Controls.Add(_search);

            Button btn = new Button();
            btn.Text = "بحث";
            btn.SetBounds(220, 10, 80, 28);
            btn.Click += delegate { Search(); };
            Controls.Add(btn);

            _grid = new DataGridView();
            _grid.SetBounds(12, 48, 680, 280);
            _grid.AllowUserToAddRows = false;
            _grid.ReadOnly = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.Columns.Add("Pull", "سحب");
            _grid.Columns.Add("Name", "الاسم");
            _grid.Columns.Add("Code12", "الكود الموحد");
            _grid.Columns.Add("Nid", "قومي");
            _grid.CellClick += delegate(object s, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex < 0) { return; }
                _grid.ClearSelection();
                _grid.Rows[e.RowIndex].Selected = true;
                _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[0];
            };
            Controls.Add(_grid);

            Label tip = new Label();
            tip.Text = "بحث → click table → F2 → F9 (no سحب cell)";
            tip.SetBounds(12, 340, 480, 24);
            Controls.Add(tip);

            KeyPreview = true;
        }

        // Win32 SendShortcut posts keys to the HWND — ProcessCmdKey receives them.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F2)
            {
                // Arm pull — F9 completes (same as Coding).
                return true;
            }
            if (keyData == Keys.F9)
            {
                ConfirmPull();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void Search()
        {
            _grid.Rows.Clear();
            string q = Digits(_search.Text);
            foreach (SavedClient c in _host.Clients)
            {
                if (q.Length > 0 && c.Code12 != q && c.NationalId != q) { continue; }
                _grid.Rows.Add("سحب", c.Name, c.Code12, c.NationalId);
            }
            if (_grid.Rows.Count > 0)
            {
                _grid.ClearSelection();
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
        }

        private void ConfirmPull()
        {
            if (_grid.CurrentRow == null && _grid.SelectedRows.Count == 0) { return; }
            DataGridViewRow row = _grid.CurrentRow ?? _grid.SelectedRows[0];
            string code = Convert.ToString(row.Cells[2].Value);
            foreach (SavedClient c in _host.Clients)
            {
                if (c.Code12 == code) { c.Pulled = true; }
            }
            // Coding-like confirm so RPA can click OK after F2/F9.
            MessageBox.Show(this, "تم سحب الأكواد", "Coding",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static string Digits(string s)
        {
            if (string.IsNullOrEmpty(s)) { return ""; }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] >= '0' && s[i] <= '9') { sb.Append(s[i]); }
            }
            return sb.ToString();
        }
    }

    public class ConfirmForm : Form
    {
        private readonly MainForm _host;
        private DataGridView _grid;

        public ConfirmForm(MainForm host)
        {
            _host = host;
            Text = "تأكيد بيانات العملاء قبل إرسالها للتكويد";
            Width = 700;
            Height = 400;
            RightToLeft = RightToLeft.No;
            RightToLeftLayout = false;

            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.ReadOnly = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.Columns.Add("Name", "الاسم");
            _grid.Columns.Add("Nid", "قومي");
            _grid.Columns.Add("Code12", "كود");
            _grid.Columns.Add("Pulled", "مسحوب");
            Controls.Add(_grid);

            KeyPreview = true;
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F2)
            {
                if (_grid.Rows.Count > 0 && _grid.SelectedRows.Count == 0)
                {
                    _grid.Rows[0].Selected = true;
                    _grid.CurrentCell = _grid.Rows[0].Cells[0];
                }
                return true;
            }
            if (keyData == Keys.F9)
            {
                MessageBox.Show(this, "تم التأكيد (F2/F9)", "Coding",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void RefreshGrid()
        {
            _grid.Rows.Clear();
            foreach (SavedClient c in _host.Clients)
            {
                _grid.Rows.Add(c.Name, c.NationalId, c.Code12, c.Pulled ? "نعم" : "لا");
            }
            if (_grid.Rows.Count > 0)
            {
                _grid.ClearSelection();
                _grid.Rows[0].Selected = true;
                _grid.CurrentCell = _grid.Rows[0].Cells[0];
            }
        }
    }
}
