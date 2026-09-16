using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace ClaudeUsageWidget
{
    /// <summary>
    /// Informations de l'application. Auteur, e-mail et adresse du dépôt ne figurent pas dans le code source :
    /// build.ps1 les injecte à la compilation (paramètres ou variables d'environnement).
    /// </summary>
    static class AppInfo
    {
        static readonly Assembly Self = typeof(AppInfo).Assembly;

        public static string Version
        {
            get
            {
                var attr = Attribute.GetCustomAttribute(Self, typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
                return attr != null ? attr.InformationalVersion : Self.GetName().Version.ToString();
            }
        }

        public static string Author
        {
            get
            {
                var attr = Attribute.GetCustomAttribute(Self, typeof(AssemblyCompanyAttribute)) as AssemblyCompanyAttribute;
                return attr != null ? attr.Company : "";
            }
        }

        public static string Copyright
        {
            get
            {
                var attr = Attribute.GetCustomAttribute(Self, typeof(AssemblyCopyrightAttribute)) as AssemblyCopyrightAttribute;
                return attr != null ? attr.Copyright : "";
            }
        }

        public static string Email
        {
            get { return Metadata("ContactEmail"); }
        }

        public static string RepositoryUrl
        {
            get { return Metadata("RepositoryUrl"); }
        }

        static string Metadata(string key)
        {
            foreach (AssemblyMetadataAttribute attr in Attribute.GetCustomAttributes(Self, typeof(AssemblyMetadataAttribute)))
                if (attr.Key == key) return attr.Value ?? "";
            return "";
        }
    }

    /// <summary>Fenêtre « À propos ».</summary>
    class AboutForm : Form
    {
        const int TextWidth = 380;

        public AboutForm()
        {
            Text = L.T("AboutTitle");
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(18, 14, 18, 14);

            var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
            Controls.Add(layout);

            layout.Controls.Add(new Label
            {
                Text = L.T("Title"),
                AutoSize = true,
                Font = new Font(Font.FontFamily, Font.Size * 1.5f, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 2),
            });
            layout.Controls.Add(Line(L.F("AboutVersion", AppInfo.Version), 0, 10));
            layout.Controls.Add(Line(L.T("AboutDescription"), 0, 12, SystemColors.GrayText));

            if (AppInfo.Author.Length > 0) layout.Controls.Add(Line(L.F("AboutAuthor", AppInfo.Author), 0, 2));
            if (AppInfo.Copyright.Length > 0) layout.Controls.Add(Line(AppInfo.Copyright, 0, 2));
            if (AppInfo.Email.Length > 0) layout.Controls.Add(Link(AppInfo.Email, "mailto:" + AppInfo.Email));
            if (AppInfo.RepositoryUrl.Length > 0) layout.Controls.Add(Link(L.T("AboutRepository"), AppInfo.RepositoryUrl));

            layout.Controls.Add(Line(L.T("AboutDisclaimer"), 12, 0, SystemColors.GrayText));

            var ok = new Button { Text = L.T("Close"), AutoSize = true, MinimumSize = new Size(90, 0), DialogResult = DialogResult.OK, Anchor = AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
            layout.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = ok;
        }

        static Label Line(string text, int top, int bottom, Color? color = null)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(TextWidth, 0),
                ForeColor = color ?? SystemColors.ControlText,
                Margin = new Padding(0, top, 0, bottom),
            };
        }

        static LinkLabel Link(string text, string target)
        {
            var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 2, 0, 2) };
            link.LinkClicked += delegate
            {
                try { Process.Start(target); }
                catch (Win32Exception) { }
            };
            return link;
        }
    }
}
