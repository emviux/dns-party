using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using DNSParty.Services;
using DNSParty.UI;

[assembly: AssemblyTitle("DNS Party")]
[assembly: AssemblyDescription("DNS Party — Lightweight Windows DNS Manager")]
[assembly: AssemblyCompany("DNS Party")]
[assembly: AssemblyProduct("DNS Party")]
[assembly: AssemblyCopyright("Copyright © 2026 DNS Party")]
[assembly: AssemblyVersion(DNSParty.VersionInfo.CurrentVersion + ".0")]
[assembly: AssemblyFileVersion(DNSParty.VersionInfo.CurrentVersion + ".0")]

namespace DNSParty
{
    internal static class Program
    {
        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        private const string MutexName = @"Local\DNSParty_SingleInstance_Mutex_v1";
        private const string ShowEventName = @"Local\DNSParty_ShowWindow_Event_v1";

        [STAThread]
        public static int Main(string[] args)
        {
            // 1. Check if invoked as short-lived elevated CLI helper (--elevated-apply or --elevated-dhcp)
            if (args != null && args.Length > 0)
            {
                string firstArg = args[0];
                if (string.Equals(firstArg, "--elevated-apply", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(firstArg, "--elevated-dhcp", StringComparison.OrdinalIgnoreCase))
                {
                    return DnsService.ExecuteElevatedCliCommand(args);
                }
            }

            // 2. Enforce Single-Instance UI behavior
            bool createdNew;
            using (Mutex singleInstanceMutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    // Signal existing instance to restore its main window from tray
                    try
                    {
                        using (EventWaitHandle showEvent = EventWaitHandle.OpenExisting(ShowEventName))
                        {
                            showEvent.Set();
                        }
                    }
                    catch
                    {
                        // Ignore if event handle not open
                    }
                    return 0;
                }

                try
                {
                    SetProcessDPIAware();
                }
                catch
                {
                    // Ignore on older OS
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                bool startMinimizedArg = args != null && args.Any(a =>
                    string.Equals(a, "--minimized", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "-minimized", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "/minimized", StringComparison.OrdinalIgnoreCase));

                ConfigService configService = new ConfigService();
                SoundService soundService = new SoundService();
                using (DnsService dnsService = new DnsService())
                using (EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName))
                {
                    MainForm mainForm = new MainForm(configService, dnsService, soundService, startMinimizedArg);

                    // Background wait handle listener (zero CPU overhead, blocks on kernel object)
                    ThreadPool.RegisterWaitForSingleObject(
                        showEvent,
                        (state, timedOut) =>
                        {
                            try
                            {
                                if (!mainForm.IsDisposed && mainForm.IsHandleCreated)
                                {
                                    mainForm.BeginInvoke(new Action(mainForm.ShowFromTray));
                                }
                            }
                            catch { }
                        },
                        null,
                        Timeout.Infinite,
                        false);

                    Application.Run(mainForm);
                }

                return 0;
            }
        }
    }
}
