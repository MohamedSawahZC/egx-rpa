// =============================================================================
//  UiTheme.cs - shared modern WinForms styling for RBA (RTL-safe, no layout flip).
// =============================================================================
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace RbaUi
{
    public static class UiTheme
    {
        // Deep slate + emerald accent (markets / ops — avoid generic purple/cream AI looks).
        public static readonly Color Bg = Color.FromArgb(244, 247, 250);
        public static readonly Color Surface = Color.White;
        public static readonly Color SurfaceAlt = Color.FromArgb(232, 238, 244);
        public static readonly Color Ink = Color.FromArgb(22, 36, 48);
        public static readonly Color InkMuted = Color.FromArgb(90, 110, 128);
        public static readonly Color Accent = Color.FromArgb(0, 128, 112);
        public static readonly Color AccentDark = Color.FromArgb(0, 96, 86);
        public static readonly Color AccentSoft = Color.FromArgb(220, 242, 238);
        public static readonly Color Danger = Color.FromArgb(176, 48, 48);
        public static readonly Color Line = Color.FromArgb(210, 220, 230);
        public static readonly Color HeaderTop = Color.FromArgb(18, 42, 58);
        public static readonly Color HeaderBot = Color.FromArgb(28, 68, 88);

        public static Font TitleFont
        {
            get { return new Font("Segoe UI Semibold", 16f, FontStyle.Bold); }
        }
        public static Font SubFont
        {
            get { return new Font("Segoe UI", 9.25f); }
        }
        public static Font UiFont
        {
            get { return new Font("Segoe UI", 9.5f); }
        }

        public static void ApplyForm(Form f)
        {
            f.Font = UiFont;
            f.BackColor = Bg;
            f.ForeColor = Ink;
            // RTL text without mirroring the whole layout (avoids flipped screenshots/UI).
            f.RightToLeft = RightToLeft.Yes;
            f.RightToLeftLayout = false;
            f.StartPosition = FormStartPosition.CenterScreen;
        }

        public static Panel MakeHeader(string title, string subtitle, int height)
        {
            Panel p = new Panel();
            p.Dock = DockStyle.Top;
            p.Height = height;
            p.Padding = new Padding(20, 14, 20, 12);
            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                Rectangle r = p.ClientRectangle;
                using (LinearGradientBrush br = new LinearGradientBrush(r, HeaderTop, HeaderBot, 90f))
                {
                    e.Graphics.FillRectangle(br, r);
                }
                using (Pen pen = new Pen(Color.FromArgb(40, 255, 255, 255)))
                {
                    e.Graphics.DrawLine(pen, 0, r.Height - 1, r.Width, r.Height - 1);
                }
            };

            Label t = new Label();
            t.Text = title;
            t.Font = TitleFont;
            t.ForeColor = Color.White;
            t.AutoSize = true;
            t.Location = new Point(20, 14);
            t.BackColor = Color.Transparent;
            p.Controls.Add(t);

            if (!string.IsNullOrEmpty(subtitle))
            {
                Label s = new Label();
                s.Text = subtitle;
                s.Font = SubFont;
                s.ForeColor = Color.FromArgb(200, 220, 230);
                s.AutoSize = true;
                s.Location = new Point(22, 44);
                s.BackColor = Color.Transparent;
                p.Controls.Add(s);
            }
            return p;
        }

        public static Button MakeButton(string text, bool primary)
        {
            Button b = new Button();
            b.Text = text;
            b.Height = 36;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;
            b.Font = new Font("Segoe UI Semibold", 9.25f);
            b.Margin = new Padding(0, 0, 0, 8);
            if (primary)
            {
                b.BackColor = Accent;
                b.ForeColor = Color.White;
                b.FlatAppearance.MouseOverBackColor = AccentDark;
                b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 78, 70);
            }
            else
            {
                b.BackColor = Surface;
                b.ForeColor = Ink;
                b.FlatAppearance.BorderSize = 1;
                b.FlatAppearance.BorderColor = Line;
                b.FlatAppearance.MouseOverBackColor = AccentSoft;
                b.FlatAppearance.MouseDownBackColor = SurfaceAlt;
            }
            return b;
        }

        public static void StyleGrid(DataGridView g)
        {
            g.BackgroundColor = Surface;
            g.BorderStyle = BorderStyle.None;
            g.EnableHeadersVisualStyles = false;
            g.ColumnHeadersDefaultCellStyle.BackColor = SurfaceAlt;
            g.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
            g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 9f);
            g.ColumnHeadersHeight = 34;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            g.DefaultCellStyle.BackColor = Surface;
            g.DefaultCellStyle.ForeColor = Ink;
            g.DefaultCellStyle.SelectionBackColor = AccentSoft;
            g.DefaultCellStyle.SelectionForeColor = Ink;
            g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 253);
            g.RowTemplate.Height = 28;
            g.GridColor = Line;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        }

        public static void StyleTabs(TabControl tabs)
        {
            tabs.Font = new Font("Segoe UI Semibold", 9.5f);
            tabs.Padding = new Point(14, 6);
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.ItemSize = new Size(120, 32);
        }

        public static TextBox MakeLogBox()
        {
            TextBox t = new TextBox();
            t.Multiline = true;
            t.ReadOnly = true;
            t.ScrollBars = ScrollBars.Both;
            t.BorderStyle = BorderStyle.None;
            // Log lines carry paths, class names and handles: RTL would reorder them.
            t.RightToLeft = RightToLeft.No;
            t.Font = new Font("Cascadia Mono", 9f);
            try { t.Font = new Font("Cascadia Mono", 9f); }
            catch { t.Font = new Font("Consolas", 9f); }
            t.BackColor = Color.FromArgb(16, 28, 38);
            t.ForeColor = Color.FromArgb(210, 230, 220);
            return t;
        }

        public static Panel MakeCard()
        {
            Panel p = new Panel();
            p.BackColor = Surface;
            p.Padding = new Padding(16);
            p.Paint += delegate(object sender, PaintEventArgs e)
            {
                Rectangle r = p.ClientRectangle;
                r.Width -= 1; r.Height -= 1;
                using (Pen pen = new Pen(Line))
                {
                    e.Graphics.DrawRectangle(pen, r);
                }
            };
            return p;
        }
    }
}
