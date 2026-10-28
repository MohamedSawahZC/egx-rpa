// =============================================================================
//  SetupForm.cs - modern setup: paths, discover/map fields, import/export backup.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using RbaCore;

namespace RbaUi
{
    public class SetupForm : Form
    {
        // Anchors are unusable inside an AutoScroll panel: WinForms measures the anchor
        // gap against the panel's pre-layout width and produces nonsense. Every
        // width-dependent control registers a layout step in _layout instead, and
        // LayoutHead() replays them in order on every resize.
        private const int CanvasPad = 12;
        private const int MinCanvasWidth = 800;
        private const int RowHeight = 34;

        private readonly List<Action> _layout = new List<Action>();

        private BotConfig _cfg;
        private TextBox _txtSftp;
        private TextBox _txtSftpHost;
        private TextBox _txtSftpUser;
        private TextBox _txtSftpPassword;
        private TextBox _txtSftpInbox;
        private NumericUpDown _numSftpPort;
        private CheckBox _chkSftpOffline;
        private Label _lblSftpProbe;
        private TextBox _txtSheet;
        private TextBox _txtScans;
        private TextBox _txtExport;
        private TextBox _txtExe;
        private TextBox _txtTitle;
        private TextBox _txtEntryTitle;
        private TextBox _txtShortcut;
        private TextBox _txtMenuParent;
        private TextBox _txtMenuLeaf;
        private CheckBox _chkCloseEntryAfterSave;
        private TextBox _txtPullCodesLeaf;
        private TextBox _txtConfirmParent;
        private TextBox _txtConfirmLeaf;
        private TextBox _txtPeriodParent;
        private TextBox _txtPeriodLeaf;
        private CheckBox _chkDailyExport;
        private NumericUpDown _numExportHour;
        private NumericUpDown _numExportMinute;
        private ComboBox _cmbExportAmPm;
        private CheckBox _chkExportYearToDate;
        private Label _lblNavButton;
        private ComboBox _cmbFieldKey;
        private Label _lblFieldCaptureStatus;
        private CheckBox _chkAutoLaunch;
        private Label _lblConnectStatus;
        private Panel _head;
        private int _headContentHeight;
        private bool _headSizedByUser;
        private bool _layingOut;
        private DataGridView _grid;
        private TextBox _log;
        private Label _lblMapCount;
        private IntPtr _attachedApp = IntPtr.Zero;
        private List<AppField> _fields = new List<AppField>();
        private List<FieldSelector> _selectors = new List<FieldSelector>();
        private string _lastBundleDir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        private FieldSelector _navButton;

        public SetupForm(BotConfig cfg)
        {
            _cfg = cfg;
            Text = "إعداد RPA V1 — ربط الحقول";
            // MinimumSize keeps the settings canvas at or above MinCanvasWidth once the
            // card padding and the RTL scrollbar are taken out, so it never scrolls sideways.
            ClientSize = new Size(1064, 900);
            MinimumSize = new Size(930, 600);
            StartPosition = FormStartPosition.CenterScreen;
            UiTheme.ApplyForm(this);
            DoubleBuffered = true;

            Controls.Add(UiTheme.MakeHeader(
                "إعداد وربط الحقول · RPA V1",
                "الاتصال تلقائي بالعنوان / ملف EXE — لا نخزّن رقم نافذة يتغيّر عند الإغلاق",
                72));

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(16, 12, 16, 12);
            body.BackColor = UiTheme.Bg;
            Controls.Add(body);
            body.BringToFront();

            Panel card = UiTheme.MakeCard();
            card.Dock = DockStyle.Fill;
            body.Controls.Add(card);

            // Settings rows live in a docked, scrollable header so the footer buttons
            // (حفظ الربط / حفظ الإعدادات) can never be pushed out of view.
            Panel head = new Panel();
            head.Dock = DockStyle.Top;
            head.AutoScroll = true;
            head.BackColor = UiTheme.Surface;
            _head = head;

            int y = 12;
            // SFTP card: LTR sequential rows — avoids RTL AddPickRow + absolute Port overlap.
            const int sftpCardH = 348;
            Panel sftp = AddSection(head, ref y, sftpCardH, Color.FromArgb(232, 240, 250));
            Panel sftpInner = new Panel();
            sftpInner.RightToLeft = RightToLeft.No;
            sftpInner.BackColor = Color.FromArgb(232, 240, 250);
            sftp.Controls.Add(sftpInner);
            OnLayout(delegate
            {
                sftpInner.SetBounds(CanvasPad, 6, Math.Max(200, sftp.ClientSize.Width - (CanvasPad * 2)), sftp.ClientSize.Height - 12);
            });

            int sy = 4;
            const int sftpLabelW = 100;
            Label sftpTitle = MkTitle("إعدادات SFTP");
            sftpTitle.SetBounds(0, sy, 400, 22);
            sftpInner.Controls.Add(sftpTitle);
            sy += 24;
            Label sftpHint = MkHint("Inbox = مجلد kyc_export.csv + docs | Offline = ملفات محلية | Host=local أيضاً Offline");
            sftpHint.SetBounds(0, sy, 700, 18);
            sftpInner.Controls.Add(sftpHint);
            sy += 22;

            _txtSftp = MakeInput(string.IsNullOrEmpty(cfg.SftpLocalRoot) ? @"C:\RPA-SFTP" : cfg.SftpLocalRoot, true);
            Button btnSftpBrowse = UiTheme.MakeButton("…", false);
            btnSftpBrowse.Click += delegate
            {
                FolderBrowserDialog d = new FolderBrowserDialog();
                if (d.ShowDialog(this) == DialogResult.OK) { _txtSftp.Text = d.SelectedPath; }
            };
            AddLtrRow(sftpInner, "جذر محلي", _txtSftp, btnSftpBrowse, ref sy, sftpLabelW);

            _txtSftpHost = MakeInput(string.IsNullOrEmpty(cfg.SftpHost) ? BotConfig.DefaultSftpHost : cfg.SftpHost, true);
            AddLtrRow(sftpInner, "Host", _txtSftpHost, null, ref sy, sftpLabelW);

            _numSftpPort = new NumericUpDown();
            _numSftpPort.Minimum = 1;
            _numSftpPort.Maximum = 65535;
            _numSftpPort.Value = cfg.SftpPort > 0 ? cfg.SftpPort : 22;
            AddLtrRow(sftpInner, "Port", _numSftpPort, null, ref sy, sftpLabelW, 90);

            _txtSftpUser = MakeInput(string.IsNullOrEmpty(cfg.SftpUser) ? BotConfig.DefaultSftpUser : cfg.SftpUser, true);
            AddLtrRow(sftpInner, "User", _txtSftpUser, null, ref sy, sftpLabelW);

            _txtSftpPassword = MakeInput(cfg.SftpPassword ?? "", true);
            _txtSftpPassword.UseSystemPasswordChar = true;
            AddLtrRow(sftpInner, "Password", _txtSftpPassword, null, ref sy, sftpLabelW);

            string inboxSeed = cfg.SftpRemoteInbox;
            if (string.IsNullOrEmpty(inboxSeed) ||
                string.Equals(inboxSeed, "inbox", StringComparison.OrdinalIgnoreCase))
            {
                inboxSeed = "kyc-imports";
            }
            _txtSftpInbox = MakeInput(inboxSeed, true);
            AddLtrRow(sftpInner, "Inbox", _txtSftpInbox, null, ref sy, sftpLabelW);

            _chkSftpOffline = new CheckBox();
            _chkSftpOffline.Text = "وضع Offline — ملفات محلية بدون اتصال SFTP";
            _chkSftpOffline.Checked = cfg.SftpOfflineMode;
            _chkSftpOffline.ForeColor = UiTheme.Ink;
            _chkSftpOffline.AutoSize = false;
            _chkSftpOffline.CheckedChanged += delegate { ApplySftpSourceUi(); };
            sftpInner.Controls.Add(_chkSftpOffline);
            int offlineTop = sy;
            OnLayout(delegate
            {
                int w = Math.Max(200, sftpInner.ClientSize.Width);
                _chkSftpOffline.SetBounds(sftpLabelW + 8, offlineTop, Math.Max(220, w - sftpLabelW - 16), 24);
            });
            sy += 30;

            Button btnSftpProbe = UiTheme.MakeButton("فحص المصدر", true);
            btnSftpProbe.Click += delegate { ProbeSftpConnection(); };
            sftpInner.Controls.Add(btnSftpProbe);
            _lblSftpProbe = MkHint("اضغط للفحص وعرض ملفات الـ inbox في السجل");
            sftpInner.Controls.Add(_lblSftpProbe);
            int probeTop = sy;
            OnLayout(delegate
            {
                int w = Math.Max(200, sftpInner.ClientSize.Width);
                btnSftpProbe.SetBounds(sftpLabelW + 8, probeTop, 160, 28);
                _lblSftpProbe.SetBounds(sftpLabelW + 176, probeTop + 4, Math.Max(120, w - sftpLabelW - 184), 22);
            });
            sy += 34;
            ApplySftpSourceUi();

            _txtSheet = AddPathRow(head, "ملف العمل (من المصدر)", cfg.SheetPath, ref y, true);
            _txtScans = AddPathRow(head, "مجلد المستندات (من المصدر)", cfg.ScansRoot, ref y, false);
            _txtExport = AddPathRow(head, "مجلد التصدير", cfg.ExportFolder, ref y, false);
            _txtExe = AddPathRow(head, "برنامج التكويد", cfg.App.ExePath, ref y, true);

            _txtTitle = MakeInput(cfg.App.WindowTitlePattern);
            AddPickRow(head, MkLabel("نمط عنوان النافذة"), 130, _txtTitle, 0, null, 0, 0, y);
            _txtEntryTitle = MakeInput(cfg.App.EntryWindowTitle);
            AddPickRow(head, MkLabel("عنوان شاشة الإدخال"), 130, _txtEntryTitle, 0, null, 0, 1, y);
            y += RowHeight;

            _txtShortcut = MakeInput(string.IsNullOrEmpty(cfg.App.EntryShortcut) ? "^{F2}" : cfg.App.EntryShortcut, true);
            AddPickRow(head, MkLabel("اختصار"), 130, _txtShortcut, 110, null, 0, 0, y);

            _chkAutoLaunch = new CheckBox();
            _chkAutoLaunch.Text = "تشغيل البرنامج تلقائياً إن لم يكن مفتوحاً";
            _chkAutoLaunch.Checked = cfg.App.AutoLaunch;
            _chkAutoLaunch.ForeColor = UiTheme.Ink;
            FillColumn(head, _chkAutoLaunch, 1, y, 26);
            y += 36;

            // Auto-connect strip (no HWND picker — identity is title pattern + exe path).
            Panel connect = AddSection(head, ref y, 80, UiTheme.AccentSoft);

            Label connectHint = MkHint("الهوية الثابتة: نمط العنوان (مثل «تكويد») و/أو مسار Coding.exe — يُعاد الاتصال كل مرة بدون اختيار يدوي");
            AddFill(connect, connectHint, 6, CanvasPad, 154, 20);

            _lblConnectStatus = new Label();
            _lblConnectStatus.ForeColor = UiTheme.Ink;
            _lblConnectStatus.Font = new Font("Segoe UI Semibold", 9.25f);
            _lblConnectStatus.Text = "لم يتم الاتصال بعد — اضغط «اتصال تلقائي»";
            AddFill(connect, _lblConnectStatus, 28, CanvasPad, 154, 46);

            Button btnConnect = UiTheme.MakeButton("اتصال تلقائي", true);
            btnConnect.Click += delegate { ConnectAuto(); };
            AddFromEnd(connect, btnConnect, 20, CanvasPad, 130, 36);

            // ---- Navigate-to-add-customer (click-to-pick like a color picker) ----
            Panel nav = AddSection(head, ref y, 124, Color.FromArgb(236, 242, 248));

            AddFill(nav, MkTitle("فتح شاشة إضافة العملاء — التقاط بالنقر (مثل اختيار لون)"), 6, CanvasPad, CanvasPad, 20);
            AddFill(nav, MkHint("اضغط «التقاط» ثم انقر القائمة/الزر في برنامج التكويد — يُحفظ بالاسم وليس برقم النافذة"), 26, CanvasPad, CanvasPad, 18);

            string parentDef = "التكويد";
            string leafDef = "إدخال بيانات العملاء";
            if (cfg.App.MenuPath != null && cfg.App.MenuPath.Length >= 1 && !string.IsNullOrEmpty(cfg.App.MenuPath[0]))
            {
                parentDef = cfg.App.MenuPath[0];
            }
            if (cfg.App.MenuPath != null && cfg.App.MenuPath.Length >= 2 && !string.IsNullOrEmpty(cfg.App.MenuPath[1]))
            {
                leafDef = cfg.App.MenuPath[1];
            }

            _txtMenuParent = MakeInput(parentDef);
            Button btnPickParent = UiTheme.MakeButton("التقاط", true);
            btnPickParent.Click += delegate { PickMenuParent(); };
            AddPickRow(nav, MkLabel("قائمة الأب"), 92, _txtMenuParent, 0, btnPickParent, 78, 0, 52);

            _txtMenuLeaf = MakeInput(leafDef);
            Button btnPickLeaf = UiTheme.MakeButton("التقاط", true);
            btnPickLeaf.Click += delegate { PickMenuLeaf(); };
            AddPickRow(nav, MkLabel("عنصر القائمة"), 92, _txtMenuLeaf, 0, btnPickLeaf, 78, 1, 52);

            _lblNavButton = MkHint("زر بديل (اختياري): غير محدد");
            AddFill(nav, _lblNavButton, 88, CanvasPad, 230, 26);

            Button btnPickBtn = UiTheme.MakeButton("التقاط زر", false);
            btnPickBtn.Click += delegate { PickNavButton(); };
            AddFromEnd(nav, btnPickBtn, 86, 120, 100, 30);

            Button btnClearNav = UiTheme.MakeButton("مسح الزر", false);
            btnClearNav.Click += delegate
            {
                _navButton = null;
                RemoveNavSelector();
                RefreshNavButtonLabel();
                Log("تم مسح الزر البديل");
            };
            AddFromEnd(nav, btnClearNav, 86, CanvasPad, 100, 30);

            // ---- Close entry after save (single option, no OCR noise) ----
            Panel options = AddSection(head, ref y, 48, Color.FromArgb(245, 248, 250));
            _chkCloseEntryAfterSave = new CheckBox();
            _chkCloseEntryAfterSave.Text = "أغلق شاشة الإدخال بعد كل حفظ ناجح";
            _chkCloseEntryAfterSave.Checked = cfg.App.CloseEntryAfterSave;
            _chkCloseEntryAfterSave.ForeColor = UiTheme.Ink;
            _chkCloseEntryAfterSave.AutoSize = false;
            FillColumn(options, _chkCloseEntryAfterSave, -1, 12, 24);

            // ---- Pull codes when operator chooses سحب ----
            Panel pull = AddSection(head, ref y, 148, Color.FromArgb(232, 242, 250));
            AddFill(pull, MkTitle("سحب الأكواد"), 6, CanvasPad, CanvasPad, 20);
            AddFill(pull, MkHint("بحث بالكود الموحد → ضغط الجدول → F2 → F9"), 26, CanvasPad, CanvasPad, 18);

            string pullLeafDef = "سحب الأكواد";
            if (cfg.App.PullCodesMenuPath != null && cfg.App.PullCodesMenuPath.Length >= 2 &&
                !string.IsNullOrWhiteSpace(cfg.App.PullCodesMenuPath[1]))
            {
                pullLeafDef = cfg.App.PullCodesMenuPath[1];
            }
            _txtPullCodesLeaf = MakeInput(pullLeafDef);
            Button btnPickPullLeaf = UiTheme.MakeButton("التقاط", true);
            btnPickPullLeaf.Click += delegate { PickPullCodesLeaf(); };
            AddPickRow(pull, MkLabel("عنصر القائمة"), 92, _txtPullCodesLeaf, 0, btnPickPullLeaf, 78, 0, 48);

            Button btnPullBox = UiTheme.MakeButton("التقاط خانة الكود", true);
            btnPullBox.Click += delegate { PickPullCodesField(PullCodes.SearchBoxKey); };
            AddFromStart(pull, btnPullBox, 86, CanvasPad, 150, 30);

            Button btnPullBtn = UiTheme.MakeButton("التقاط زر البحث", true);
            btnPullBtn.Click += delegate { PickPullCodesField(PullCodes.SearchButtonKey); };
            AddFromStart(pull, btnPullBtn, 86, 170, 140, 30);

            Button btnPullGrid = UiTheme.MakeButton("التقاط الجدول", true);
            btnPullGrid.Click += delegate { PickPullCodesField(PullCodes.GridKey); };
            AddFromStart(pull, btnPullGrid, 86, 318, 140, 30);

            Button btnPullSize = UiTheme.MakeButton("حفظ حجم سحب الأكواد", true);
            btnPullSize.Click += delegate { PromptSaveWindowSize(WindowSizeSaver.SlotPullCodes); };
            AddFromStart(pull, btnPullSize, 118, CanvasPad, 200, 28);

            // ---- Post-save: confirm clients + period export ----
            Panel post = AddSection(head, ref y, 300, Color.FromArgb(236, 248, 242));
            AddFill(post, MkTitle("تأكيد العملاء + تصدير الفترة (مرة يومياً — ليس بعد كل حفظ)"), 6, CanvasPad, CanvasPad, 20);
            AddFill(post, MkHint("التصدير يُختبر بالزر أدناه، وفي التشغيل الحقيقي مرة واحدة يومياً في الوقت المحدد"), 26, CanvasPad, CanvasPad, 18);

            _chkDailyExport = new CheckBox();
            _chkDailyExport.Text = "تصدير يومي تلقائي في الوقت المحدد (صباحاً/مساءً)";
            _chkDailyExport.Checked = cfg.Settings == null || cfg.Settings.DailyExportEnabled;
            _chkDailyExport.ForeColor = UiTheme.Ink;
            FillColumn(post, _chkDailyExport, 0, 48, 24);

            _chkExportYearToDate = new CheckBox();
            _chkExportYearToDate.Text = "نطاق تاريخ الاختبار: من 1/1 للسنة حتى اليوم (الإنتاج = اليوم فقط)";
            _chkExportYearToDate.Checked = cfg.Settings != null && cfg.Settings.ExportDateRangeYearToDate;
            _chkExportYearToDate.ForeColor = UiTheme.Ink;
            FillColumn(post, _chkExportYearToDate, 1, 48, 24);

            int hour12 = cfg.Settings != null ? cfg.Settings.DailyExportHour : 5;
            int minute = cfg.Settings != null ? cfg.Settings.DailyExportMinute : 0;
            bool pm = cfg.Settings == null || cfg.Settings.DailyExportPm;
            AddFromStart(post, MkLabel("ساعة"), 76, CanvasPad, 40, 20);
            _numExportHour = new NumericUpDown();
            _numExportHour.Minimum = 1;
            _numExportHour.Maximum = 12;
            _numExportHour.Value = hour12 < 1 || hour12 > 12 ? 5 : hour12;
            AddFromStart(post, _numExportHour, 72, 56, 56, 26);
            AddFromStart(post, MkLabel("دقيقة"), 76, 124, 44, 20);
            _numExportMinute = new NumericUpDown();
            _numExportMinute.Minimum = 0;
            _numExportMinute.Maximum = 59;
            _numExportMinute.Value = minute < 0 || minute > 59 ? 0 : minute;
            AddFromStart(post, _numExportMinute, 72, 172, 56, 26);
            _cmbExportAmPm = new ComboBox();
            _cmbExportAmPm.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbExportAmPm.Items.Add("AM");
            _cmbExportAmPm.Items.Add("PM");
            _cmbExportAmPm.SelectedIndex = pm ? 1 : 0;
            AddFromStart(post, _cmbExportAmPm, 72, 238, 64, 26);

            string confParent = "نظام تكويد العملاء";
            string confLeaf = "تأكيد بيانات العملاء قبل إرسالها للتكويد";
            if (cfg.App.ConfirmMenuPath != null && cfg.App.ConfirmMenuPath.Length >= 2)
            {
                if (!string.IsNullOrEmpty(cfg.App.ConfirmMenuPath[0])) { confParent = cfg.App.ConfirmMenuPath[0]; }
                if (!string.IsNullOrEmpty(cfg.App.ConfirmMenuPath[1])) { confLeaf = cfg.App.ConfirmMenuPath[1]; }
            }
            _txtConfirmParent = MakeInput(confParent);
            Button btnPickConfParent = UiTheme.MakeButton("التقاط", true);
            btnPickConfParent.Click += delegate { PickPostMenuParent(_txtConfirmParent, "تأكيد"); };
            AddPickRow(post, MkLabel("أب التأكيد"), 92, _txtConfirmParent, 0, btnPickConfParent, 78, 0, 112);

            _txtConfirmLeaf = MakeInput(confLeaf);
            Button btnPickConfLeaf = UiTheme.MakeButton("التقاط", true);
            btnPickConfLeaf.Click += delegate { PickPostMenuLeaf(_txtConfirmLeaf, "تأكيد بيانات العملاء قبل إرسالها للتكويد"); };
            AddPickRow(post, MkLabel("عنصر التأكيد"), 92, _txtConfirmLeaf, 0, btnPickConfLeaf, 78, 1, 112);

            string perParent = "نظام تكويد العملاء";
            string perLeaf = "عمليات التكويد خلال فترة";
            if (cfg.App.PeriodMenuPath != null && cfg.App.PeriodMenuPath.Length >= 2)
            {
                if (!string.IsNullOrEmpty(cfg.App.PeriodMenuPath[0])) { perParent = cfg.App.PeriodMenuPath[0]; }
                if (!string.IsNullOrEmpty(cfg.App.PeriodMenuPath[1])) { perLeaf = cfg.App.PeriodMenuPath[1]; }
            }
            _txtPeriodParent = MakeInput(perParent);
            Button btnPickPerParent = UiTheme.MakeButton("التقاط", true);
            btnPickPerParent.Click += delegate { PickPostMenuParent(_txtPeriodParent, "فترة"); };
            AddPickRow(post, MkLabel("أب الفترة"), 92, _txtPeriodParent, 0, btnPickPerParent, 78, 0, 148);

            _txtPeriodLeaf = MakeInput(perLeaf);
            Button btnPickPerLeaf = UiTheme.MakeButton("التقاط", true);
            btnPickPerLeaf.Click += delegate { PickPostMenuLeaf(_txtPeriodLeaf, "عمليات التكويد خلال فترة"); };
            AddPickRow(post, MkLabel("عنصر الفترة"), 92, _txtPeriodLeaf, 0, btnPickPerLeaf, 78, 1, 148);

            Button btnPickConfirmGrid = UiTheme.MakeButton("التقاط جدول التأكيد", true);
            btnPickConfirmGrid.Click += delegate { PickPostSaveField(CodingPostSave.ConfirmGridKey); };
            AddFromStart(post, btnPickConfirmGrid, 184, CanvasPad, 160, 28);

            Button btnPickPeriodDates = UiTheme.MakeButton("التقاط تواريخ الفترة", true);
            btnPickPeriodDates.Click += delegate { PickPeriodDatesWizard(); };
            AddFromStart(post, btnPickPeriodDates, 184, 180, 150, 28);

            Button btnPickExportBtn = UiTheme.MakeButton("زر Export", true);
            btnPickExportBtn.Click += delegate { PickPostSaveField(CodingPostSave.PeriodExportBtnKey); };
            AddFromStart(post, btnPickExportBtn, 184, 338, 110, 28);

            Button btnPickExcelPath = UiTheme.MakeButton("مسار XML", true);
            btnPickExcelPath.Click += delegate { PickPostSaveField(CodingPostSave.PeriodExcelPathKey); };
            AddFromStart(post, btnPickExcelPath, 184, 456, 110, 28);

            AddFill(post, MkHint("التقط الجدول + 6 تواريخ + مسار XML + زر Export مرة واحدة"), 216, CanvasPad, CanvasPad, 18);

            Button btnConfirmSize = UiTheme.MakeButton("حفظ حجم التأكيد", true);
            btnConfirmSize.Click += delegate { PromptSaveWindowSize(WindowSizeSaver.SlotConfirm); };
            AddFromStart(post, btnConfirmSize, 248, CanvasPad, 170, 28);

            Button btnPeriodSize = UiTheme.MakeButton("حفظ حجم التصدير", true);
            btnPeriodSize.Click += delegate { PromptSaveWindowSize(WindowSizeSaver.SlotPeriod); };
            AddFromStart(post, btnPeriodSize, 248, 188, 180, 28);

            AddFill(post, MkHint("احفظ الحجم والموضع بعد ضبط الشاشة — إعادة الفتح تستخدم نفس الإحداثيات"), 284, CanvasPad, CanvasPad, 18);

            // ---- Click-to-map every entry field (same picker UX as menu) ----
            Panel fields = AddSection(head, ref y, 152, Color.FromArgb(232, 245, 240));

            AddFill(fields, MkTitle("التقاط الحقول بالنقر — كل التواريخ: يوم → شهر → سنة"), 6, CanvasPad, CanvasPad, 20);
            AddFill(fields, MkHint("عند التاريخ: التقط خانة اليوم أولاً ثم الشهر ثم السنة — الترتيب ثابت في التعبئة أيضاً"), 26, CanvasPad, CanvasPad, 18);

            AddFromStart(fields, MkLabel("الحقل"), 56, CanvasPad, 50, 20);

            _cmbFieldKey = new ComboBox();
            _cmbFieldKey.DropDownStyle = ComboBoxStyle.DropDownList;
            _cmbFieldKey.FlatStyle = FlatStyle.Flat;
            _cmbFieldKey.DropDownWidth = 340;
            for (int i = 0; i < FieldCatalog.All.Length; i++)
            {
                // Hide retired inquiry/OCR field keys from the picker.
                if (ClientLookup.IsLookupKey(FieldCatalog.All[i].FieldKey)) { continue; }
                _cmbFieldKey.Items.Add(FieldCatalog.All[i].Display);
            }
            if (_cmbFieldKey.Items.Count > 0) { _cmbFieldKey.SelectedIndex = 0; }
            AddFill(fields, _cmbFieldKey, 52, 70, 594, 26);

            // Fixed to the left end so the combo absorbs every width change.
            Button btnPickField = UiTheme.MakeButton("التقاط هذا الحقل", true);
            btnPickField.Click += delegate { PickSelectedCatalogField(); };
            AddFromEnd(fields, btnPickField, 50, 446, 140, 30);

            Button btnPickRow = UiTheme.MakeButton("التقاط الصف المحدد", false);
            btnPickRow.Click += delegate { PickSelectedGridRow(); };
            AddFromEnd(fields, btnPickRow, 50, 298, 140, 30);

            Button btnWizard = UiTheme.MakeButton("التقاط الكل بالتتالي", true);
            btnWizard.Click += delegate { PickAllFieldsWizard(); };
            AddFromEnd(fields, btnWizard, 50, 130, 160, 30);

            Button btnSeed = UiTheme.MakeButton("تهيئة القائمة", false);
            btnSeed.Click += delegate
            {
                SeedFieldCatalogRows(false);
                FillGridFromSelectors();
                Log("تمت تهيئة قائمة الحقول القياسية");
            };
            AddFromEnd(fields, btnSeed, 50, CanvasPad, 110, 30);

            Button btnDocs = UiTheme.MakeButton("التقاط مسار المستندات (ملفات)", true);
            btnDocs.Click += delegate { PickDocAddButtonsWizard(); };
            AddFromStart(fields, btnDocs, 88, CanvasPad, 200, 30);

            Button btnEntrySize = UiTheme.MakeButton("حفظ حجم إدخال العملاء", true);
            btnEntrySize.Click += delegate { PromptSaveWindowSize(WindowSizeSaver.SlotEntry); };
            AddFromStart(fields, btnEntrySize, 88, 216, 190, 30);

            Button btnWinSizes = UiTheme.MakeButton("حفظ المفتوحة", false);
            btnWinSizes.Click += delegate { CaptureWindowSizes(); };
            AddFromStart(fields, btnWinSizes, 88, 414, 130, 30);

            Button btnCheckMap = UiTheme.MakeButton("فحص صحة الربط", false);
            btnCheckMap.Click += delegate { ShowMapHealth(); };
            AddFromStart(fields, btnCheckMap, 124, CanvasPad, 140, 26);

            _lblFieldCaptureStatus = MkHint("لم يُلتقط حقل بعد — افتح شاشة إضافة العملاء ثم احفظ حجمها قبل التقاط الحقول");
            AddFill(fields, _lblFieldCaptureStatus, 124, 158, CanvasPad, 22);

            _lblMapCount = MkHint("لا يوجد ربط محمّل");
            AddFill(head, _lblMapCount, y, CanvasPad, CanvasPad, 20);
            y += 32;

            // Vertical-only scrolling: a horizontal scrollbar in an RTL panel shifts the
            // whole canvas left and hides the label column, so the layout stays fluid.
            _headContentHeight = y;
            head.AutoScrollMinSize = new Size(0, _headContentHeight);
            head.Height = Math.Min(_headContentHeight, 430);

            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.AllowUserToAddRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.Columns.Add("key", "FieldKey");
            _grid.Columns.Add("label", "Label");
            _grid.Columns.Add("kind", "Kind");
            _grid.Columns.Add("cls", "Class");
            _grid.Columns.Add("ord", "ClassOrdinal");
            _grid.Columns.Add("col", "SheetColumn");
            _grid.Columns.Add("screen", "Screen");
            _grid.Columns["col"].ReadOnly = false;
            _grid.Columns["key"].ReadOnly = false;
            UiTheme.StyleGrid(_grid);
            _grid.CellDoubleClick += delegate
            {
                PickSelectedGridRow();
            };
            // Grid fills what is left. The splitter lets the user trade space between the
            // settings canvas and the mapping grid instead of scrolling both.
            Splitter split = new Splitter();
            split.Dock = DockStyle.Top;
            split.Height = 6;
            split.BackColor = UiTheme.Line;
            split.MinExtra = 140;
            split.MinSize = 160;
            split.SplitterMoved += delegate { _headSizedByUser = true; };

            card.Controls.Add(_grid);
            card.Controls.Add(split);
            card.Controls.Add(head);

            // Footer is docked to the form so save / discover / backup actions are always
            // reachable. Buttons sit in a fixed-width strip; the log takes what is left.
            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 102;
            footer.BackColor = UiTheme.Surface;
            footer.Padding = new Padding(12, 10, 12, 10);
            Controls.Add(footer);

            const int footerButtonsWidth = 500;

            Panel logHost = new Panel();
            logHost.Dock = DockStyle.Fill;
            logHost.BackColor = UiTheme.Surface;
            logHost.Padding = new Padding(0, 0, 12, 0);
            footer.Controls.Add(logHost);

            Panel footerButtons = new Panel();
            footerButtons.Dock = DockStyle.Right;
            footerButtons.Width = footerButtonsWidth;
            footerButtons.BackColor = UiTheme.Surface;
            footer.Controls.Add(footerButtons);

            // Right-to-left reading order inside the strip: primary action first.
            Button btnSaveMap = UiTheme.MakeButton("حفظ الربط", true);
            btnSaveMap.SetBounds(footerButtonsWidth - 130, 0, 130, 34);
            btnSaveMap.Click += delegate { SaveMapping(); };

            Button btnSaveCfg = UiTheme.MakeButton("حفظ الإعدادات", false);
            btnSaveCfg.SetBounds(footerButtonsWidth - 270, 0, 130, 34);
            btnSaveCfg.Click += delegate { SaveConfig(); };

            Button btnClose = UiTheme.MakeButton("إغلاق", false);
            btnClose.SetBounds(footerButtonsWidth - 380, 0, 100, 34);
            btnClose.Click += delegate { DialogResult = DialogResult.OK; Close(); };

            Button btnDiscover = UiTheme.MakeButton("اكتشاف الحقول", true);
            btnDiscover.SetBounds(footerButtonsWidth - 140, 42, 140, 34);
            btnDiscover.Click += delegate { Discover(); };

            Button btnImport = UiTheme.MakeButton("استيراد نسخة مقفلة…", false);
            btnImport.SetBounds(footerButtonsWidth - 315, 42, 165, 34);
            btnImport.Click += delegate { ImportBackup(); };

            Button btnExport = UiTheme.MakeButton("تصدير نسخة مقفلة…", false);
            btnExport.SetBounds(footerButtonsWidth - 490, 42, 165, 34);
            btnExport.Click += delegate { ExportBackup(); };

            footerButtons.Controls.Add(btnSaveMap);
            footerButtons.Controls.Add(btnSaveCfg);
            footerButtons.Controls.Add(btnClose);
            footerButtons.Controls.Add(btnDiscover);
            footerButtons.Controls.Add(btnImport);
            footerButtons.Controls.Add(btnExport);

            _log = UiTheme.MakeLogBox();
            _log.Dock = DockStyle.Fill;
            logHost.Controls.Add(_log);

            if (File.Exists(cfg.SelectorsPath))
            {
                _selectors = FieldDiscovery.ImportSelectors(cfg.SelectorsPath);
                for (int i = 0; i < _selectors.Count; i++)
                {
                    if (string.Equals(_selectors[i].FieldKey, "navNewClient", StringComparison.OrdinalIgnoreCase))
                    {
                        _navButton = _selectors[i];
                        break;
                    }
                }
                FillGridFromSelectors();
                Log("تم تحميل الربط الحالي: " + _selectors.Count + " حقل");
            }
            else
            {
                SeedFieldCatalogRows(true);
                FillGridFromSelectors();
                Log("تم تجهيز قائمة الحقول للتقاط بالنقر");
            }
            RefreshNavButtonLabel();
            RefreshFieldCaptureStatus();
            head.ClientSizeChanged += delegate { LayoutHead(); };
            LayoutHead();
            ConnectAuto();
        }

        // DPI scaling can grow the form past the desktop and hide the action bar.
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            Rectangle work = Screen.FromControl(this).WorkingArea;
            if (Height > work.Height) { Height = work.Height; }
            if (Width > work.Width) { Width = work.Width; }
            if (Top < work.Top) { Top = work.Top; }
            if (Left < work.Left) { Left = work.Left; }

            LayoutHead();
            FitHeaderHeight();
            // Focus-driven auto-scroll can open the panel mid-way — start at the top.
            ScrollHeaderToTop();
        }

        // Resizing the header re-runs layout, which can leave the panel scrolled mid-way,
        // so the reset happens after the form is fully shown as well.
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            ScrollHeaderToTop();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutHead();
            FitHeaderHeight();
        }

        private void ScrollHeaderToTop()
        {
            if (_head == null) { return; }
            if (_txtSheet != null)
            {
                ActiveControl = _txtSheet;
                _txtSheet.SelectionLength = 0;
            }
            _head.AutoScrollPosition = new Point(0, 0);
        }

        // Show as much of the settings area as fits, always leaving room for the grid.
        // Content height is fixed at build time, so scroll position never skews it.
        // Once the splitter is dragged the manual height wins.
        private void FitHeaderHeight()
        {
            if (_headSizedByUser) { return; }
            if (_head == null || _head.Parent == null || _headContentHeight <= 0) { return; }

            const int gridMinHeight = 160;
            int max = _head.Parent.ClientSize.Height - gridMinHeight;
            if (max < 240) { max = 240; }
            _head.Height = Math.Min(_headContentHeight + 8, max);
        }

        private void RefreshNavButtonLabel()
        {
            if (_navButton == null || string.IsNullOrEmpty(_navButton.Caption) && string.IsNullOrEmpty(_navButton.Class))
            {
                _lblNavButton.Text = "زر بديل (اختياري): غير محدد — إن فشلت القائمة يُستخدم هذا الزر";
                _lblNavButton.ForeColor = UiTheme.InkMuted;
                return;
            }
            string cap = string.IsNullOrEmpty(_navButton.Caption) ? "(بدون نص)" : _navButton.Caption;
            _lblNavButton.Text = "زر بديل: «" + cap + "»  [" + _navButton.Class + " #" + _navButton.ClassOrdinal + "]";
            _lblNavButton.ForeColor = UiTheme.AccentDark;
        }

        private void RemoveNavSelector()
        {
            for (int i = _selectors.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_selectors[i].FieldKey, "navNewClient", StringComparison.OrdinalIgnoreCase))
                {
                    _selectors.RemoveAt(i);
                }
            }
        }

        private void UpsertNavSelector(FieldSelector sel)
        {
            RemoveNavSelector();
            if (sel != null) { _selectors.Insert(0, sel); }
            FillGridFromSelectors();
        }

        private IntPtr EnsureConnectedForPick()
        {
            IntPtr app = RequireAttachedApp();
            if (app == IntPtr.Zero || !Win32Interop.IsWindow(app))
            {
                MessageBox.Show(this,
                    "افتح برنامج التكويد أولاً ثم اضغط «اتصال تلقائي».",
                    "غير متصل", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return app;
        }

        private void PickMenuParent()
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }
            try { Win32Driver.SetForeground(app); } catch { }

            UiPickResult hit = PickCaptureForm.Run(
                this,
                "انقر عنصر قائمة الأب في شريط قوائم التكويد (مثل «التكويد»)",
                app,
                UiPickKind.MenuItem);
            if (hit.Cancelled) { Log("أُلغي التقاط قائمة الأب"); return; }
            if (!hit.Ok || string.IsNullOrWhiteSpace(hit.Caption))
            {
                Log("فشل التقاط قائمة الأب: " + hit.Note);
                return;
            }
            _txtMenuParent.Text = hit.Caption;
            Log("قائمة الأب ← «" + hit.Caption + "»");
        }

        private void PickLookupParent()
        { Log("الاستعلام ملغى من التدفق"); }


        private void PickLookupLeaf()
        { Log("الاستعلام ملغى من التدفق"); }


        private void PickPullCodesLeaf()
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }

            MessageBox.Show(this,
                "1) افتح يدوياً نفس قائمة أب إدخال العملاء (مثل «التكويد»)\n" +
                "2) اضغط موافق ثم انقر عنصر «سحب الأكواد»",
                "التقاط سحب الأكواد", MessageBoxButtons.OK, MessageBoxIcon.Information);

            try { Win32Driver.SetForeground(app); } catch { }
            UiPickResult hit = PickCaptureForm.Run(
                this,
                "انقر عنصر القائمة «سحب الأكواد»",
                app,
                UiPickKind.MenuItem);
            if (hit.Cancelled) { Log("أُلغي التقاط سحب الأكواد"); return; }
            if (!hit.Ok || string.IsNullOrWhiteSpace(hit.Caption))
            {
                Log("فشل التقاط سحب الأكواد: " + hit.Note);
                return;
            }
            if (_txtPullCodesLeaf != null) { _txtPullCodesLeaf.Text = hit.Caption; }
            Log("سحب الأكواد ← «" + hit.Caption + "»");
        }

        private void PickPullCodesField(string fieldKey)
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }

            IntPtr screen = EnsurePullCodesForPick(app);
            if (screen == IntPtr.Zero)
            {
                MessageBox.Show(this,
                    "لم تُفتح «سحب الأكواد».\nافتحها من قائمة التكويد ثم أعد المحاولة.",
                    "الشاشة غير ظاهرة", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            FieldCatalogItem item = FieldCatalog.Find(fieldKey);
            if (item == null) { Log("حقل سحب أكواد غير معروف: " + fieldKey); return; }

            SyncFieldCombo(fieldKey);
            if (!CaptureOnScreen(item, screen)) { return; }
            SavePullCodesWindowSize(screen);
            RefreshFieldCaptureStatus();
        }

        // Dry run of the duplicate check so the operator sees exactly what happens.
        private void TestLookup()
        { MessageBox.Show(this, "تم إلغاء مسار الاستعلام/OCR.", "إعداد", MessageBoxButtons.OK, MessageBoxIcon.Information); }


        // Operator clicks the live result table; we dump HWND/MSAA/clipboard/screenshot
        // so the grid-read path can be fixed from real Coding.exe output.
        private void DumpLookupGrid()
        { MessageBox.Show(this, "تم إلغاء تشخيص جدول الاستعلام.", "إعداد", MessageBoxButtons.OK, MessageBoxIcon.Information); }


        // Isolated confirm / export — same steps as after-save, without filling a client.
        private void TestPostSavePiece(bool confirmOnly)
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }

            PullGridEdits();
            SaveConfigIntoCfgObject();

            string title = confirmOnly ? "اختبار التأكيد فقط" : "اختبار التصدير فقط";
            string body = confirmOnly
                ? "سيفتح شاشة التأكيد (أو يستخدمها إن كانت مفتوحة)، ينقر الجدول ثم F2 ثم F9.\nتُترك الشاشة مفتوحة للمراجعة."
                : "سيفتح شاشة الفترة، يملأ التواريخ، يصدّر Clients*.xml ثم يكتب NIN/CODE12 في ملف العملاء الرئيسي.\nتُترك الشاشة مفتوحة للمراجعة.";
            if (MessageBox.Show(this, body, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Information)
                != DialogResult.OK)
            {
                return;
            }

            CodingPostSaveOptions opt = new CodingPostSaveOptions();
            opt.Config = _cfg;
            opt.Selectors = _selectors;
            opt.AppWindow = app;
            opt.ExportDateRangeYearToDate = true;
            opt.DoWriteBack = !confirmOnly;
            opt.SheetPathForWriteBack = _cfg.SheetPath;
            opt.SettleMs = 400;
            opt.LeaveScreensOpen = true;
            opt.Log = delegate(string m, string lvl) { Log(m); };

            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    CodingPostSaveResult res = confirmOnly
                        ? CodingPostSave.RunConfirmOnly(opt)
                        : CodingPostSave.RunExportOnly(opt);
                    BeginInvoke(new Action(delegate
                    {
                        Log((res.Ok ? "OK: " : "FAIL: ") + res.Note);
                        if (!string.IsNullOrEmpty(res.XmlPath)) { Log("XML: " + res.XmlPath); }
                        string extra = res.Ok && !string.IsNullOrEmpty(res.XmlPath)
                            ? "\n\nXML: " + res.XmlPath
                            : "";
                        MessageBox.Show(this, res.Note + extra, title,
                            MessageBoxButtons.OK,
                            res.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(delegate
                    {
                        Log(title + ": " + ex.Message);
                        MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }));
                }
            });
        }

        // One click = one control on «الإستعلام للتعديل»; auto-detect misses these.
        // Capture one of the three browser lists inside «المستند». "reject" names the
        // class the operator must NOT click, so a mix-up is caught while they are still
        // in Setup rather than during a run.
        private bool CaptureDocBrowserList(IntPtr app, IntPtr entry, string fieldKey, string prompt, string reject)
        {
            FieldCatalogItem item = FieldCatalog.Find(fieldKey);
            if (item == null) { return false; }

            IntPtr pickRoot = FindMostanadPickRoot(app, entry);
            try { Win32Driver.SetForeground(pickRoot); } catch { }

            UiPickResult hit = PickCaptureForm.Run(this, prompt, pickRoot, UiPickKind.Control);
            if (!hit.Ok || hit.Cancelled) { Log("تخطّي " + item.ArabicName); return false; }

            FieldSelector sel = UiControlPicker.BuildFieldSelector(
                hit, item.FieldKey, item.ArabicName, "", "", "المستند");
            FieldCatalog.EnforceDocButton(sel);

            string cls = sel.Class != null ? sel.Class : "";
            if (!string.IsNullOrEmpty(reject) &&
                cls.IndexOf(reject, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                MessageBox.Show(this,
                    "هذه هي " + reject + " وليست القائمة المطلوبة.\nتم تجاهل الالتقاط.",
                    "التقاط غير صحيح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Log("تخطّي " + fieldKey + " (قائمة خاطئة: " + cls + ")");
                return false;
            }

            UpsertFieldSelector(sel);
            Log("✓ " + item.ArabicName + " ← [" + cls + " #" + sel.ClassOrdinal + "]");
            return true;
        }

        private void PickLookupField(string fieldKey)
        { Log("الاستعلام ملغى: " + fieldKey); }


        private void PickPostMenuParent(TextBox target, string label)
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }
            try { Win32Driver.SetForeground(app); } catch { }

            UiPickResult hit = PickCaptureForm.Run(
                this,
                "انقر قائمة الأب لشاشة «" + label + "» (مثل «نظام تكويد العملاء»)",
                app,
                UiPickKind.MenuItem);
            if (hit.Cancelled) { Log("أُلغي التقاط قائمة أب " + label); return; }
            if (!hit.Ok || string.IsNullOrWhiteSpace(hit.Caption))
            {
                Log("فشل التقاط قائمة أب " + label + ": " + hit.Note);
                return;
            }
            if (target != null) { target.Text = hit.Caption; }
            Log("قائمة أب " + label + " ← «" + hit.Caption + "»");
        }

        private void PickPostMenuLeaf(TextBox target, string expectedLeaf)
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }

            MessageBox.Show(this,
                "1) افتح يدوياً قائمة الأب (مثل «نظام تكويد العملاء»)\n" +
                "2) اضغط موافق ثم انقر عنصر «" + expectedLeaf + "»",
                "التقاط عنصر القائمة", MessageBoxButtons.OK, MessageBoxIcon.Information);

            try { Win32Driver.SetForeground(app); } catch { }
            UiPickResult hit = PickCaptureForm.Run(
                this,
                "افتح القائمة ثم انقر «" + expectedLeaf + "»",
                app,
                UiPickKind.MenuItem);
            if (hit.Cancelled) { Log("أُلغي التقاط عنصر القائمة"); return; }
            if (!hit.Ok || string.IsNullOrWhiteSpace(hit.Caption))
            {
                Log("فشل التقاط عنصر القائمة: " + hit.Note);
                return;
            }
            if (target != null) { target.Text = hit.Caption; }
            Log("عنصر القائمة ← «" + hit.Caption + "»");
        }

        private void PickPostSaveField(string fieldKey)
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }

            FieldCatalogItem item = FieldCatalog.Find(fieldKey);
            if (item == null) { Log("حقل غير معروف: " + fieldKey); return; }

            IntPtr screen = EnsurePostSaveScreenForPick(app, fieldKey);
            if (screen == IntPtr.Zero)
            {
                string title = CodingPostSave.IsConfirmKey(fieldKey)
                    ? "تأكيد بيانات العملاء قبل إرسالها للتكويد"
                    : "عمليات التكويد خلال فترة / عملاء التكويد خلال فترة";
                DialogResult retry = MessageBox.Show(this,
                    "لم تُكتشف شاشة «" + title + "».\n\n" +
                    "افتحها يدوياً من القائمة ثم اضغط نعم لإعادة المحاولة.\n" +
                    "(الجدول في التكويد غالباً Frame مثل جدول الاستعلام — انقر وسط الجدول.)",
                    "الشاشة غير ظاهرة", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (retry != DialogResult.Yes) { return; }
                screen = EnsurePostSaveScreenForPick(app, fieldKey);
                if (screen == IntPtr.Zero)
                {
                    Log("فشل اكتشاف شاشة ما بعد الحفظ — open titles غير متطابقة");
                    return;
                }
            }

            SyncFieldCombo(fieldKey);
            if (!CaptureOnScreen(item, screen)) { return; }
            SavePostSaveWindowSize(screen, fieldKey);
            RefreshFieldCaptureStatus();
        }

        private void PickPeriodDatesWizard()
        {
            string[] keys = new string[]
            {
                CodingPostSave.PeriodFromDayKey, CodingPostSave.PeriodFromMonthKey, CodingPostSave.PeriodFromYearKey,
                CodingPostSave.PeriodToDayKey, CodingPostSave.PeriodToMonthKey, CodingPostSave.PeriodToYearKey
            };
            for (int i = 0; i < keys.Length; i++)
            {
                PickPostSaveField(keys[i]);
            }
        }

        private IntPtr EnsurePostSaveScreenForPick(IntPtr app, string fieldKey)
        {
            SaveConfigIntoCfgObject();
            // Parent≠leaf mistakes (اب → اب) made FindScreen look for the MDI caption.
            if (_cfg.App != null)
            {
                _cfg.App.ConfirmMenuPath = CodingPostSave.SanitizeMenuPath(_cfg.App.ConfirmMenuPath, "confirm");
                _cfg.App.PeriodMenuPath = CodingPostSave.SanitizeMenuPath(_cfg.App.PeriodMenuPath, "period");
                if (string.IsNullOrWhiteSpace(_cfg.App.ConfirmWindowTitle) ||
                    ArabicText.Normalize(_cfg.App.ConfirmWindowTitle) ==
                    ArabicText.Normalize("نظام تكويد العملاء") ||
                    !ArabicText.Contains(_cfg.App.ConfirmWindowTitle, "تأكيد"))
                {
                    _cfg.App.ConfirmWindowTitle = "تأكيد بيانات العملاء قبل إرسالها للتكويد";
                }
            }
            if (CodingPostSave.IsConfirmKey(fieldKey))
            {
                IntPtr open = CodingPostSave.FindOpenConfirm(app, _cfg.App, _selectors);
                if (open != IntPtr.Zero)
                {
                    SavePostSaveWindowSize(open, fieldKey);
                    return open;
                }
                IntPtr opened = CodingPostSave.EnsureConfirmScreen(
                    app, _cfg.App, _selectors, 500, delegate(string m, string l) { Log(m); });
                if (opened != IntPtr.Zero) { SavePostSaveWindowSize(opened, fieldKey); }
                return opened;
            }
            IntPtr per = CodingPostSave.FindOpenPeriod(app, _cfg.App, _selectors);
            if (per != IntPtr.Zero)
            {
                SavePostSaveWindowSize(per, fieldKey);
                return per;
            }
            IntPtr period = CodingPostSave.EnsurePeriodScreen(
                app, _cfg.App, _selectors, 500, delegate(string m, string l) { Log(m); });
            if (period != IntPtr.Zero) { SavePostSaveWindowSize(period, fieldKey); }
            return period;
        }

        private void SavePostSaveWindowSize(IntPtr screen, string fieldKey)
        {
            WindowBounds b = WindowLayout.Capture(screen);
            if (!b.IsValid) { return; }
            if (_cfg.Windows == null) { _cfg.Windows = new WindowLayoutConfig(); }
            if (CodingPostSave.IsConfirmKey(fieldKey))
            {
                _cfg.Windows.ConfirmScreen = b;
                _cfg.SaveBotConfig();
                Log("حُفظ حجم شاشة التأكيد تلقائياً: " + b.ToShort());
            }
            else if (CodingPostSave.IsPeriodKey(fieldKey))
            {
                _cfg.Windows.PeriodScreen = b;
                _cfg.SaveBotConfig();
                Log("حُفظ حجم شاشة الفترة تلقائياً: " + b.ToShort());
            }
        }

        // Snapshot the inquiry screen bounds silently: the operator never asked for it,
        // but the position map is only meaningful against the size it was captured on.
        private void SaveLookupWindowSize(IntPtr screen)
        { }


        private void PickMenuLeaf()
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }

            MessageBox.Show(this,
                "1) افتح يدوياً قائمة الأب (مثل «التكويد»)\n" +
                "2) اضغط موافق ثم انقر عنصر «إدخال بيانات العملاء» من القائمة المنسدلة",
                "التقاط عنصر القائمة", MessageBoxButtons.OK, MessageBoxIcon.Information);

            try { Win32Driver.SetForeground(app); } catch { }
            UiPickResult hit = PickCaptureForm.Run(
                this,
                "افتح القائمة ثم انقر عنصر إضافة/إدخال بيانات العملاء",
                app,
                UiPickKind.MenuItem);
            if (hit.Cancelled) { Log("أُلغي التقاط عنصر القائمة"); return; }
            if (!hit.Ok || string.IsNullOrWhiteSpace(hit.Caption))
            {
                Log("فشل التقاط عنصر القائمة: " + hit.Note);
                return;
            }
            _txtMenuLeaf.Text = hit.Caption;
            Log("عنصر القائمة ← «" + hit.Caption + "»");
        }

        private void PickNavButton()
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }
            try { Win32Driver.SetForeground(app); } catch { }

            UiPickResult hit = PickCaptureForm.Run(
                this,
                "انقر الزر الذي يفتح شاشة إضافة العملاء (بديل للقائمة)",
                app,
                UiPickKind.Button);
            if (hit.Cancelled) { Log("أُلغي التقاط الزر"); return; }
            if (!hit.Ok)
            {
                Log("فشل التقاط الزر: " + hit.Note);
                return;
            }
            if (string.IsNullOrWhiteSpace(hit.Caption) && string.IsNullOrWhiteSpace(hit.Class))
            {
                Log("الزر بلا اسم/فئة كافية للتعريف الثابت");
                return;
            }
            _navButton = hit.AsSelector != null ? hit.AsSelector : UiControlPicker.BuildNavSelector(hit);
            UpsertNavSelector(_navButton);
            RefreshNavButtonLabel();
            Log("زر بديل ← «" + _navButton.Caption + "» [" + _navButton.Class + " #" + _navButton.ClassOrdinal + "]");
        }

        private void SeedFieldCatalogRows(bool onlyIfEmpty)
        {
            if (onlyIfEmpty && _selectors.Count > 0)
            {
                FieldCatalog.EnforceDobPartsAll(_selectors);
                return;
            }
            for (int i = 0; i < FieldCatalog.All.Length; i++)
            {
                FieldCatalogItem item = FieldCatalog.All[i];
                int idx = FindSelectorIndex(item.FieldKey);
                if (idx >= 0)
                {
                    FieldCatalog.EnforceDobParts(_selectors[idx]);
                    continue;
                }
                FieldSelector s = new FieldSelector();
                s.FieldKey = item.FieldKey;
                s.FieldLabel = item.ArabicName;
                s.Label = item.ArabicName;
                s.SheetColumn = item.SheetColumn;
                s.DatePart = item.DatePart;
                s.Kind = item.Prefer == UiPickKind.Button ? "Button" : "";
                FieldCatalog.EnforceDobParts(s);
                _selectors.Add(s);
            }
            _selectors = FieldCatalog.OrderAndClean(_selectors);
        }

        private int FindSelectorIndex(string fieldKey)
        {
            for (int i = 0; i < _selectors.Count; i++)
            {
                if (string.Equals(_selectors[i].FieldKey, fieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private void UpsertFieldSelector(FieldSelector sel)
        {
            if (sel == null || string.IsNullOrEmpty(sel.FieldKey)) { return; }
            int idx = FindSelectorIndex(sel.FieldKey);
            if (idx >= 0) { _selectors[idx] = sel; }
            else { _selectors.Add(sel); }
            FillGridFromSelectors();
            RefreshFieldCaptureStatus();
            HighlightSelectorRow(sel.FieldKey);
        }

        private void SyncFieldCombo(string fieldKey)
        {
            if (_cmbFieldKey == null || string.IsNullOrEmpty(fieldKey)) { return; }
            FieldCatalogItem want = FieldCatalog.Find(fieldKey);
            if (want == null) { return; }
            for (int i = 0; i < _cmbFieldKey.Items.Count; i++)
            {
                if (string.Equals(_cmbFieldKey.Items[i].ToString(), want.Display, StringComparison.Ordinal))
                {
                    _cmbFieldKey.SelectedIndex = i;
                    return;
                }
            }
        }

        // Combo is filtered (no lookup keys) — resolve by Display, not catalog index.
        private FieldCatalogItem GetSelectedCatalogItem()
        {
            if (_cmbFieldKey == null || _cmbFieldKey.SelectedIndex < 0) { return null; }
            string display = _cmbFieldKey.SelectedItem != null ? _cmbFieldKey.SelectedItem.ToString() : "";
            if (string.IsNullOrEmpty(display)) { return null; }
            for (int i = 0; i < FieldCatalog.All.Length; i++)
            {
                if (string.Equals(FieldCatalog.All[i].Display, display, StringComparison.Ordinal))
                {
                    return FieldCatalog.All[i];
                }
            }
            return null;
        }

        private void HighlightSelectorRow(string fieldKey)
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                object v = _grid.Rows[i].Cells["key"].Value;
                if (v != null && string.Equals(v.ToString(), fieldKey, StringComparison.OrdinalIgnoreCase))
                {
                    _grid.ClearSelection();
                    _grid.Rows[i].Selected = true;
                    try { _grid.FirstDisplayedScrollingRowIndex = i; } catch { }
                    break;
                }
            }
        }

        private void RefreshFieldCaptureStatus()
        {
            int mapped = 0;
            int total = FieldCatalog.All.Length;
            int docs = 0;
            int lookup = 0;
            int post = 0;
            for (int i = 0; i < FieldCatalog.All.Length; i++)
            {
                int idx = FindSelectorIndex(FieldCatalog.All[i].FieldKey);
                if (idx < 0) { continue; }
                if (!string.IsNullOrEmpty(_selectors[idx].Class) || !string.IsNullOrEmpty(_selectors[idx].ClassOrdinal))
                {
                    mapped++;
                    string fk = FieldCatalog.All[i].FieldKey;
                    if (IsLookupFieldKey(fk)) { lookup++; }
                    if (IsPostSaveFieldKey(fk)) { post++; }
                    if (FieldCatalog.IsDocAddKey(fk) ||
                        string.Equals(fk, FieldCatalog.ChkFilesKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fk, FieldCatalog.DocPathKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fk, FieldCatalog.DocsCountKey, StringComparison.OrdinalIgnoreCase))
                    {
                        docs++;
                    }
                }
            }
            _lblFieldCaptureStatus.ForeColor = mapped > 0 ? UiTheme.AccentDark : UiTheme.InkMuted;
            _lblFieldCaptureStatus.Text = "التقاط: " + mapped + "/" + total +
                "  |  مستندات: " + docs + " (ملفات+خانات)" +
                "  |  " +
                "  |  بعد الحفظ: " + post + "/10  |  انقر مرتين على صف للالتقاط";
        }

        private IntPtr EnsureEntryForPick(IntPtr app)
        {
            string entryTitle = string.IsNullOrWhiteSpace(_txtEntryTitle.Text)
                ? "بيانات العملاء" : _txtEntryTitle.Text.Trim();
            string shortcut = string.IsNullOrWhiteSpace(_txtShortcut.Text)
                ? "^{F2}" : _txtShortcut.Text.Trim();
            string[] menuPath = BuildMenuPathFromUi();
            IntPtr entry = ClientEntry.GetEntryScanRoot(
                app, entryTitle, "ThunderRT6FormDC", _cfg.App.ChildWindow,
                shortcut, _navButton, menuPath, false, 700);
            if (entry == IntPtr.Zero)
            {
                entry = ClientEntry.FindEntryForm(app, entryTitle, "ThunderRT6FormDC");
            }
            return entry;
        }

        private static bool IsLookupFieldKey(string fieldKey)
        {
            return ClientLookup.IsLookupKey(fieldKey);
        }

        private static bool IsPostSaveFieldKey(string fieldKey)
        {
            return CodingPostSave.IsPostSaveKey(fieldKey);
        }

        private IntPtr EnsureLookupForPick(IntPtr app)
        { return IntPtr.Zero; }


        private IntPtr EnsurePullCodesForPick(IntPtr app)
        {
            SaveConfigIntoCfgObject();
            IntPtr open = CodingPostSave.FindOpenPullCodes(app, _cfg.App, _selectors);
            if (open != IntPtr.Zero)
            {
                Log("شاشة سحب الأكواد مفتوحة ← «" + Win32Driver.GetWindowTitle(open) + "»");
                return open;
            }
            return CodingPostSave.EnsurePullCodesScreen(
                app, _cfg.App, _selectors, 500, delegate(string m, string l) { Log(m); });
        }

        private void SavePullCodesWindowSize(IntPtr screen)
        {
            WindowBounds b = WindowLayout.Capture(screen);
            if (!b.IsValid) { return; }
            if (_cfg.Windows == null) { _cfg.Windows = new WindowLayoutConfig(); }
            _cfg.Windows.PullCodesScreen = b;
            _cfg.SaveBotConfig();
            Log("حُفظ حجم سحب الأكواد تلقائياً: " + b.ToShort());
        }

        private void PickSelectedCatalogField()
        {
            FieldCatalogItem item = GetSelectedCatalogItem();
            if (item == null)
            {
                Log("اختر حقلاً من القائمة أولاً");
                return;
            }
            CaptureCatalogItem(item);
        }

        private void PickSelectedGridRow()
        {
            if (_grid.SelectedRows.Count == 0)
            {
                Log("حدد صفاً في الجدول أولاً");
                return;
            }
            object keyObj = _grid.SelectedRows[0].Cells["key"].Value;
            string key = keyObj != null ? keyObj.ToString().Trim() : "";
            if (string.IsNullOrEmpty(key))
            {
                Log("الصف بلا FieldKey");
                return;
            }
            FieldCatalogItem item = FieldCatalog.Find(key);
            if (item == null)
            {
                item = new FieldCatalogItem(key, key, GuessSheetColumn(key), "",
                    "انقر عنصر الواجهة للحقل «" + key + "»",
                    key.StartsWith("btn", StringComparison.OrdinalIgnoreCase) ? UiPickKind.Button : UiPickKind.Control);
            }
            SyncFieldCombo(key);
            CaptureCatalogItem(item);
        }

        private void CaptureCatalogItem(FieldCatalogItem item)
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }

            // Lookup / post-save fields live on their own screens — not the entry form.
            bool onLookupScreen = IsLookupFieldKey(item.FieldKey);
            bool onPullCodes = PullCodes.IsKey(item.FieldKey);
            bool onPostSave = IsPostSaveFieldKey(item.FieldKey);
            IntPtr entry;
            if (onLookupScreen)
            {
                entry = EnsureLookupForPick(app);
            }
            else if (onPullCodes)
            {
                entry = EnsurePullCodesForPick(app);
            }
            else if (onPostSave)
            {
                entry = EnsurePostSaveScreenForPick(app, item.FieldKey);
            }
            else
            {
                entry = EnsureEntryForPick(app);
            }
            if (entry == IntPtr.Zero)
            {
                string hint = onLookupScreen
                    ? "افتح شاشة «الإستعلام للتعديل» أولاً (أو اضبط مسار قائمتها) ثم أعد التقاط الحقل."
                    : (onPullCodes
                        ? "افتح شاشة «سحب الأكواد» من قائمة التكويد ثم أعد التقاط الحقل."
                        : (onPostSave
                        ? "افتح شاشة التأكيد أو الفترة أولاً (من قائمة نظام تكويد العملاء) ثم أعد التقاط الحقل."
                        : "افتح شاشة إضافة العملاء أولاً (من القائمة أو اختصار Ctrl+F2) ثم أعد التقاط الحقل."));
                MessageBox.Show(this, hint, "الشاشة غير ظاهرة", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!onLookupScreen && !onPullCodes && !onPostSave) { try { Win32Driver.SetForeground(app); } catch { } }
            if (!CaptureOnScreen(item, entry)) { return; }
            if (onLookupScreen) { SaveLookupWindowSize(entry); }
            else if (onPullCodes) { SavePullCodesWindowSize(entry); }
            else if (onPostSave) { SavePostSaveWindowSize(entry, item.FieldKey); }
        }

        // Capture one catalog item against an already-resolved screen (entry or lookup).
        private bool CaptureOnScreen(FieldCatalogItem item, IntPtr entry)
        {
            if (item == null || entry == IntPtr.Zero) { return false; }
            try { Win32Driver.SetForeground(entry); } catch { }

            UiPickResult hit = PickCaptureForm.Run(
                this,
                item.Hint + "  ←  " + item.ArabicName,
                entry,
                item.Prefer);
            if (hit.Cancelled) { Log("أُلغي التقاط «" + item.ArabicName + "»"); return false; }
            if (!hit.Ok)
            {
                Log("فشل التقاط «" + item.ArabicName + "»: " + hit.Note);
                return false;
            }

            if (string.Equals(item.FieldKey, ClientLookup.GridKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.FieldKey, PullCodes.GridKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.FieldKey, CodingPostSave.ConfirmGridKey, StringComparison.OrdinalIgnoreCase))
            {
                IntPtr flex = FlexGridSurface.Find(entry, hit.Control);
                if (flex != IntPtr.Zero && flex != hit.Control)
                {
                    hit.Control = flex;
                    hit.Class = Win32Driver.GetClassName(flex);
                    hit.ControlKind = "Grid";
                    // Keep the operator click point — replay uses it, not the grid centre.
                    Log("التقاط الجدول ← MSHFlexGrid بدلاً من الإطار");
                }
            }

            string screen = Win32Driver.GetWindowText(entry);
            FieldSelector sel = UiControlPicker.BuildFieldSelector(
                hit, item.FieldKey, item.ArabicName, item.SheetColumn, item.DatePart, screen);
            FieldCatalog.EnforceDobParts(sel);
            FieldCatalog.EnforceDocButton(sel);
            FieldCatalog.EnforceCodeInput(sel);

            if (string.Equals(item.FieldKey, FieldCatalog.ChkFilesKey, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(sel.Kind, "CheckBox", StringComparison.OrdinalIgnoreCase))
                {
                    DialogResult keep = MessageBox.Show(this,
                        "تم التقاط عنصر من نوع «" + sel.Kind + "» وليس CheckBox.\n" +
                        "«ملفات» يجب أن يكون مربع اختيار.\n\nهل تعتمد هذا الالتقاط؟",
                        "تحقق من مربع ملفات", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (keep != DialogResult.Yes)
                    {
                        Log("أُلغي التقاط ملفات");
                        return false;
                    }
                }
                FieldCatalog.EnforceDocButton(sel);
            }

            if (FieldCatalog.IsDocAddKey(item.FieldKey))
            {
                if (!DocumentAttach.LooksLikeDocAddCaption(sel.Caption) &&
                    string.IsNullOrWhiteSpace(sel.Caption))
                {
                    Log("تنبيه: زر المستند بلا نص ظاهر — سيُعتمد على ClassOrdinal");
                }
                else if (!string.IsNullOrWhiteSpace(sel.Caption) &&
                         !DocumentAttach.LooksLikeDocAddCaption(sel.Caption))
                {
                    DialogResult keep = MessageBox.Show(this,
                        "الزر المضغوط نصّه «" + sel.Caption + "»\n" +
                        "وقد لا يكون زر إضافة مستند/صورة.\n\nهل تعتمد هذا الالتقاط؟",
                        "تحقق من زر المستند", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                    if (keep != DialogResult.Yes)
                    {
                        Log("أُلغي التقاط زر المستند");
                        return false;
                    }
                }
            }

            // Preserve sheet column edits if operator already set one.
            int existing = FindSelectorIndex(item.FieldKey);
            if (existing >= 0 && !string.IsNullOrEmpty(_selectors[existing].SheetColumn) &&
                string.IsNullOrEmpty(item.SheetColumn))
            {
                sel.SheetColumn = _selectors[existing].SheetColumn;
            }

            UpsertFieldSelector(sel);
            Log("تم التقاط «" + item.ArabicName + "» فقط ← Class #" + sel.ClassOrdinal +
                " Index=" + sel.Index + " pos=" + sel.PosX + "," + sel.PosY +
                " (لن يُستخدم أي حقل آخر بنفس الاسم)");
            return true;
        }

        private void PickAllFieldsWizard()
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }
            IntPtr entry = EnsureEntryForPick(app);
            if (entry == IntPtr.Zero)
            {
                MessageBox.Show(this,
                    "افتح شاشة إضافة العملاء أولاً ثم ابدأ التقاط الكل بالتتالي.",
                    "شاشة الإدخال غير ظاهرة", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult go = MessageBox.Show(this,
                "سيتم طلب التقاط كل حقل بالتتالي.\n\n" +
                "مهم لتاريخ الميلاد:\n" +
                "  ١) يوم   ٢) شهر   ٣) سنة\n" +
                "(بهذا الترتيب دائماً)\n\n" +
                "Esc يتخطى الحقل الحالي.\n\nمتابعة؟",
                "التقاط الكل بالتتالي", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (go != DialogResult.Yes) { return; }

            SeedFieldCatalogRows(false);
            int captured = 0;
            int skipped = 0;
            for (int i = 0; i < FieldCatalog.All.Length; i++)
            {
                FieldCatalogItem item = FieldCatalog.All[i];
                // Lookup / post-save controls belong to other screens — capture them separately.
                if (IsLookupFieldKey(item.FieldKey) || PullCodes.IsKey(item.FieldKey) ||
                    IsPostSaveFieldKey(item.FieldKey)) { continue; }
                SyncFieldCombo(item.FieldKey);
                try { Win32Driver.SetForeground(entry); } catch { }

                UiPickResult hit = PickCaptureForm.Run(
                    this,
                    "(" + (i + 1) + "/" + FieldCatalog.All.Length + ") " + item.Hint + "  ←  " + item.ArabicName,
                    entry,
                    item.Prefer);
                if (hit.Cancelled)
                {
                    skipped++;
                    Log("تخطّي «" + item.ArabicName + "»");
                    continue;
                }
                if (!hit.Ok)
                {
                    skipped++;
                    Log("فشل «" + item.ArabicName + "»: " + hit.Note);
                    continue;
                }

                string screen = Win32Driver.GetWindowText(entry);
                FieldSelector sel = UiControlPicker.BuildFieldSelector(
                    hit, item.FieldKey, item.ArabicName, item.SheetColumn, item.DatePart, screen);
                FieldCatalog.EnforceDobParts(sel);
                FieldCatalog.EnforceDocButton(sel);
                FieldCatalog.EnforceCodeInput(sel);
                UpsertFieldSelector(sel);
                captured++;
                Log("✓ " + item.ArabicName);
            }
            MessageBox.Show(this,
                "انتهى التقاط المتتابع.\nتم: " + captured + "\nتخطّي/فشل: " + skipped,
                "التقاط الحقول", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFieldCaptureStatus();
        }

        private void PickDocAddButtonsWizard()
        {
            IntPtr app = EnsureConnectedForPick();
            if (app == IntPtr.Zero) { return; }
            IntPtr entry = EnsureEntryForPick(app);
            if (entry == IntPtr.Zero)
            {
                MessageBox.Show(this,
                    "افتح شاشة إدخال بيانات العملاء أولاً حتى تظهر لوحة المستندات.",
                    "شاشة الإدخال غير ظاهرة", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult go = MessageBox.Show(this,
                "مسار إرفاق المستندات في التكويد:\n\n" +
                "١) التقط مربع اختيار «ملفات» فقط (CheckBox — ليس النص بجانبه)\n" +
                "٢) التقط أزرار إضافة ملف ١–٤\n" +
                "٣) داخل المستند: قائمة/خانة مسار الملف\n" +
                "٤) اختياري: زر التأكيد داخل «المستند»\n\n" +
                "التشغيل: تفعيل المربع مرة واحدة → إضافة → مسار (لكل ملف JPG).\n" +
                "Esc لتخطي عنصر.\n\nمتابعة؟",
                "التقاط مسار المستندات", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (go != DialogResult.Yes) { return; }

            int captured = 0;

            FieldCatalogItem chkItem = FieldCatalog.Find(FieldCatalog.ChkFilesKey);
            if (chkItem != null)
            {
                try { Win32Driver.SetForeground(entry); } catch { }
                UiPickResult hitChk = PickCaptureForm.Run(
                    this,
                    "مربع اختيار «ملفات» — ليس زرًا: انقر على الـ CheckBox نفسه",
                    entry,
                    UiPickKind.CheckBox);
                if (hitChk.Ok && !hitChk.Cancelled)
                {
                    string screenChk = Win32Driver.GetWindowText(entry);
                    FieldSelector selChk = UiControlPicker.BuildFieldSelector(
                        hitChk, chkItem.FieldKey, chkItem.ArabicName, "", "", screenChk);
                    FieldCatalog.EnforceDocButton(selChk);
                    UpsertFieldSelector(selChk);
                    captured++;
                    Log("✓ ملفات (CheckBox) ← [" + selChk.Class + " #" + selChk.ClassOrdinal + "] «" +
                        (selChk.Caption ?? "") + "» kind=" + selChk.Kind);
                }
                else
                {
                    Log("تخطّي مربع ملفات");
                }
            }

            for (int i = 0; i < FieldCatalog.DocAddKeys.Length; i++)
            {
                FieldCatalogItem item = FieldCatalog.Find(FieldCatalog.DocAddKeys[i]);
                if (item == null) { continue; }

                try { Win32Driver.SetForeground(entry); } catch { }
                UiPickResult hit = PickCaptureForm.Run(
                    this,
                    "(" + (i + 1) + "/4) " + item.Hint + " — أو Esc للتخطي",
                    entry,
                    UiPickKind.Button);
                if (hit.Cancelled)
                {
                    Log("تخطّي «" + item.ArabicName + "»");
                    continue;
                }
                if (!hit.Ok)
                {
                    Log("فشل «" + item.ArabicName + "»: " + hit.Note);
                    continue;
                }

                string screen = Win32Driver.GetWindowText(entry);
                FieldSelector sel = UiControlPicker.BuildFieldSelector(
                    hit, item.FieldKey, item.ArabicName, "", "", screen);
                FieldCatalog.EnforceDocButton(sel);
                UpsertFieldSelector(sel);
                captured++;
                Log("✓ خانة " + (i + 1) + " ← [" + sel.Class + " #" + sel.ClassOrdinal + "] «" +
                    (sel.Caption ?? "") + "»");
            }

            DialogResult pathPick = MessageBox.Show(this,
                "بعد تفعيل «ملفات» اضغط إضافة ملف لفتح «المستند».\n\n" +
                "مهم جداً: التقط قائمة الملفات (FileListBox) التي تظهر أسماء الملفات\n" +
                "— وليس قائمة الأقراص Drive (مثل d:\\) ولا مجلدات Dir.\n" +
                "أو اضغط لا ليُستخدم التنقل التلقائي Drive→Dir→File.",
                "قائمة الملفات داخل المستند", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (pathPick == DialogResult.Yes)
            {
                FieldCatalogItem pathItem = FieldCatalog.Find(FieldCatalog.DocPathKey);
                IntPtr pickRoot = FindMostanadPickRoot(app, entry);
                if (pathItem != null)
                {
                    try { Win32Driver.SetForeground(pickRoot); } catch { }
                    UiPickResult hitPath = PickCaptureForm.Run(
                        this,
                        "انقر قائمة الملفات (أسماء JPG) — ليس Drive d:\\",
                        pickRoot,
                        UiPickKind.Control);
                    if (hitPath.Ok && !hitPath.Cancelled)
                    {
                        FieldSelector selPath = UiControlPicker.BuildFieldSelector(
                            hitPath, pathItem.FieldKey, pathItem.ArabicName, "", "", "المستند");
                        FieldCatalog.EnforceDocButton(selPath);
                        string pcls = selPath.Class != null ? selPath.Class : "";
                        if (pcls.IndexOf("DriveListBox", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            pcls.IndexOf("DirListBox", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            MessageBox.Show(this,
                                "هذا Drive/Dir وليس قائمة الملفات.\n" +
                                "تم تجاهل الالتقاط — سيستخدم البوت التنقل التلقائي لاختيار الملف.",
                                "التقاط غير صحيح", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            Log("تخطّي docPath (Drive/Dir بالخطأ)");
                        }
                        else
                        {
                            UpsertFieldSelector(selPath);
                            captured++;
                            Log("✓ قائمة الملفات ← [" + selPath.Class + " #" + selPath.ClassOrdinal + "] kind=" + selPath.Kind);
                        }
                    }
                    else
                    {
                        Log("تخطّي مسار الملف");
                    }
                }
            }

            // The folder list drives the walk to the client folder; when the automatic
            // class scan grabs the wrong list the bot never leaves the drive root.
            DialogResult dirPick = MessageBox.Show(this,
                "التقط الآن قائمة المجلدات (Dir) داخل «المستند»\n" +
                "— القائمة التي تظهر فيها أسماء المجلدات ويتنقل فيها البوت.\n\n" +
                "اضغط لا إن كان الاكتشاف التلقائي يعمل.",
                "قائمة المجلدات داخل المستند", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (dirPick == DialogResult.Yes)
            {
                if (CaptureDocBrowserList(app, entry, FieldCatalog.DocDirListKey,
                        "انقر قائمة المجلدات (Dir) داخل المستند", "FileListBox")) { captured++; }
            }

            DialogResult drivePick = MessageBox.Show(this,
                "هل تريد التقاط قائمة الأقراص (Drive) أيضاً؟\n" +
                "اضغط لا إن كان اختيار القرص يعمل بالفعل.",
                "قائمة الأقراص داخل المستند", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (drivePick == DialogResult.Yes)
            {
                if (CaptureDocBrowserList(app, entry, FieldCatalog.DocDriveListKey,
                        "انقر قائمة الأقراص المنسدلة داخل المستند", null)) { captured++; }
            }

            DialogResult okPick = MessageBox.Show(this,
                "هل نافذة «المستند» مفتوحة الآن لتلتقط زر التأكيد؟\n" +
                "(إن لم تكن: فعّل ملفات ثم إضافة ملف يدوياً ثم موافق هنا)",
                "زر التأكيد في المستند", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (okPick == DialogResult.Yes)
            {
                FieldCatalogItem okItem = FieldCatalog.Find(FieldCatalog.BtnDocOkKey);
                IntPtr pickRoot = FindMostanadPickRoot(app, entry);

                if (okItem != null)
                {
                    try { Win32Driver.SetForeground(pickRoot); } catch { }
                    UiPickResult hitOk = PickCaptureForm.Run(
                        this, "انقر زر التأكيد/موافق/فتح داخل نافذة المستند", pickRoot, UiPickKind.Button);
                    if (hitOk.Ok && !hitOk.Cancelled)
                    {
                        FieldSelector selOk = UiControlPicker.BuildFieldSelector(
                            hitOk, okItem.FieldKey, okItem.ArabicName, "", "", "المستند");
                        FieldCatalog.EnforceDocButton(selOk);
                        UpsertFieldSelector(selOk);
                        captured++;
                        Log("✓ تأكيد المستند ← [" + selOk.Class + " #" + selOk.ClassOrdinal + "]");
                    }
                }
            }

            MessageBox.Show(this,
                "تم التقاط " + captured + " عنصر(ات) لمسار المستندات.\n" +
                "مهم: احفظ الربط. التدفق = CheckBox ملفات مرة واحدة → إضافة → مسار → تأكيد.",
                "مسار المستندات", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshFieldCaptureStatus();
        }

        private static IntPtr FindMostanadPickRoot(IntPtr app, IntPtr entryFallback)
        {
            try
            {
                List<W32WindowInfo> tops = Win32Driver.GetProcessTopWindows(Win32Driver.GetProcessId(app));
                for (int t = 0; t < tops.Count; t++)
                {
                    string title = tops[t].Title != null ? tops[t].Title : "";
                    if (title.IndexOf("المستند", StringComparison.Ordinal) >= 0 ||
                        title.Trim() == "مستند")
                    {
                        return tops[t].Handle;
                    }
                }
            }
            catch { }
            return entryFallback;
        }

        private static Label MkLabel(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.ForeColor = UiTheme.InkMuted;
            l.AutoSize = false;
            return l;
        }

        private static Label MkTitle(string text)
        {
            Label l = MkLabel(text);
            l.Font = new Font("Segoe UI Semibold", 9.5f);
            l.ForeColor = UiTheme.Ink;
            return l;
        }

        private static Label MkHint(string text)
        {
            Label l = MkLabel(text);
            l.ForeColor = UiTheme.InkMuted;
            return l;
        }

        private static TextBox MakeInput(string value)
        {
            return MakeInput(value, false);
        }

        // Latin content (paths, shortcut codes) must opt out of RTL: bidi reordering
        // otherwise moves the drive letter and braces away from where they belong.
        private static TextBox MakeInput(string value, bool latinContent)
        {
            TextBox t = new TextBox();
            t.Text = value != null ? value : "";
            if (latinContent)
            {
                t.RightToLeft = RightToLeft.No;
                t.TextAlign = HorizontalAlignment.Left;
            }
            StyleInput(t);
            return t;
        }

        // LTR label | field [| browse] row for SFTP card (no RTL mirroring).
        private void AddLtrRow(
            Control host, string caption, Control input, Button browse, ref int top, int labelW)
        {
            AddLtrRow(host, caption, input, browse, ref top, labelW, 0);
        }

        private void AddLtrRow(
            Control host, string caption, Control input, Button browse, ref int top, int labelW, int fixedInputW)
        {
            Label l = MkLabel(caption);
            l.TextAlign = ContentAlignment.MiddleLeft;
            host.Controls.Add(l);
            host.Controls.Add(input);
            if (browse != null) { host.Controls.Add(browse); }

            int rowTop = top;
            OnLayout(delegate
            {
                int w = host.ClientSize.Width;
                if (w < 200) { w = 200; }
                l.SetBounds(0, rowTop, labelW, 26);
                int browseW = browse != null ? 40 : 0;
                int gap = 8;
                int fieldX = labelW + gap;
                int fieldRight = w - (browse != null ? browseW + gap : 0);
                int fieldW = fixedInputW > 0 ? fixedInputW : Math.Max(80, fieldRight - fieldX);
                input.SetBounds(fieldX, rowTop, fieldW, 26);
                if (browse != null)
                {
                    browse.SetBounds(fieldX + fieldW + gap, rowTop, browseW, 26);
                }
            });
            top += RowHeight;
        }

        private static void StyleInput(TextBox t)
        {
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = Color.White;
            t.ForeColor = UiTheme.Ink;
        }

        // -------------------------------------------------------- responsive layout

        // Usable width of a layout host. The settings panel never lays out below
        // MinCanvasWidth, which the form's MinimumSize keeps reachable without
        // horizontal scrolling.
        private int HostWidth(Control host)
        {
            if (host == _head)
            {
                int w = _head.ClientSize.Width;
                return w < MinCanvasWidth ? MinCanvasWidth : w;
            }
            return host.ClientSize.Width;
        }

        // Column geometry: -1 spans the full width. Column 0 is the first column, which
        // in this right-to-left canvas is the right-hand one.
        private void ColumnBounds(Control host, int column, out int left, out int right)
        {
            int w = HostWidth(host);
            if (column < 0)
            {
                left = CanvasPad;
                right = w - CanvasPad;
                return;
            }
            int half = (w - (CanvasPad * 3)) / 2;
            if (column == 0)
            {
                right = w - CanvasPad;
                left = right - half;
            }
            else
            {
                left = CanvasPad;
                right = left + half;
            }
        }

        // Steps replay in registration order, so a container is always resized before
        // the controls that measure themselves against it.
        private void OnLayout(Action step)
        {
            _layout.Add(step);
        }

        // Re-entrant guard: showing or hiding the vertical scrollbar changes the panel's
        // client width, which raises ClientSizeChanged from inside this very pass.
        private void LayoutHead()
        {
            if (_head == null || _layout.Count == 0 || _layingOut) { return; }
            _layingOut = true;
            try
            {
                _head.SuspendLayout();
                for (int i = 0; i < _layout.Count; i++) { _layout[i](); }
                _head.ResumeLayout(true);
            }
            finally { _layingOut = false; }
        }

        private Panel AddSection(Control host, ref int top, int height, Color back)
        {
            Panel p = new Panel();
            p.SetBounds(CanvasPad, top, MinCanvasWidth - (CanvasPad * 2), height);
            p.BackColor = back;
            host.Controls.Add(p);
            OnLayout(delegate { p.Width = HostWidth(host) - (CanvasPad * 2); });
            top += height + 12;
            return p;
        }

        // Every line on the canvas starts at the right edge and grows leftwards, so
        // "start" means the right side and "end" means the left side.

        private void AddFromStart(Control host, Control c, int top, int startGap, int width, int height)
        {
            c.SetBounds(startGap, top, width, height);
            host.Controls.Add(c);
            OnLayout(delegate
            {
                c.SetBounds(HostWidth(host) - startGap - width, top, width, height);
            });
        }

        private void AddFromEnd(Control host, Control c, int top, int endGap, int width, int height)
        {
            c.SetBounds(endGap, top, width, height);
            host.Controls.Add(c);
        }

        private void AddFill(Control host, Control c, int top, int startGap, int endGap, int height)
        {
            c.SetBounds(endGap, top, 120, height);
            host.Controls.Add(c);
            OnLayout(delegate
            {
                c.SetBounds(endGap, top, Math.Max(60, HostWidth(host) - startGap - endGap), height);
            });
        }

        private void FillColumn(Control host, Control c, int column, int top, int height)
        {
            c.SetBounds(CanvasPad, top, 200, height);
            host.Controls.Add(c);
            OnLayout(delegate
            {
                int left, right;
                ColumnBounds(host, column, out left, out right);
                c.SetBounds(left, top, Math.Max(60, right - left), height);
            });
        }

        // Reading order right-to-left: caption, then the input, then the capture button
        // on the far left. inputWidth of 0 lets the input absorb the remaining space.
        private void AddPickRow(
            Control host, Label caption, int captionWidth,
            Control input, int inputWidth,
            Button action, int actionWidth,
            int column, int top)
        {
            host.Controls.Add(caption);
            host.Controls.Add(input);
            if (action != null) { host.Controls.Add(action); }

            OnLayout(delegate
            {
                int left, right;
                ColumnBounds(host, column, out left, out right);
                caption.SetBounds(right - captionWidth, top + 4, captionWidth, 20);

                int inputLeft = left;
                if (action != null)
                {
                    action.SetBounds(left, top - 2, actionWidth, 30);
                    inputLeft = left + actionWidth + 8;
                }
                int inputRight = right - captionWidth - 8;
                int width = inputWidth > 0 ? inputWidth : inputRight - inputLeft;
                if (width < 60) { width = 60; }
                input.SetBounds(inputRight - width, top, width, 26);
            });
        }

        private TextBox AddPathRow(Control parent, string label, string value, ref int y, bool file)
        {
            TextBox box = MakeInput(value, true);
            Button browse = UiTheme.MakeButton("…", false);
            bool isFile = file;
            browse.Click += delegate
            {
                if (isFile)
                {
                    OpenFileDialog d = new OpenFileDialog();
                    d.Filter = "Sheets|*.csv;*.xlsx|All|*.*";
                    if (d.ShowDialog(this) == DialogResult.OK) { box.Text = d.FileName; }
                }
                else
                {
                    FolderBrowserDialog d = new FolderBrowserDialog();
                    if (d.ShowDialog(this) == DialogResult.OK) { box.Text = d.SelectedPath; }
                }
            };

            AddPickRow(parent, MkLabel(label), 130, box, 0, browse, 44, -1, y);
            y += RowHeight;
            return box;
        }

        private void Log(string m)
        {
            _log.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + m + "\r\n");
        }

        private void ApplySftpSourceUi()
        {
            bool remote = _chkSftpOffline == null || !_chkSftpOffline.Checked;
            if (_txtSftpHost != null) { _txtSftpHost.Enabled = remote; }
            if (_numSftpPort != null) { _numSftpPort.Enabled = remote; }
            if (_txtSftpUser != null) { _txtSftpUser.Enabled = remote; }
        }

        private void ProbeSftpConnection()
        {
            if (!SettingsLock.EnsureUnlocked(this)) { return; }
            SaveConfigIntoCfgObject();
            if (_lblSftpProbe != null)
            {
                _lblSftpProbe.ForeColor = UiTheme.InkMuted;
                _lblSftpProbe.Text = "جاري فحص الاتصال…";
            }
            Log("SFTP probe started…");
            Cursor = Cursors.WaitCursor;
            try
            {
                SftpProbeResult r = SftpInbox.TestConnection(_cfg, delegate(string msg, string level)
                {
                    Log("[" + level + "] " + msg);
                });
                if (_lblSftpProbe != null)
                {
                    _lblSftpProbe.ForeColor = r.Ok ? UiTheme.AccentDark : Color.Firebrick;
                    _lblSftpProbe.Text = r.Ok
                        ? ("متصل — " + r.FileCount + " عنصر")
                        : ("فشل — " + (r.Note ?? ""));
                }
                MessageBox.Show(this,
                    r.Ok
                        ? ("الاتصال ناجح.\n" + (r.Note ?? "") + "\n\nالتفاصيل في سجل الإعداد بالأسفل.")
                        : ("تعذر الاتصال:\n" + (r.Note ?? "")),
                    "فحص SFTP",
                    MessageBoxButtons.OK,
                    r.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void UpdateMapCount()
        {
            _lblMapCount.Text = _selectors.Count == 0
                ? "لا يوجد ربط — اضغط تهيئة القائمة أو التقاط الكل بالتتالي"
                : ("الحقول في الجدول: " + _selectors.Count);
        }

        private void FillGridFromSelectors()
        {
            _grid.Rows.Clear();
            for (int i = 0; i < _selectors.Count; i++)
            {
                FieldSelector s = _selectors[i];
                _grid.Rows.Add(
                    s.FieldKey,
                    string.IsNullOrEmpty(s.Label) ? s.FieldLabel : s.Label,
                    s.Kind, s.Class, s.ClassOrdinal, s.SheetColumn, s.Screen);
            }
            UpdateMapCount();
            if (_lblFieldCaptureStatus != null) { RefreshFieldCaptureStatus(); }
        }

        private void ConnectAuto()
        {
            SaveConfigIntoCfgObject();
            string title = _txtTitle.Text.Trim();
            if (string.IsNullOrEmpty(title)) { title = "تكويد"; _txtTitle.Text = title; }
            string exe = _txtExe.Text.Trim();
            bool launch = _chkAutoLaunch.Checked;

            _attachedApp = FieldDiscovery.GetTargetAppWindow(title, exe, launch, _cfg.App.LaunchTimeoutSec);
            if (_attachedApp == IntPtr.Zero)
            {
                // Fallbacks used by CodingReplica / slightly different captions.
                _attachedApp = FieldDiscovery.FindRunningTarget("برنامج التكويد", exe);
            }
            if (_attachedApp == IntPtr.Zero)
            {
                _attachedApp = FieldDiscovery.FindRunningTarget("تكويد", exe);
            }

            if (_attachedApp == IntPtr.Zero || !Win32Interop.IsWindow(_attachedApp))
            {
                _lblConnectStatus.ForeColor = UiTheme.Danger;
                _lblConnectStatus.Text = "غير متصل — افتح Coding.exe (أو المحاكي) أو فعّل التشغيل التلقائي وحدّد مسار الـ EXE";
                Log("فشل الاتصال التلقائي (لا يُحفظ أي HWND — أعد المحاولة بعد فتح البرنامج)");
                return;
            }

            string winTitle = Win32Driver.GetWindowTitle(_attachedApp);
            string winClass = Win32Driver.GetClassName(_attachedApp);
            int pid = Win32Driver.GetProcessId(_attachedApp);
            _lblConnectStatus.ForeColor = UiTheme.AccentDark;
            _lblConnectStatus.Text = "متصل الآن: «" + winTitle + "»  [" + winClass + "]  pid=" + pid +
                "\n(عند إغلاق البرنامج وإعادة فتحه يُكتشف من جديد بالعنوان/EXE — ليس برقم النافذة)";
            Log("اتصال تلقائي: " + winTitle);
        }

        private IntPtr RequireAttachedApp()
        {
            if (_attachedApp != IntPtr.Zero && Win32Interop.IsWindow(_attachedApp))
            {
                return _attachedApp;
            }
            ConnectAuto();
            return _attachedApp;
        }

        private void ImportBackup()
        {
            MappingBundleResult b = SettingsBackupUi.PickAndRead(this);
            if (b == null || string.Equals(b.Note, "cancelled", StringComparison.Ordinal)) { return; }
            if (!b.Ok)
            {
                MessageBox.Show(this, b.Note, "فشل الاستيراد", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log("استيراد فشل: " + b.Note);
                return;
            }
            string sourceName = string.IsNullOrEmpty(b.SourcePath) ? "" : Path.GetFileName(b.SourcePath);

            string winSummary = "-";
            if (b.Windows != null && b.Windows.EntryForm != null && b.Windows.EntryForm.IsValid)
            {
                winSummary = "عملاء " + b.Windows.EntryForm.ToShort();
                if (b.Windows.MainApp != null && b.Windows.MainApp.IsValid)
                {
                    winSummary = "Coding " + b.Windows.MainApp.ToShort() + " | " + winSummary;
                }
            }

            string msg =
                "سيُستبدل الإعدادات والربط الحالي بما في الملف.\n\n" +
                "الملف: " + (string.IsNullOrEmpty(sourceName) ? "-" : sourceName) + "\n" +
                "صُدّر في: " + (string.IsNullOrEmpty(b.ExportedAt) ? "(غير معروف)" : b.ExportedAt) +
                "  على: " + (string.IsNullOrEmpty(b.ExportedOn) ? "-" : b.ExportedOn) + "\n" +
                "عدد الحقول: " + b.Selectors.Count + "\n" +
                "المسارات: sheet/scans/export\n" +
                "النوافذ: " + winSummary + "\n" +
                "الشاشة: " + (string.IsNullOrEmpty(b.EntryScreen) ? (b.EntryWindowTitle ?? "-") : b.EntryScreen) +
                "\n\nهل تريد المتابعة؟";
            if (MessageBox.Show(this, msg, "استيراد نسخة كاملة", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            // Apply ALL settings (paths, names, windows, settings) then refresh UI.
            MappingBundle.ApplyToConfig(_cfg, b);
            if (_txtSftp != null) { _txtSftp.Text = _cfg.SftpLocalRoot ?? ""; }
            if (_txtSftpHost != null)
            {
                _txtSftpHost.Text = string.IsNullOrEmpty(_cfg.SftpHost) ? BotConfig.DefaultSftpHost : _cfg.SftpHost;
            }
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
            if (_txtSftpInbox != null)
            {
                _txtSftpInbox.Text = string.IsNullOrEmpty(_cfg.SftpRemoteInbox) ? "kyc-imports" : _cfg.SftpRemoteInbox;
            }
            if (_chkSftpOffline != null) { _chkSftpOffline.Checked = _cfg.SftpOfflineMode; }
            ApplySftpSourceUi();
            _txtSheet.Text = _cfg.SheetPath ?? "";
            _txtScans.Text = _cfg.ScansRoot ?? "";
            _txtExport.Text = _cfg.ExportFolder ?? "";
            _txtExe.Text = _cfg.App.ExePath ?? "";
            _txtTitle.Text = _cfg.App.WindowTitlePattern ?? "";
            if (!string.IsNullOrEmpty(_cfg.App.EntryWindowTitle)) { _txtEntryTitle.Text = _cfg.App.EntryWindowTitle; }
            if (!string.IsNullOrEmpty(_cfg.App.EntryShortcut)) { _txtShortcut.Text = _cfg.App.EntryShortcut; }
            _chkAutoLaunch.Checked = _cfg.App.AutoLaunch;
            if (_cfg.App.MenuPath != null && _cfg.App.MenuPath.Length >= 1 && _txtMenuParent != null)
            {
                _txtMenuParent.Text = _cfg.App.MenuPath[0];
                if (_cfg.App.MenuPath.Length >= 2 && _txtMenuLeaf != null)
                {
                    _txtMenuLeaf.Text = _cfg.App.MenuPath[1];
                }
            }
            _cfg.App.LookupBeforeEntry = false;
            if (_chkCloseEntryAfterSave != null) { _chkCloseEntryAfterSave.Checked = _cfg.App.CloseEntryAfterSave; }
            if (_cfg.App.ConfirmMenuPath != null && _cfg.App.ConfirmMenuPath.Length >= 2)
            {
                if (_txtConfirmParent != null) { _txtConfirmParent.Text = _cfg.App.ConfirmMenuPath[0]; }
                if (_txtConfirmLeaf != null) { _txtConfirmLeaf.Text = _cfg.App.ConfirmMenuPath[1]; }
            }
            if (_cfg.App.PeriodMenuPath != null && _cfg.App.PeriodMenuPath.Length >= 2)
            {
                if (_txtPeriodParent != null) { _txtPeriodParent.Text = _cfg.App.PeriodMenuPath[0]; }
                if (_txtPeriodLeaf != null) { _txtPeriodLeaf.Text = _cfg.App.PeriodMenuPath[1]; }
            }
            if (_cfg.App.PullCodesMenuPath != null && _cfg.App.PullCodesMenuPath.Length >= 2 &&
                _txtPullCodesLeaf != null)
            {
                _txtPullCodesLeaf.Text = _cfg.App.PullCodesMenuPath[1];
            }
            if (_chkDailyExport != null && _cfg.Settings != null)
            {
                _chkDailyExport.Checked = _cfg.Settings.DailyExportEnabled;
            }
            if (_chkExportYearToDate != null && _cfg.Settings != null)
            {
                _chkExportYearToDate.Checked = _cfg.Settings.ExportDateRangeYearToDate;
            }
            ApplyDailyTimeToUi();

            if (b.Selectors != null && b.Selectors.Count > 0)
            {
                _selectors = b.Selectors;
                for (int i = 0; i < _selectors.Count; i++)
                {
                    FieldCatalog.EnforceDocButton(_selectors[i]);
                    FieldCatalog.EnforceCodeInput(_selectors[i]);
                    FieldCatalog.EnforceDateParts(_selectors[i]);
                }
            }
            FillGridFromSelectors();
            _cfg.SaveBotConfig();
            _cfg.SaveSettings();
            if (_selectors.Count > 0)
            {
                FieldDiscovery.SaveSelectors(_cfg.SelectorsPath, FieldCatalog.OrderAndClean(_selectors));
            }
            Log("تم الاستيراد الكامل: " + b.Note);
            MessageBox.Show(this,
                "تم استيراد الإعدادات كاملة (" + b.Selectors.Count + " حقل).\n" +
                "يشمل: المسارات + الأسماء + أحجام/مواضع النوافذ + الربط.\n" +
                "احفظ الربط بعد التحقق، وأعد «حفظ أحجام النوافذ» إن لزم.",
                "تم الاستيراد", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ExportBackup()
        {
            PullGridEdits();
            SaveConfigIntoCfgObject();
            TryCaptureWindowSizesQuiet();

            string confirmedTitle = _txtTitle.Text.Trim();
            string confirmedScreen = _txtEntryTitle.Text.Trim();
            if (_attachedApp != IntPtr.Zero && Win32Interop.IsWindow(_attachedApp))
            {
                string t = Win32Driver.GetWindowTitle(_attachedApp);
                if (!string.IsNullOrEmpty(t)) { confirmedTitle = t; }
            }
            try
            {
                _cfg.SaveBotConfig();
                _cfg.SaveSettings();
                if (!SettingsBackupUi.SaveLocked(this, _cfg, _selectors, confirmedTitle, confirmedScreen))
                {
                    return;
                }
                string winNote = "-";
                if (_cfg.Windows != null && _cfg.Windows.EntryForm != null && _cfg.Windows.EntryForm.IsValid)
                {
                    winNote = _cfg.Windows.EntryForm.ToShort();
                }
                Log("تم تصدير نسخة مقفلة (" + _selectors.Count + " حقل، نوافذ=" + winNote + ")");
            }
            catch (Exception ex)
            {
                Log("فشل التصدير: " + ex.Message);
                MessageBox.Show(this, ex.Message, "فشل التصدير", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Discover()
        {
            IntPtr top = RequireAttachedApp();
            if (top == IntPtr.Zero || !Win32Interop.IsWindow(top))
            {
                Log("تعذّر الاتصال ببرنامج التكويد — افتحه ثم اضغط اتصال تلقائي");
                MessageBox.Show(this,
                    "لم يتم العثور على برنامج التكويد.\n\nضع نمط العنوان (تكويد) و/أو مسار Coding.exe ثم اضغط «اتصال تلقائي».",
                    "غير متصل", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string entryTitle = string.IsNullOrWhiteSpace(_txtEntryTitle.Text)
                ? "بيانات العملاء" : _txtEntryTitle.Text.Trim();
            string shortcut = string.IsNullOrWhiteSpace(_txtShortcut.Text)
                ? "^{F2}" : _txtShortcut.Text.Trim();
            string[] menuPath = BuildMenuPathFromUi();
            IntPtr root = ClientEntry.GetEntryScanRoot(
                top, entryTitle, "ThunderRT6FormDC", _cfg.App.ChildWindow,
                shortcut, _navButton, menuPath, false, 700);
            if (root == IntPtr.Zero)
            {
                root = FieldDiscovery.GetScanRoot(top, _cfg.App.ChildWindow, true);
            }
            if (root == IntPtr.Zero)
            {
                Log("تعذّر فتح شاشة الإدخال عبر القائمة — تأكد من القائمة «التكويد → إدخال بيانات العملاء»");
                return;
            }
            Log("جذر الاكتشاف: '" + Win32Driver.GetWindowText(root) + "' [" + Win32Driver.GetClassName(root) + "]");

            if (FieldCatalog.HasClickMappedFields(_selectors))
            {
                DialogResult overwrite = MessageBox.Show(this,
                    "لديك التقاط يدوي للحقول.\n\nالاكتشاف التلقائي قد يخلط الاسم/العنوان/الرقم القومي.\n\nهل تستبدل الالتقاط اليدوي؟\n(مستحسن: لا — استخدم التقاط بالنقر فقط)",
                    "تحذير", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (overwrite != DialogResult.Yes)
                {
                    Log("أُلغي الاكتشاف — الإبقاء على الالتقاط اليدوي");
                    return;
                }
            }

            FieldMap map = FieldDiscovery.GetFieldMap(root, false);
            _fields = map.Fields;
            _selectors = FieldCatalog.OrderAndClean(ReplicaMapper.BuildFromMap(map));
            if (_selectors.Count == 0)
            {
                SeedFieldCatalogRows(false);
            }
            else
            {
                string screen = Win32Driver.GetWindowText(root);
                for (int i = 0; i < _selectors.Count; i++)
                {
                    if (string.IsNullOrEmpty(_selectors[i].Screen)) { _selectors[i].Screen = screen; }
                }
            }
            if (_navButton != null) { UpsertNavSelector(_navButton); }
            FillGridFromSelectors();
            Log("اكتشاف منظّف: " + _selectors.Count + " حقل بالترتيب القياسي");
        }

        private static string GuessKey(AppField f)
        {
            if (f.Kind == "Button")
            {
                string c = FieldDiscovery.NormalizeKey(f.Caption, true);
                if (c.IndexOf("حفظ") >= 0 || c.IndexOf("حافظ") >= 0 || c.IndexOf("save", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "btnSave";
                }
                if (c.IndexOf("اضافه") >= 0 || c.IndexOf("إضافة") >= 0) { return "btnDocAdd"; }
                return "btn" + f.ClassOrdinal;
            }
            string lab = FieldDiscovery.NormalizeKey(f.Label, true);
            if (lab.IndexOf("اسم") >= 0) { return "name"; }
            if (lab.IndexOf("عنوان") >= 0) { return "address"; }
            if (lab.IndexOf("قوم") >= 0 || lab.IndexOf("تحقيق") >= 0) { return "nationalId"; }
            if (lab.IndexOf("ميلاد") >= 0) { return "dateOfBirth"; }
            if (lab.IndexOf("جنس") >= 0) { return "gender"; }
            if (lab.IndexOf("محافظ") >= 0) { return "governorate"; }
            if (lab.IndexOf("جنسي") >= 0) { return "nationality"; }
            if (lab.IndexOf("موبايل") >= 0 || lab.IndexOf("جوال") >= 0 || lab.IndexOf("محمول") >= 0) { return "mobile"; }
            if (lab.IndexOf("وظيف") >= 0) { return "job"; }
            return "f" + f.ClassOrdinal;
        }

        private static string GuessSheetColumn(string key)
        {
            if (key == "name") { return "Name"; }
            if (key == "nationalId") { return "NationalId"; }
            if (key == "address") { return "Address"; }
            if (key == "dateOfBirth") { return "DateOfBirth"; }
            if (key == "gender") { return "GenderCode"; }
            if (key == "governorate") { return "GovernorateCode"; }
            if (key == "country") { return "CountryCode"; }
            if (key == "nationality") { return "NationalityCode"; }
            if (key == "mobile") { return "Mobile"; }
            if (key == "job") { return "Job"; }
            if (key == "brokerCode") { return "BrokerCode"; }
            return "";
        }

        private void PullGridEdits()
        {
            for (int i = 0; i < _selectors.Count && i < _grid.Rows.Count; i++)
            {
                object k = _grid.Rows[i].Cells["key"].Value;
                object v = _grid.Rows[i].Cells["col"].Value;
                object sc = _grid.Rows[i].Cells["screen"].Value;
                if (k != null) { _selectors[i].FieldKey = k.ToString(); }
                _selectors[i].SheetColumn = v != null ? v.ToString() : "";
                if (sc != null) { _selectors[i].Screen = sc.ToString(); }
            }
        }

        private void SaveMapping()
        {
            PullGridEdits();
            FieldCatalog.EnforceDobPartsAll(_selectors);
            for (int i = 0; i < _selectors.Count; i++)
            {
                FieldCatalog.EnforceDocButton(_selectors[i]);
                FieldCatalog.EnforceCodeInput(_selectors[i]);
            }
            _selectors = FieldCatalog.OrderAndClean(_selectors);
            FieldDiscovery.SaveSelectors(_cfg.SelectorsPath, _selectors);
            FillGridFromSelectors();
            Log("حُفظ الربط مرتّباً ونظيفاً → " + _cfg.SelectorsPath + " (" + _selectors.Count + ")");
            UpdateMapCount();
            if (_lblFieldCaptureStatus != null) { RefreshFieldCaptureStatus(); }
            // Keep window sizes in sync when operator finishes mapping (if windows are open).
            TryCaptureWindowSizesQuiet();
            WarnIfMapUnhealthy();
        }

        // Ordinal-only maps are the root cause of wrong-field writes — say so loudly.
        private void WarnIfMapUnhealthy()
        {
            MapHealthReport rep = MapHealth.Inspect(_selectors);
            if (rep.Issues.Count == 0) { return; }
            Log("فحص الربط: " + rep.Issues.Count + " ملاحظة — اضغط «فحص صحة الربط» للتفاصيل");
            if (!rep.HasErrors) { return; }
            MessageBox.Show(
                rep.ToArabicSummary(),
                "فحص صحة الربط",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        private void ShowMapHealth()
        {
            PullGridEdits();
            MapHealthReport rep = MapHealth.Inspect(_selectors);
            MessageBox.Show(
                rep.ToArabicSummary(),
                "فحص صحة الربط",
                MessageBoxButtons.OK,
                rep.HasErrors ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        // Open the named Coding screen, let the operator size it, then persist bounds
        // (same idea as capturing an entry field — maps break if size changes on reopen).
        private void PromptSaveWindowSize(string slot)
        {
            ConnectAuto();
            if (_attachedApp == IntPtr.Zero || !Win32Interop.IsWindow(_attachedApp))
            {
                MessageBox.Show(
                    "افتح Coding.exe أولاً ثم اضغط الزر مرة أخرى.",
                    "حفظ حجم النافذة",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            SaveConfigIntoCfgObject();
            Action<string, string> log = delegate(string m, string l) { Log(m); };
            IntPtr hwnd = WindowSizeSaver.FindOrOpen(
                _attachedApp, _cfg, _selectors, slot, 600, log);
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
                hwnd = WindowSizeSaver.FindOrOpen(_attachedApp, _cfg, _selectors, slot, 600, log);
                if (hwnd == IntPtr.Zero)
                {
                    Log("تعذر فتح «" + WindowSizeSaver.ArabicName(slot) + "» لحفظ الحجم");
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
                hwnd = WindowSizeSaver.FindOrOpen(_attachedApp, _cfg, _selectors, slot, 400, log);
            }
            if (!WindowSizeSaver.Save(_cfg, _attachedApp, hwnd, slot, log))
            {
                MessageBox.Show("تعذر قراءة حجم الشاشة.", "حفظ حجم النافذة",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WindowBounds b = WindowSizeSaver.GetSaved(_cfg, slot);
            string shortSize = b != null ? b.ToShort() : "";
            Log("حُفظ حجم «" + WindowSizeSaver.ArabicName(slot) + "» ← " + shortSize);
            MessageBox.Show(
                "تم حفظ حجم «" + WindowSizeSaver.ArabicName(slot) + "».\n" + shortSize,
                "حفظ حجم النافذة",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        // Capture Coding + currently-open child screens; keep previous bounds for closed ones.
        private void CaptureWindowSizes()
        {
            ConnectAuto();
            if (_attachedApp == IntPtr.Zero || !Win32Interop.IsWindow(_attachedApp))
            {
                MessageBox.Show(
                    "افتح Coding.exe أولاً ثم اضبط حجم النافذة واضغط مرة أخرى.",
                    "أحجام النوافذ",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string entryTitle = _txtEntryTitle != null ? _txtEntryTitle.Text.Trim() : "";
            if (string.IsNullOrEmpty(entryTitle)) { entryTitle = "بيانات العملاء"; }
            IntPtr entry = ClientEntry.FindEntryForm(_attachedApp, entryTitle, "ThunderRT6FormDC");

            int pid = Win32Driver.GetProcessId(_attachedApp);
            IntPtr mostanad = WindowLayout.FindMostanadIfOpen(pid);
            SaveConfigIntoCfgObject();
            IntPtr lookupScreen = ClientLookup.FindOpenScreen(_attachedApp, _cfg.App, _selectors);
            IntPtr confirmScreen = CodingPostSave.FindOpenConfirm(_attachedApp, _cfg.App, _selectors);
            IntPtr periodScreen = CodingPostSave.FindOpenPeriod(_attachedApp, _cfg.App, _selectors);

            WindowLayoutConfig previous = _cfg.Windows;
            WindowLayoutConfig layout = WindowLayout.CaptureFromLive(
                _attachedApp, entry, mostanad, lookupScreen, confirmScreen, periodScreen);
            if (layout.MainApp == null || !layout.MainApp.IsValid)
            {
                MessageBox.Show("تعذر قراءة حجم نافذة Coding.", "أحجام النوافذ",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            WindowLayout.KeepPreviousIfMissing(layout, previous);
            _cfg.Windows = layout;
            SaveConfigIntoCfgObject();
            _cfg.SaveBotConfig();
            WindowLayout.Active = _cfg.Windows;

            string msg = "Coding: " + layout.MainApp.ToShort();
            msg += layout.EntryForm != null && layout.EntryForm.IsValid
                ? " | العملاء: " + layout.EntryForm.ToShort()
                : " | العملاء: (استخدم «حفظ حجم إدخال العملاء»)";
            if (layout.Mostanad != null && layout.Mostanad.IsValid)
            {
                msg += " | المستند: " + layout.Mostanad.ToShort();
            }
            else
            {
                msg += " | المستند: (افتحها أثناء الحفظ إن أمكن)";
            }
            msg += layout.LookupScreen != null && layout.LookupScreen.IsValid
                ? " | الإستعلام: " + layout.LookupScreen.ToShort()
                : " | الإستعلام: (افتحها ثم أعد الحفظ)";
            msg += layout.ConfirmScreen != null && layout.ConfirmScreen.IsValid
                ? " | التأكيد: " + layout.ConfirmScreen.ToShort()
                : " | التأكيد: (افتحها ثم أعد الحفظ)";
            msg += layout.PeriodScreen != null && layout.PeriodScreen.IsValid
                ? " | الفترة: " + layout.PeriodScreen.ToShort()
                : " | الفترة: (افتحها ثم أعد الحفظ)";
            Log("حُفظت أحجام النوافذ → " + msg);
            MessageBox.Show(
                "تم حفظ أحجام النوافذ.\n" + msg +
                "\n\nعند التشغيل سيُعاد ضبط النوافذ لهذه الأحجام قبل التعبئة.",
                "أحجام النوافذ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void TryCaptureWindowSizesQuiet()
        {
            try
            {
                if (_attachedApp == IntPtr.Zero || !Win32Interop.IsWindow(_attachedApp)) { return; }
                string entryTitle = _txtEntryTitle != null ? _txtEntryTitle.Text.Trim() : "بيانات العملاء";
                if (string.IsNullOrEmpty(entryTitle)) { entryTitle = "بيانات العملاء"; }
                IntPtr entry = FieldDiscovery.FindRunningTarget(entryTitle, "");
                if (entry == IntPtr.Zero)
                {
                    List<W32WindowInfo> forms = Win32Driver.GetFormWindows(_attachedApp, 4);
                    for (int i = 0; i < forms.Count; i++)
                    {
                        string t = forms[i].Title ?? "";
                        if (t.IndexOf("بيانات العملاء", StringComparison.Ordinal) >= 0)
                        {
                            entry = forms[i].Handle;
                            break;
                        }
                    }
                }
                if (entry == IntPtr.Zero) { return; }
                int pid = Win32Driver.GetProcessId(_attachedApp);
                IntPtr mostanad = WindowLayout.FindMostanadIfOpen(pid);
                IntPtr lookupScreen = ClientLookup.FindOpenScreen(_attachedApp, _cfg.App, _selectors);
                IntPtr confirmScreen = CodingPostSave.FindOpenConfirm(_attachedApp, _cfg.App, _selectors);
                IntPtr periodScreen = CodingPostSave.FindOpenPeriod(_attachedApp, _cfg.App, _selectors);
                WindowLayoutConfig layout = WindowLayout.CaptureFromLive(
                    _attachedApp, entry, mostanad, lookupScreen, confirmScreen, periodScreen);
                if (layout.MainApp != null && layout.MainApp.IsValid &&
                    layout.EntryForm != null && layout.EntryForm.IsValid)
                {
                    WindowLayout.KeepPreviousIfMissing(layout, _cfg.Windows);
                    _cfg.Windows = layout;
                    _cfg.SaveBotConfig();
                    WindowLayout.Active = layout;
                    Log("تحديث أحجام النوافذ تلقائياً مع حفظ الربط");
                }
            }
            catch { }
        }

        private string[] BuildMenuPathFromUi()
        {
            string parent = _txtMenuParent != null ? _txtMenuParent.Text.Trim() : "";
            string leaf = _txtMenuLeaf != null ? _txtMenuLeaf.Text.Trim() : "";
            if (string.IsNullOrEmpty(parent)) { parent = "التكويد"; }
            if (string.IsNullOrEmpty(leaf)) { leaf = "إدخال بيانات العملاء"; }
            return new string[] { parent, leaf };
        }

        private string[] BuildLookupPathFromUi()
        {
            return new string[] { "التكويد", "الإستعلام للتعديل" };
        }

        private string[] BuildConfirmPathFromUi()
        {
            string parent = _txtConfirmParent != null ? _txtConfirmParent.Text.Trim() : "";
            string leaf = _txtConfirmLeaf != null ? _txtConfirmLeaf.Text.Trim() : "";
            if (string.IsNullOrEmpty(parent)) { parent = "نظام تكويد العملاء"; }
            if (string.IsNullOrEmpty(leaf)) { leaf = "تأكيد بيانات العملاء قبل إرسالها للتكويد"; }
            return CodingPostSave.SanitizeMenuPath(new string[] { parent, leaf }, "confirm");
        }

        private string[] BuildPeriodPathFromUi()
        {
            string parent = _txtPeriodParent != null ? _txtPeriodParent.Text.Trim() : "";
            string leaf = _txtPeriodLeaf != null ? _txtPeriodLeaf.Text.Trim() : "";
            if (string.IsNullOrEmpty(parent)) { parent = "نظام تكويد العملاء"; }
            if (string.IsNullOrEmpty(leaf)) { leaf = "عمليات التكويد خلال فترة"; }
            return CodingPostSave.SanitizeMenuPath(new string[] { parent, leaf }, "period");
        }

        private string[] BuildPullCodesPathFromUi()
        {
            string parent = _txtMenuParent != null ? _txtMenuParent.Text.Trim() : "";
            string leaf = _txtPullCodesLeaf != null ? _txtPullCodesLeaf.Text.Trim() : "";
            if (string.IsNullOrEmpty(parent)) { parent = "التكويد"; }
            if (string.IsNullOrEmpty(leaf)) { leaf = "سحب الأكواد"; }
            return new string[] { parent, leaf };
        }

        private void SaveConfigIntoCfgObject()
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
            _cfg.ExportFolder = _txtExport.Text.Trim();
            _cfg.App.ExePath = _txtExe.Text.Trim();
            _cfg.App.WindowTitlePattern = _txtTitle.Text.Trim();
            _cfg.App.EntryWindowTitle = _txtEntryTitle.Text.Trim();
            _cfg.App.EntryShortcut = _txtShortcut.Text.Trim();
            _cfg.App.AutoLaunch = _chkAutoLaunch.Checked;
            _cfg.App.MenuPath = BuildMenuPathFromUi();
            // Inquiry/OCR path retired — keep defaults but never enable lookup-before-entry.
            _cfg.App.LookupBeforeEntry = false;
            _cfg.App.LookupMenuPath = new string[] { "التكويد", "الإستعلام للتعديل" };
            _cfg.App.LookupWindowTitle = "الإستعلام للتعديل";
            if (_chkCloseEntryAfterSave != null) { _cfg.App.CloseEntryAfterSave = _chkCloseEntryAfterSave.Checked; }

            _cfg.App.ConfirmMenuPath = BuildConfirmPathFromUi();
            _cfg.App.ConfirmWindowTitle = _cfg.App.ConfirmMenuPath != null && _cfg.App.ConfirmMenuPath.Length >= 2
                ? _cfg.App.ConfirmMenuPath[1]
                : "تأكيد بيانات العملاء قبل إرسالها للتكويد";
            _cfg.App.PeriodMenuPath = BuildPeriodPathFromUi();
            _cfg.App.PeriodWindowTitle = _cfg.App.PeriodMenuPath != null && _cfg.App.PeriodMenuPath.Length >= 2
                ? _cfg.App.PeriodMenuPath[1]
                : "عمليات التكويد خلال فترة";
            _cfg.App.PullCodesMenuPath = BuildPullCodesPathFromUi();
            _cfg.App.PullCodesWindowTitle = _cfg.App.PullCodesMenuPath != null && _cfg.App.PullCodesMenuPath.Length >= 2
                ? _cfg.App.PullCodesMenuPath[1]
                : "سحب الأكواد";
            // Keep leaf textboxes aligned with sanitized paths after a bad pick.
            if (_txtConfirmLeaf != null) { _txtConfirmLeaf.Text = _cfg.App.ConfirmMenuPath[1]; }
            if (_txtPeriodLeaf != null) { _txtPeriodLeaf.Text = _cfg.App.PeriodMenuPath[1]; }
            if (_txtPullCodesLeaf != null) { _txtPullCodesLeaf.Text = _cfg.App.PullCodesMenuPath[1]; }

            if (_cfg.Settings == null) { _cfg.Settings = new BotSettings(); }
            PullDailyExportFromUi();
            if (_chkExportYearToDate != null)
            {
                _cfg.Settings.ExportDateRangeYearToDate = _chkExportYearToDate.Checked;
            }
        }

        private void ApplyDailyTimeToUi()
        {
            if (_cfg.Settings == null) { return; }
            if (_numExportHour != null)
            {
                int h = _cfg.Settings.DailyExportHour;
                if (h < 1 || h > 12) { h = 5; }
                _numExportHour.Value = h;
            }
            if (_numExportMinute != null)
            {
                int m = _cfg.Settings.DailyExportMinute;
                if (m < 0 || m > 59) { m = 0; }
                _numExportMinute.Value = m;
            }
            if (_cmbExportAmPm != null)
            {
                _cmbExportAmPm.SelectedIndex = _cfg.Settings.DailyExportPm ? 1 : 0;
            }
        }

        private void PullDailyExportFromUi()
        {
            if (_cfg.Settings == null) { _cfg.Settings = new BotSettings(); }
            if (_chkDailyExport != null) { _cfg.Settings.DailyExportEnabled = _chkDailyExport.Checked; }
            if (_numExportHour != null) { _cfg.Settings.DailyExportHour = (int)_numExportHour.Value; }
            if (_numExportMinute != null) { _cfg.Settings.DailyExportMinute = (int)_numExportMinute.Value; }
            if (_cmbExportAmPm != null) { _cfg.Settings.DailyExportPm = _cmbExportAmPm.SelectedIndex == 1; }
            _cfg.Settings.PostSaveConfirmExport = false;
        }

        private void SaveConfig()
        {
            SaveConfigIntoCfgObject();
            _cfg.SaveBotConfig();
            _cfg.SaveSettings();
            if (_navButton != null)
            {
                UpsertNavSelector(_navButton);
                FieldDiscovery.SaveSelectors(_cfg.SelectorsPath, _selectors);
            }
            Log("حُفظت الإعدادات (مسار قائمة + عنوان/EXE — بدون رقم نافذة)");
            ConnectAuto();
        }
    }
}
