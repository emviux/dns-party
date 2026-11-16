using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("DNS Party Setup")]
[assembly: AssemblyDescription("DNS Party — Windows DNS Manager Installer")]
[assembly: AssemblyCompany("DNS Party")]
[assembly: AssemblyProduct("DNS Party Setup")]

namespace DNSPartySetup
{
    public class InstallerForm : Form
    {
        private enum WizardStep { Welcome, License, Location, Progress, Complete }
        private WizardStep _currentStep = WizardStep.Welcome;

        private string _installDir;
        private readonly bool _isSilent;
        private readonly bool _isUninstall;
        private readonly bool _isUpdate;
        private readonly bool _isPurge;

        private Panel _pnlContainer;
        private Panel _pnlWelcome;
        private Panel _pnlLicense;
        private Panel _pnlLocation;
        private Panel _pnlProgress;
        private Panel _pnlComplete;

        private Button _btnBack;
        private Button _btnNext;
        private Button _btnCancel;

        private CheckBox _chkAccept;
        private TextBox _txtLocation;
        private CheckBox _chkDesktopShortcut;
        private CheckBox _chkStartMenuShortcut;
        private CheckBox _chkLaunchNow;
        private Label _lblProgressStatus;
        private ProgressBar _progressBar;

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

            if (string.Equals(Path.GetFileName(Application.ExecutablePath), "Uninstall.exe", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileName(Application.ExecutablePath), "unins000.exe", StringComparison.OrdinalIgnoreCase))
            {
                _isUninstall = true;
            }

            Text = $"DNS Party v{DNSParty.VersionInfo.CurrentVersion} — Setup";
            Size = new Size(480, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(14, 16, 28);
            ForeColor = Color.FromArgb(248, 250, 252);
            Font = new Font("Segoe UI", 9.5f);

            LoadIcons();
            InitializeWizardUI();

            SetStep(WizardStep.Welcome);
        }

        private PictureBox CreateLogo()
        {
            PictureBox pic = new PictureBox { SizeMode = PictureBoxSizeMode.Zoom, Location = new Point(0, 10), Size = new Size(48, 48), BackColor = Color.Transparent };
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream pngStream = asm.GetManifestResourceStream("DNSParty.installer-logo-512.png")
                                ?? asm.GetManifestResourceStream("DNSParty.overlay-badge-256.png")
                                ?? asm.GetManifestResourceStream("DNSParty.dns-party-64.png");
                if (pngStream != null)
                {
                    pic.Image = new Bitmap(pngStream);
                }
            }
            catch { }
            return pic;
        }

        private void InitializeWizardUI()
        {
            _pnlContainer = new Panel { Location = new Point(24, 12), Size = new Size(416, 230) };
            Controls.Add(_pnlContainer);

            // Bottom bar
            _btnBack = new Button { Text = "< Back", BackColor = Color.FromArgb(30, 34, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(144, 264), Size = new Size(88, 36) };
            _btnBack.FlatAppearance.BorderSize = 0;
            _btnBack.Click += (s, e) => NavigateBack();

            _btnNext = new Button { Text = "Next >", BackColor = Color.FromArgb(124, 58, 237), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(242, 264), Size = new Size(100, 36) };
            _btnNext.FlatAppearance.BorderSize = 0;
            _btnNext.Click += (s, e) => NavigateNext();

            _btnCancel = new Button { Text = "Cancel", BackColor = Color.FromArgb(30, 34, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(352, 264), Size = new Size(88, 36) };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { if (_currentStep != WizardStep.Progress) Close(); };

            Controls.Add(_btnBack);
            Controls.Add(_btnNext);
            Controls.Add(_btnCancel);

            BuildWelcomePanel();
            BuildLicensePanel();
            BuildLocationPanel();
            BuildProgressPanel();
            BuildCompletePanel();
        }

        private void BuildWelcomePanel()
        {
            _pnlWelcome = new Panel { Dock = DockStyle.Fill, Visible = false };
            PictureBox logo = CreateLogo();
            
            Label title = new Label { Text = $"Welcome to DNS Party Setup", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Color.FromArgb(167, 139, 250), Location = new Point(60, 10), AutoSize = true };
            Label sub = new Label { Text = $"This wizard will guide you through the installation of DNS Party v{DNSParty.VersionInfo.CurrentVersion}.\r\n\r\nDNS Party is a lightweight, gaming-inspired Windows DNS Manager designed to help you quickly switch between DNS providers.\r\n\r\nClick Next to continue, or Cancel to exit Setup.", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(0, 80), Size = new Size(416, 120) };
            
            _pnlWelcome.Controls.Add(logo);
            _pnlWelcome.Controls.Add(title);
            _pnlWelcome.Controls.Add(sub);
            _pnlContainer.Controls.Add(_pnlWelcome);
        }

        private void BuildLicensePanel()
        {
            _pnlLicense = new Panel { Dock = DockStyle.Fill, Visible = false };
            Label title = new Label { Text = "License Agreement", Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Color.FromArgb(167, 139, 250), Location = new Point(0, 0), AutoSize = true };
            Label sub = new Label { Text = "Please read the following important information before continuing.", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(0, 26), Size = new Size(416, 20) };
            
            TextBox txtLicense = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Location = new Point(0, 50), Size = new Size(416, 130), BackColor = Color.FromArgb(11, 13, 24), ForeColor = Color.FromArgb(200, 210, 220), BorderStyle = BorderStyle.FixedSingle };
            
            string licenseText = "MIT License\r\n\r\nCopyright (c) 2026 DNS Party Contributors\r\n\r\nPermission is hereby granted, free of charge...";
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream("DNSParty.LICENSE"))
                {
                    if (stream != null)
                    {
                        using (StreamReader reader = new StreamReader(stream))
                        {
                            licenseText = reader.ReadToEnd();
                        }
                    }
                }
            }
            catch { }
            
            txtLicense.Text = licenseText;
            txtLicense.SelectionStart = 0;
            txtLicense.SelectionLength = 0;

            _chkAccept = new CheckBox { Text = "I accept the terms and conditions.", ForeColor = Color.White, Location = new Point(0, 190), Size = new Size(400, 24) };
            _chkAccept.CheckedChanged += (s, e) => { if (_currentStep == WizardStep.License) _btnNext.Enabled = _chkAccept.Checked; };

            _pnlLicense.Controls.Add(title);
            _pnlLicense.Controls.Add(sub);
            _pnlLicense.Controls.Add(txtLicense);
            _pnlLicense.Controls.Add(_chkAccept);
            _pnlContainer.Controls.Add(_pnlLicense);
        }

        private void BuildLocationPanel()
        {
            _pnlLocation = new Panel { Dock = DockStyle.Fill, Visible = false };
            Label title = new Label { Text = "Select Installation Location", Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Color.FromArgb(167, 139, 250), Location = new Point(0, 0), AutoSize = true };
            Label sub = new Label { Text = "Where should DNS Party be installed?", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(0, 26), Size = new Size(416, 20) };
            
            _txtLocation = new TextBox { Text = _installDir, Location = new Point(0, 60), Size = new Size(320, 26), BackColor = Color.FromArgb(11, 13, 24), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            Button btnBrowse = new Button { Text = "Browse...", BackColor = Color.FromArgb(30, 34, 54), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(328, 59), Size = new Size(88, 28) };
            btnBrowse.FlatAppearance.BorderSize = 0;
            btnBrowse.Click += (s, e) => {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog { SelectedPath = _txtLocation.Text, Description = "Select Installation Folder" })
                {
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        string selected = fbd.SelectedPath;
                        if (!selected.EndsWith("DNS Party", StringComparison.OrdinalIgnoreCase) && !selected.EndsWith("DNSParty", StringComparison.OrdinalIgnoreCase))
                        {
                            selected = Path.Combine(selected, "DNS Party");
                        }
                        if (!Directory.Exists(selected))
                        {
                            try { Directory.CreateDirectory(selected); } catch { }
                        }
                        _txtLocation.Text = selected;
                    }
                }
            };

            _chkDesktopShortcut = new CheckBox { Text = "Create Desktop shortcut", Checked = true, Location = new Point(0, 110), Size = new Size(380, 24), ForeColor = Color.White };
            _chkStartMenuShortcut = new CheckBox { Text = "Create Start Menu shortcut", Checked = true, Location = new Point(0, 140), Size = new Size(380, 24), ForeColor = Color.White };

            _pnlLocation.Controls.Add(title);
            _pnlLocation.Controls.Add(sub);
            _pnlLocation.Controls.Add(_txtLocation);
            _pnlLocation.Controls.Add(btnBrowse);
            _pnlLocation.Controls.Add(_chkDesktopShortcut);
            _pnlLocation.Controls.Add(_chkStartMenuShortcut);
            _pnlContainer.Controls.Add(_pnlLocation);
        }

        private void BuildProgressPanel()
        {
            _pnlProgress = new Panel { Dock = DockStyle.Fill, Visible = false };
            Label title = new Label { Text = "Installing", Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Color.FromArgb(167, 139, 250), Location = new Point(0, 0), AutoSize = true };
            Label sub = new Label { Text = "Please wait while DNS Party is installed on your computer.", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(0, 26), Size = new Size(416, 20) };
            
            _progressBar = new ProgressBar { Style = ProgressBarStyle.Marquee, Location = new Point(0, 80), Size = new Size(416, 20), MarqueeAnimationSpeed = 30 };
            _lblProgressStatus = new Label { Text = "Preparing to install...", ForeColor = Color.White, Location = new Point(0, 110), Size = new Size(416, 20) };

            _pnlProgress.Controls.Add(title);
            _pnlProgress.Controls.Add(sub);
            _pnlProgress.Controls.Add(_progressBar);
            _pnlProgress.Controls.Add(_lblProgressStatus);
            _pnlContainer.Controls.Add(_pnlProgress);
        }

        private void BuildCompletePanel()
        {
            _pnlComplete = new Panel { Dock = DockStyle.Fill, Visible = false };
            PictureBox logo = CreateLogo();
            
            Label title = new Label { Text = "Installation Complete", Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = Color.FromArgb(167, 139, 250), Location = new Point(60, 10), AutoSize = true };
            Label sub = new Label { Text = "DNS Party has been successfully installed on your computer.\r\n\r\nClick Finish to exit Setup.", ForeColor = Color.FromArgb(148, 163, 184), Location = new Point(0, 80), Size = new Size(416, 60) };
            
            _chkLaunchNow = new CheckBox { Text = "Launch DNS Party now", Checked = true, Location = new Point(0, 160), Size = new Size(380, 24), ForeColor = Color.White };

            _pnlComplete.Controls.Add(logo);
            _pnlComplete.Controls.Add(title);
            _pnlComplete.Controls.Add(sub);
            _pnlComplete.Controls.Add(_chkLaunchNow);
            _pnlContainer.Controls.Add(_pnlComplete);
        }

        private void SetStep(WizardStep step)
        {
            _currentStep = step;
            _pnlWelcome.Visible = (step == WizardStep.Welcome);
            _pnlLicense.Visible = (step == WizardStep.License);
            _pnlLocation.Visible = (step == WizardStep.Location);
            _pnlProgress.Visible = (step == WizardStep.Progress);
            _pnlComplete.Visible = (step == WizardStep.Complete);

            _btnBack.Enabled = true;
            _btnNext.Enabled = true;
            _btnCancel.Enabled = true;

            switch (step)
            {
                case WizardStep.Welcome:
                    _btnBack.Enabled = false;
                    _btnNext.Text = "Next >";
                    break;
                case WizardStep.License:
                    _btnNext.Text = "Next >";
                    _btnNext.Enabled = _chkAccept.Checked;
                    break;
                case WizardStep.Location:
                    _btnNext.Text = "Install";
                    break;
                case WizardStep.Progress:
                    _btnBack.Enabled = false;
                    _btnNext.Enabled = false;
                    _btnCancel.Enabled = false;
                    break;
                case WizardStep.Complete:
                    _btnBack.Enabled = false;
                    _btnCancel.Enabled = false;
                    _btnNext.Text = "Finish";
                    break;
            }
        }

        private void NavigateBack()
        {
            if (_currentStep == WizardStep.License) SetStep(WizardStep.Welcome);
            else if (_currentStep == WizardStep.Location) SetStep(WizardStep.License);
        }

        private void NavigateNext()
        {
            if (_currentStep == WizardStep.Welcome) SetStep(WizardStep.License);
            else if (_currentStep == WizardStep.License) SetStep(WizardStep.Location);
            else if (_currentStep == WizardStep.Location) StartInstallation();
            else if (_currentStep == WizardStep.Complete) FinishInstallation();
        }

        private void UpdateProgress(string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => UpdateProgress(status)));
                return;
            }
            _lblProgressStatus.Text = status;
        }

        private async void StartInstallation()
        {
            _installDir = _txtLocation.Text.Trim();
            if (string.IsNullOrEmpty(_installDir) || _installDir.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                MessageBox.Show("Please enter a valid installation path.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetStep(WizardStep.Progress);
            
            bool success = await Task.Run(() => RunInstallLogic(false));
            
            if (success)
            {
                SetStep(WizardStep.Complete);
            }
            else
            {
                SetStep(WizardStep.Location);
                MessageBox.Show("Installation failed. Please try again.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FinishInstallation()
        {
            if (_chkLaunchNow.Checked)
            {
                try
                {
                    string targetExe = Path.Combine(_installDir, "DNSParty.exe");
                    if (File.Exists(targetExe))
                    {
                        Process.Start(targetExe);
                    }
                }
                catch { }
            }
            Close();
        }

        private void LoadIcons()
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream icoStream = asm.GetManifestResourceStream("DNSParty.app.ico") ?? asm.GetManifestResourceStream("DNSParty.dns-party.ico");
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
            catch { }
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
                return RunInstallLogic(true) ? 0 : 1;
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

        private bool RunInstallLogic(bool silent)
        {
            try
            {
                UpdateProgress("Stopping existing processes...");
                KillDNSPartyProcesses();

                if (!Directory.Exists(_installDir))
                {
                    Directory.CreateDirectory(_installDir);
                }

                UpdateProgress("Extracting DNS Party files...");
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream payloadStream = asm.GetManifestResourceStream("DNSParty.Payload.zip"))
                {
                    if (payloadStream == null) throw new InvalidOperationException("Embedded payload archive not found.");

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

                UpdateProgress("Extracting files...");
                string targetExe = Path.Combine(_installDir, "DNSParty.exe");
                string uninstallerPath = Path.Combine(_installDir, "Unins000.exe");

                bool doDesktop = silent ? true : (_chkDesktopShortcut != null && _chkDesktopShortcut.Checked);
                bool doStartMenu = silent ? true : (_chkStartMenuShortcut != null && _chkStartMenuShortcut.Checked);

                if (doDesktop)
                {
                    UpdateProgress("Creating Desktop shortcut...");
                    string desktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "DNS Party.lnk");
                    CreateShortcut(desktopLink, targetExe, _installDir, "DNS Party — Windows DNS Manager");
                }

                if (doStartMenu)
                {
                    UpdateProgress("Creating Start Menu shortcut...");
                    string startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs", "DNS Party");
                    if (!Directory.Exists(startMenuDir)) Directory.CreateDirectory(startMenuDir);

                    string startMenuLink = Path.Combine(startMenuDir, "DNS Party.lnk");
                    CreateShortcut(startMenuLink, targetExe, _installDir, "DNS Party");

                    string uninstallLink = Path.Combine(startMenuDir, "Uninstall DNS Party.lnk");
                    CreateShortcut(uninstallLink, uninstallerPath, _installDir, "Uninstall DNS Party");
                }

                UpdateProgress("Registering application...");
                try
                {
                    string runKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKey, true))
                    {
                        if (key != null && key.GetValue("DNSParty") != null)
                        {
                            key.SetValue("DNSParty", "\"" + targetExe + "\" --minimized");
                        }
                    }

                    string uninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DNSParty";
                    using (RegistryKey key = Registry.CurrentUser.CreateSubKey(uninstallKey))
                    {
                        if (key != null)
                        {
                            key.SetValue("DisplayName", "DNS Party");
                            key.SetValue("DisplayVersion", DNSParty.VersionInfo.CurrentVersion);
                            key.SetValue("Publisher", "DNS Party");
                            key.SetValue("DisplayIcon", targetExe + ",0");
                            key.SetValue("InstallLocation", _installDir);
                            key.SetValue("UninstallString", "\"" + uninstallerPath + "\" /U");
                            key.SetValue("QuietUninstallString", "\"" + uninstallerPath + "\" /U /S");
                            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                            key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                            
                            try
                            {
                                long size = 0;
                                foreach (string f in Directory.GetFiles(_installDir, "*", SearchOption.AllDirectories))
                                {
                                    size += new FileInfo(f).Length;
                                }
                                key.SetValue("EstimatedSize", (int)(size / 1024), RegistryValueKind.DWord);
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                if (silent && _isUpdate)
                {
                    try
                    {
                        if (File.Exists(targetExe)) Process.Start(targetExe);
                    }
                    catch { }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void RunUninstall()
        {
            try
            {
                bool purgeData = _isPurge;
                if (!_isSilent && !_isPurge)
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

                if (!string.IsNullOrEmpty(_installDir) && Directory.Exists(_installDir))
                {
                    string currentExe = Application.ExecutablePath;
                    foreach (string f in Directory.GetFiles(_installDir))
                    {
                        if (!string.Equals(f, currentExe, StringComparison.OrdinalIgnoreCase))
                        {
                            try { File.Delete(f); } catch { }
                        }
                    }
                    foreach (string d in Directory.GetDirectories(_installDir))
                    {
                        try { Directory.Delete(d, true); } catch { }
                    }

                    try
                    {
                        string batchPath = Path.Combine(Path.GetTempPath(), "remove_dnsparty.bat");
                        File.WriteAllText(batchPath, "@echo off\r\nping 127.0.0.1 -n 2 > nul\r\nrmdir /s /q \"" + _installDir + "\"\r\ndel \"" + batchPath + "\"");
                        ProcessStartInfo psi = new ProcessStartInfo { FileName = "cmd.exe", Arguments = "/c \"" + batchPath + "\"", WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true };
                        Process.Start(psi);
                    }
                    catch { }
                }

                if (!_isSilent)
                {
                    MessageBox.Show("DNS Party was successfully removed from your computer.", "Uninstall Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                if (!_isSilent)
                {
                    MessageBox.Show("An error occurred during uninstallation: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
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
