using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ClaudeUsageWidget
{
    /// <summary>
    /// Fenêtre « Configuration » : assistant de mise en route (Claude Code, ligne de statut, données),
    /// options de lancement et réglages avancés.
    /// </summary>
    class ConfigForm : Form
    {
        const int FieldWidth = 420;

        static readonly Color Ok = Color.FromArgb(16, 124, 16);
        static readonly Color Ko = Color.FromArgb(196, 43, 28);
        static readonly Color Pending = Color.FromArgb(157, 93, 0);

        readonly Settings settings;
        readonly Label cliIcon, cliText, slIcon, slText, dataIcon, dataText, lblState;
        readonly Button btnInstall, btnStatusLine, btnLaunch;
        readonly CheckBox chkLaunchOnStart, chkShowAssistant;
        readonly TextBox txtWorkDir, txtPath, txtStatusLine;
        readonly System.Windows.Forms.Timer refreshTimer;

        string cliPath;
        StatusLineInfo statusLine;

        /// <summary>Le widget doit redémarrer depuis son emplacement d'installation.</summary>
        public bool RestartRequested { get; private set; }

        public ConfigForm(Settings settings)
        {
            this.settings = settings;

            Text = L.T("CfgTitle");
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true; // retrouvable même si le widget est masqué
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(14);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
            };
            Controls.Add(layout);

            // --- Mise en route
            layout.Controls.Add(Heading(L.T("CfgSetupHeading"), 0));
            layout.Controls.Add(Hint(L.T("CfgSourceHint")));

            var steps = new TableLayoutPanel { ColumnCount = 3, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 4, 0, 4) };
            steps.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            steps.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, FieldWidth));
            steps.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.Controls.Add(steps);

            btnInstall = StepButton(L.T("SetupInstall"), delegate { ClaudeCli.OpenInstallDocs(); });
            btnStatusLine = StepButton(L.T("Connect"), delegate { OnStatusLineButton(); });
            btnLaunch = StepButton(L.T("LaunchClaude"), delegate { LaunchClaude(); });
            AddStep(steps, L.T("SetupCli"), btnInstall, out cliIcon, out cliText);
            AddStep(steps, L.T("SetupStatusLine"), btnStatusLine, out slIcon, out slText);
            AddStep(steps, L.T("SetupData"), btnLaunch, out dataIcon, out dataText);

            chkLaunchOnStart = new CheckBox { Text = L.T("OptLaunchOnStart"), AutoSize = true, Checked = settings.LaunchClaudeOnStart, Margin = new Padding(0, 6, 0, 2) };
            layout.Controls.Add(chkLaunchOnStart);

            txtWorkDir = new TextBox { Width = FieldWidth - 80, Text = settings.ClaudeWorkDirOrDefault };
            var btnWorkDir = new Button { Text = L.T("Browse"), AutoSize = true };
            btnWorkDir.Click += delegate { BrowseFolder(); };
            var lblWorkDir = new Label { Text = L.T("OptWorkDir"), AutoSize = true, Margin = new Padding(0, 7, 4, 0) };
            layout.Controls.Add(Row(lblWorkDir, txtWorkDir, btnWorkDir));

            chkShowAssistant = new CheckBox { Text = L.T("OptShowAssistant"), AutoSize = true, Checked = settings.ShowAssistantOnStart, Margin = new Padding(0, 2, 0, 2) };
            layout.Controls.Add(chkShowAssistant);

            // --- Avancé
            layout.Controls.Add(Heading(L.T("CfgAdvancedHeading"), 16));
            layout.Controls.Add(new Label { Text = L.T("CfgFileHeading"), AutoSize = true, Margin = new Padding(0, 2, 0, 0) });
            txtPath = new TextBox { Width = FieldWidth, Text = settings.DataPath };
            var btnBrowse = new Button { Text = L.T("Browse"), AutoSize = true };
            btnBrowse.Click += delegate { BrowseDataFile(); };
            var btnDefault = new Button { Text = L.T("Default"), AutoSize = true };
            btnDefault.Click += delegate { txtPath.Text = UsageStore.DefaultPath(); };
            layout.Controls.Add(Row(txtPath, btnBrowse, btnDefault));

            lblState = new Label { AutoSize = true, MaximumSize = new Size(FieldWidth, 0), Margin = new Padding(0, 7, 0, 0) };
            var btnCheck = new Button { Text = L.T("Check"), AutoSize = true };
            btnCheck.Click += delegate { UpdateDataFileState(); };
            layout.Controls.Add(Row(btnCheck, lblState));

            layout.Controls.Add(Hint(L.T("CfgStatusLineHint")));
            txtStatusLine = Snippet(4);
            layout.Controls.Add(Row(txtStatusLine, CopyButton(txtStatusLine)));
            layout.Controls.Add(Hint(L.T("CfgExistingHint")));

            // --- Boutons
            var btnOk = new Button { Text = L.T("Save"), AutoSize = true, MinimumSize = new Size(90, 0) };
            btnOk.Click += delegate { Save(); };
            var btnCancel = new Button { Text = L.T("Cancel"), AutoSize = true, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };
            var buttons = Row(btnCancel, btnOk);
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Right;
            buttons.Margin = new Padding(0, 14, 0, 0);
            layout.Controls.Add(buttons);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            UpdateSnippet();
            RefreshSetup();
            UpdateDataFileState();

            // Suit l'arrivée des premières données pendant que la fenêtre est ouverte.
            refreshTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            refreshTimer.Tick += delegate { RefreshDataStep(); };
            refreshTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) refreshTimer.Dispose();
            base.Dispose(disposing);
        }

        // ------------------------------------------------------------------ construction

        Label Heading(string text, int top)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(Font.FontFamily, Font.Size * 1.1f, FontStyle.Bold),
                Margin = new Padding(0, top, 0, 3),
            };
        }

        static Label Hint(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(FieldWidth + 160, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(0, 0, 0, 4),
            };
        }

        static Button StepButton(string text, EventHandler onClick)
        {
            var button = new Button { Text = text, AutoSize = true, MinimumSize = new Size(120, 0), Anchor = AnchorStyles.Left | AnchorStyles.Top };
            button.Click += onClick;
            return button;
        }

        void AddStep(TableLayoutPanel table, string title, Button button, out Label icon, out Label detail)
        {
            icon = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Symbol", Font.Size * 1.2f, FontStyle.Bold),
                Margin = new Padding(0, 4, 6, 0),
            };
            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0, 4, 8, 6),
            };
            panel.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = Padding.Empty });
            detail = new Label { AutoSize = true, MaximumSize = new Size(FieldWidth - 10, 0), ForeColor = SystemColors.GrayText, Margin = Padding.Empty };
            panel.Controls.Add(detail);
            button.Margin = new Padding(0, 4, 0, 6);

            int row = table.RowCount;
            table.RowCount = row + 1;
            table.Controls.Add(icon, 0, row);
            table.Controls.Add(panel, 1, row);
            table.Controls.Add(button, 2, row);
        }

        TextBox Snippet(int lines)
        {
            var box = new TextBox
            {
                Width = FieldWidth + 70,
                ReadOnly = true,
                Multiline = lines > 1,
                WordWrap = false,
                Font = new Font("Consolas", Font.SizeInPoints),
                BackColor = SystemColors.Window,
            };
            if (lines > 1) box.Height = (int)Math.Ceiling(box.Font.GetHeight() * lines) + 8;
            return box;
        }

        static Button CopyButton(TextBox source)
        {
            var button = new Button { Text = L.T("Copy"), AutoSize = true };
            button.Click += delegate
            {
                try
                {
                    Clipboard.SetText(source.Text);
                    button.Text = L.T("Copied");
                }
                catch (Exception)
                {
                    button.Text = L.T("CopyFailed");
                }
            };
            return button;
        }

        static FlowLayoutPanel Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(0, 2, 0, 2),
            };
            row.Controls.AddRange(controls);
            return row;
        }

        static void SetStep(Label icon, Label detail, string glyph, Color color, string text)
        {
            icon.Text = glyph;
            icon.ForeColor = color;
            detail.Text = text;
        }

        // ------------------------------------------------------------------ mise en route

        void RefreshSetup()
        {
            cliPath = ClaudeCli.Find();
            if (cliPath != null)
                SetStep(cliIcon, cliText, "✔", Ok, L.F("SetupCliFound", cliPath));
            else
                SetStep(cliIcon, cliText, "✖", Ko, L.T("SetupCliMissing"));
            btnInstall.Visible = cliPath == null;

            statusLine = StatusLineSetup.Inspect(SelfInstall.InstalledExe);
            string duplicates = statusLine.HasDuplicateKeys ? L.T("SlDuplicate") : "";
            switch (statusLine.State)
            {
                case StatusLineState.Connected:
                    if (SelfInstall.InstalledCopyIsOlder())
                    {
                        SetStep(slIcon, slText, "!", Pending, L.T("SlOutdated") + duplicates);
                        btnStatusLine.Text = L.T("Repair");
                    }
                    else
                    {
                        SetStep(slIcon, slText, "✔", Ok, L.T("SlConnected") + duplicates);
                        btnStatusLine.Text = L.T("Disconnect");
                    }
                    break;
                case StatusLineState.OutdatedPath:
                    SetStep(slIcon, slText, "!", Pending, L.T("SlOutdated") + duplicates);
                    btnStatusLine.Text = L.T("Repair");
                    break;
                case StatusLineState.OtherStatusLine:
                    SetStep(slIcon, slText, "✖", Ko, L.T("SlOther") + duplicates);
                    btnStatusLine.Text = L.T("Connect");
                    break;
                case StatusLineState.Invalid:
                    SetStep(slIcon, slText, "✖", Ko, L.F("SlInvalid", statusLine.Error));
                    btnStatusLine.Text = L.T("Connect");
                    break;
                default:
                    SetStep(slIcon, slText, "✖", Ko, L.T("SlNotConfigured"));
                    btnStatusLine.Text = L.T("Connect");
                    break;
            }
            btnStatusLine.Enabled = statusLine.State != StatusLineState.Invalid;

            RefreshDataStep();
        }

        void RefreshDataStep()
        {
            ReadResult data = UsageStore.Read(txtPath.Text.Trim().Length > 0 ? txtPath.Text.Trim() : settings.DataPath);
            if (data.Snapshot != null)
            {
                DateTime local = data.Snapshot.UpdatedAt.LocalDateTime;
                SetStep(dataIcon, dataText, "✔", Ok, L.F("DataReceived", local.ToString("g", L.Format), Ago(DateTime.Now - local)));
            }
            else if (cliPath != null && ClaudeCli.IsRunning(cliPath))
                SetStep(dataIcon, dataText, "!", Pending, L.T("DataWaitingRunning"));
            else
                SetStep(dataIcon, dataText, "✖", Ko, L.T("DataWaiting"));
            btnLaunch.Enabled = cliPath != null;
        }

        void OnStatusLineButton()
        {
            if (statusLine.State == StatusLineState.Connected && !SelfInstall.InstalledCopyIsOlder())
                DisconnectStatusLine();
            else
                ConnectStatusLine();
        }

        void ConnectStatusLine()
        {
            bool install = !SelfInstall.IsRunningInstalled;
            string message = L.F("ConfirmConnect", StatusLineSetup.SettingsPath());
            if (statusLine.State == StatusLineState.OtherStatusLine) message += L.T("ConfirmConnectOther");
            if (install) message += L.F("ConfirmConnectInstall", SelfInstall.InstallDir);
            message += L.T("ConfirmContinue");
            if (MessageBox.Show(this, message, Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            string backup;
            try
            {
                if (install) CopyWithRetry();
                backup = StatusLineSetup.Connect(SelfInstall.InstalledExe);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.F("ActionFailed", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                RefreshSetup();
                return;
            }

            string details = backup != null ? L.F("BackupInfo", backup) : "";
            if (install) details += L.F("RestartInstalled", SelfInstall.InstallDir);
            MessageBox.Show(this, L.F("ConnectDone", details), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (install)
            {
                // Le widget redémarre depuis la copie installée : on enregistre les options en même temps.
                if (!ApplyOptions()) return;
                RestartRequested = true;
                DialogResult = DialogResult.OK;
                return;
            }
            RefreshSetup();
        }

        /// <summary>La copie peut être brièvement verrouillée si Claude Code l'exécute pour sa ligne de statut.</summary>
        static void CopyWithRetry()
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    SelfInstall.CopyToInstallDir();
                    return;
                }
                catch (IOException)
                {
                    if (attempt >= 10) throw;
                    Thread.Sleep(300);
                }
            }
        }

        void DisconnectStatusLine()
        {
            if (MessageBox.Show(this, L.T("ConfirmDisconnect"), Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                string backup = StatusLineSetup.Disconnect();
                MessageBox.Show(this, L.F("DisconnectDone", backup != null ? L.F("BackupInfo", backup) : ""), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.F("ActionFailed", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshSetup();
        }

        void LaunchClaude()
        {
            if (cliPath == null) return;
            try
            {
                ClaudeCli.Launch(cliPath, txtWorkDir.Text.Trim());
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.F("LaunchFailed", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void BrowseFolder()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = L.T("FolderDialog");
                if (Directory.Exists(txtWorkDir.Text.Trim())) dialog.SelectedPath = txtWorkDir.Text.Trim();
                if (dialog.ShowDialog(this) == DialogResult.OK) txtWorkDir.Text = dialog.SelectedPath;
            }
        }

        // ------------------------------------------------------------------ avancé

        string EnteredPath
        {
            get { return txtPath.Text.Trim(); }
        }

        void UpdateSnippet()
        {
            string command = "\"" + StatusLineSetup.CommandPathFor(SelfInstall.InstalledExe) + "\" " + Program.StatusLineArgument;
            txtStatusLine.Text =
                "\"statusLine\": {\r\n" +
                "  \"type\": \"command\",\r\n" +
                "  \"command\": " + new JavaScriptSerializer().Serialize(command) + "\r\n" +
                "}";
        }

        void UpdateDataFileState()
        {
            string path = EnteredPath;
            ReadResult result = path.Length > 0 ? UsageStore.Read(path) : new ReadResult { Error = L.T("CfgNoFile") };
            if (result.Snapshot == null)
            {
                lblState.ForeColor = SystemColors.GrayText;
                lblState.Text = File.Exists(path) ? result.Error : L.T("CfgNoData");
                return;
            }
            DateTime local = result.Snapshot.UpdatedAt.LocalDateTime;
            lblState.ForeColor = Ok;
            lblState.Text = L.F("CfgReceived", local.ToString("g", L.Format), Ago(DateTime.Now - local));
        }

        static string Ago(TimeSpan span)
        {
            if (span.TotalMinutes < 1) return L.T("AgoNow");
            if (span.TotalHours < 1) return L.F("AgoMinutes", (int)span.TotalMinutes);
            if (span.TotalDays < 1) return L.F("AgoHours", (int)span.TotalHours);
            return L.F("AgoDays", (int)span.TotalDays);
        }

        void BrowseDataFile()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = L.T("CfgFileHeading");
                dialog.Filter = L.T("JsonFilter");
                dialog.OverwritePrompt = false;
                dialog.FileName = Path.GetFileName(EnteredPath);
                string dir = Path.GetDirectoryName(EnteredPath.Length > 0 ? EnteredPath : UsageStore.DefaultPath());
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dialog.InitialDirectory = dir;
                if (dialog.ShowDialog(this) == DialogResult.OK) txtPath.Text = dialog.FileName;
            }
        }

        // ------------------------------------------------------------------ enregistrement

        void Save()
        {
            if (ApplyOptions()) DialogResult = DialogResult.OK;
        }

        /// <summary>Valide et reporte les options dans les réglages. Retourne false si une saisie est invalide.</summary>
        bool ApplyOptions()
        {
            string path = EnteredPath;
            if (!Path.IsPathRooted(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, L.T("ErrPathInvalid"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.F("ErrCreateFolder", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            settings.CustomDataPath = string.Equals(path, UsageStore.DefaultPath(), StringComparison.OrdinalIgnoreCase) ? "" : path;
            string workDir = txtWorkDir.Text.Trim();
            string defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            settings.ClaudeWorkDir = string.Equals(workDir, defaultDir, StringComparison.OrdinalIgnoreCase) ? "" : workDir;
            settings.LaunchClaudeOnStart = chkLaunchOnStart.Checked;
            settings.ShowAssistantOnStart = chkShowAssistant.Checked;
            return true;
        }
    }
}
