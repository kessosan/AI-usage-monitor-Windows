using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Claude Usage Widget")]
[assembly: AssemblyProduct("Claude Usage Widget")]
// La version est injectée par build.ps1 (obj/VersionInfo.cs).

namespace ClaudeUsageWidget
{
    static class Program
    {
        public const string StatusLineArgument = "--statusline";

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            // Appelé par Claude Code comme commande de ligne de statut : pas d'interface.
            if (args.Length > 0 && args[0] == StatusLineArgument)
                return StatusLineBridge.Run();

            bool createdNew;
            using (var mutex = new Mutex(true, "ClaudeUsageWidget_SingleInstance", out createdNew))
            {
                if (!createdNew) return 0;

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new WidgetForm());
            }
            return 0;
        }
    }
}
