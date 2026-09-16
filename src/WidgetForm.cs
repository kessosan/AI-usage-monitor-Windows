using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Runtime.InteropServices;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

namespace ClaudeUsageWidget
{
    class WidgetForm : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        const int WS_EX_TOOLWINDOW = 0x80;
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWA_BORDER_COLOR = 34;
        const int DWMWCP_ROUND = 2;

        class Palette
        {
            public Color Bg, Border, Track, TextMain, TextDim, Accent, Warn, Crit;
        }

        static readonly Palette DarkPalette = new Palette
        {
            Bg = Color.FromArgb(28, 28, 30),
            Border = Color.FromArgb(62, 62, 66),
            Track = Color.FromArgb(54, 54, 58),
            TextMain = Color.FromArgb(236, 236, 238),
            TextDim = Color.FromArgb(152, 152, 158),
            Accent = Color.FromArgb(217, 119, 87),   // terracotta Claude
            Warn = Color.FromArgb(242, 186, 75),
            Crit = Color.FromArgb(235, 87, 87),
        };

        // Teintes assombries pour rester lisibles sur fond clair.
        static readonly Palette LightPalette = new Palette
        {
            Bg = Color.FromArgb(249, 249, 251),
            Border = Color.FromArgb(214, 214, 219),
            Track = Color.FromArgb(228, 228, 233),
            TextMain = Color.FromArgb(28, 28, 32),
            TextDim = Color.FromArgb(104, 104, 112),
            Accent = Color.FromArgb(193, 95, 60),
            Warn = Color.FromArgb(176, 116, 18),
            Crit = Color.FromArgb(200, 48, 52),
        };

        static readonly Color NoData = Color.FromArgb(110, 110, 116);

        Palette theme = DarkPalette;
        Color Bg { get { return theme.Bg; } }
        Color Track { get { return theme.Track; } }
        Color TextMain { get { return theme.TextMain; } }
        Color TextDim { get { return theme.TextDim; } }
        Color Accent { get { return theme.Accent; } }
        Color Warn { get { return theme.Warn; } }

        static readonly string Title = L.T("Title");
        static readonly string ShortTitle = L.T("ShortTitle");

        class Segment
        {
            public string Text;
            public Font Font;
            public Color Color;
            public Segment(string text, Font font, Color color) { Text = text; Font = font; Color = color; }
        }

        readonly Settings settings;
        readonly float scale;
        readonly Font fontTitle, fontMain, fontBold, fontSmall;
        readonly StringFormat typo;
        readonly Bitmap measureBitmap = new Bitmap(1, 1);
        readonly Graphics measure;
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly Timer tickTimer;

        List<UsageWindow> windows;
        DateTimeOffset? updatedAt;
        string status;
        string lastReadPath;
        DateTime lastFileStamp = DateTime.MinValue;
        bool quitting, initialVisibilityApplied, dragging, dragMoved;
        ConfigForm configForm;
        Point dragOrigin;
        string trayIconKey;

        ToolStripMenuItem miShow, miCompact, miTopMost, miStartup, miLaunch, miLaunchOnStart;
        readonly List<ToolStripMenuItem> miTheme = new List<ToolStripMenuItem>();
        readonly List<ToolStripMenuItem> miOpacity = new List<ToolStripMenuItem>();

        public WidgetForm()
        {
            settings = Settings.Load();
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                scale = g.DpiX / 96f;

            fontTitle = MakeFont("Segoe UI Semibold", 13.5f, FontStyle.Bold);
            fontMain = MakeFont("Segoe UI", 12.5f, FontStyle.Regular);
            fontBold = MakeFont("Segoe UI Semibold", 12.5f, FontStyle.Bold);
            fontSmall = MakeFont("Segoe UI", 11f, FontStyle.Regular);
            typo = (StringFormat)StringFormat.GenericTypographic.Clone();
            typo.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            measure = Graphics.FromImage(measureBitmap);
            measure.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

            Text = "Claude Usage";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            theme = ResolvePalette();
            BackColor = Bg;
            TopMost = settings.TopMost;
            if (settings.OpacityPercent < 100) Opacity = settings.OpacityPercent / 100.0;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);

            // Lecture d'un petit fichier local : vérifier sa date toutes les 5 s ne coûte rien.
            tickTimer = new Timer { Interval = 5000 };
            tickTimer.Tick += delegate { OnTick(); };

            menu = BuildMenu();
            ContextMenuStrip = menu;

            tray = new NotifyIcon { ContextMenuStrip = menu, Text = Title + " · " + L.T("LoadingShort") };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleVisible(); };
            UpdateTray();
            tray.Visible = true;

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            Relayout();
            PlaceInitially();
        }

        // ---------------------------------------------------------------- fenêtre

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW; // absent de la barre des tâches et d'Alt+Tab
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyWindowFrame();
            tickTimer.Start();
            RefreshData(true);
            BeginInvoke((MethodInvoker)StartupFlow);
        }

        /// <summary>
        /// Au démarrage : assistant de mise en route si la ligne de statut n'est pas connectée,
        /// sinon lancement de Claude Code s'il n'est pas déjà ouvert (option activée par défaut).
        /// </summary>
        void StartupFlow()
        {
            StatusLineInfo statusLine = StatusLineSetup.Inspect(SelfInstall.InstalledExe);
            bool needsSetup = statusLine.State != StatusLineState.Connected || SelfInstall.InstalledCopyIsOlder();
            if (needsSetup && settings.ShowAssistantOnStart)
            {
                OpenConfig();
                return;
            }
            if (settings.LaunchClaudeOnStart) LaunchClaude(true);
        }

        /// <summary>Ouvre Claude Code dans un terminal. Si onlyIfClosed, ne fait rien quand il tourne déjà.</summary>
        void LaunchClaude(bool onlyIfClosed)
        {
            string cli = ClaudeCli.Find();
            if (cli == null)
            {
                if (!onlyIfClosed) OpenConfig(); // l'assistant indique comment l'installer
                return;
            }
            if (onlyIfClosed && ClaudeCli.IsRunning(cli)) return;
            try
            {
                ClaudeCli.Launch(cli, settings.ClaudeWorkDirOrDefault);
            }
            catch (Exception ex)
            {
                if (!onlyIfClosed) MessageBox.Show(L.F("LaunchFailed", ex.Message), Title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!initialVisibilityApplied)
            {
                initialVisibilityApplied = true;
                if (!settings.Visible)
                {
                    value = false;
                    if (!IsHandleCreated) CreateHandle();
                }
            }
            base.SetVisibleCore(value);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!quitting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true; // Alt+F4 masque le widget, l'application reste dans la zone de notification
                ToggleVisible();
                return;
            }
            SaveSettings();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                tray.Visible = false;
                if (tray.Icon != null) tray.Icon.Dispose();
                tray.Dispose();
                tickTimer.Dispose();
                menu.Dispose();
                measure.Dispose();
                measureBitmap.Dispose();
                typo.Dispose();
                fontTitle.Dispose();
                fontMain.Dispose();
                fontBold.Dispose();
                fontSmall.Dispose();
            }
            base.Dispose(disposing);
        }

        void PlaceInitially()
        {
            var p = new Point(settings.X, settings.Y);
            bool onScreen = false;
            if (settings.X != Settings.Unset && settings.Y != Settings.Unset)
                foreach (Screen screen in Screen.AllScreens)
                    if (screen.WorkingArea.Contains(p.X + S(20), p.Y + S(20))) onScreen = true;
            if (!onScreen)
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                p = new Point(wa.Right - Width - S(16), wa.Bottom - S(150));
            }
            Location = p;
            ClampToScreen();
        }

        void ClampToScreen()
        {
            Rectangle wa = Screen.FromRectangle(Bounds).WorkingArea;
            int x = Math.Max(wa.Left, Math.Min(Left, wa.Right - Width));
            int y = Math.Max(wa.Top, Math.Min(Top, wa.Bottom - Height));
            if (x != Left || y != Top) Location = new Point(x, y);
        }

        void Relayout()
        {
            Size size = settings.Compact ? CompactSize() : FullSize();
            if (ClientSize != size) ClientSize = size;
            ClampToScreen();
            Invalidate();
        }

        void ApplyWindowFrame()
        {
            if (!IsHandleCreated) return;
            try
            {
                int corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                int border = theme.Border.R | (theme.Border.G << 8) | (theme.Border.B << 16);
                DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            }
            catch (Exception)
            {
                // Windows 10 ou DWM indisponible : coins carrés, sans conséquence.
            }
        }

        Palette ResolvePalette()
        {
            switch (settings.Theme)
            {
                case Settings.ThemeLight: return LightPalette;
                case Settings.ThemeSystem: return WindowsUsesLightTheme() ? LightPalette : DarkPalette;
                default: return DarkPalette;
            }
        }

        static bool WindowsUsesLightTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value != 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        void ApplyTheme()
        {
            Palette next = ResolvePalette();
            if (next == theme) return;
            theme = next;
            BackColor = Bg;
            ApplyWindowFrame();
            Invalidate();
        }

        void SetTheme(string value)
        {
            settings.Theme = value;
            SaveSettings();
            ApplyTheme();
        }

        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // Le passage clair/sombre de Windows arrive dans la catégorie General.
            if (e.Category != UserPreferenceCategory.General || settings.Theme != Settings.ThemeSystem || !IsHandleCreated) return;
            BeginInvoke((MethodInvoker)ApplyTheme);
        }

        void ToggleVisible()
        {
            if (Visible)
                Hide();
            else
            {
                Show();
                ClampToScreen();
            }
            settings.Visible = Visible;
            SaveSettings();
        }

        void SaveSettings()
        {
            settings.X = Left;
            settings.Y = Top;
            settings.Save();
        }

        // ---------------------------------------------------------------- souris

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragMoved = false;
            dragOrigin = e.Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;

            var p = new Point(Left + e.X - dragOrigin.X, Top + e.Y - dragOrigin.Y);
            Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
            int snap = S(14);
            if (Math.Abs(p.X - wa.Left) < snap) p.X = wa.Left;
            if (Math.Abs(wa.Right - (p.X + Width)) < snap) p.X = wa.Right - Width;
            if (Math.Abs(p.Y - wa.Top) < snap) p.Y = wa.Top;
            if (Math.Abs(wa.Bottom - (p.Y + Height)) < snap) p.Y = wa.Bottom - Height;
            if (p != Location) dragMoved = true;
            Location = p;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            if (dragMoved)
                SaveSettings();
            else if (windows == null && e.Button == MouseButtons.Left)
                OpenConfig(); // clic sur « En attente de Claude Code : cliquez ici »
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) ToggleCompact();
        }

        void ToggleCompact()
        {
            settings.Compact = !settings.Compact;
            Relayout();
            SaveSettings();
        }

        // ---------------------------------------------------------------- données

        /// <summary>Relit le fichier de données s'il a changé (ou systématiquement si force).</summary>
        void RefreshData(bool force)
        {
            string path = settings.DataPath;
            DateTime stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
            if (!force && path == lastReadPath && stamp == lastFileStamp) return;
            lastReadPath = path;
            lastFileStamp = stamp;

            ReadResult result = UsageStore.Read(path);
            if (result.Snapshot != null)
            {
                windows = result.Snapshot.Windows;
                updatedAt = result.Snapshot.UpdatedAt;
                status = null;
            }
            else
            {
                status = result.Error;
                if (stamp == DateTime.MinValue)
                {
                    windows = null;
                    updatedAt = null;
                }
                else
                {
                    lastFileStamp = DateTime.MinValue; // fichier présent mais illisible : nouvel essai au prochain tick
                }
            }
            Relayout();
            UpdateTray();
        }

        void OpenConfig()
        {
            if (configForm != null)
            {
                configForm.Activate();
                return;
            }
            bool restart;
            using (configForm = new ConfigForm(settings))
            {
                configForm.TopMost = settings.TopMost;
                if (configForm.ShowDialog() == DialogResult.OK)
                {
                    SaveSettings();
                    RefreshData(true);
                }
                restart = configForm.RestartRequested;
            }
            configForm = null;

            if (restart)
            {
                // Le widget vient d'être installé : la copie installée prend le relais.
                SelfInstall.StartInstalled();
                quitting = true;
                Close();
            }
        }

        void OnTick()
        {
            RefreshData(false);
            // Le compte à rebours peut s'allonger (ex. « 1 j 0 h » -> « 23 h 59 ») : on élargit si besoin, sans jamais rétrécir entre deux lectures.
            if (!settings.Compact && FullSize().Width > ClientSize.Width) Relayout();
            Invalidate();   // comptes à rebours et passage des remises à zéro
            UpdateTray();
        }

        UsageWindow Find(string key)
        {
            if (windows == null) return null;
            foreach (UsageWindow w in windows)
                if (w.Key == key) return w;
            return null;
        }

        // ---------------------------------------------------------------- dessin

        Size FullSize()
        {
            int rows = windows == null ? 0 : windows.Count;
            int h = S(10) + S(24);
            if (rows == 0)
                h += S(32);
            else
                h += rows * S(48) + (status != null ? S(32) : 0);
            return new Size((int)Math.Ceiling(FullContentWidth()) + 2 * S(12), h + S(4));
        }

        /// <summary>Largeur utile : 228 px au minimum, davantage si les textes de la langue l'exigent.</summary>
        float FullContentWidth()
        {
            float gap = S(16);
            float width = Math.Max(S(228), Measure(measure, Title, fontTitle) + gap + Measure(measure, UpdatedLabel(), fontSmall));
            if (windows != null)
                foreach (UsageWindow w in windows)
                {
                    width = Math.Max(width, Measure(measure, w.Label, fontMain) + gap + Measure(measure, FormatPercent(w.CurrentPercent), fontBold));
                    width = Math.Max(width, Measure(measure, ResetLabel(w), fontSmall) + gap); // marge pour le compte à rebours
                }
            return width;
        }

        Size CompactSize()
        {
            float w = 0;
            foreach (Segment seg in CompactSegments())
                w += Measure(measure, seg.Text, seg.Font);
            return new Size((int)Math.Ceiling(w) + 2 * S(12), S(30));
        }

        List<Segment> CompactSegments()
        {
            var list = new List<Segment>();
            list.Add(new Segment(ShortTitle + "   ", fontBold, TextMain));
            if (windows == null)
            {
                list.Add(new Segment(status != null ? L.T("ErrorShort") : L.T("LoadingShort"), fontSmall, status != null ? Warn : TextDim));
                return list;
            }

            UsageWindow session = Find("five_hour");
            UsageWindow week = Find("seven_day");
            if (session != null)
            {
                list.Add(new Segment(L.T("CompactSession") + "  ", fontSmall, TextDim));
                list.Add(new Segment(FormatPercent(session.CurrentPercent), fontBold, SeverityColor(session.CurrentPercent)));
                if (session.ResetsAt.HasValue && !session.IsReset)
                    list.Add(new Segment("  → " + FormatWhen(session.ResetsAt.Value.LocalDateTime, true), fontSmall, TextDim));
            }
            if (week != null)
            {
                list.Add(new Segment((session != null ? "     " : "") + L.T("CompactWeek") + "  ", fontSmall, TextDim));
                list.Add(new Segment(FormatPercent(week.CurrentPercent), fontBold, SeverityColor(week.CurrentPercent)));
            }
            if (status != null)
                list.Add(new Segment("  !", fontBold, Warn));
            return list;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Bg);

            if (settings.Compact)
                PaintCompact(g);
            else
                PaintFull(g);
        }

        void PaintCompact(Graphics g)
        {
            float x = S(12);
            float baseline = ClientSize.Height / 2f + Ascent(fontBold) / 2f - S(1);
            foreach (Segment seg in CompactSegments())
            {
                DrawText(g, seg.Text, seg.Font, seg.Color, x, baseline);
                x += Measure(g, seg.Text, seg.Font);
            }
        }

        void PaintFull(Graphics g)
        {
            int pad = S(12);
            int width = ClientSize.Width;
            float y = S(10);

            float baseline = y + Ascent(fontTitle);
            DrawText(g, Title, fontTitle, TextMain, pad, baseline);
            DrawTextRight(g, UpdatedLabel(), fontSmall, TextDim, width - pad, baseline);
            y += S(24);

            if (windows == null || windows.Count == 0)
            {
                DrawWrapped(g, status ?? L.T("Loading"), status == null ? TextDim : Warn, pad, y, width - 2 * pad);
                return;
            }

            foreach (UsageWindow w in windows)
            {
                double percent = w.CurrentPercent;
                Color color = SeverityColor(percent);
                float rowBaseline = y + Ascent(fontBold);
                DrawText(g, w.Label, fontMain, TextMain, pad, rowBaseline);
                DrawTextRight(g, FormatPercent(percent), fontBold, color, width - pad, rowBaseline);

                float barY = y + S(20);
                float barW = width - 2 * pad;
                FillRounded(g, Track, pad, barY, barW, S(5), S(2.5f));
                float fill = (float)(barW * Math.Min(percent, 100) / 100.0);
                if (fill > 0) FillRounded(g, color, pad, barY, Math.Max(fill, S(5)), S(5), S(2.5f));

                DrawText(g, ResetLabel(w), fontSmall, TextDim, pad, y + S(30) + Ascent(fontSmall));
                y += S(48);
            }

            if (status != null)
                DrawWrapped(g, status, Warn, pad, y, width - 2 * pad);
        }

        void DrawText(Graphics g, string text, Font font, Color color, float x, float baseline)
        {
            using (var brush = new SolidBrush(color))
                g.DrawString(text, font, brush, x, baseline - Ascent(font), typo);
        }

        void DrawTextRight(Graphics g, string text, Font font, Color color, float right, float baseline)
        {
            DrawText(g, text, font, color, right - Measure(g, text, font), baseline);
        }

        void DrawWrapped(Graphics g, string text, Color color, float x, float y, float width)
        {
            using (var brush = new SolidBrush(color))
            using (var format = new StringFormat { Trimming = StringTrimming.EllipsisWord })
                g.DrawString(text, fontSmall, brush, new RectangleF(x - S(2), y, width + S(4), S(30)), format);
        }

        float Measure(Graphics g, string text, Font font)
        {
            return string.IsNullOrEmpty(text) ? 0 : g.MeasureString(text, font, PointF.Empty, typo).Width;
        }

        static float Ascent(Font font)
        {
            FontFamily family = font.FontFamily;
            return font.Size * family.GetCellAscent(font.Style) / family.GetEmHeight(font.Style);
        }

        static void FillRounded(Graphics g, Color color, float x, float y, float w, float h, float radius)
        {
            if (w <= 0 || h <= 0) return;
            float d = Math.Min(radius * 2, Math.Min(w, h));
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(color))
            {
                path.AddArc(x, y, d, d, 180, 90);
                path.AddArc(x + w - d, y, d, d, 270, 90);
                path.AddArc(x + w - d, y + h - d, d, d, 0, 90);
                path.AddArc(x, y + h - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
            }
        }

        Font MakeFont(string family, float px, FontStyle fallbackStyle)
        {
            var font = new Font(family, S(px), FontStyle.Regular, GraphicsUnit.Pixel);
            if (font.Name == family) return font;
            font.Dispose();
            return new Font("Segoe UI", S(px), fallbackStyle, GraphicsUnit.Pixel);
        }

        int S(int v) { return (int)Math.Round(v * scale); }
        float S(float v) { return v * scale; }

        // ---------------------------------------------------------------- formats

        Color SeverityColor(double percent)
        {
            return SeverityColor(percent, theme);
        }

        static Color SeverityColor(double percent, Palette palette)
        {
            if (percent >= 90) return palette.Crit;
            if (percent >= 75) return palette.Warn;
            return palette.Accent;
        }

        static string FormatPercent(double percent)
        {
            return L.Percent(percent);
        }

        string UpdatedLabel()
        {
            if (!updatedAt.HasValue) return "";
            DateTime local = updatedAt.Value.LocalDateTime;
            return L.F("Updated", local.Date == DateTime.Today ? L.Time(local) : L.MonthDay(local) + " " + L.Time(local));
        }

        static string ResetLabel(UsageWindow w)
        {
            if (!w.ResetsAt.HasValue)
                return w.Key == "five_hour" ? L.T("NoSession") : "";
            DateTime local = w.ResetsAt.Value.LocalDateTime;
            if (w.IsReset)
                return L.F("ResetDone", FormatWhen(local, false));
            return L.F("ResetsIn", FormatWhen(local, false), FormatSpan(local - DateTime.Now));
        }

        static string FormatWhen(DateTime local, bool compact)
        {
            string time = L.Time(local);
            if (local.Date == DateTime.Today) return time;
            if (local.Date == DateTime.Today.AddDays(1)) return L.F(compact ? "TomorrowShort" : "Tomorrow", time);
            return (compact ? local.ToString("ddd", L.Format) : L.WeekdayMonthDay(local)) + " " + time;
        }

        static string FormatSpan(TimeSpan span)
        {
            int totalMinutes = (int)Math.Ceiling(span.TotalMinutes);
            int days = totalMinutes / 1440;
            int hours = totalMinutes % 1440 / 60;
            int minutes = totalMinutes % 60;
            if (days > 0) return L.F("SpanDaysHours", days, hours);
            if (hours > 0) return L.F("SpanHoursMinutes", hours, minutes);
            return L.F("SpanMinutes", minutes);
        }

        // ---------------------------------------------------------------- zone de notification

        void UpdateTray()
        {
            UsageWindow session = Find("five_hour");
            string text = session != null ? ((int)Math.Round(session.CurrentPercent)).ToString(CultureInfo.InvariantCulture) : "?";
            // L'icône garde les teintes vives quel que soit le thème du widget.
            Color color = session != null ? SeverityColor(session.CurrentPercent, DarkPalette) : NoData;

            string key = text + "|" + color.ToArgb();
            if (key != trayIconKey)
            {
                Icon old = tray.Icon;
                tray.Icon = RenderTrayIcon(text, color);
                if (old != null) old.Dispose();
                trayIconKey = key;
            }

            string tip;
            if (windows == null)
                tip = Title + " · " + (status ?? L.T("LoadingShort"));
            else
            {
                tip = Title;
                if (session != null)
                {
                    tip += " · " + L.F("TipSession", FormatPercent(session.CurrentPercent));
                    if (session.ResetsAt.HasValue && !session.IsReset)
                        tip += " (→ " + FormatWhen(session.ResetsAt.Value.LocalDateTime, true) + ")";
                }
                UsageWindow week = Find("seven_day");
                if (week != null) tip += "\n" + L.F("TipWeek", FormatPercent(week.CurrentPercent));
                if (status != null) tip += "\n" + status;
            }
            tray.Text = tip.Length > 63 ? tip.Substring(0, 62) + "…" : tip;
        }

        Icon RenderTrayIcon(string text, Color color)
        {
            int size = SystemInformation.SmallIconSize.Width;
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.Clear(Color.Transparent);
                FillRounded(g, color, 0, 0, size, size, size / 4f);

                float px = size * (text.Length >= 3 ? 0.58f : text.Length == 2 ? 0.74f : 0.86f);
                Font font = null;
                try
                {
                    // Réduit la police tant que le texte déborde de l'icône.
                    while (true)
                    {
                        font = new Font("Segoe UI Semibold", px, FontStyle.Regular, GraphicsUnit.Pixel);
                        if (px <= 6 || g.MeasureString(text, font, PointF.Empty, typo).Width <= size) break;
                        font.Dispose();
                        px -= 0.5f;
                    }
                    Color ink = color == DarkPalette.Warn ?Color.FromArgb(30, 30, 30) : Color.White;
                    using (var brush = new SolidBrush(ink))
                    using (var format = new StringFormat(typo) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        g.DrawString(text, font, brush, new RectangleF(0, size * 0.04f, size, size), format);
                }
                finally
                {
                    if (font != null) font.Dispose();
                }

                IntPtr handle = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(handle).Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        // ---------------------------------------------------------------- menu

        ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();

            miShow = new ToolStripMenuItem(L.T("MenuShow"), null, delegate { ToggleVisible(); });
            var miRefresh = new ToolStripMenuItem(L.T("MenuRefresh"), null, delegate { RefreshData(true); });
            var miConfig = new ToolStripMenuItem(L.T("MenuSettings"), null, delegate { OpenConfig(); });
            miLaunch = new ToolStripMenuItem(L.T("LaunchClaude"), null, delegate { LaunchClaude(false); });
            miLaunchOnStart = new ToolStripMenuItem(L.T("MenuLaunchOnStart"), null, delegate
            {
                settings.LaunchClaudeOnStart = !settings.LaunchClaudeOnStart;
                SaveSettings();
            });
            miCompact = new ToolStripMenuItem(L.T("MenuCompact"), null, delegate { ToggleCompact(); });
            miTopMost = new ToolStripMenuItem(L.T("MenuTopMost"), null, delegate
            {
                settings.TopMost = !settings.TopMost;
                TopMost = settings.TopMost;
                SaveSettings();
            });

            var opacityMenu = new ToolStripMenuItem(L.T("MenuOpacity"));
            foreach (int value in new[] { 100, 90, 80, 70, 50 })
            {
                int v = value;
                var item = new ToolStripMenuItem(v + " %", null, delegate
                {
                    settings.OpacityPercent = v;
                    Opacity = v / 100.0;
                    SaveSettings();
                }) { Tag = v };
                miOpacity.Add(item);
                opacityMenu.DropDownItems.Add(item);
            }

            var themeMenu = new ToolStripMenuItem(L.T("MenuTheme"));
            string[][] themes =
            {
                new[] { Settings.ThemeDark, L.T("ThemeDark") },
                new[] { Settings.ThemeLight, L.T("ThemeLight") },
                new[] { Settings.ThemeSystem, L.T("ThemeSystem") },
            };
            foreach (string[] entry in themes)
            {
                string value = entry[0];
                var item = new ToolStripMenuItem(entry[1], null, delegate { SetTheme(value); }) { Tag = value };
                miTheme.Add(item);
                themeMenu.DropDownItems.Add(item);
            }

            miStartup = new ToolStripMenuItem(L.T("MenuStartup"), null, delegate
            {
                try { Startup.Set(!Startup.IsEnabled()); }
                catch (Exception ex) { MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            });

            var miQuit = new ToolStripMenuItem(L.T("MenuQuit"), null, delegate
            {
                quitting = true;
                Close();
            });

            m.Items.AddRange(new ToolStripItem[]
            {
                miShow, miRefresh, miLaunch, new ToolStripSeparator(),
                miConfig, miCompact, miTopMost, themeMenu, opacityMenu, miStartup, miLaunchOnStart,
                new ToolStripSeparator(), miQuit,
            });

            m.Opening += delegate
            {
                miShow.Checked = Visible;
                miCompact.Checked = settings.Compact;
                miTopMost.Checked = settings.TopMost;
                foreach (ToolStripMenuItem item in miTheme) item.Checked = (string)item.Tag == settings.Theme;
                foreach (ToolStripMenuItem item in miOpacity) item.Checked = (int)item.Tag == settings.OpacityPercent;
                miLaunchOnStart.Checked = settings.LaunchClaudeOnStart;
                try { miStartup.Checked = Startup.IsEnabled(); }
                catch (Exception) { miStartup.Checked = false; }
            };
            return m;
        }
    }
}
