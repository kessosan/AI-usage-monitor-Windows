using System;
using System.Drawing;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace ClaudeUsageWidget
{
    /// <summary>
    /// Fenêtre « Configuration » : emplacement du fichier de données et instructions pour que
    /// Claude Code (ou claude-hud) l'alimente via la ligne de statut.
    /// </summary>
    class ConfigForm : Form
    {
        const int FieldWidth = 420;

        readonly Settings settings;
        readonly TextBox txtPath, txtStatusLine, txtHud;
        readonly Label lblState;

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

            layout.Controls.Add(Heading("Source des données", 0));
            layout.Controls.Add(Hint(
                "Le widget n'accède ni à votre compte ni au réseau. Il affiche les quotas que Claude Code " +
                "transmet à sa ligne de statut (fonctionnalité officielle), enregistrés dans un fichier local.", 0));

            layout.Controls.Add(Heading("Fichier de données", 10));
            txtPath = new TextBox { Width = FieldWidth, Text = settings.DataPath };
            var btnBrowse = new Button { Text = "Parcourir…", AutoSize = true };
            btnBrowse.Click += delegate { Browse(); };
            var btnDefault = new Button { Text = "Par défaut", AutoSize = true };
            btnDefault.Click += delegate { txtPath.Text = UsageStore.DefaultPath(); };
            layout.Controls.Add(Row(0, txtPath, btnBrowse, btnDefault));

            lblState = new Label { AutoSize = true, MaximumSize = new Size(FieldWidth, 0), Margin = new Padding(0, 7, 0, 0) };
            var btnCheck = new Button { Text = "Vérifier", AutoSize = true };
            btnCheck.Click += delegate { UpdateState(); };
            layout.Controls.Add(Row(0, btnCheck, lblState));

            layout.Controls.Add(Heading("Brancher Claude Code (un seul des deux cas)", 14));

            layout.Controls.Add(Heading("1. Vous n'avez pas encore de ligne de statut", 6, false));
            layout.Controls.Add(Hint("Ajoutez ce bloc dans %USERPROFILE%\\.claude\\settings.json, puis redémarrez Claude Code. " +
                "Si vous déplacez l'exécutable, mettez à jour le chemin.", 0));
            txtStatusLine = Snippet(4);
            layout.Controls.Add(Row(0, txtStatusLine, CopyButton(txtStatusLine)));

            layout.Controls.Add(Heading("2. Vous utilisez déjà claude-hud comme ligne de statut", 10, false));
            layout.Controls.Add(Hint("Ajoutez cette ligne dans la section « display » de " +
                "%USERPROFILE%\\.claude\\plugins\\claude-hud\\config.json :", 0));
            txtHud = Snippet(1);
            layout.Controls.Add(Row(0, txtHud, CopyButton(txtHud)));

            layout.Controls.Add(Hint("Une autre ligne de statut peut aussi écrire ce fichier : le format est décrit dans le README.", 0));

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

            txtPath.TextChanged += delegate { UpdateSnippets(); };
            UpdateSnippets();
            UpdateState();
        }

        // ------------------------------------------------------------------ construction

        Label Heading(string text, int top, bool large = true)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = new Font(Font.FontFamily, large ? Font.Size * 1.1f : Font.Size, FontStyle.Bold),
                Margin = new Padding(0, top, 0, 3),
            };
        }

        static Label Hint(string text, int indent)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(FieldWidth + 160, 0),
                ForeColor = SystemColors.GrayText,
                Margin = new Padding(indent, 0, 0, 4),
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

        Button CopyButton(TextBox source)
        {
            var button = new Button { Text = "Copier", AutoSize = true };
            button.Click += delegate
            {
                try
                {
                    Clipboard.SetText(source.Text);
                    button.Text = "Copié";
                }
                catch (Exception)
                {
                    button.Text = "Échec";
                }
            };
            return button;
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

        // ------------------------------------------------------------------ comportement

        string EnteredPath
        {
            get { return txtPath.Text.Trim(); }
        }

        void UpdateSnippets()
        {
            var json = new JavaScriptSerializer();
            string exe = Application.ExecutablePath.Replace('\\', '/');
            string command = "\"" + exe + "\" " + Program.StatusLineArgument;
            txtStatusLine.Text =
                "\"statusLine\": {\r\n" +
                "  \"type\": \"command\",\r\n" +
                "  \"command\": " + json.Serialize(command) + "\r\n" +
                "}";
            txtHud.Text = "\"externalUsageWritePath\": " + json.Serialize(EnteredPath);
        }

        void UpdateState()
        {
            string path = EnteredPath;
            ReadResult result = path.Length > 0 ? UsageStore.Read(path) : new ReadResult { Error = "Aucun fichier indiqué" };
            if (result.Snapshot == null)
            {
                lblState.ForeColor = SystemColors.GrayText;
                lblState.Text = File.Exists(path) ? result.Error : "Aucune donnée reçue pour l'instant.";
                return;
            }
            DateTime local = result.Snapshot.UpdatedAt.LocalDateTime;
            lblState.ForeColor = Color.FromArgb(16, 124, 16);
            lblState.Text = "Données reçues le " + local.ToString("dd/MM/yyyy 'à' HH:mm") + " (" + Ago(DateTime.Now - local) + ").";
        }

        static string Ago(TimeSpan span)
        {
            if (span.TotalMinutes < 1) return "à l'instant";
            if (span.TotalHours < 1) return "il y a " + (int)span.TotalMinutes + " min";
            if (span.TotalDays < 1) return "il y a " + (int)span.TotalHours + " h";
            return "il y a " + (int)span.TotalDays + " j";
        }

        void Browse()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Fichier de données";
                dialog.Filter = "Fichiers JSON (*.json)|*.json";
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
                MessageBox.Show(this, "Indiquez un chemin complet vers un fichier .json.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)); // claude-hud exige un dossier existant
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Impossible de créer le dossier : " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            settings.CustomDataPath = string.Equals(path, UsageStore.DefaultPath(), StringComparison.OrdinalIgnoreCase) ? "" : path;
            DialogResult = DialogResult.OK;
        }
    }
}
