using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ClaudeUsageWidget
{
    /// <summary>Fenêtre « Configuration » : choix de la source du jeton d'accès au compte Claude.</summary>
    class ConfigForm : Form
    {
        const int FieldWidth = 360;

        readonly Settings settings;
        readonly RadioButton rbAuto, rbFile, rbManual;
        readonly TextBox txtPath, txtToken;
        readonly Button btnBrowse, btnClearToken, btnTest;
        readonly Label lblTokenState, lblTest;
        bool tokenCleared;

        public ConfigForm(Settings settings)
        {
            this.settings = settings;

            Text = "Utilisation Claude · Configuration";
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

            layout.Controls.Add(new Label
            {
                Text = "Accès au compte Claude",
                Font = new Font(Font.FontFamily, Font.Size * 1.15f, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6),
            });
            layout.Controls.Add(Hint(
                "Aucun identifiant ni mot de passe n'est enregistré par le widget. " +
                "Choisissez comment il obtient le jeton d'accès à l'API d'usage :", 0));

            rbAuto = Radio("Automatique : session Claude Code (recommandé)");
            layout.Controls.Add(rbAuto);
            layout.Controls.Add(Hint("Lu dans " + UsageClient.DefaultCredentialsPath() + ", renouvelé par Claude Code.", 18));

            rbFile = Radio("Fichier d'identifiants personnalisé (format Claude Code)");
            layout.Controls.Add(rbFile);
            txtPath = new TextBox { Width = FieldWidth, Text = settings.CredentialsPath };
            btnBrowse = new Button { Text = "Parcourir…", AutoSize = true };
            btnBrowse.Click += delegate { Browse(); };
            layout.Controls.Add(Row(18, txtPath, btnBrowse));

            rbManual = Radio("Jeton OAuth saisi manuellement");
            layout.Controls.Add(rbManual);
            txtToken = new TextBox { Width = FieldWidth, UseSystemPasswordChar = true };
            btnClearToken = new Button { Text = "Effacer", AutoSize = true };
            btnClearToken.Click += delegate
            {
                tokenCleared = true;
                txtToken.Clear();
                UpdateTokenState();
            };
            layout.Controls.Add(Row(18, txtToken, btnClearToken));
            lblTokenState = Hint("", 18);
            layout.Controls.Add(lblTokenState);

            btnTest = new Button { Text = "Tester la connexion", AutoSize = true };
            btnTest.Click += delegate { RunTest(); };
            lblTest = new Label { AutoSize = true, MaximumSize = new Size(FieldWidth, 0), Margin = new Padding(8, 7, 0, 0) };
            layout.Controls.Add(Row(0, btnTest, lblTest));

            var btnOk = new Button { Text = "Enregistrer", AutoSize = true, MinimumSize = new Size(90, 0) };
            btnOk.Click += delegate { Save(); };
            var btnCancel = new Button { Text = "Annuler", AutoSize = true, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };
            var buttons = Row(0, btnCancel, btnOk);
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.Dock = DockStyle.Right;
            buttons.Margin = new Padding(0, 14, 0, 0);
            layout.Controls.Add(buttons);
            AcceptButton = btnOk;
            CancelButton = btnCancel;

            switch (settings.TokenSource)
            {
                case Settings.SourceFile: rbFile.Checked = true; break;
                case Settings.SourceManual: rbManual.Checked = true; break;
                default: rbAuto.Checked = true; break;
            }
            txtToken.TextChanged += delegate { UpdateTokenState(); };
            UpdateEnabled();
            UpdateTokenState();
        }

        RadioButton Radio(string text)
        {
            var rb = new RadioButton { Text = text, AutoSize = true, Margin = new Padding(0, 10, 0, 2) };
            rb.CheckedChanged += delegate { UpdateEnabled(); };
            return rb;
        }

        static Label Hint(string text, int indent)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(FieldWidth + 100, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(indent, 0, 0, 2),
            };
        }

        static FlowLayoutPanel Row(int indent, params Control[] controls)
        {
            var row = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = new Padding(indent, 2, 0, 2),
            };
            row.Controls.AddRange(controls);
            return row;
        }

        string SelectedSource
        {
            get
            {
                if (rbFile.Checked) return Settings.SourceFile;
                if (rbManual.Checked) return Settings.SourceManual;
                return Settings.SourceClaudeCode;
            }
        }

        bool HasStoredToken
        {
            get { return !tokenCleared && TokenStore.Exists(); }
        }

        void UpdateEnabled()
        {
            txtPath.Enabled = btnBrowse.Enabled = rbFile.Checked;
            txtToken.Enabled = btnClearToken.Enabled = rbManual.Checked;
        }

        void UpdateTokenState()
        {
            string state = txtToken.TextLength > 0 ? "Le nouveau jeton remplacera l'ancien."
                : HasStoredToken ? "Un jeton est enregistré ; laissez vide pour le conserver."
                : "Aucun jeton enregistré.";
            lblTokenState.Text = state + " Stockage chiffré avec votre session Windows (DPAPI).";
        }

        void Browse()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Fichier d'identifiants";
                dialog.Filter = "Fichiers JSON (*.json)|*.json|Tous les fichiers (*.*)|*.*";
                string current = txtPath.Text.Trim();
                string dir = current.Length > 0 ? Path.GetDirectoryName(current) : Path.GetDirectoryName(UsageClient.DefaultCredentialsPath());
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dialog.InitialDirectory = dir;
                if (dialog.ShowDialog(this) == DialogResult.OK) txtPath.Text = dialog.FileName;
            }
        }

        AccessConfig BuildAccess()
        {
            string typed = TokenStore.Normalize(txtToken.Text);
            return new AccessConfig
            {
                Source = SelectedSource,
                CredentialsPath = txtPath.Text.Trim(),
                ManualToken = typed.Length > 0 ? typed : HasStoredToken ? TokenStore.Load() : null,
            };
        }

        void RunTest()
        {
            btnTest.Enabled = false;
            lblTest.ForeColor = SystemColors.GrayText;
            lblTest.Text = "Test en cours…";
            AccessConfig access = BuildAccess();
            ThreadPool.QueueUserWorkItem(delegate
            {
                FetchResult result = UsageClient.Fetch(access);
                try
                {
                    BeginInvoke((MethodInvoker)delegate { ShowTestResult(result); });
                }
                catch (InvalidOperationException)
                {
                    // Fenêtre fermée pendant le test.
                }
            });
        }

        void ShowTestResult(FetchResult result)
        {
            btnTest.Enabled = true;
            if (result.Windows == null)
            {
                lblTest.ForeColor = Color.FromArgb(196, 43, 28);
                lblTest.Text = result.Error;
                return;
            }
            var parts = new string[Math.Min(2, result.Windows.Count)];
            for (int i = 0; i < parts.Length; i++)
                parts[i] = result.Windows[i].Label + " " + Math.Round(result.Windows[i].Percent) + " %";
            lblTest.ForeColor = Color.FromArgb(16, 124, 16);
            lblTest.Text = "Connexion réussie : " + string.Join(", ", parts);
        }

        void Save()
        {
            string source = SelectedSource;
            string path = txtPath.Text.Trim();
            string typed = TokenStore.Normalize(txtToken.Text);

            if (source == Settings.SourceFile && !File.Exists(path))
            {
                MessageBox.Show(this, "Le fichier d'identifiants indiqué est introuvable.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (source == Settings.SourceManual && typed.Length == 0 && !HasStoredToken)
            {
                MessageBox.Show(this, "Saisissez un jeton ou choisissez une autre source.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Un jeton manuel n'est conservé que tant que cette source est active.
                if (source != Settings.SourceManual || (tokenCleared && typed.Length == 0))
                    TokenStore.Clear();
                if (source == Settings.SourceManual && typed.Length > 0)
                    TokenStore.Save(typed);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Impossible d'enregistrer le jeton : " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            settings.TokenSource = source;
            settings.CredentialsPath = source == Settings.SourceFile ? path : "";
            DialogResult = DialogResult.OK;
        }
    }
}
