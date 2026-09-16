using System;
using System.Net;
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
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "ClaudeUsageWidget_SingleInstance", out createdNew))
            {
                if (!createdNew) return;

                SetProcessDPIAware();
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288; // TLS 1.3
                }
                catch (NotSupportedException)
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new WidgetForm());
            }
        }
    }
}
