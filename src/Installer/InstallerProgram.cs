using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("DNS Party Setup")]
[assembly: AssemblyDescription("DNS Party — Windows DNS Manager Installer")]
[assembly: AssemblyCompany("DNS Party")]
[assembly: AssemblyProduct("DNS Party Setup")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace DNSPartySetup
{
    public class InstallerForm : Form
    {
        private readonly string _installDir;
        private readonly bool _isSilent;
        private readonly bool _isUninstall;
        private readonly bool _isUpdate;
        private readonly bool _isPurge;
        private CheckBox _chkDesktopShortcut;
        private CheckBox _chkStartMenuShortcut;
        private CheckBox _chkLaunchNow;
        private Label _lblStatus;
        private Button _btnInstall;
        private Button _btnCancel;

        public InstallerForm(string[] args)
        {
            _installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "DNSParty");
            foreach (string a in args)
            {
                if (string.Equals(a, "/S", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase))
                {
                    _isSilent = true;
                }
                else if (string.Equals(a, "/U", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(a, "--uninstall", StringComparison.OrdinalIgnoreCase))
                {
                    _isUninstall = true;
                }
                else if (string.Equals(a, "/UPDATE", StringComparison.OrdinalIgnoreCase))
                {
                    _isUpdate = true;
                }
                else if (string.Equals(a, "/PURGE", StringComparison.OrdinalIgnoreCase))
                {
                    _isPurge = true;
                }
            }

            Text = $"DNS Party v{DNSParty.VersionInfo.CurrentVersion} — Setup";
            Size = new Size(480, 340);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(14, 16, 28);
            ForeColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9.5f);

            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream icoStream = asm.GetManifestResourceStream("DNSParty.app.ico")
                                ?? asm.GetManifestResourceStream("DNSParty.dns-party.ico");
                using (icoStream)
                {
                    if (icoStream != null)
                    {
                        string tempFile = Path.Combine(Path.GetTempPath(), "dnsparty_installer_" + Guid.NewGuid().ToString("N") + ".ico");
                        using (FileStream fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write))
                        {
                            icoStream.CopyTo(fs);
                        }
                        Icon = new Icon(tempFile);
                        try { File.Delete(tempFile); } catch { }
                    }
                    else
                    {
                        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                }
            }
            catch { }

            PictureBox picLogo = null;
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream pngStream = asm.GetManifestResourceStream("DNSParty.installer-logo-512.png")
                                ?? asm.GetManifestResourceStream("DNSParty.overlay-badge-256.png")
                                ?? asm.GetManifestResourceStream("DNSParty.dns-party-64.png");
                using (pngStream)
                {
                    if (pngStream != null)
                    {
                        picLogo = new PictureBox
                        {
                            Image = new Bitmap(pngStream),
                            SizeMode = PictureBoxSizeMode.Zoom,
                            Location = new Point(24, 18),
                            Size = new Size(48, 48),
                            BackColor = Color.Transparent
                        };
                    }
                }
            }
            catch { }

            int textLeft = picLogo != null ? 82 : 24;

            var lblTitle = new Label
            {
                Text = $"Install DNS Party v{DNSParty.VersionInfo.CurrentVersion}",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(167, 139, 250),
                Location = new Point(textLeft, 18),
                AutoSize = true
            };

            var lblSub = new Label
            {
                Text = "Target folder: " + _installDir,
                ForeColor = Color.FromArgb(148, 163, 184),
                Location = new Point(textLeft + 2, 50),
                Size = new Size(440 - textLeft, 36)
            };

            _chkDesktopShortcut = new CheckBox
            {
                Text = "Create Desktop shortcut",
                Checked = true,
                Location = new Point(28, 100),
                Size = new Size(380, 26),
                ForeColor = Color.White
            };

            _chkStartMenuShortcut = new CheckBox
            {
                Text = "Create Start Menu shortcut",
                Checked = true,
                Location = new Point(28, 132),
                Size = new Size(380, 26),
                ForeColor = Color.White
            };

            _chkLaunchNow = new CheckBox
            {
                Text = "Launch DNS Party after installation",
                Checked = true,
                Location = new Point(28, 164),
                Size = new Size(380, 26),
                ForeColor = Color.White
            };

            _lblStatus = new Label
            {
                Text = "Ready to install.",
                ForeColor = Color.FromArgb(34, 197, 94),
                Location = new Point(26, 204),
                Size = new Size(420, 24)
            };

            _btnInstall = new Button
            {
                Text = "Install Now",
                BackColor = Color.FromArgb(124, 58, 237),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(236, 242),
                Size = new Size(110, 36)
            };
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Click += (s, e) => RunInstall(false);

            _btnCancel = new Button
            {
                Text = "Cancel",
                BackColor = Color.FromArgb(30, 34, 54),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(356, 242),
                Size = new Size(88, 36)
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => Close();

            if (picLogo != null)
            {
                Controls.Add(picLogo);
            }
            Controls.AddRange(new Control[]
            {
                lblTitle, lblSub, _chkDesktopShortcut, _chkStartMenuShortcut, _chkLaunchNow, _lblStatus, _btnInstall, _btnCancel
            });
        }

        public int ExecuteCliIfRequested()
        {
            if (_isUninstall)
            {
                RunUninstall();
                return 0;
            }
            if (_isSilent)
            {
                return RunInstall(true) ? 0 : 1;
            }
            return -1;
        }

        private void KillDNSPartyProcesses()
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

        private bool RunInstall(bool silent)
        {
            try
            {
                if (!silent)
                {
                    _btnInstall.Enabled = false;
                    _lblStatus.Text = "Extracting DNS Party files...";
                    Application.DoEvents();
                }

                KillDNSPartyProcesses();

                if (!Directory.Exists(_installDir))
                {
                    Directory.CreateDirectory(_installDir);
                }

                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream payloadStream = asm.GetManifestResourceStream("DNSParty.Payload.zip"))
                {
                    if (payloadStream == null)
                    {
                        throw new InvalidOperationException("Embedded payload archive not found.");
                    }

                    string tempZip = Path.Combine(Path.GetTempPath(), "DNSParty_Setup_" + Guid.NewGuid().ToString("N") + ".zip");
                    try
                    {
                        using (FileStream fs = File.Create(tempZip))
                        {
                            payloadStream.CopyTo(fs);
                        }

                        using (ZipArchive archive = ZipFile.OpenRead(tempZip))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string fullPath = Path.Combine(_installDir, entry.FullName);
                                string dir = Path.GetDirectoryName(fullPath);
                                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                {
                                    Directory.CreateDirectory(dir);
                                }
                                if (!string.IsNullOrEmpty(entry.Name))
                                {
                                    entry.ExtractToFile(fullPath, true);
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (File.Exists(tempZip)) File.Delete(tempZip);
                    }
                }

                string targetExe = Path.Combine(_installDir, "DNSParty.exe");

                if (silent || _chkDesktopShortcut.Checked)
                {
                    string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DNS Party.lnk");
                    CreateShortcut(desktopLink, targetExe, _installDir, "DNS Party — Windows DNS Manager");
                }

                if (silent || _chkStartMenuShortcut.Checked)
                {
                    string programsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");
                    string startLink = Path.Combine(programsDir, "DNS Party.lnk");
                    CreateShortcut(startLink, targetExe, _installDir, "DNS Party — Windows DNS Manager");
                }

                string uninstallerPath = Path.Combine(_installDir, "Uninstall.exe");
                try
                {
                    if (!string.Equals(Application.ExecutablePath, uninstallerPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(Application.ExecutablePath, uninstallerPath, true);
                    }
                }
                catch { }

                RegisterUninstaller(targetExe, uninstallerPath);

                if (!silent)
                {
                    _lblStatus.Text = "Installation completed successfully!";
                    if (_chkLaunchNow.Checked && File.Exists(targetExe))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = targetExe,
                            WorkingDirectory = _installDir,
                            UseShellExecute = true
                        });
                    }
                    MessageBox.Show(this, $"DNS Party v{DNSParty.VersionInfo.CurrentVersion} has been installed to:\r\n" + _installDir, "DNS Party Setup", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    Close();
                }

                return true;
            }
            catch (Exception ex)
            {
                if (!silent)
                {
                    _lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
                    _lblStatus.Text = "Installation failed: " + ex.Message;
                    _btnInstall.Enabled = true;
                }
                return false;
            }
        }

        private void RunUninstall()
        {
            try
            {
                KillDNSPartyProcesses();

                bool purge = _isPurge;
                if (!_isSilent && !purge)
                {
                    var result = MessageBox.Show(
                        "Do you also want to delete personal settings (configurations, logs, etc.)?",
                        "DNS Party Uninstall",
                        MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Cancel)
                    {
                        return;
                    }
                    if (result == DialogResult.Yes)
                    {
                        purge = true;
                    }
                }

                if (purge)
                {
                    string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DNSParty");
                    if (Directory.Exists(configDir))
                    {
                        try { Directory.Delete(configDir, true); } catch { }
                    }
                }

                string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DNS Party.lnk");
                if (File.Exists(desktopLink)) try { File.Delete(desktopLink); } catch { }

                string startLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "DNS Party.lnk");
                if (File.Exists(startLink)) try { File.Delete(startLink); } catch { }

                if (Directory.Exists(_installDir))
                {
                    try
                    {
                        foreach (string file in Directory.GetFiles(_installDir))
                        {
                            if (!string.Equals(file, Application.ExecutablePath, StringComparison.OrdinalIgnoreCase))
                            {
                                try { File.Delete(file); } catch { }
                            }
                        }
                        foreach (string dir in Directory.GetDirectories(_installDir))
                        {
                            try { Directory.Delete(dir, true); } catch { }
                        }
                        
                        try { Directory.Delete(_installDir, false); } catch { }
                    }
                    catch { }
                }

                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    if (key != null) try { key.DeleteSubKeyTree("DNSParty", false); } catch { }
                }
            }
            catch { }
        }

        private void RegisterUninstaller(string targetExe, string uninstallerPath)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DNSParty"))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "DNS Party — Windows DNS Manager");
                        key.SetValue("DisplayVersion", DNSParty.VersionInfo.CurrentVersion);
                        key.SetValue("Publisher", DNSParty.VersionInfo.Publisher);
                        key.SetValue("DisplayIcon", targetExe);
                        key.SetValue("InstallLocation", _installDir);
                        key.SetValue("UninstallString", "\"" + uninstallerPath + "\" /U");
                        key.SetValue("QuietUninstallString", "\"" + uninstallerPath + "\" /U /S");
                        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                        long sizeInBytes = 0;
                        if (Directory.Exists(_installDir))
                        {
                            try
                            {
                                foreach (string file in Directory.GetFiles(_installDir, "*", SearchOption.AllDirectories))
                                {
                                    sizeInBytes += new FileInfo(file).Length;
                                }
                            }
                            catch { }
                        }
                        key.SetValue("EstimatedSize", (int)(sizeInBytes / 1024), RegistryValueKind.DWord);
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = targetPath;
                shortcut.WorkingDirectory = workingDir;
                shortcut.Description = description;
                string icoPath = Path.Combine(workingDir, "app.ico");
                if (!File.Exists(icoPath)) icoPath = Path.Combine(workingDir, "dns-party.ico");
                shortcut.IconLocation = File.Exists(icoPath) ? icoPath : (targetPath + ",0");
                shortcut.Save();
            }
            catch { }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new InstallerForm(args ?? new string[0]);
            int cliResult = form.ExecuteCliIfRequested();
            if (cliResult >= 0)
            {
                return cliResult;
            }
            Application.Run(form);
            return 0;
        }
    }
}
