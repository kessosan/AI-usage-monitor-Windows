using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ClaudeUsageWidget
{
    /// <summary>
    /// Inscription dans « Applications installées » de Windows et désinstallation complète.
    /// Les copies de sauvegarde de settings.json sont volontairement conservées.
    /// </summary>
    static class Uninstaller
    {
        public const string Argument = "--uninstall";

        static string KeyPath
        {
            get { return @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ClaudeUsageWidget" + Settings.TestSuffix; }
        }

        /// <summary>Déclare (ou met à jour) la copie installée dans « Applications installées ».</summary>
        public static void Register()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
                {
                    string exe = SelfInstall.InstalledExe;
                    key.SetValue("DisplayName", L.T("Title"));
                    key.SetValue("DisplayVersion", AppInfo.Version);
                    if (AppInfo.Author.Length > 0) key.SetValue("Publisher", AppInfo.Author);
                    if (AppInfo.Email.Length > 0) key.SetValue("HelpLink", "mailto:" + AppInfo.Email);
                    if (AppInfo.RepositoryUrl.Length > 0) key.SetValue("URLInfoAbout", AppInfo.RepositoryUrl);
                    key.SetValue("DisplayIcon", exe);
                    key.SetValue("InstallLocation", SelfInstall.InstallDir);
                    key.SetValue("UninstallString", "\"" + exe + "\" " + Argument);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    if (File.Exists(exe)) key.SetValue("EstimatedSize", (int)Math.Max(1, new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
                    if (key.GetValue("InstallDate") == null) key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                }
            }
            catch (Exception)
            {
                // Non bloquant : le widget fonctionne sans cette inscription.
            }
        }

        public static bool IsRegistered()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
                return key != null;
        }

        /// <summary>Désinstalle. Retourne la liste des étapes en échec (vide si tout s'est bien passé).</summary>
        public static List<string> Run(bool removeData)
        {
            var errors = new List<string>();
            CloseOtherInstances();

            // 1. Ligne de statut : retirer la commande du widget et restaurer l'originale.
            try
            {
                StatusLineInfo info = StatusLineSetup.Inspect(SelfInstall.InstalledExe);
                if (info.State == StatusLineState.Connected || info.State == StatusLineState.OutdatedPath)
                    StatusLineSetup.Disconnect();
            }
            catch (Exception ex)
            {
                errors.Add(L.F("UninstallErrStatusLine", ex.Message));
            }

            // 2. Démarrage avec Windows et inscription dans « Applications installées ».
            try
            {
                Startup.Set(false);
                Registry.CurrentUser.DeleteSubKeyTree(KeyPath, false);
            }
            catch (Exception ex)
            {
                errors.Add(L.F("UninstallErrRegistry", ex.Message));
            }

            // 3. Réglages et données.
            if (removeData)
            {
                try
                {
                    if (Directory.Exists(Settings.AppDataDir)) DeleteDirectoryWithRetry(Settings.AppDataDir);
                }
                catch (Exception ex)
                {
                    errors.Add(L.F("UninstallErrData", ex.Message));
                }
            }

            // 4. Copie installée : impossible de supprimer l'exécutable en cours, on délègue après la fermeture.
            try
            {
                if (Directory.Exists(SelfInstall.InstallDir))
                {
                    if (SelfInstall.IsRunningInstalled) ScheduleDirectoryDeletion(SelfInstall.InstallDir);
                    else DeleteDirectoryWithRetry(SelfInstall.InstallDir);
                }
            }
            catch (Exception ex)
            {
                errors.Add(L.F("UninstallErrFiles", ex.Message));
            }
            return errors;
        }

        /// <summary>Ferme les autres instances du widget (lancé depuis « Applications installées », par exemple).</summary>
        static void CloseOtherInstances()
        {
            int self = Process.GetCurrentProcess().Id;
            foreach (Process process in Process.GetProcessesByName("ClaudeUsageWidget"))
            {
                using (process)
                {
                    if (process.Id == self) continue;
                    try
                    {
                        process.Kill();
                        process.WaitForExit(3000);
                    }
                    catch (Exception)
                    {
                        // Déjà terminé ou accès refusé : la suppression des fichiers le signalera au besoin.
                    }
                }
            }
        }

        static void DeleteDirectoryWithRetry(string dir)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    Directory.Delete(dir, true);
                    return;
                }
                catch (IOException)
                {
                    // Un appel de la ligne de statut peut verrouiller l'exe quelques instants.
                    if (attempt >= 10) throw;
                    Thread.Sleep(300);
                }
                catch (UnauthorizedAccessException)
                {
                    if (attempt >= 10) throw;
                    Thread.Sleep(300);
                }
            }
        }

        static void ScheduleDirectoryDeletion(string dir)
        {
            string literal = "'" + dir.Replace("'", "''") + "'";
            string script =
                "Start-Sleep -Seconds 2; " +
                "for ($i = 0; $i -lt 15 -and (Test-Path -LiteralPath " + literal + "); $i++) { " +
                "Remove-Item -LiteralPath " + literal + " -Recurse -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 1 }";
            var start = new ProcessStartInfo(
                Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
                "-NoProfile -NonInteractive -WindowStyle Hidden -Command \"" + script + "\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath(),
            };
            Process.Start(start);
        }
    }

    /// <summary>Fenêtre de confirmation de la désinstallation.</summary>
    class UninstallForm : Form
    {
        const int TextWidth = 460;
        readonly CheckBox chkRemoveData;

        public UninstallForm()
        {
            Text = L.T("UninstallTitle");
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(16, 14, 16, 14);

            var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Dock = DockStyle.Fill };
            Controls.Add(layout);

            layout.Controls.Add(Line(L.T("UninstallIntro"), FontStyle.Bold, 6));
            layout.Controls.Add(Line(L.T("UninstallStepStatusLine"), FontStyle.Regular, 2));
            layout.Controls.Add(Line(L.T("UninstallStepStartup"), FontStyle.Regular, 2));
            layout.Controls.Add(Line(L.F("UninstallStepFiles", SelfInstall.InstallDir), FontStyle.Regular, 8));

            chkRemoveData = new CheckBox
            {
                Text = L.T("UninstallRemoveData"),
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 0, 0, 0),
            };
            layout.Controls.Add(chkRemoveData);
            var dataPath = Line(Settings.AppDataDir, FontStyle.Regular, 8);
            dataPath.ForeColor = SystemColors.GrayText;
            dataPath.Margin = new Padding(18, 0, 0, 8);
            layout.Controls.Add(dataPath);

            var keep = Line(L.T("UninstallKeepBackups"), FontStyle.Regular, 0);
            keep.ForeColor = SystemColors.GrayText;
            layout.Controls.Add(keep);

            var btnUninstall = new Button { Text = L.T("UninstallButton"), AutoSize = true, MinimumSize = new Size(100, 0) };
            btnUninstall.Click += delegate { RunUninstall(); };
            var btnCancel = new Button { Text = L.T("Cancel"), AutoSize = true, MinimumSize = new Size(90, 0), DialogResult = DialogResult.Cancel };
            var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Right, Margin = new Padding(0, 16, 0, 0) };
            buttons.Controls.Add(btnCancel);
            buttons.Controls.Add(btnUninstall);
            layout.Controls.Add(buttons);
            CancelButton = btnCancel;
        }

        Label Line(string text, FontStyle style, int bottom)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(TextWidth, 0),
                Font = new Font(Font, style),
                Margin = new Padding(0, 0, 0, bottom),
            };
        }

        void RunUninstall()
        {
            UseWaitCursor = true;
            List<string> errors = Uninstaller.Run(chkRemoveData.Checked);
            UseWaitCursor = false;

            string message = L.T("UninstallDone") + "\n\n" + L.T("UninstallDoneRestart");
            if (!SelfInstall.IsRunningInstalled)
                message += "\n\n" + L.F("UninstallDoneDownloaded", Application.ExecutablePath);
            if (errors.Count > 0)
                message += "\n\n" + L.F("UninstallErrors", "• " + string.Join("\n• ", errors));
            MessageBox.Show(this, message, Text, MessageBoxButtons.OK, errors.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
        }
    }
}
