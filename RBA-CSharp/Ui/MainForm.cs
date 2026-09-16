// =============================================================================
//  MainForm.cs - compiled RBA dashboard (replaces RBA.ps1 WinForms host).
//  No PowerShell / cmd at runtime.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using RbaCore;

namespace RbaUi
{
    public class MainForm : Form
    {
        private BotConfig _cfg;
        private TabControl _tabs;
        private TextBox _log;
        private ToolStripStatusLabel _status;
        private ProgressBar _progress;
        private DataGridView _trkGrid;
        private DataGridView _runsGrid;
        private TextBox _txtSftp;
        private TextBox _txtSftpHost;
        private TextBox _txtSftpUser;
        private TextBox _txtSftpPassword;
        private TextBox _txtSftpInbox;
        private NumericUpDown _numSftpPort;
        private CheckBox _chkSftpOffline;
        private TextBox _txtSheet;
        private TextBox _txtScans;
        private TextBox _txtStamp;
        private TextBox _txtSign1;
        private TextBox _txtSign2;
        private NumericUpDown _numDocWidth;
        private TextBox _txtExport;
        private TextBox _txtApp;
        private CheckBox _chkWriteBack;
        private CheckBox _chkDailyExport;
        private NumericUpDown _numExportHour;
        private NumericUpDown _numExportMinute;
        private ComboBox _cmbExportAmPm;
        private CheckBox _chkDetailedLog;
        private Label _lblLogPath;
        private Label _lblSftpProbe;
        private volatile bool _stop;
        private volatile bool _running;
        private Button _btnStart;
        private Button _btnStop;
        private System.Windows.Forms.Timer _dailyTimer;
        private DateTime _dailyRetryAfter = DateTime.MinValue;

        public MainForm(string baseDir)
        {
            _cfg = BotConfig.Load(baseDir);
            Text = "RPA V1 — روبوت تكويد العملاء";
            ClientSize = new Size(1040, 720);
            MinimumSize = new Size(860, 600);
            UiTheme.ApplyForm(this);
            DoubleBuffered = true;

            Controls.Add(UiTheme.MakeHeader(
                "RPA V1 · تكويد العملاء",
                "مزامنة SFTP ثم تصنيف وتنفيذ العملاء",
                68));

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            UiTheme.StyleTabs(_tabs);
            Controls.Add(_tabs);
            _tabs.BringToFront();

            TabPage tabRun = new TabPage("تشغيل");
            TabPage tabTrack = new TabPage("متابعة");
            TabPage tabRuns = new TabPage("سجل التشغيل");
            TabPage tabSettings = new TabPage("إعدادات");
            tabRun.BackColor = UiTheme.Bg;
            tabTrack.BackColor = UiTheme.Bg;
            tabRuns.BackColor = UiTheme.Bg;
            tabSettings.BackColor = UiTheme.Bg;
            tabSettings.RightToLeft = RightToLeft.No;
            _tabs.TabPages.Add(tabRun);
            _tabs.TabPages.Add(tabTrack);
            _tabs.TabPages.Add(tabRuns);
            _tabs.TabPages.Add(tabSettings);

            BuildRunTab(tabRun);
            BuildTrackTab(tabTrack);
            BuildRunsTab(tabRuns);
            BuildSettingsTab(tabSettings);

            _tabs.Selecting += delegate(object sender, TabControlCancelEventArgs e)
            {
                if (e.TabPage != tabSettings) { return; }
                if (SettingsLock.IsUnlocked) { return; }
                e.Cancel = !SettingsLock.EnsureUnlocked(this);
            };

            StatusStrip ss = new StatusStrip();
            ss.BackColor = UiTheme.SurfaceAlt;
            ss.SizingGrip = false;
            _status = new ToolStripStatusLabel("جاهز — مترجم بدون PowerShell");
            _status.ForeColor = UiTheme.InkMuted;
            ss.Items.Add(_status);
            Controls.Add(ss);

            LoadSettingsIntoUi();
            RefreshTracking();
            RefreshRuns();
            ActionLog.Configure(_cfg.LogsDir, _cfg.Settings.DetailedActionLog);
            Log("RPA V1 compiled GUI ready — no PowerShell host", "OK");
            Log("logs folder: " + _cfg.LogsDir, "INFO");
            Log("daily export: " + (_cfg.Settings.DailyExportEnabled
                ? DailyExport.Describe(_cfg.Settings) : "off"), "INFO");

            _dailyTimer = new System.Windows.Forms.Timer();
            _dailyTimer.Interval = 30000;
            _dailyTimer.Tick += delegate { TickDailyExport(); };
            _dailyTimer.Start();
        }

        private void BuildRunTab(TabPage tab)
        {
            Panel left = new Panel();
            left.Dock = DockStyle.Right;
            left.Width = 220;
            left.Padding = new Padding(12, 12, 12, 12);
            left.BackColor = UiTheme.Surface;
            tab.Controls.Add(left);

            Label sideTitle = new Label();
            sideTitle.Text = "الإجراءات";
            sideTitle.Font = new Font("Segoe UI Semibold", 10f);
            sideTitle.ForeColor = UiTheme.InkMuted;
            sideTitle.Dock = DockStyle.Top;
            sideTitle.Height = 28;
            left.Controls.Add(sideTitle);

            _btnStart = MakeButton("ابدأ التشغيل", 40, true);
            _btnStart.Dock = DockStyle.Top;
            _btnStart.Click += delegate { ChooseStartMode(); };
            _btnStop = MakeButton("إيقاف الجلسة", 40, false);
            _btnStop.Dock = DockStyle.Top;
            _btnStop.Enabled = true;
            _btnStop.BackColor = Color.FromArgb(176, 42, 42);
            _btnStop.ForeColor = Color.White;
            _btnStop.Click += delegate
            {
                if (!_running)
                {
                    Log("لا توجد جلسة قيد التشغيل", "INFO");
                    return;
                }
                _stop = true;
                _btnStop.Text = "جارٍ الإيقاف…";
                Log("stop requested — will halt after the current client", "WARN");
                if (_status != null) { _status.Text = "إيقاف بعد العميل الحالي…"; }
            };

            Button btnTestAdd = MakeButton("اختبار إضافة", 32, false);
            btnTestAdd.Dock = DockStyle.Top;
            btnTestAdd.Click += delegate { StartSmokeTest(true); };

            Button btnTestPull = MakeButton("اختبار سحب", 32, false);
            btnTestPull.Dock = DockStyle.Top;
            btnTestPull.Click += delegate { StartSmokeTest(false); };

            Button btnSetup = MakeButton("فتح الإعداد…", 36, true);
            btnSetup.Dock = DockStyle.Top;
            btnSetup.Click += delegate
            {
                if (!SettingsLock.EnsureUnlocked(this)) { return; }
                using (SetupForm sf = new SetupForm(_cfg))
                {
                    sf.ShowDialog(this);
                }
                _cfg = BotConfig.Load(_cfg.BaseDir);
                LoadSettingsIntoUi();
            };

            // Dock Top stacks reverse of add order — add bottom-first.
            left.Controls.Add(btnSetup);
            left.Controls.Add(btnTestPull);
            left.Controls.Add(btnTestAdd);
            left.Controls.Add(_btnStop);
            left.Controls.Add(_btnStart);

            Panel main = new Panel();
            main.Dock = DockStyle.Fill;
            main.Padding = new Padding(14);
            main.BackColor = UiTheme.Bg;
            tab.Controls.Add(main);

            _progress = new ProgressBar();
            _progress.Dock = DockStyle.Top;
            _progress.Height = 10;
            _progress.Style = ProgressBarStyle.Continuous;
            main.Controls.Add(_progress);

            Panel logCard = UiTheme.MakeCard();
            logCard.Dock = DockStyle.Fill;
            logCard.Padding = new Padding(8);
            main.Controls.Add(logCard);
            logCard.BringToFront();

            _log = UiTheme.MakeLogBox();
            _log.Dock = DockStyle.Fill;
            logCard.Controls.Add(_log);
        }

        private void BuildTrackTab(TabPage tab)
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 40;
            Button btnRefresh = MakeButton("تحديث", 28, false);
            btnRefresh.SetBounds(8, 6, 100, 28);
            btnRefresh.Click += delegate { RefreshTracking(); };
            Button btnOpen = MakeButton("فتح ملف الفرع", 28, false);
            btnOpen.SetBounds(120, 6, 140, 28);
            btnOpen.Click += delegate
            {
                if (!string.IsNullOrEmpty(_cfg.SheetPath) && File.Exists(_cfg.SheetPath))
                {
                    System.Diagnostics.Process.Start(_cfg.SheetPath);
                }
            };
            bar.Controls.Add(btnRefresh);
            bar.Controls.Add(btnOpen);
            tab.Controls.Add(bar);

            _trkGrid = new DataGridView();
            _trkGrid.Dock = DockStyle.Fill;
            _trkGrid.ReadOnly = true;
            _trkGrid.AllowUserToAddRows = false;
            _trkGrid.RowHeadersVisible = false;
            _trkGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            string[] cols = new string[] { "الرقم القومي", "الاسم", "الحالة", "الكود الموحد", "كود 12", "سبب الرفض", "ملاحظات", "آخر تحديث" };
            for (int i = 0; i < cols.Length; i++) { _trkGrid.Columns.Add(cols[i], cols[i]); }
            UiTheme.StyleGrid(_trkGrid);
            tab.Controls.Add(_trkGrid);
            _trkGrid.BringToFront();
        }

        private void BuildRunsTab(TabPage tab)
        {
            Panel bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 40;
            Button btnRefresh = MakeButton("تحديث", 28, false);
            btnRefresh.SetBounds(8, 6, 100, 28);
            btnRefresh.Click += delegate { RefreshRuns(); };
            bar.Controls.Add(btnRefresh);
            tab.Controls.Add(bar);

            _runsGrid = new DataGridView();
            _runsGrid.Dock = DockStyle.Fill;
            _runsGrid.ReadOnly = true;
            _runsGrid.AllowUserToAddRows = false;
            _runsGrid.RowHeadersVisible = false;
            _runsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            string[] cols = new string[] { "RunId", "StartedAt", "Status", "FileName", "Validated", "Failed", "Coded", "Rejected", "Message" };
            for (int i = 0; i < cols.Length; i++) { _runsGrid.Columns.Add(cols[i], cols[i]); }
            UiTheme.StyleGrid(_runsGrid);
            tab.Controls.Add(_runsGrid);
            _runsGrid.BringToFront();
        }

        private void BuildSettingsTab(TabPage tab)
        {
            // Absolute SetBounds + form RightToLeft=Yes mirrors X and overlaps rows.
            // Keep settings canvas LTR for stable columns; Arabic text still renders fine.
            Panel canvas = new Panel();
            canvas.Dock = DockStyle.Fill;
            canvas.AutoScroll = true;
            canvas.BackColor = UiTheme.Bg;
            canvas.RightToLeft = RightToLeft.No;
            canvas.Padding = new Padding(16);
            tab.Controls.Add(canvas);

            const int labelW = 170;
            const int gap = 10;
            const int left = 16;
            const int fieldX = left + labelW + gap;
            const int browseW = 40;
            const int rowH = 34;
            int y = 12;
            int contentW = 780;

            Label sftpTitle = new Label();
            sftpTitle.Text = "إعدادات SFTP";
            sftpTitle.Font = new Font("Segoe UI Semibold", 11f);
            sftpTitle.ForeColor = UiTheme.Ink;
            sftpTitle.AutoSize = false;
            sftpTitle.SetBounds(left, y, contentW, 24);
            canvas.Controls.Add(sftpTitle);
            y += 30;

            _txtSftp = AddSettingsPath(canvas, "جذر SFTP المحلي", ref y, false, left, labelW, fieldX, browseW, contentW, rowH);
            _txtSftpHost = AddSettingsSimple(canvas, "Host (IP / local)", ref y, left, labelW, fieldX, contentW, rowH);
            _numSftpPort = new NumericUpDown();
            _numSftpPort.Minimum = 1;
            _numSftpPort.Maximum = 65535;
            _numSftpPort.Value = 22;
            AddSettingsLabeledControl(canvas, "Port", _numSftpPort, ref y, 90, left, labelW, fieldX, rowH);
            _txtSftpUser = AddSettingsSimple(canvas, "User", ref y, left, labelW, fieldX, contentW, rowH);
            _txtSftpPassword = AddSettingsPassword(canvas, "Password", ref y, left, labelW, fieldX, contentW, rowH);
            _txtSftpInbox = AddSettingsSimple(canvas, "مجلد Inbox (أو . للمنزل)", ref y, left, labelW, fieldX, contentW, rowH);

            _chkSftpOffline = new CheckBox();
            _chkSftpOffline.Text = "وضع Offline — العمل من الملفات المحلية بدون SFTP";
            _chkSftpOffline.AutoSize = false;
            _chkSftpOffline.SetBounds(fieldX, y, Math.Max(360, contentW - labelW), 24);
            _chkSftpOffline.CheckedChanged += delegate { ApplySftpSourceUi(); };
            canvas.Controls.Add(_chkSftpOffline);
            y += 30;

            Button btnSftpProbe = MakeButton("فحص المصدر", 30, true);
            btnSftpProbe.SetBounds(fieldX, y, 180, 30);
            btnSftpProbe.Click += delegate { ProbeSftpConnection(); };
            canvas.Controls.Add(btnSftpProbe);

            _lblSftpProbe = new Label();
            _lblSftpProbe.AutoSize = false;
            _lblSftpProbe.ForeColor = UiTheme.InkMuted;
            _lblSftpProbe.Text = "اضغط الفحص للتأكد من الاتصال وعرض الملفات في الـ inbox";
            _lblSftpProbe.SetBounds(fieldX + 190, y + 4, Math.Max(200, contentW - labelW - 200), 24);
            canvas.Controls.Add(_lblSftpProbe);
            y += 40;

            y += 10;
            Label workTitle = new Label();
            workTitle.Text = "مسارات العمل (تُحدَّث تلقائياً بعد المزامنة)";
            workTitle.Font = new Font("Segoe UI Semibold", 10f);
            workTitle.ForeColor = UiTheme.Ink;
            workTitle.AutoSize = false;
            workTitle.SetBounds(left, y, contentW, 22);
            canvas.Controls.Add(workTitle);
            y += 28;

            _txtSheet = AddSettingsPath(canvas, "ملف العمل", ref y, true, left, labelW, fieldX, browseW, contentW, rowH);
            _txtScans = AddSettingsPath(canvas, "مجلد المستندات", ref y, false, left, labelW, fieldX, browseW, contentW, rowH);
            _txtStamp = AddSettingsPath(canvas, "صورة الختم", ref y, true, left, labelW, fieldX, browseW, contentW, rowH, true);
            _txtSign1 = AddSettingsPath(canvas, "التوقيع الأول", ref y, true, left, labelW, fieldX, browseW, contentW, rowH, true);
            _txtSign2 = AddSettingsPath(canvas, "التوقيع الثاني", ref y, true, left, labelW, fieldX, browseW, contentW, rowH, true);
            Label lblW = new Label();
            lblW.Text = "عرض الصورة بعد السحب";
            lblW.AutoSize = false;
            lblW.TextAlign = ContentAlignment.MiddleLeft;
            lblW.SetBounds(left, y, labelW, 26);
            canvas.Controls.Add(lblW);
            _numDocWidth = new NumericUpDown();
            _numDocWidth.Minimum = 400;
            _numDocWidth.Maximum = 2400;
            _numDocWidth.Increment = 50;
            _numDocWidth.SetBounds(fieldX, y, 90, 26);
            canvas.Controls.Add(_numDocWidth);
            y += rowH;
            _txtExport = AddSettingsPath(canvas, "مجلد التصدير", ref y, false, left, labelW, fieldX, browseW, contentW, rowH);
            _txtApp = AddSettingsPath(canvas, "برنامج التكويد", ref y, true, left, labelW, fieldX, browseW, contentW, rowH);

            y += 6;
            _chkWriteBack = new CheckBox();
            _chkWriteBack.Text = "كتابة النتائج لملف العملاء";
            _chkWriteBack.AutoSize = false;
            _chkWriteBack.SetBounds(left, y, contentW, 24);
            canvas.Controls.Add(_chkWriteBack);
            y += 30;

            _chkDailyExport = new CheckBox();
            _chkDailyExport.Text = "تصدير يومي تلقائي عند الوقت المحدد";
            _chkDailyExport.AutoSize = false;
            _chkDailyExport.SetBounds(left, y, contentW, 24);
            canvas.Controls.Add(_chkDailyExport);
            y += 32;

            Label lblHour = new Label();
            lblHour.Text = "ساعة";
            lblHour.TextAlign = ContentAlignment.MiddleLeft;
            lblHour.SetBounds(left, y, 44, 26);
            canvas.Controls.Add(lblHour);
            _numExportHour = new NumericUpDown();
            _numExportHour.Minimum = 1;
            _numExportHour.Maximum = 12;
            _numExportHour.SetBounds(left + 48, y, 56, 26);
            canvas.Controls.Add(_numExportHour);

            Label lblMin = new Label();
            lblMin.Text = "دقيقة";
            lblMin.TextAlign = ContentAlignment.MiddleLeft;
            lblMin.SetBounds(left + 118, y, 44, 26);
            canvas.Controls.Add(lblMin);
            _numExportMinute = new NumericUpDown();
            _numExportMinute.Minimum = 0;
            _numExportMinute.Maximum = 59;
            _numExportMinute.SetBounds(left + 166, y, 56, 26);
            canvas.Controls.Add(_numExportMinute);

            _cmbExportAmPm = new ComboBox();
            _cmbExportAmPm.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbExportAmPm.Items.Add("AM");
            _cmbExportAmPm.Items.Add("PM");
            _cmbExportAmPm.SetBounds(left + 236, y, 64, 26);
            canvas.Controls.Add(_cmbExportAmPm);
            y += 36;

            _chkDetailedLog = new CheckBox();
            _chkDetailedLog.Text = "تسجيل تفصيلي لكل إجراء (للتشخيص)";
            _chkDetailedLog.AutoSize = false;
            _chkDetailedLog.Checked = true;
            _chkDetailedLog.SetBounds(left, y, contentW, 24);
            canvas.Controls.Add(_chkDetailedLog);
            y += 32;

            _lblLogPath = new Label();
            _lblLogPath.AutoSize = false;
            _lblLogPath.SetBounds(left, y, contentW, 22);
            _lblLogPath.ForeColor = UiTheme.InkMuted;
            canvas.Controls.Add(_lblLogPath);
            y += 32;

            Button btnSave = MakeButton("حفظ الإعدادات", 32, true);
            btnSave.SetBounds(left, y, 160, 32);
            btnSave.Click += delegate
            {
                PersistSettingsFromUi();
                Log("settings saved", "OK");
                MessageBox.Show(this, "تم حفظ الإعدادات.", "إعدادات",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            canvas.Controls.Add(btnSave);

            Button btnLogs = MakeButton("فتح السجلات", 32, false);
            btnLogs.SetBounds(left + 170, y, 140, 32);
            btnLogs.Click += delegate { OpenLogsFolder(); };
            canvas.Controls.Add(btnLogs);

            Button btnLatest = MakeButton("آخر سجل", 32, false);
            btnLatest.SetBounds(left + 320, y, 120, 32);
            btnLatest.Click += delegate { OpenLatestSessionLog(); };
            canvas.Controls.Add(btnLatest);

            y += 44;
            Button btnExportLocked = MakeButton("تصدير نسخة مقفلة", 32, false);
            btnExportLocked.SetBounds(left, y, 180, 32);
            btnExportLocked.Click += delegate { ExportLockedSettings(); };
            canvas.Controls.Add(btnExportLocked);

            Button btnImportLocked = MakeButton("استيراد نسخة مقفلة", 32, false);
            btnImportLocked.SetBounds(left + 190, y, 180, 32);
            btnImportLocked.Click += delegate { ImportLockedSettings(); };
            canvas.Controls.Add(btnImportLocked);

            y += 48;
            canvas.AutoScrollMinSize = new Size(contentW + 40, y);
        }

        private TextBox AddSettingsSimple(
            Control parent, string label, ref int y,
            int left, int labelW, int fieldX, int contentW, int rowH)
        {
            Label l = new Label();
            l.Text = label;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.SetBounds(left, y, labelW, 26);
            TextBox t = new TextBox();
            int fieldW = Math.Max(180, contentW - labelW - 10);
            t.SetBounds(fieldX, y, fieldW, 26);
            t.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            parent.Controls.Add(l);
            parent.Controls.Add(t);
            y += rowH;
            return t;
        }

        private TextBox AddSettingsPassword(
            Control parent, string label, ref int y,
            int left, int labelW, int fieldX, int contentW, int rowH)
        {
            TextBox t = AddSettingsSimple(parent, label, ref y, left, labelW, fieldX, contentW, rowH);
            t.UseSystemPasswordChar = true;
            return t;
        }

        private void AddSettingsLabeledControl(
            Control parent, string label, Control c, ref int y, int w,
            int left, int labelW, int fieldX, int rowH)
        {
            Label l = new Label();
            l.Text = label;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.SetBounds(left, y, labelW, 26);
            c.SetBounds(fieldX, y, w, 26);
            parent.Controls.Add(l);
            parent.Controls.Add(c);
            y += rowH;
        }

        private TextBox AddSettingsPath(
            Control parent, string label, ref int y, bool file,
            int left, int labelW, int fieldX, int browseW, int contentW, int rowH)
        {
            return AddSettingsPath(parent, label, ref y, file, left, labelW, fieldX, browseW, contentW, rowH, false);
        }

        private TextBox AddSettingsPath(
            Control parent, string label, ref int y, bool file,
            int left, int labelW, int fieldX, int browseW, int contentW, int rowH, bool imageFile)
        {
            Label l = new Label();
            l.Text = label;
            l.AutoSize = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.SetBounds(left, y, labelW, 26);

            int browseX = left + contentW - browseW;
            int fieldW = Math.Max(160, browseX - fieldX - 8);
            TextBox t = new TextBox();
            t.SetBounds(fieldX, y, fieldW, 26);

            Button b = new Button();
            b.Text = "...";
            b.SetBounds(browseX, y, browseW, 26);
            TextBox box = t;
            bool isFile = file;
            bool isImage = imageFile;
            b.Click += delegate
            {
                if (isFile)
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = isImage
                        ? "Images|*.png;*.jpg;*.jpeg;*.bmp|All|*.*"
                        : "All|*.*|Sheets|*.csv;*.xlsx|Exe|*.exe";
                    if (d.ShowDialog(this) == DialogResult.OK) { box.Text = d.FileName; }
                }
                else
                {
                    FolderBrowserDialog d = new FolderBrowserDialog();
                    if (d.ShowDialog(this) == DialogResult.OK) { box.Text = d.SelectedPath; }
                }
            };
            parent.Controls.Add(l);
            parent.Controls.Add(t);
            parent.Controls.Add(b);
            y += rowH;
            return t;
        }

        private void ApplySftpSourceUi()
        {
            bool remote = _chkSftpOffline == null || !_chkSftpOffline.Checked;
            if (_txtSftpHost != null) { _txtSftpHost.Enabled = remote; }
            if (_numSftpPort != null) { _numSftpPort.Enabled = remote; }
            if (_txtSftpUser != null) { _txtSftpUser.Enabled = remote; }
            // Password stays editable so فحص SFTP can use it even if Offline was left on.
        }

        private void ProbeSftpConnection()
        {
            if (!SettingsLock.EnsureUnlocked(this)) { return; }
            PersistSettingsFromUi();
            if (_lblSftpProbe != null)
            {
                _lblSftpProbe.ForeColor = UiTheme.InkMuted;
                _lblSftpProbe.Text = "جاري فحص الاتصال…";
            }
            Log("SFTP probe started…", "INFO");
            Cursor = Cursors.WaitCursor;
            try
            {
                SftpProbeResult r = SftpInbox.TestConnection(_cfg, Log);
                if (_lblSftpProbe != null)
                {
                    _lblSftpProbe.ForeColor = r.Ok ? UiTheme.AccentDark : Color.Firebrick;
                    _lblSftpProbe.Text = r.Ok
                        ? ("متصل — " + r.FileCount + " عنصر (" + r.Mode + ")")
                        : ("فشل — " + (r.Note ?? ""));
                }
                if (r.Ok)
                {
                    MessageBox.Show(this,
                        "الاتصال ناجح.\n" + (r.Note ?? "") +
                        "\n\nالتفاصيل في سجل التشغيل (تبويب التشغيل).",
                        "فحص SFTP", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show(this,
                        "تعذر الاتصال:\n" + (r.Note ?? ""),
                        "فحص SFTP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private Button MakeButton(string text, int h, bool primary)
        {
            Button b = UiTheme.MakeButton(text, primary);
            b.Height = h;
            b.Width = 196;
            return b;
        }

        private void LoadSettingsIntoUi()
        {
            if (_txtSftp != null) { _txtSftp.Text = _cfg.SftpLocalRoot ?? ""; }
            if (_txtSftpHost != null) { _txtSftpHost.Text = string.IsNullOrEmpty(_cfg.SftpHost) ? BotConfig.DefaultSftpHost : _cfg.SftpHost; }
            if (_numSftpPort != null)
            {
                int p = _cfg.SftpPort > 0 ? _cfg.SftpPort : 22;
                _numSftpPort.Value = Math.Min(_numSftpPort.Maximum, Math.Max(_numSftpPort.Minimum, p));
            }
            if (_txtSftpUser != null)
            {
                _txtSftpUser.Text = string.IsNullOrEmpty(_cfg.SftpUser) ? BotConfig.DefaultSftpUser : _cfg.SftpUser;
            }
            if (_txtSftpPassword != null) { _txtSftpPassword.Text = _cfg.SftpPassword ?? ""; }
            if (_txtSftpInbox != null) { _txtSftpInbox.Text = string.IsNullOrEmpty(_cfg.SftpRemoteInbox) ? "kyc-imports" : _cfg.SftpRemoteInbox; }
            if (_chkSftpOffline != null) { _chkSftpOffline.Checked = _cfg.SftpOfflineMode; }
            ApplySftpSourceUi();
            _txtSheet.Text = _cfg.SheetPath;
            _txtScans.Text = _cfg.ScansRoot;
            if (_txtStamp != null) { _txtStamp.Text = _cfg.StampImagePath ?? ""; }
            if (_txtSign1 != null) { _txtSign1.Text = _cfg.SignatureImagePath ?? ""; }
            if (_txtSign2 != null) { _txtSign2.Text = _cfg.Signature2ImagePath ?? ""; }
            if (_numDocWidth != null)
            {
                int w = _cfg.DocImageMaxWidth >= 400 ? _cfg.DocImageMaxWidth : 1100;
                _numDocWidth.Value = Math.Min(_numDocWidth.Maximum, Math.Max(_numDocWidth.Minimum, w));
            }
            _txtExport.Text = _cfg.ExportFolder;
            _txtApp.Text = _cfg.App.ExePath;
            _chkWriteBack.Checked = _cfg.Settings.DoWriteBack;
            if (_chkDailyExport != null) { _chkDailyExport.Checked = _cfg.Settings.DailyExportEnabled; }
            if (_numExportHour != null)
            {
                int h = _cfg.Settings.DailyExportHour;
                _numExportHour.Value = (h < 1 || h > 12) ? 5 : h;
            }
            if (_numExportMinute != null)
            {
                int m = _cfg.Settings.DailyExportMinute;
                _numExportMinute.Value = (m < 0 || m > 59) ? 0 : m;
            }
            if (_cmbExportAmPm != null) { _cmbExportAmPm.SelectedIndex = _cfg.Settings.DailyExportPm ? 1 : 0; }
            if (_chkDetailedLog != null) { _chkDetailedLog.Checked = _cfg.Settings.DetailedActionLog; }
            if (_lblLogPath != null) { _lblLogPath.Text = "مجلد السجلات: " + (_cfg.LogsDir ?? ""); }
            ActionLog.Configure(_cfg.LogsDir, _cfg.Settings.DetailedActionLog);
        }

        private void ExportLockedSettings()
        {
            PersistSettingsFromUi();
            List<FieldSelector> selectors = new List<FieldSelector>();
            try
            {
                if (File.Exists(_cfg.SelectorsPath))
                {
                    selectors = FieldDiscovery.ImportSelectors(_cfg.SelectorsPath);
                }
            }
            catch { }
            try
            {
                SettingsBackupUi.SaveLocked(
                    this,
                    _cfg,
                    selectors,
                    _cfg.App != null ? _cfg.App.WindowTitlePattern : "",
                    _cfg.App != null ? _cfg.App.EntryWindowTitle : "");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "فشل التصدير", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ImportLockedSettings()
        {
            MappingBundleResult b = SettingsBackupUi.PickAndRead(this);
            if (b == null || string.Equals(b.Note, "cancelled", StringComparison.Ordinal)) { return; }
            if (!b.Ok)
            {
                MessageBox.Show(this, b.Note, "فشل الاستيراد", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string msg =
                "سيُستبدل الإعداد الحالي بالنسخة المقفلة.\n\n" +
                "الحقول: " + (b.Selectors != null ? b.Selectors.Count : 0) + "\n" +
                "صُدّر: " + (string.IsNullOrEmpty(b.ExportedAt) ? "-" : b.ExportedAt) + "\n\n" +
                "هل تريد المتابعة؟";
            if (MessageBox.Show(this, msg, "استيراد نسخة مقفلة", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }
            MappingBundle.ApplyToConfig(_cfg, b);
            _cfg.SaveBotConfig();
            _cfg.SaveSettings();
            if (b.Selectors != null && b.Selectors.Count > 0)
            {
                FieldDiscovery.SaveSelectors(_cfg.SelectorsPath, FieldCatalog.OrderAndClean(b.Selectors));
            }
            LoadSettingsIntoUi();
            Log("locked settings imported — fields=" + (b.Selectors != null ? b.Selectors.Count : 0), "OK");
            MessageBox.Show(this,
                "تم استيراد النسخة المقفلة.",
                "استيراد",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void PersistSettingsFromUi()
        {
            if (_txtSftp != null) { _cfg.SftpLocalRoot = _txtSftp.Text.Trim(); }
            bool offline = _chkSftpOffline != null && _chkSftpOffline.Checked;
            _cfg.SftpOfflineMode = offline;
            if (_txtSftpHost != null) { _cfg.SftpHost = _txtSftpHost.Text.Trim(); }
            if (_numSftpPort != null) { _cfg.SftpPort = (int)_numSftpPort.Value; }
            if (_txtSftpUser != null) { _cfg.SftpUser = _txtSftpUser.Text.Trim(); }
            if (_txtSftpPassword != null) { _cfg.SftpPassword = _txtSftpPassword.Text; }
            if (_txtSftpInbox != null) { _cfg.SftpRemoteInbox = _txtSftpInbox.Text.Trim(); }
            _cfg.SheetPath = _txtSheet.Text.Trim();
            _cfg.ScansRoot = _txtScans.Text.Trim();
            if (_txtStamp != null) { _cfg.StampImagePath = _txtStamp.Text.Trim(); }
            if (_txtSign1 != null) { _cfg.SignatureImagePath = _txtSign1.Text.Trim(); }
            if (_txtSign2 != null) { _cfg.Signature2ImagePath = _txtSign2.Text.Trim(); }
            if (_numDocWidth != null) { _cfg.DocImageMaxWidth = (int)_numDocWidth.Value; }
            _cfg.ExportFolder = _txtExport.Text.Trim();
            _cfg.App.ExePath = _txtApp.Text.Trim();
            _cfg.App.LookupBeforeEntry = false;
            _cfg.Settings.DoValidate = true;
            _cfg.Settings.DoReconcile = false;
            _cfg.Settings.DoWriteBack = _chkWriteBack.Checked;
            if (_chkDailyExport != null) { _cfg.Settings.DailyExportEnabled = _chkDailyExport.Checked; }
            if (_numExportHour != null) { _cfg.Settings.DailyExportHour = (int)_numExportHour.Value; }
            if (_numExportMinute != null) { _cfg.Settings.DailyExportMinute = (int)_numExportMinute.Value; }
            if (_cmbExportAmPm != null) { _cfg.Settings.DailyExportPm = _cmbExportAmPm.SelectedIndex == 1; }
            _cfg.Settings.PostSaveConfirmExport = false;
            _cfg.Settings.ExportDateRangeYearToDate = false;
            if (_chkDetailedLog != null) { _cfg.Settings.DetailedActionLog = _chkDetailedLog.Checked; }
            _cfg.SaveBotConfig();
            _cfg.SaveSettings();
            ActionLog.Configure(_cfg.LogsDir, _cfg.Settings.DetailedActionLog);
            if (_lblLogPath != null) { _lblLogPath.Text = "مجلد السجلات: " + (_cfg.LogsDir ?? ""); }
        }

        private void Log(string msg, string level)
        {
            if (string.IsNullOrEmpty(level)) { level = "INFO"; }
            string line = DateTime.Now.ToString("HH:mm:ss") + " [" + level + "] " + msg + "\r\n";
            if (InvokeRequired)
            {
                BeginInvoke(new Action(delegate { _log.AppendText(line); _status.Text = msg; }));
            }
            else
            {
                _log.AppendText(line);
                _status.Text = msg;
            }
            try { ActionLog.Write(level, msg); } catch { }
        }

        private void OpenLogsFolder()
        {
            try
            {
                if (!Directory.Exists(_cfg.LogsDir)) { Directory.CreateDirectory(_cfg.LogsDir); }
                System.Diagnostics.Process.Start("explorer.exe", _cfg.LogsDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "سجلات", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void OpenLatestSessionLog()
        {
            string path = ActionLog.FindLatestSession();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                MessageBox.Show(this,
                    "لا يوجد سجل جلسة بعد.\nشغّل اختبار إدخال/حفظ مع تفعيل التسجيل التفصيلي.",
                    "سجلات", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try { System.Diagnostics.Process.Start(path); }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "سجلات", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void CopyLatestLogPath()
        {
            string path = ActionLog.FindLatestSession();
            if (string.IsNullOrEmpty(path))
            {
                path = _cfg.LogsDir;
            }
            try
            {
                Clipboard.SetText(path ?? "");
                Log("copied log path: " + path, "OK");
                MessageBox.Show(this,
                    "تم نسخ المسار:\n" + path +
                    "\n\nالصق المسار هنا في المحادثة أو أرفق ملف session_*.log",
                    "سجلات", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "سجلات", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void SetProgress(int done, int total)
        {
            Action a = delegate
            {
                _progress.Maximum = Math.Max(1, total);
                _progress.Value = Math.Min(_progress.Maximum, Math.Max(0, done));
            };
            if (InvokeRequired) { BeginInvoke(a); } else { a(); }
        }

        private void RefreshTracking()
        {
            _trkGrid.Rows.Clear();
            List<OrderedDictionary> rows = RunTracker.LoadTracking(_cfg.TrackingPath);
            for (int i = 0; i < rows.Count; i++)
            {
                OrderedDictionary r = rows[i];
                _trkGrid.Rows.Add(
                    CsvUtil.Get(r, "NationalId", ""),
                    CsvUtil.Get(r, "Name", ""),
                    CsvUtil.Get(r, "Status", ""),
                    CsvUtil.Get(r, "NIN", ""),
                    CsvUtil.Get(r, "CODE12", ""),
                    CsvUtil.Get(r, "RejectReason", ""),
                    CsvUtil.Get(r, "Notes", ""),
                    CsvUtil.Get(r, "LastUpdated", ""));
            }
        }

        private void RefreshRuns()
        {
            _runsGrid.Rows.Clear();
            List<OrderedDictionary> rows = RunTracker.GetRuns(_cfg.StateDir, 200);
            for (int i = 0; i < rows.Count; i++)
            {
                OrderedDictionary r = rows[i];
                _runsGrid.Rows.Add(
                    CsvUtil.Get(r, "RunId", ""),
                    CsvUtil.Get(r, "StartedAt", ""),
                    CsvUtil.Get(r, "Status", ""),
                    CsvUtil.Get(r, "FileName", ""),
                    CsvUtil.Get(r, "Validated", ""),
                    CsvUtil.Get(r, "Failed", ""),
                    CsvUtil.Get(r, "Coded", ""),
                    CsvUtil.Get(r, "Rejected", ""),
                    CsvUtil.Get(r, "Message", ""));
            }
        }

        private void StartSmokeTest(bool testAdd)
        {
            if (_running) { return; }
            if (!SettingsLock.EnsureUnlocked(this)) { return; }
            PersistSettingsFromUi();
            _stop = false;
            _running = true;
            SetSessionBusy(true);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ActionLog.BeginSession(testAdd ? "test-add" : "test-pull");
                    OperatorFlowResult r = testAdd
                        ? OperatorFlow.RunTestAdd(_cfg, Log, delegate { return _stop; })
                        : OperatorFlow.RunTestPull(_cfg, Log, delegate { return _stop; });
                    _cfg = BotConfig.Load(_cfg.BaseDir);
                    Log(r.Note ?? "", r.Ok ? "OK" : "ERROR");
                    BeginInvoke(new Action(delegate
                    {
                        LoadSettingsIntoUi();
                        RefreshTracking();
                        MessageBox.Show(this,
                            (r.Ok ? "تم.\n" : "فشل/توقف.\n") + (r.Note ?? "") +
                            "\n\nالملف: " + (r.SheetPath ?? ""),
                            testAdd ? "اختبار إضافة" : "اختبار سحب",
                            MessageBoxButtons.OK,
                            r.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    }));
                }
                catch (Exception ex)
                {
                    Log(ex.Message, "ERROR");
                }
                finally
                {
                    try { ActionLog.EndSession(testAdd ? "test-add done" : "test-pull done"); } catch { }
                    BeginInvoke(new Action(delegate
                    {
                        _running = false;
                        SetSessionBusy(false);
                        RefreshTracking();
                        RefreshRuns();
                    }));
                }
            });
        }

        private void SetSessionBusy(bool busy)
        {
            if (_btnStart != null) { _btnStart.Enabled = !busy; }
            if (_btnStop != null)
            {
                _btnStop.Enabled = true;
                if (!busy) { _btnStop.Text = "إيقاف الجلسة"; }
            }
        }

        private void ChooseStartMode()
        {
            if (_running) { return; }
            using (StartModeForm dlg = new StartModeForm())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) { return; }
                if (dlg.Chosen == StartMode.Inquire)
                {
                    StartOperatorFlow(true);
                    return;
                }
                if (dlg.Chosen == StartMode.Code)
                {
                    StartOperatorFlow(false);
                }
            }
        }

        private void StartOperatorFlow(bool classifyOnly)
        {
            if (_running) { return; }
            PersistSettingsFromUi();
            if (!string.IsNullOrEmpty(_cfg.ScansRoot) && SftpInbox.IsInternalCachePath(_cfg.ScansRoot))
            {
                _cfg.ScansRoot = SftpInbox.ResolveOperatorDocsRoot(_cfg);
                if (_txtScans != null) { _txtScans.Text = _cfg.ScansRoot; }
            }
            _stop = false;
            _running = true;
            SetSessionBusy(true);

            OperatorFlowOptions opt = new OperatorFlowOptions();
            opt.Config = _cfg;
            opt.ClassifyOnly = classifyOnly;
            opt.SkipClassify = !classifyOnly;
            opt.IsCancelled = delegate { return _stop; };
            opt.Log = Log;
            opt.ShowClassify = delegate(string sheetPath, List<OrderedDictionary> rows)
            {
                bool finished = false;
                if (InvokeRequired)
                {
                    Invoke(new Action(delegate
                    {
                        using (ClassifyForm cf = new ClassifyForm(sheetPath, rows, Log))
                        {
                            cf.ShowDialog(this);
                            finished = cf.Completed || cf.ClassifiedCount > 0;
                        }
                    }));
                }
                else
                {
                    using (ClassifyForm cf = new ClassifyForm(sheetPath, rows, Log))
                    {
                        cf.ShowDialog(this);
                        finished = cf.Completed || cf.ClassifiedCount > 0;
                    }
                }
                return finished;
            };

            // Classify must run on UI; sync can be background before classify via same thread pool
            // with marshalled dialog — OperatorFlow calls ShowClassify synchronously.
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ActionLog.BeginSession(classifyOnly ? "inquire" : "coding");
                    OperatorFlowResult r = OperatorFlow.Run(opt);
                    _cfg = BotConfig.Load(_cfg.BaseDir);
                    Log(r.Note ?? (r.Ok ? "operator flow done" : "operator flow failed"),
                        r.Ok ? "OK" : "ERROR");
                    BeginInvoke(new Action(delegate
                    {
                        LoadSettingsIntoUi();
                        RefreshTracking();
                        MessageBox.Show(this,
                            (r.Ok ? "تم.\n" : "توقف.\n") + (r.Note ?? "") +
                            "\n\nجديد=" + r.NewCount + "  سحب=" + r.PullCount +
                            "  موقوف=" + r.SuspendedCount + "  معلّق=" + r.PendingCount +
                            "\nالملف: " + (r.SheetPath ?? ""),
                            classifyOnly ? "استعلام" : "تكويد", MessageBoxButtons.OK,
                            r.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    }));
                }
                catch (Exception ex)
                {
                    Log(ex.Message, "ERROR");
                }
                finally
                {
                    try { ActionLog.EndSession("operator done"); } catch { }
                    BeginInvoke(new Action(delegate
                    {
                        _running = false;
                        SetSessionBusy(false);
                        RefreshTracking();
                        RefreshRuns();
                    }));
                }
            });
        }

        private void StartCycle()
        {
            ChooseStartMode();
        }

        private void RunCycle()
        {
            string sheet = _cfg.SheetPath;
            if (string.IsNullOrEmpty(sheet))
            {
                Log("sheet path is empty — open Setup", "ERROR");
                return;
            }

            ActionLog.Configure(_cfg.LogsDir, _cfg.Settings.DetailedActionLog);
            string session = ActionLog.BeginSession("cycle");
            if (!string.IsNullOrEmpty(session)) { Log("session log: " + session, "INFO"); }

            BotRun run = RunTracker.NewRun(_cfg.StateDir, "Manual", sheet, "Cycle");
            RunTracker.SaveRun(_cfg.StateDir, run, 2000);

            if (_cfg.Settings.DoValidate && !_stop)
            {
                Log("validate...", "INFO");
                ValidateResult vr = Validator.ValidateFile(sheet, _cfg.ScansRoot, _cfg.TrackingPath);
                for (int i = 0; i < vr.LogLines.Count; i++) { Log(vr.LogLines[i], "INFO"); }
                run.TotalRows = vr.Rows.Count;
                run.Validated = vr.Ok;
                run.Failed = vr.Bad;
                run.Processed = vr.Rows.Count;
                SetProgress(vr.Rows.Count, Math.Max(1, vr.Rows.Count));
            }

            if (_cfg.Settings.DoReconcile && !_stop)
            {
                Log("reconcile...", "INFO");
                ReconcileResult rr = Reconcile.Run(_cfg.ExportFolder, _cfg.TrackingPath, 1256);
                for (int i = 0; i < rr.LogLines.Count; i++) { Log(rr.LogLines[i], "INFO"); }
                run.Coded = rr.Coded;
                run.Rejected = rr.Rejected;
            }

            if (_cfg.Settings.DoWriteBack && !_stop)
            {
                Log("write-back to branch sheet...", "INFO");
                try
                {
                    List<OrderedDictionary> tracking = RunTracker.LoadTracking(_cfg.TrackingPath);
                    SheetUpdateResult ur = SheetSync.UpdateClientSheet(sheet, tracking);
                    Log("sheet updated: " + ur.Path, "OK");
                }
                catch (Exception ex)
                {
                    Log("write-back failed: " + ex.Message, "ERROR");
                    run.Errors++;
                }
            }

            RunTracker.CompleteRun(run, _stop ? "STOPPED" : "COMPLETED", "");
            RunTracker.SaveRun(_cfg.StateDir, run, 2000);
            Log("cycle finished", "OK");
            ActionLog.EndSession(_stop ? "STOPPED" : "COMPLETED");
        }

        private void RunEntryTest(bool oneClient, bool doSave)
        {
            if (_running) { Log("cycle running - wait", "WARN"); return; }
            PersistSettingsFromUi();

            if (doSave)
            {
                DialogResult ans = MessageBox.Show(
                    this,
                    "تحذير: اختبار الحفظ سيضغط «حفظ» ويعالج 3 عملاء حقيقيين من\n" +
                    "Sample\\Test-Save-Client.csv (رقم قومي مختلف + مجلد مستندات لكل عميل).\n\nهل تريد المتابعة؟",
                    "تأكيد الحفظ - Test-Save",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (ans != DialogResult.Yes) { Log("save cancelled", "WARN"); return; }
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try { DoEntryTest(oneClient, doSave); }
                catch (Exception ex) { Log("entry test error: " + ex.Message, "ERROR"); }
                finally
                {
                    BeginInvoke(new Action(delegate { RefreshTracking(); RefreshRuns(); }));
                }
            });
        }

        // Attach-only run against Sample\Test-Save-Client.csv: the entry form must already
        // be open, nothing is saved, and the full picker diagnostic lands in the log.
        private void RunDocsTest()
        {
            if (_running) { Log("cycle running - wait", "WARN"); return; }
            PersistSettingsFromUi();

            DialogResult ans = MessageBox.Show(
                this,
                "افتح «بيانات العملاء (إضافة)» في Coding أولاً، ثم اضغط موافق.\n\n" +
                "سيُجرَّب إرفاق صور العميل التجريبي فقط — بدون كتابة حقول وبدون حفظ.",
                "اختبار المرفقات فقط",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);
            if (ans != DialogResult.OK) { return; }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try { DoDocsTest(); }
                catch (Exception ex) { Log("docs test error: " + ex.Message, "ERROR"); }
            });
        }

        // Isolated confirm (F2/F9) or period export — no client fill / save / attachments.
        private void RunPostSavePiece(string kind)
        {
            if (_running) { Log("cycle running - wait", "WARN"); return; }
            PersistSettingsFromUi();

            bool confirm = string.Equals(kind, "confirm", StringComparison.OrdinalIgnoreCase);
            string title = confirm ? "اختبار التأكيد فقط" : "اختبار التصدير فقط";
            string body = confirm
                ? "سيفتح شاشة «تأكيد بيانات العملاء» (أو يستخدمها إن كانت مفتوحة)،\n" +
                  "ينقر الجدول ثم F2 ثم F9.\n\nبدون إدخال عميل وبدون تصدير.\nتُترك الشاشة مفتوحة للمراجعة."
                : "سيفتح شاشة الفترة، يملأ التواريخ ثم المسار، يضغط زر Export مرة واحدة، وينتظر Clients*.xml\n" +
                  "ثم يكتب NIN / كود 12 في ملف العملاء الرئيسي.\n\n" +
                  "بدون تأكيد العملاء (F2/F9).\nتُترك الشاشة مفتوحة للمراجعة.";

            DialogResult ans = MessageBox.Show(
                this, body, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (ans != DialogResult.OK) { return; }

            ThreadPool.QueueUserWorkItem(delegate
            {
                try { DoPostSavePiece(confirm, true, true); }
                catch (Exception ex) { Log((confirm ? "confirm" : "export") + " test error: " + ex.Message, "ERROR"); }
            });
        }

        private void TickDailyExport()
        {
            if (_running) { return; }
            if (DateTime.Now < _dailyRetryAfter) { return; }
            if (_cfg == null || _cfg.Settings == null) { return; }
            if (!DailyExport.IsDue(_cfg.Settings, _cfg.StateDir, DateTime.Now)) { return; }

            _running = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    Log("=== Daily period export at " + DailyExport.Describe(_cfg.Settings) + " ===", "INFO");
                    bool ok = DoPostSavePiece(false, false, _cfg.Settings.ExportDateRangeYearToDate);
                    if (ok) { DailyExport.MarkDone(_cfg.StateDir, DateTime.Now); }
                    else { _dailyRetryAfter = DateTime.Now.AddMinutes(10); }
                }
                catch (Exception ex)
                {
                    Log("daily export error: " + ex.Message, "ERROR");
                    _dailyRetryAfter = DateTime.Now.AddMinutes(10);
                }
                finally
                {
                    _running = false;
                    BeginInvoke(new Action(delegate { RefreshTracking(); RefreshRuns(); }));
                }
            });
        }

        private bool DoPostSavePiece(bool confirmOnly, bool leaveOpen, bool yearToDate)
        {
            string tag = confirmOnly ? "confirm-only" : "export-only";
            ActionLog.Configure(_cfg.LogsDir, true);
            string session = ActionLog.BeginSession(tag);
            if (!string.IsNullOrEmpty(session)) { Log("session log: " + session, "INFO"); }

            try
            {
                IntPtr app = FieldDiscovery.GetTargetAppWindow(
                    _cfg.App.WindowTitlePattern, _cfg.App.ExePath, false, 5);
                if (app == IntPtr.Zero)
                {
                    Log("Coding window not found — افتح البرنامج أولاً", "ERROR");
                    ActionLog.EndSession("FAIL: Coding not attached");
                    return false;
                }

                WindowLayout.Active = _cfg.Windows;
                List<FieldSelector> selectors = FieldDiscovery.ImportSelectors(_cfg.SelectorsPath);

                CodingPostSaveOptions opt = new CodingPostSaveOptions();
                opt.Config = _cfg;
                opt.Selectors = selectors;
                opt.AppWindow = app;
                opt.ExportDateRangeYearToDate = yearToDate;
                opt.DoWriteBack = !confirmOnly;
                opt.SheetPathForWriteBack = _cfg.SheetPath;
                opt.SettleMs = 400;
                opt.LeaveScreensOpen = leaveOpen;
                opt.Log = delegate(string m, string level) { Log(m, level); };

                Log(confirmOnly ? "=== اختبار التأكيد فقط (F2/F9) ===" : "=== اختبار التصدير فقط ===", "INFO");
                CodingPostSaveResult res = confirmOnly
                    ? CodingPostSave.RunConfirmOnly(opt)
                    : CodingPostSave.RunExportOnly(opt);

                Log(res.Note, res.Ok ? "OK" : "ERROR");
                if (!confirmOnly && !string.IsNullOrEmpty(res.XmlPath))
                {
                    Log("export XML: " + res.XmlPath, "OK");
                }
                ActionLog.EndSession((res.Ok ? "OK: " : "FAIL: ") + res.Note);
                return res.Ok;
            }
            catch (Exception ex)
            {
                Log(tag + " error: " + ex.Message, "ERROR");
                ActionLog.EndSession("ERROR: " + ex.Message);
                return false;
            }
        }

        private void DoDocsTest()
        {
            ActionLog.Configure(_cfg.LogsDir, true);
            string session = ActionLog.BeginSession("docs-test");
            if (!string.IsNullOrEmpty(session)) { Log("session log: " + session, "INFO"); }

            try
            {
                string sheet = Path.Combine(_cfg.BaseDir, @"Sample\Test-Save-Client.csv");
                if (!File.Exists(sheet))
                {
                    Log("Sample\\Test-Save-Client.csv not found", "ERROR");
                    return;
                }

                SheetImportResult imported = SheetSync.ImportClientSheet(sheet);
                if (imported.Rows.Count == 0) { Log("sample sheet has no rows", "ERROR"); return; }
                OrderedDictionary row = imported.Rows[0];

                IntPtr app = FieldDiscovery.GetTargetAppWindow(
                    _cfg.App.WindowTitlePattern, _cfg.App.ExePath, false, 5);
                if (app == IntPtr.Zero) { Log("Coding window not found — افتح البرنامج أولاً", "ERROR"); return; }

                string entryTitle = _cfg.App.EntryWindowTitle;
                if (string.IsNullOrWhiteSpace(entryTitle)) { entryTitle = "بيانات العملاء"; }
                string entryClass = _cfg.App.EntryWindowClass;
                if (string.IsNullOrWhiteSpace(entryClass)) { entryClass = "ThunderRT6FormDC"; }

                IntPtr entry = ClientEntry.FindEntryForm(app, entryTitle, entryClass);
                if (entry == IntPtr.Zero)
                {
                    Log("نافذة «بيانات العملاء (إضافة)» غير مفتوحة", "ERROR");
                    return;
                }

                // Same window sizes the click-map was captured on, or the map is meaningless.
                WindowLayout.Active = _cfg.Windows;
                WindowLayout.ApplyAll(app, entry, IntPtr.Zero, _cfg.Windows,
                    delegate(string m, string level) { Log(m, level); });

                List<FieldSelector> selectors = FieldDiscovery.ImportSelectors(_cfg.SelectorsPath);
                string scans = Path.Combine(_cfg.BaseDir, "Sample\\Docs");
                if (!Directory.Exists(scans)) { scans = _cfg.ScansRoot; }

                Log("=== اختبار المرفقات فقط ===", "INFO");
                Log("scans root: " + scans, "INFO");

                DocAttachResult res = DocumentAttach.Attach(
                    entry, row, selectors, scans, true, 350,
                    delegate(string m, string level) { Log(m, level); });

                Log("attached " + res.Attached + "/" + res.Attempted + " — " + res.Note,
                    res.Ok ? "OK" : "ERROR");
                ActionLog.EndSession((res.Ok ? "OK: " : "FAIL: ") + res.Note);
            }
            catch (Exception ex)
            {
                Log("docs test error: " + ex.Message, "ERROR");
                ActionLog.EndSession("ERROR: " + ex.Message);
            }
        }

        private void DoEntryTest(bool oneClient, bool doSave)
        {
            ActionLog.Configure(_cfg.LogsDir, _cfg.Settings.DetailedActionLog);
            string kind = doSave ? "test-save" : (oneClient ? "mock-fill" : "entry-test");
            string session = ActionLog.BeginSession(kind);
            if (!string.IsNullOrEmpty(session))
            {
                Log("session log: " + session, "INFO");
            }

            try
            {
                EntryTestOptions opt = new EntryTestOptions();
                opt.BaseDir = _cfg.BaseDir;
                opt.MockOne = oneClient && !doSave;
                opt.DoSave = doSave;
                opt.ForceCompleteFlow = doSave;
                opt.Watch = true;
                opt.AutoMapIfEmpty = true;
                opt.UpdateTracking = true;
                opt.PostSaveConfirmExport = false;
                // Test-Save sample always uses year-to-date; production follows the settings checkbox.
                if (doSave) { opt.ExportDateRangeYearToDate = true; }
                else { opt.ExportDateRangeYearToDate = _cfg.Settings.ExportDateRangeYearToDate; }
                opt.StepMs = 350;
                if (doSave)
                {
                    string saveSheet = Path.Combine(_cfg.BaseDir, @"Sample\Test-Save-Client.csv");
                    opt.SheetPath = File.Exists(saveSheet) ? saveSheet : _cfg.SheetPath;
                    Log("=== Test-Save Customer E2E ===", "INFO");
                    Log("sheet: " + opt.SheetPath, "INFO");
                }
                else
                {
                    opt.SheetPath = _cfg.SheetPath;
                    Log("=== Entry test (no save) ===", "INFO");
                }
                opt.Log = delegate(string m, string level) { Log(m, level); };

                EntryTestResult res = EntryTest.Run(opt);
                Log(res.Note, res.Ok ? "OK" : "ERROR");
                if (!string.IsNullOrEmpty(res.PostSaveNote))
                {
                    Log("post-save: " + res.PostSaveNote, res.PostSaveOk ? "OK" : "WARN");
                }
                if (doSave && !string.IsNullOrEmpty(res.SavedEvidencePath) && File.Exists(res.SavedEvidencePath))
                {
                    Log("evidence: " + res.SavedEvidencePath, "INFO");
                }
                ActionLog.EndSession(res.Ok ? "OK: " + res.Note : "FAIL: " + res.Note);
            }
            catch (Exception ex)
            {
                Log("entry test error: " + ex.Message, "ERROR");
                ActionLog.EndSession("ERROR: " + ex.Message);
                throw;
            }
        }

        private IntPtr AttachCoding()
        {
            string title = _cfg.App != null ? _cfg.App.WindowTitlePattern : "";
            string exe = _cfg.App != null ? _cfg.App.ExePath : "";
            bool launch = _cfg.App != null && _cfg.App.AutoLaunch;
            int timeout = _cfg.App != null ? _cfg.App.LaunchTimeoutSec : 8;
            IntPtr app = FieldDiscovery.GetTargetAppWindow(title, exe, launch, timeout);
            if (app == IntPtr.Zero) { app = FieldDiscovery.GetTargetAppWindow("تكويد", "", false, 5); }
            if (app == IntPtr.Zero) { app = FieldDiscovery.GetTargetAppWindow("برنامج التكويد", "", false, 3); }
            return app;
        }

        private void PromptSaveWindowSize(string slot)
        {
            PersistSettingsFromUi();
            IntPtr app = AttachCoding();
            if (app == IntPtr.Zero)
            {
                MessageBox.Show(this, "افتح Coding.exe أولاً ثم اضغط الزر مرة أخرى.",
                    "حفظ حجم النافذة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            List<FieldSelector> selectors = FieldDiscovery.ImportSelectors(_cfg.SelectorsPath);
            Action<string, string> log = delegate(string m, string l) { Log(m, l); };
            IntPtr hwnd = WindowSizeSaver.FindOrOpen(app, _cfg, selectors, slot, 600, log);
            if (hwnd == IntPtr.Zero)
            {
                DialogResult retry = MessageBox.Show(
                    this,
                    "لم تُفتح شاشة «" + WindowSizeSaver.ArabicName(slot) + "».\n\n" +
                    "افتحها يدوياً من القائمة ثم اضغط نعم لإعادة المحاولة.",
                    "الشاشة غير ظاهرة",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (retry != DialogResult.Yes) { return; }
                hwnd = WindowSizeSaver.FindOrOpen(app, _cfg, selectors, slot, 600, log);
                if (hwnd == IntPtr.Zero)
                {
                    Log("تعذر فتح «" + WindowSizeSaver.ArabicName(slot) + "» لحفظ الحجم", "ERROR");
                    return;
                }
            }

            WindowSizeSaver.ApplySaved(hwnd, _cfg, slot, log);
            DialogResult ans = MessageBox.Show(
                this,
                "اضبط حجم وموضع «" + WindowSizeSaver.ArabicName(slot) + "» الآن، ثم اضغط موافق للحفظ.\n\n" +
                "عند إعادة الفتح سيُعاد نفس الحجم قبل التعبئة حتى لا ينكسر الربط.",
                "حفظ حجم النافذة",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information);
            if (ans != DialogResult.OK) { return; }

            if (!Win32Interop.IsWindow(hwnd))
            {
                hwnd = WindowSizeSaver.FindOrOpen(app, _cfg, selectors, slot, 400, log);
            }
            if (!WindowSizeSaver.Save(_cfg, app, hwnd, slot, log))
            {
                MessageBox.Show(this, "تعذر قراءة حجم الشاشة.", "حفظ حجم النافذة",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WindowBounds b = WindowSizeSaver.GetSaved(_cfg, slot);
            Log("حُفظ حجم «" + WindowSizeSaver.ArabicName(slot) + "» ← " +
                (b != null ? b.ToShort() : ""), "OK");
            MessageBox.Show(
                this,
                "تم حفظ حجم «" + WindowSizeSaver.ArabicName(slot) + "».\n" +
                (b != null ? b.ToShort() : ""),
                "حفظ حجم النافذة",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void CaptureOpenWindowSizes()
        {
            PersistSettingsFromUi();
            IntPtr app = AttachCoding();
            if (app == IntPtr.Zero)
            {
                MessageBox.Show(this, "افتح Coding.exe أولاً ثم اضبط النوافذ واضغط مرة أخرى.",
                    "أحجام النوافذ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string entryTitle = _cfg.App != null && !string.IsNullOrWhiteSpace(_cfg.App.EntryWindowTitle)
                ? _cfg.App.EntryWindowTitle.Trim() : "بيانات العملاء";
            string entryClass = _cfg.App != null && !string.IsNullOrWhiteSpace(_cfg.App.EntryWindowClass)
                ? _cfg.App.EntryWindowClass.Trim() : "ThunderRT6FormDC";
            IntPtr entry = ClientEntry.FindEntryForm(app, entryTitle, entryClass);
            int pid = Win32Driver.GetProcessId(app);
            IntPtr mostanad = WindowLayout.FindMostanadIfOpen(pid);
            List<FieldSelector> selectors = FieldDiscovery.ImportSelectors(_cfg.SelectorsPath);
            IntPtr lookupScreen = ClientLookup.FindOpenScreen(app, _cfg.App, selectors);
            IntPtr confirmScreen = CodingPostSave.FindOpenConfirm(app, _cfg.App, selectors);
            IntPtr periodScreen = CodingPostSave.FindOpenPeriod(app, _cfg.App, selectors);

            WindowLayoutConfig previous = _cfg.Windows;
            WindowLayoutConfig layout = WindowLayout.CaptureFromLive(
                app, entry, mostanad, lookupScreen, confirmScreen, periodScreen);
            if (layout.MainApp == null || !layout.MainApp.IsValid)
            {
                MessageBox.Show(this, "تعذر قراءة حجم نافذة Coding.", "أحجام النوافذ",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WindowLayout.KeepPreviousIfMissing(layout, previous);
            _cfg.Windows = layout;
            _cfg.SaveBotConfig();
            WindowLayout.Active = layout;

            string msg = "Coding: " + layout.MainApp.ToShort();
            msg += layout.EntryForm != null && layout.EntryForm.IsValid
                ? " | العملاء: " + layout.EntryForm.ToShort()
                : " | العملاء: (استخدم الإعداد → حفظ حجم إدخال العملاء)";
            msg += layout.PeriodScreen != null && layout.PeriodScreen.IsValid
                ? " | التصدير: " + layout.PeriodScreen.ToShort()
                : " | التصدير: (استخدم «حفظ حجم التصدير»)";
            msg += layout.ConfirmScreen != null && layout.ConfirmScreen.IsValid
                ? " | التأكيد: " + layout.ConfirmScreen.ToShort()
                : " | التأكيد: (افتحها ثم أعد الحفظ)";
            Log("حُفظت أحجام النوافذ المفتوحة → " + msg, "OK");
            MessageBox.Show(
                this,
                "تم حفظ أحجام النوافذ المفتوحة.\n" + msg +
                "\n\nللشاشات المغلقة استخدم الزر المخصص (مثل «حفظ حجم التصدير») حتى لا ينكسر الموضع عند إعادة الفتح.",
                "أحجام النوافذ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
}
