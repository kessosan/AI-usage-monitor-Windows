using System;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ClaudeUsageWidget
{
    /// <summary>
    /// Fenêtre « Configuration » : emplacement du fichier de données et bloc de configuration
    /// permettant à Claude Code de l'alimenter via sa ligne de statut.
    /// </summary>
    class ConfigForm : Form
    {
        const int FieldWidth = 420;

        readonly Settings settings;
        readonly TextBox txtPath, txtStatusLine;
        readonly Label lblState;

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

            layout.Controls.Add(Heading(L.T("CfgSourceHeading"), 0));
            layout.Controls.Add(Hint(L.T("CfgSourceHint")));

            layout.Controls.Add(Heading(L.T("CfgFileHeading"), 10));
            txtPath = new TextBox { Width = FieldWidth, Text = settings.DataPath };
            var btnBrowse = new Button { Text = L.T("Browse"), AutoSize = true };
            btnBrowse.Click += delegate { Browse(); };
            var btnDefault = new Button { Text = L.T("Default"), AutoSize = true };
            btnDefault.Click += delegate { txtPath.Text = UsageStore.DefaultPath(); };
            layout.Controls.Add(Row(txtPath, btnBrowse, btnDefault));

            lblState = new Label { AutoSize = true, MaximumSize = new Size(FieldWidth, 0), Margin = new Padding(0, 7, 0, 0) };
            var btnCheck = new Button { Text = L.T("Check"), AutoSize = true };
            btnCheck.Click += delegate { UpdateState(); };
            layout.Controls.Add(Row(btnCheck, lblState));

            layout.Controls.Add(Heading(L.T("CfgConnectHeading"), 14));
            layout.Controls.Add(Hint(L.T("CfgStatusLineHint")));
            txtStatusLine = Snippet(4);
            layout.Controls.Add(Row(txtStatusLine, CopyButton(txtStatusLine)));
            layout.Controls.Add(Hint(L.T("CfgExistingHint")));

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
            UpdateState();
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

        // ------------------------------------------------------------------ comportement

        string EnteredPath
        {
            get { return txtPath.Text.Trim(); }
        }

        void UpdateSnippet()
        {
            string exe = Application.ExecutablePath.Replace('\\', '/');
            string command = "\"" + exe + "\" " + Program.StatusLineArgument;
            txtStatusLine.Text =
                "\"statusLine\": {\r\n" +
                "  \"type\": \"command\",\r\n" +
                "  \"command\": " + new JavaScriptSerializer().Serialize(command) + "\r\n" +
                "}";
        }

        void UpdateState()
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
            lblState.ForeColor = Color.FromArgb(16, 124, 16);
            lblState.Text = L.F("CfgReceived", local.ToString("g", L.Format), Ago(DateTime.Now - local));
        }

        static string Ago(TimeSpan span)
        {
            if (span.TotalMinutes < 1) return L.T("AgoNow");
            if (span.TotalHours < 1) return L.F("AgoMinutes", (int)span.TotalMinutes);
            if (span.TotalDays < 1) return L.F("AgoHours", (int)span.TotalHours);
            return L.F("AgoDays", (int)span.TotalDays);
        }

        void Browse()
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

        void Save()
        {
            string path = EnteredPath;
            if (!Path.IsPathRooted(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, L.T("ErrPathInvalid"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, L.F("ErrCreateFolder", ex.Message), Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            settings.CustomDataPath = string.Equals(path, UsageStore.DefaultPath(), StringComparison.OrdinalIgnoreCase) ? "" : path;
            DialogResult = DialogResult.OK;
        }
    }
}
