using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("DNS Party Uninstaller")]
[assembly: AssemblyDescription("DNS Party — Windows DNS Manager Uninstaller")]
[assembly: AssemblyCompany("DNS Party")]
[assembly: AssemblyProduct("DNS Party Uninstaller")]

namespace DNSPartyUninstaller
{
    public static class UninstallerProgram
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool isSilent = false;
            bool isPurge = false;

            foreach (string a in args)
            {
                if (string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase))
                {
                    isSilent = true;
                }
                else if (string.Equals(a, "/PURGE", StringComparison.OrdinalIgnoreCase))
                {
                    isPurge = true;
                }
            }

            try
            {
                bool purgeData = isPurge;
                if (!isSilent && !isPurge)
                {
                    DialogResult res = MessageBox.Show("Are you sure you want to completely remove DNS Party and all of its components?", "Uninstall DNS Party", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (res != DialogResult.Yes) return;

                    DialogResult dataRes = MessageBox.Show("Do you also want to remove your personal settings and DNS profiles?", "Uninstall DNS Party", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (dataRes == DialogResult.Yes)
                    {
                        purgeData = true;
                    }
                }

                KillDNSPartyProcesses();

                if (purgeData)
                {
                    try
                    {
                        string localDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DNSParty");
                        if (Directory.Exists(localDataDir))
                        {
                            Directory.Delete(localDataDir, true);
                        }
                    }
                    catch { }
                }

                try
                {
                    string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "DNS Party");
                    if (Directory.Exists(startMenuDir))
                    {
                        Directory.Delete(startMenuDir, true);
                    }
                }
                catch { }

                try
                {
                    string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DNS Party.lnk");
                    if (File.Exists(desktopLink)) File.Delete(desktopLink);
                }
                catch { }

                try
                {
                    string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
                    {
                        if (key != null && key.GetValue("DNSParty") != null)
                        {
                            key.DeleteValue("DNSParty");
                        }
                    }

                    string uninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(uninstallKey, true))
                    {
                        if (key != null)
                        {
                            key.DeleteSubKeyTree("DNSParty", false);
                        }
                    }
                }
                catch { }

                string installDir = Path.GetDirectoryName(Application.ExecutablePath);

                if (!string.IsNullOrEmpty(installDir) && Directory.Exists(installDir))
                {
                    string currentExe = Application.ExecutablePath;
                    foreach (string f in Directory.GetFiles(installDir))
                    {
                        if (!string.Equals(f, currentExe, StringComparison.OrdinalIgnoreCase))
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }
                    foreach (string d in Directory.GetDirectories(installDir))
                    {
                        try { Directory.Delete(d, true); } catch { }
                    }

                    try
                    {
                        string batchPath = Path.Combine(Path.GetTempPath(), "remove_dnsparty.bat");
                        File.WriteAllText(batchPath, "@echo off\r\nping 127.0.0.1 -n 2 > nul\r\nrmdir /s /q \"" + installDir + "\"\r\ndel \"" + batchPath + "\"");
                        ProcessStartInfo psi = new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c \"" + batchPath + "\"", WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                        Process.Start(psi);
                    }
                    catch { }
                }

                if (!isSilent)
                {
                    MessageBox.Show("DNS Party was successfully removed from your computer.", "Uninstall Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (!isSilent)
                {
                    MessageBox.Show("An error occurred during uninstallation: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private static void KillDNSPartyProcesses()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("DNSParty"))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(3000);
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
