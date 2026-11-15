using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using DNSParty.Models;
using DNSParty.Services;

namespace DNSParty.UI
{
    public enum NavTab
    {
        Home = 0,
        Favorites = 1,
        Settings = 2,
        About = 3
    }

    public class MainForm : Form
    {
        [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
        private static extern IntPtr CreateRoundRectRgn(
            int nLeftRect,
            int nTopRect,
            int nRightRect,
            int nBottomRect,
            int nWidthEllipse,
            int nHeightEllipse);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private readonly ConfigService _configService;
        private readonly DnsService _dnsService;
        private readonly SoundService _soundService;
        private readonly bool _startMinimized;

        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _trayMenu;
        private ContextMenuStrip _cardMenu;
        private OverlayForm _overlayForm;
        private HotkeyService _hotkeyService;

        private NavTab _activeTab = NavTab.Home;
        private string _selectedProfileId = string.Empty;
        private DnsStatusSnapshot _currentSnapshot;
        private bool _isExitingApplication;
        private bool _isHiddenInTray;
        private bool _isBusy;
        private bool _isRecordingOverlayShortcut;
        private string _busyText = string.Empty;
        private string _toastText = string.Empty;
        private bool _toastIsError;
        private System.Windows.Forms.Timer _toastTimer;

        // Latency cache per profile ID
        private readonly Dictionary<string, DnsTestResult> _testResults = new Dictionary<string, DnsTestResult>(StringComparer.OrdinalIgnoreCase);

        // Scroll offset for profile list and settings
        private int _profileScrollOffset;
        private int _maxProfileScroll;

        // Hit-test rectangles
        private Rectangle _titleBarRect;
        private Rectangle _minBtnRect;
        private Rectangle _closeBtnRect;

        private readonly Rectangle[] _sidebarItemRects = new Rectangle[5];
        private readonly List<CardHitBox> _cardHitBoxes = new List<CardHitBox>();

        private Rectangle _btnAddDnsRect;
        private Rectangle _btnEditDnsRect;
        private Rectangle _btnDeleteDnsRect;
        private Rectangle _btnFlushBottomRect;

        private Rectangle _qaDisableRect;
        private Rectangle _qaFlushRect;
        private Rectangle _qaTestRect;

        // Settings hit-test rectangles
        private readonly Dictionary<string, Rectangle> _settingsHitRects = new Dictionary<string, Rectangle>();

        // Hover tracking
        private string _hoveredElement = string.Empty;
        private int _hoveredCardIndex = -1;
        private string _hoveredCardPart = string.Empty; // "card", "star", "more"

        // Dragging titlebar
        private bool _isDraggingTitleBar;
        private Point _dragStartPoint;

        private class CardHitBox
        {
            public DnsProfile Profile;
            public Rectangle CardRect;
            public Rectangle StarRect;
            public Rectangle MoreRect;
        }

        public MainForm(ConfigService configService, DnsService dnsService, SoundService soundService, bool startMinimized)
        {
            _configService = configService;
            _dnsService = dnsService;
            _soundService = soundService;
            _startMinimized = startMinimized || _configService.Settings.StartMinimized;

            _hotkeyService = new HotkeyService();
            _hotkeyService.RegisterShortcut(_configService.Settings.OverlayShortcut);
            _hotkeyService.HotkeyPressed += (s, ev) => ToggleOverlay();

            Theme.DarkMode = _configService.Settings.DarkMode;

            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.UserPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = false; // Never force above other apps
            BackColor = Theme.WindowBackground;
            ForeColor = Theme.TextPrimary;
            Font = Theme.CreateFont(9.5f);
            Icon = IconRenderer.GetAppIcon();
            Text = "DNS Party";
            ClientSize = new Size(1040, 680);
            MinimumSize = new Size(960, 620);
            KeyPreview = true;

            _toastTimer = new System.Windows.Forms.Timer { Interval = 4500 };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                _toastText = string.Empty;
                if (!_isHiddenInTray) Invalidate();
            };

            _currentSnapshot = _dnsService.GetActualDnsSnapshot(
                _configService.Profiles,
                _configService.Settings.SelectedAdapterId,
                _configService.Settings.AutoDetectAdapter,
                _configService.Settings.LastRequestedProfileId);

            if (!string.IsNullOrEmpty(_currentSnapshot.ActiveProfileId) &&
                _configService.Profiles.Any(p => string.Equals(p.Id, _currentSnapshot.ActiveProfileId, StringComparison.OrdinalIgnoreCase)))
            {
                _selectedProfileId = _currentSnapshot.ActiveProfileId;
            }
            else if (_configService.Profiles.Count > 0)
            {
                _selectedProfileId = _configService.Profiles[0].Id;
            }

            InitializeTrayAndMenus();

            _dnsService.NetworkConfigurationChanged += OnNetworkConfigurationChanged;

            if (_configService.RecoveredFromCorruption)
            {
                ShowBannerNotification("Corrupted config file recovered and backed up. Default settings restored.", false);
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                // CS_DROPSHADOW = 0x00020000
                cp.ClassStyle |= 0x00020000;
                // When hidden in tray, apply WS_EX_TOOLWINDOW (0x80) so Windows never shows it in Alt+Tab
                if (_isHiddenInTray)
                {
                    cp.ExStyle |= 0x00000080;
                }
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyRoundedWindowRegion();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (_startMinimized)
            {
                HideToTray(false);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedWindowRegion();

            if (WindowState == FormWindowState.Minimized && _configService.Settings.MinimizeToTray)
            {
                HideToTray(false);
            }
            else
            {
                RecalculateLayout();
                Invalidate();
            }
        }

        private void ApplyRoundedWindowRegion()
        {
            if ( Width <= 0 || Height <= 0 || !IsHandleCreated) return;
            try
            {
                int pref = 2; // DWMWCP_ROUND on Windows 11
                int hr = DwmSetWindowAttribute(Handle, 33, ref pref, sizeof(int));
                if (hr != 0)
                {
                    IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 20, 20);
                    Region = Region.FromHrgn(rgn);
                }
            }
            catch
            {
                try
                {
                    IntPtr rgn = CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 20, 20);
                    Region = Region.FromHrgn(rgn);
                }
                catch { }
            }
        }

        private void InitializeTrayAndMenus()
        {
            _trayMenu = new ContextMenuStrip
            {
                Renderer = new TrayMenuRenderer(),
                ShowImageMargin = true,
                ShowCheckMargin = true,
                BackColor = Theme.CardBackground,
                ForeColor = Theme.TextPrimary,
                Font = Theme.CreateFont(9.2f)
            };

            _cardMenu = new ContextMenuStrip
            {
                Renderer = new TrayMenuRenderer(),
                ShowImageMargin = true,
                BackColor = Theme.CardBackground,
                ForeColor = Theme.TextPrimary,
                Font = Theme.CreateFont(9.2f)
            };

            _notifyIcon = new NotifyIcon
            {
                Icon = IconRenderer.GetTrayIcon(),
                Text = "DNS Party — Windows DNS Manager",
                Visible = true,
                ContextMenuStrip = _trayMenu
            };

            _notifyIcon.MouseClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ShowFromTray();
                }
            };

            _notifyIcon.BalloonTipClicked += (s, e) => ShowFromTray();

            RebuildTrayMenu();
        }

        public void RebuildTrayMenu()
        {
            if (_trayMenu == null) return;
            _trayMenu.SuspendLayout();
            _trayMenu.Items.Clear();
            _trayMenu.BackColor = Theme.CardBackground;
            _trayMenu.ForeColor = Theme.TextPrimary;

            // Header: DNS Party (Open DNS Party)
            var headerItem = new ToolStripMenuItem("DNS Party")
            {
                Font = Theme.CreateBoldFont(9.5f),
                Image = TrayMenuRenderer.CreateMenuIconBitmap("app-logo", Theme.AccentViolet, 18),
                ToolTipText = "Open DNS Party"
            };
            headerItem.Click += (s, e) => ShowFromTray();
            _trayMenu.Items.Add(headerItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            // Quick DNS Switching section
            string activeId = _currentSnapshot != null ? _currentSnapshot.ActiveProfileId : string.Empty;

            // Show built-in providers + any user favorites in quick switch
            var quickProfiles = new List<DnsProfile>();
            foreach (var p in _configService.Profiles)
            {
                if (p.IsDefaultProvider || p.IsFavorite)
                {
                    quickProfiles.Add(p);
                }
            }

            // If user deleted all defaults/favorites, show up to 5 available profiles
            if (quickProfiles.Count == 0)
            {
                quickProfiles.AddRange(_configService.Profiles.Take(5));
            }

            foreach (var profile in quickProfiles)
            {
                DnsProfile captured = profile;
                bool isActive = !_currentSnapshot.IsDhcpDns &&
                                string.Equals(activeId, captured.Id, StringComparison.OrdinalIgnoreCase);

                string label = isActive
                    ? string.Format("{0} ({1})", captured.Name, captured.PrimaryDns)
                    : (captured.IsFavorite && !captured.IsDefaultProvider
                        ? string.Format("★ {0} ({1})", captured.Name, captured.PrimaryDns)
                        : captured.Name);

                var item = new ToolStripMenuItem(label)
                {
                    Checked = isActive,
                    Font = isActive ? Theme.CreateSemiboldFont(9.2f) : Theme.CreateFont(9.2f)
                };
                item.Click += async (s, e) => await ApplyDnsProfileAsync(captured, true);
                _trayMenu.Items.Add(item);
            }

            // Favorites submenu if user has favorites
            var favs = _configService.Profiles.Where(p => p.IsFavorite).ToList();
            if (favs.Count > 0)
            {
                var favMenu = new ToolStripMenuItem("User Favorites")
                {
                    Image = TrayMenuRenderer.CreateMenuIconBitmap("star", Theme.StarGold, 16)
                };
                favMenu.DropDown.Renderer = new TrayMenuRenderer();
                favMenu.DropDown.BackColor = Theme.CardBackground;
                favMenu.DropDown.ForeColor = Theme.TextPrimary;

                foreach (var fav in favs)
                {
                    DnsProfile capturedFav = fav;
                    bool isActive = !_currentSnapshot.IsDhcpDns &&
                                    string.Equals(activeId, capturedFav.Id, StringComparison.OrdinalIgnoreCase);
                    var subItem = new ToolStripMenuItem(string.Format("{0} ({1})", capturedFav.Name, capturedFav.PrimaryDns))
                    {
                        Checked = isActive
                    };
                    subItem.Click += async (s, e) => await ApplyDnsProfileAsync(capturedFav, true);
                    favMenu.DropDownItems.Add(subItem);
                }
                _trayMenu.Items.Add(favMenu);
            }

            _trayMenu.Items.Add(new ToolStripSeparator());

            // Actions: Disable DNS & Flush DNS Cache
            var disableItem = new ToolStripMenuItem("Disable DNS")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("disable", Theme.TextPrimary, 17),
                Checked = _currentSnapshot != null && _currentSnapshot.IsDhcpDns
            };
            disableItem.Click += async (s, e) => await DisableDnsAsync(true);
            _trayMenu.Items.Add(disableItem);

            var flushItem = new ToolStripMenuItem("Flush DNS Cache")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("flush", Theme.TextPrimary, 17)
            };
            flushItem.Click += async (s, e) => await FlushDnsCacheAsync(true);
            _trayMenu.Items.Add(flushItem);

            _trayMenu.Items.Add(new ToolStripSeparator());

            // Application: Open DNS Party, In-Game Overlay, Settings, Exit
            var openItem = new ToolStripMenuItem("Open DNS Party")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("app-logo", Theme.AccentViolet, 17)
            };
            openItem.Click += (s, e) => ShowFromTray();
            _trayMenu.Items.Add(openItem);

            var overlayItem = new ToolStripMenuItem("In-Game Overlay (" + (_configService.Settings.OverlayShortcut ?? "Shift+Tab") + ")")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("gamepad", Theme.AccentViolet, 17)
            };
            overlayItem.Click += (s, e) => ToggleOverlay();
            _trayMenu.Items.Add(overlayItem);

            var settingsItem = new ToolStripMenuItem("Settings")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("settings", Theme.TextSecondary, 17)
            };
            settingsItem.Click += (s, e) =>
            {
                _activeTab = NavTab.Settings;
                ShowFromTray();
            };
            _trayMenu.Items.Add(settingsItem);

            var exitItem = new ToolStripMenuItem("Exit")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("power", Theme.DangerRed, 17)
            };
            exitItem.Click += (s, e) => ExitApplication();
            _trayMenu.Items.Add(exitItem);

            _trayMenu.ResumeLayout();

            // Update tray icon state & tooltip (< 63 chars)
            if (_notifyIcon != null)
            {
                string trayState = _isBusy
                    ? "changing"
                    : (_currentSnapshot == null || !_currentSnapshot.IsConnected
                        ? "error"
                        : (!_currentSnapshot.IsDhcpDns ? "connected" : "default"));
                _notifyIcon.Icon = IconRenderer.GetTrayIcon(trayState);

                string statusShort = (_currentSnapshot != null && !_currentSnapshot.IsDhcpDns)
                    ? ("Active: " + _currentSnapshot.ActiveProfileName)
                    : "Automatic (DHCP)";
                string tip = "DNS Party - " + statusShort;
                if (tip.Length >= 63) tip = tip.Substring(0, 62);
                _notifyIcon.Text = tip;
            }
        }

        public OverlayForm OverlayWindow
        {
            get
            {
                if (_overlayForm == null || _overlayForm.IsDisposed)
                {
                    _overlayForm = new OverlayForm(_configService, _dnsService, _soundService, () =>
                    {
                        RefreshActualDnsState();
                    });
                }
                return _overlayForm;
            }
        }

        public void ShowOverlay()
        {
            OverlayWindow.ShowOverlay();
        }

        public void HideOverlay()
        {
            if (_overlayForm != null && !_overlayForm.IsDisposed)
            {
                _overlayForm.HideOverlay();
            }
        }

        public void ToggleOverlay()
        {
            OverlayWindow.ToggleOverlay();
        }

        public void HideToTray(bool showNotification = false)
        {
            _isHiddenInTray = true;
            _toastTimer.Stop();
            ShowInTaskbar = false;
            Visible = false;
            Hide();
            if (IsHandleCreated)
            {
                UpdateStyles();
            }

            if (showNotification && _configService.Settings.EnableNotifications && _notifyIcon != null)
            {
                _notifyIcon.ShowBalloonTip(
                    2000,
                    "DNS Party is running in the background",
                    "Use the system tray icon to quickly switch DNS servers.",
                    ToolTipIcon.Info);
            }
        }

        public void ShowFromTray()
        {
            _isHiddenInTray = false;
            ShowInTaskbar = true;
            if (IsHandleCreated)
            {
                UpdateStyles();
            }
            Show();
            WindowState = FormWindowState.Normal;
            TopMost = false;
            Activate();
            BringToFront();
            RefreshActualDnsState();
            RecalculateLayout();
            Invalidate();
        }

        public void ExitApplication()
        {
            _isExitingApplication = true;
            _toastTimer.Stop();

            if (_hotkeyService != null)
            {
                _hotkeyService.Dispose();
                _hotkeyService = null;
            }

            if (_overlayForm != null && !_overlayForm.IsDisposed)
            {
                _overlayForm.Close();
                _overlayForm.Dispose();
                _overlayForm = null;
            }
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            Close();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_isExitingApplication &&
                e.CloseReason == CloseReason.UserClosing &&
                _configService.Settings.CloseMinimizesToTray)
            {
                e.Cancel = true;
                HideToTray(false);
                return;
            }

            if (_hotkeyService != null)
            {
                _hotkeyService.Dispose();
                _hotkeyService = null;
            }

            if (_overlayForm != null && !_overlayForm.IsDisposed)
            {
                _overlayForm.Close();
                _overlayForm.Dispose();
                _overlayForm = null;
            }

            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            base.OnFormClosing(e);
        }

        private void OnNetworkConfigurationChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(RefreshActualDnsState));
                }
                else
                {
                    RefreshActualDnsState();
                }
            }
            catch
            {
                // Ignore if handle not created yet
            }
        }

        public void RefreshActualDnsState()
        {
            _currentSnapshot = _dnsService.GetActualDnsSnapshot(
                _configService.Profiles,
                _configService.Settings.SelectedAdapterId,
                _configService.Settings.AutoDetectAdapter,
                _configService.Settings.LastRequestedProfileId);

            RebuildTrayMenu();
            if (!_isHiddenInTray)
            {
                RecalculateLayout();
                Invalidate();
            }
        }

        private void ShowBannerNotification(string message, bool isError, bool showTrayBalloon = false)
        {
            _toastText = message ?? string.Empty;
            _toastIsError = isError;

            if (!_isHiddenInTray)
            {
                _toastTimer.Stop();
                _toastTimer.Start();
                Invalidate();
            }

            if ((showTrayBalloon || _isHiddenInTray) && _configService.Settings.EnableNotifications && _notifyIcon != null)
            {
                try
                {
                    _notifyIcon.ShowBalloonTip(
                        2500,
                        isError ? "DNS Party — Notice" : "DNS Party",
                        message,
                        isError ? ToolTipIcon.Warning : ToolTipIcon.Info);
                }
                catch { }
            }
        }

        private List<DnsProfile> GetVisibleProfiles()
        {
            if (_activeTab == NavTab.Favorites)
            {
                return _configService.Profiles.Where(p => p.IsFavorite).ToList();
            }
            return _configService.Profiles.ToList();
        }

        public async Task ApplyDnsProfileAsync(DnsProfile profile, bool triggeredFromTray = false)
        {
            if (profile == null) return;
            if (_dnsService.IsOperationInProgress)
            {
                ShowBannerNotification("Another DNS operation is already in progress.", true, triggeredFromTray);
                return;
            }

            _selectedProfileId = profile.Id;
            _configService.Settings.LastRequestedProfileId = profile.Id;
            _isBusy = true;
            _busyText = "Applying " + profile.Name + "...";
            if (!_isHiddenInTray) Invalidate();

            DnsOperationResult res = await _dnsService.ApplyDnsProfileAsync(
                profile,
                _configService.Settings.SelectedAdapterId,
                _configService.Settings.AutoDetectAdapter,
                _configService.Profiles);

            _isBusy = false;
            _busyText = string.Empty;

            if (res.Snapshot != null)
            {
                _currentSnapshot = res.Snapshot;
            }
            else
            {
                RefreshActualDnsState();
            }

            if (res.Success)
            {
                _configService.Settings.LastChangeText = "Today, " + DateTime.Now.ToString("HH:mm");
                _configService.Save();
                _soundService.PlayConnect(_configService.Settings.EnableSounds);
                ShowBannerNotification(res.Message, false, triggeredFromTray);
            }
            else
            {
                ShowBannerNotification(res.Message, true, triggeredFromTray);
            }

            RebuildTrayMenu();
            if (!_isHiddenInTray)
            {
                RecalculateLayout();
                Invalidate();
            }
        }

        public async Task DisableDnsAsync(bool triggeredFromTray = false)
        {
            if (_dnsService.IsOperationInProgress)
            {
                ShowBannerNotification("Another DNS operation is already in progress.", true, triggeredFromTray);
                return;
            }

            if (_configService.Settings.ConfirmBeforeDisable && !triggeredFromTray)
            {
                var confirm = MessageBox.Show(
                    this,
                    "Restore Windows Automatic (DHCP) DNS configuration on the active network adapter?",
                    "Disable Custom DNS",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.Yes)
                {
                    return;
                }
            }

            _isBusy = true;
            _busyText = "Restoring Automatic (DHCP) DNS...";
            if (!_isHiddenInTray) Invalidate();

            DnsOperationResult res = await _dnsService.DisableDnsAsync(
                _configService.Settings.SelectedAdapterId,
                _configService.Settings.AutoDetectAdapter,
                _configService.Profiles);

            _isBusy = false;
            _busyText = string.Empty;

            if (res.Snapshot != null)
            {
                _currentSnapshot = res.Snapshot;
            }
            else
            {
                RefreshActualDnsState();
            }

            if (res.Success)
            {
                _configService.Settings.LastChangeText = "Today, " + DateTime.Now.ToString("HH:mm");
                _configService.Save();
                _soundService.PlayDisconnect(_configService.Settings.EnableSounds);
                ShowBannerNotification(res.Message, false, triggeredFromTray);
            }
            else
            {
                ShowBannerNotification(res.Message, true, triggeredFromTray);
            }

            RebuildTrayMenu();
            if (!_isHiddenInTray)
            {
                RecalculateLayout();
                Invalidate();
            }
        }

        public async Task FlushDnsCacheAsync(bool triggeredFromTray = false)
        {
            _isBusy = true;
            _busyText = "Flushing DNS Cache...";
            if (!_isHiddenInTray) Invalidate();

            DnsOperationResult res = await _dnsService.FlushDnsCacheAsync();

            _isBusy = false;
            _busyText = string.Empty;

            ShowBannerNotification(res.Message, !res.Success, triggeredFromTray || _configService.Settings.EnableNotifications);
            if (!_isHiddenInTray) Invalidate();
        }

        public async Task TestAllOrSelectedDnsAsync(DnsProfile specificProfile = null)
        {
            var targets = specificProfile != null
                ? new List<DnsProfile> { specificProfile }
                : GetVisibleProfiles();

            if (targets.Count == 0) return;

            _isBusy = true;
            _busyText = specificProfile != null
                ? ("Testing " + specificProfile.Name + "...")
                : "Testing DNS servers...";
            if (!_isHiddenInTray) Invalidate();

            foreach (var p in targets)
            {
                DnsTestResult r = await _dnsService.TestDnsServerAsync(p.PrimaryDns, 1800);
                _testResults[p.Id] = r;
                if (!_isHiddenInTray) Invalidate();
            }

            _isBusy = false;
            _busyText = string.Empty;

            if (specificProfile != null && _testResults.ContainsKey(specificProfile.Id))
            {
                var r = _testResults[specificProfile.Id];
                string msg = r.IsReachable
                    ? string.Format("{0} ({1}) is reachable — Response time: {2} ms", specificProfile.Name, specificProfile.PrimaryDns, r.LatencyMs)
                    : string.Format("{0} ({1}) is unreachable ({2})", specificProfile.Name, specificProfile.PrimaryDns, r.Details);
                ShowBannerNotification(msg, !r.IsReachable, false);
            }
            else
            {
                var best = targets
                    .Where(t => _testResults.ContainsKey(t.Id) && _testResults[t.Id].IsReachable)
                    .OrderBy(t => _testResults[t.Id].LatencyMs)
                    .FirstOrDefault();

                if (best != null)
                {
                    ShowBannerNotification(
                        string.Format("DNS Test Complete — Fastest: {0} ({1} ms)", best.Name, _testResults[best.Id].LatencyMs),
                        false,
                        false);
                }
                else
                {
                    ShowBannerNotification("DNS Test Complete — No servers responded on UDP port 53.", true, false);
                }
            }

            if (!_isHiddenInTray) Invalidate();
        }

        private void OpenAddDnsDialog()
        {
            using (var dlg = new AddEditDnsDialog(null))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultProfile != null)
                {
                    _configService.Profiles.Add(dlg.ResultProfile);
                    _selectedProfileId = dlg.ResultProfile.Id;
                    _configService.Save();
                    RebuildTrayMenu();
                    RecalculateLayout();
                    Invalidate();
                    ShowBannerNotification("Added DNS profile \"" + dlg.ResultProfile.Name + "\".", false);
                }
            }
        }

        private void OpenEditDnsDialog(DnsProfile profile)
        {
            if (profile == null)
            {
                profile = GetSelectedOrActiveProfile();
            }
            if (profile == null)
            {
                ShowBannerNotification("Select a DNS profile to edit.", true);
                return;
            }

            using (var dlg = new AddEditDnsDialog(profile))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.ResultProfile != null)
                {
                    int idx = _configService.Profiles.FindIndex(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0)
                    {
                        // Preserve provider icon if editing a built-in profile
                        if (profile.IsDefaultProvider)
                        {
                            dlg.ResultProfile.IsDefaultProvider = true;
                            dlg.ResultProfile.IconKey = profile.IconKey;
                        }
                        _configService.Profiles[idx] = dlg.ResultProfile;
                        _configService.Save();
                        RefreshActualDnsState();
                        ShowBannerNotification("Updated DNS profile \"" + dlg.ResultProfile.Name + "\".", false);
                    }
                }
            }
        }

        private void DeleteDnsProfile(DnsProfile profile)
        {
            if (profile == null)
            {
                profile = GetSelectedOrActiveProfile();
            }
            if (profile == null)
            {
                ShowBannerNotification("Select a DNS profile to delete.", true);
                return;
            }

            var confirm = MessageBox.Show(
                this,
                string.Format("Are you sure you want to delete the DNS profile \"{0}\" ({1})?", profile.Name, profile.PrimaryDns),
                "Delete DNS Profile",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                _configService.Profiles.RemoveAll(p => string.Equals(p.Id, profile.Id, StringComparison.OrdinalIgnoreCase));
                if (string.Equals(_selectedProfileId, profile.Id, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedProfileId = _configService.Profiles.Count > 0 ? _configService.Profiles[0].Id : string.Empty;
                }
                _configService.Save();
                RefreshActualDnsState();
                ShowBannerNotification("Deleted DNS profile \"" + profile.Name + "\".", false);
            }
        }

        private DnsProfile GetSelectedOrActiveProfile()
        {
            if (!string.IsNullOrEmpty(_selectedProfileId))
            {
                var sel = _configService.Profiles.FirstOrDefault(p => string.Equals(p.Id, _selectedProfileId, StringComparison.OrdinalIgnoreCase));
                if (sel != null) return sel;
            }
            if (_currentSnapshot != null && !string.IsNullOrEmpty(_currentSnapshot.ActiveProfileId))
            {
                var act = _configService.Profiles.FirstOrDefault(p => string.Equals(p.Id, _currentSnapshot.ActiveProfileId, StringComparison.OrdinalIgnoreCase));
                if (act != null) return act;
            }
            return _configService.Profiles.FirstOrDefault();
        }

        private void ShowCardContextMenu(DnsProfile profile, Point screenLocation)
        {
            if (profile == null) return;
            _selectedProfileId = profile.Id;
            Invalidate();

            _cardMenu.Items.Clear();
            _cardMenu.BackColor = Theme.CardBackground;
            _cardMenu.ForeColor = Theme.TextPrimary;

            var applyItem = new ToolStripMenuItem("Connect / Apply " + profile.Name)
            {
                Font = Theme.CreateSemiboldFont(9.2f),
                Image = TrayMenuRenderer.CreateMenuIconBitmap("check", Theme.StatusGreen, 16)
            };
            applyItem.Click += async (s, e) => await ApplyDnsProfileAsync(profile, false);
            _cardMenu.Items.Add(applyItem);

            var testItem = new ToolStripMenuItem("Test Response Time")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("test", Theme.AccentViolet, 16)
            };
            testItem.Click += async (s, e) => await TestAllOrSelectedDnsAsync(profile);
            _cardMenu.Items.Add(testItem);

            var favItem = new ToolStripMenuItem(profile.IsFavorite ? "Remove from Favorites" : "Add to Favorites")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("star", Theme.StarGold, 16)
            };
            favItem.Click += (s, e) =>
            {
                profile.IsFavorite = !profile.IsFavorite;
                _configService.Save();
                RebuildTrayMenu();
                RecalculateLayout();
                Invalidate();
            };
            _cardMenu.Items.Add(favItem);

            _cardMenu.Items.Add(new ToolStripSeparator());

            var editItem = new ToolStripMenuItem("Edit DNS Profile")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("edit", Theme.TextPrimary, 16)
            };
            editItem.Click += (s, e) => OpenEditDnsDialog(profile);
            _cardMenu.Items.Add(editItem);

            var deleteItem = new ToolStripMenuItem("Delete DNS Profile")
            {
                Image = TrayMenuRenderer.CreateMenuIconBitmap("trash", Theme.DangerRed, 16)
            };
            deleteItem.Click += (s, e) => DeleteDnsProfile(profile);
            _cardMenu.Items.Add(deleteItem);

            _cardMenu.Show(screenLocation);
        }

        private void RecalculateLayout()
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            _titleBarRect = new Rectangle(0, 0, w, 52);
            _closeBtnRect = new Rectangle(w - 46, 12, 32, 28);
            _minBtnRect = new Rectangle(w - 84, 12, 32, 28);

            // Sidebar items (Left column: width 212)
            int navY = 68;
            for (int i = 0; i < 5; i++)
            {
                _sidebarItemRects[i] = new Rectangle(16, navY + i * 48, 180, 40);
            }

            // Center area bounds
            int centerLeft = 236;
            int rightColWidth = 266;
            int rightColLeft = w - rightColWidth - 24;
            int centerWidth = rightColLeft - centerLeft - 22;

            // Bottom action buttons
            int bottomY = h - 58;
            _btnAddDnsRect = new Rectangle(centerLeft, bottomY, 120, 40);
            _btnEditDnsRect = new Rectangle(centerLeft + 132, bottomY, 84, 40);
            _btnDeleteDnsRect = new Rectangle(centerLeft + 228, bottomY, 94, 40);
            _btnFlushBottomRect = new Rectangle(centerLeft + centerWidth - 122, bottomY, 122, 40);

            // Right column Quick Actions buttons
            int qaCardTop = 368;
            int qaBtnX = rightColLeft + 16;
            int qaBtnW = rightColWidth - 32;
            _qaDisableRect = new Rectangle(qaBtnX, qaCardTop + 48, qaBtnW, 54);
            _qaFlushRect = new Rectangle(qaBtnX, qaCardTop + 112, qaBtnW, 50);
            _qaTestRect = new Rectangle(qaBtnX, qaCardTop + 172, qaBtnW, 54);

            // DNS Cards in Center column
            _cardHitBoxes.Clear();
            if (_activeTab == NavTab.Home || _activeTab == NavTab.Favorites)
            {
                var visible = GetVisibleProfiles();
                int listTop = 132;
                int listBottom = bottomY - 14;
                int listHeight = Math.Max(100, listBottom - listTop);
                int cardH = 68;
                int cardGap = 10;
                int totalContentH = visible.Count * (cardH + cardGap);

                _maxProfileScroll = Math.Max(0, totalContentH - listHeight);
                if (_profileScrollOffset > _maxProfileScroll) _profileScrollOffset = _maxProfileScroll;
                if (_profileScrollOffset < 0) _profileScrollOffset = 0;

                for (int i = 0; i < visible.Count; i++)
                {
                    int cy = listTop + i * (cardH + cardGap) - _profileScrollOffset;
                    Rectangle cardRect = new Rectangle(centerLeft, cy, centerWidth, cardH);
                    Rectangle starRect = new Rectangle(cardRect.Right - 82, cy + (cardH - 30) / 2, 30, 30);
                    Rectangle moreRect = new Rectangle(cardRect.Right - 44, cy + (cardH - 30) / 2, 30, 30);

                    _cardHitBoxes.Add(new CardHitBox
                    {
                        Profile = visible[i],
                        CardRect = cardRect,
                        StarRect = starRect,
                        MoreRect = moreRect
                    });
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            Theme.ConfigureHighQuality(g);

            RecalculateLayout();

            int w = ClientSize.Width;
            int h = ClientSize.Height;

            // 1. Main Window Background
            using (SolidBrush bgBrush = new SolidBrush(Theme.WindowBackground))
            {
                g.FillRectangle(bgBrush, ClientRectangle);
            }

            // 2. Left Sidebar Background (below titlebar)
            Rectangle sidebarRect = new Rectangle(0, 52, 212, h - 52);
            using (SolidBrush sbBrush = new SolidBrush(Theme.SidebarBackground))
            using (Pen sbLine = new Pen(Theme.SidebarBorder, 1f))
            {
                g.FillRectangle(sbBrush, sidebarRect);
                g.DrawLine(sbLine, 212, 52, 212, h);
            }

            // 3. TitleBar
            DrawTitleBar(g, w);

            // 4. Sidebar Navigation & Footer
            DrawSidebar(g, h);

            // 5. Active View Content
            if (_activeTab == NavTab.Home || _activeTab == NavTab.Favorites)
            {
                DrawDnsManagerView(g);
                DrawRightColumn(g, w);
            }
            else if (_activeTab == NavTab.Settings)
            {
                DrawSettingsView(g, w, h);
            }
            else if (_activeTab == NavTab.About)
            {
                DrawAboutView(g, w, h);
            }

            // 6. Outer Window Border
            using (Pen borderPen = new Pen(Theme.WindowBorder, 1.5f))
            {
                Theme.DrawRoundedRect(g, borderPen, new Rectangle(1, 1, w - 2, h - 2), 14);
            }
        }

        private void DrawTitleBar(Graphics g, int w)
        {
            using (SolidBrush tbBrush = new SolidBrush(Theme.TitleBarBackground))
            using (Pen sepPen = new Pen(Theme.SidebarBorder, 1f))
            {
                g.FillRectangle(tbBrush, _titleBarRect);
                g.DrawLine(sepPen, 0, 52, w, 52);
            }

            // App Logo + "DNS Party"
            IconRenderer.DrawAppLogo(g, new RectangleF(20, 13, 26, 26), true);
            using (Font titleFont = Theme.CreateSemiboldFont(12f))
            using (SolidBrush textBrush = new SolidBrush(Theme.TextPrimary))
            {
                g.DrawString("DNS Party", titleFont, textBrush, 56, 14);
            }

            // Status / Toast notification pill in TitleBar center-right if active
            if (_isBusy && !string.IsNullOrEmpty(_busyText))
            {
                DrawTitleBarBadge(g, _busyText, Theme.AccentViolet, w);
            }
            else if (!string.IsNullOrEmpty(_toastText))
            {
                DrawTitleBarBadge(g, _toastText, _toastIsError ? Theme.DangerRed : Theme.StatusGreen, w);
            }

            // Minimize & Close buttons
            if (_hoveredElement == "min")
            {
                using (SolidBrush hb = new SolidBrush(Theme.SidebarHover))
                {
                    Theme.FillRoundedRect(g, hb, _minBtnRect, 6);
                }
            }
            IconRenderer.DrawIcon(g, "minimize", new RectangleF(_minBtnRect.X + 8, _minBtnRect.Y + 6, 16, 16), Theme.TextSecondary);

            if (_hoveredElement == "close")
            {
                using (SolidBrush hb = new SolidBrush(Theme.DangerRed))
                {
                    Theme.FillRoundedRect(g, hb, _closeBtnRect, 6);
                }
            }
            IconRenderer.DrawIcon(
                g,
                "close",
                new RectangleF(_closeBtnRect.X + 8, _closeBtnRect.Y + 6, 16, 16),
                _hoveredElement == "close" ? Color.White : Theme.TextSecondary);
        }

        private void DrawTitleBarBadge(Graphics g, string text, Color accentColor, int w)
        {
            using (Font f = Theme.CreateFont(8.8f))
            {
                SizeF sz = g.MeasureString(text, f);
                int pillW = Math.Min(480, (int)sz.Width + 32);
                int pillX = Math.Max(220, (w - pillW) / 2);
                Rectangle pillRect = new Rectangle(pillX, 12, pillW, 28);

                using (SolidBrush bg = new SolidBrush(Color.FromArgb(40, accentColor)))
                using (Pen border = new Pen(Color.FromArgb(140, accentColor), 1f))
                using (SolidBrush dot = new SolidBrush(accentColor))
                using (SolidBrush txt = new SolidBrush(Theme.TextPrimary))
                {
                    Theme.FillRoundedRect(g, bg, pillRect, 14);
                    Theme.DrawRoundedRect(g, border, pillRect, 14);
                    g.FillEllipse(dot, pillRect.X + 10, pillRect.Y + 10, 8, 8);

                    RectangleF textRect = new RectangleF(pillRect.X + 24, pillRect.Y + 5, pillRect.Width - 30, 20);
                    StringFormat sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
                    g.DrawString(text, f, txt, textRect, sf);
                }
            }
        }

        private void DrawSidebar(Graphics g, int h)
        {
            string[] labels = { "Home", "Favorites", "Add DNS", "Settings", "About" };
            string[] icons = { "home", "star", "plus", "settings", "info" };

            for (int i = 0; i < 5; i++)
            {
                Rectangle r = _sidebarItemRects[i];
                bool isActive = (i == 0 && _activeTab == NavTab.Home) ||
                                (i == 1 && _activeTab == NavTab.Favorites) ||
                                (i == 3 && _activeTab == NavTab.Settings) ||
                                (i == 4 && _activeTab == NavTab.About);
                bool isHovered = _hoveredElement == ("nav_" + i);

                if (isActive)
                {
                    Theme.FillRoundedGradient(g, r, 10, Theme.SidebarActiveStart, Theme.SidebarActiveEnd, LinearGradientMode.Horizontal);
                    using (Pen p = new Pen(Theme.SidebarActiveBorder, 1f))
                    {
                        Theme.DrawRoundedRect(g, p, r, 10);
                    }
                }
                else if (isHovered)
                {
                    using (SolidBrush hb = new SolidBrush(Theme.SidebarHover))
                    {
                        Theme.FillRoundedRect(g, hb, r, 10);
                    }
                }

                Color itemColor = isActive ? Color.White : (isHovered ? Theme.TextPrimary : Theme.TextSecondary);
                IconRenderer.DrawIcon(g, icons[i], new RectangleF(r.X + 14, r.Y + 10, 20, 20), itemColor, false);

                using (Font f = isActive ? Theme.CreateSemiboldFont(10f) : Theme.CreateFont(10f))
                using (SolidBrush tb = new SolidBrush(itemColor))
                {
                    g.DrawString(labels[i], f, tb, r.X + 46, r.Y + 10);
                }
            }

            // Bottom-left footer: audio wave icon + DNS Party v1.0.0
            RectangleF waveRect = new RectangleF(24, h - 54, 24, 24);
            IconRenderer.DrawAudioWaveIcon(g, waveRect, Theme.AccentViolet);

            using (Font fTitle = Theme.CreateSemiboldFont(9f))
            using (Font fVer = Theme.CreateFont(8.2f))
            using (SolidBrush bTitle = new SolidBrush(Theme.TextSecondary))
            using (SolidBrush bVer = new SolidBrush(Theme.TextMuted))
            {
                g.DrawString("DNS Party", fTitle, bTitle, 56, h - 58);
                g.DrawString("v1.0.0", fVer, bVer, 56, h - 41);
            }
        }

        private void DrawDnsManagerView(Graphics g)
        {
            int centerLeft = 236;
            int rightColWidth = 266;
            int rightColLeft = ClientSize.Width - rightColWidth - 24;
            int centerWidth = rightColLeft - centerLeft - 22;

            // Header
            string headerTitle = _activeTab == NavTab.Favorites ? "Favorite DNS Servers" : "DNS Manager";
            string headerSub = _activeTab == NavTab.Favorites
                ? "Your starred DNS servers for quick access here and in the system tray."
                : "Switch between DNS servers, flush cache, and manage your favorites.";

            using (Font hFont = Theme.CreateBoldFont(15.5f))
            using (Font sFont = Theme.CreateFont(9.5f))
            using (SolidBrush hBrush = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sBrush = new SolidBrush(Theme.TextSecondary))
            {
                g.DrawString(headerTitle, hFont, hBrush, centerLeft - 2, 68);
                g.DrawString(headerSub, sFont, sBrush, centerLeft, 100);
            }

            // Clip region for scrollable card list
            int listTop = 130;
            int listBottom = _btnAddDnsRect.Top - 12;
            Rectangle clipRect = new Rectangle(centerLeft - 4, listTop - 2, centerWidth + 8, listBottom - listTop + 4);
            Region oldClip = g.Clip;
            g.SetClip(clipRect, CombineMode.Intersect);

            if (_cardHitBoxes.Count == 0)
            {
                using (Font emptyFont = Theme.CreateFont(10.5f))
                using (SolidBrush emptyBrush = new SolidBrush(Theme.TextSecondary))
                {
                    string emptyMsg = _activeTab == NavTab.Favorites
                        ? "No favorite DNS profiles yet. Click the ★ icon on any DNS card to star it."
                        : "No DNS profiles configured. Click '+ Add DNS' below to create one.";
                    g.DrawString(emptyMsg, emptyFont, emptyBrush, centerLeft + 8, listTop + 30);
                }
            }
            else
            {
                string activeId = (_currentSnapshot != null && !_currentSnapshot.IsDhcpDns)
                    ? _currentSnapshot.ActiveProfileId
                    : string.Empty;

                for (int i = 0; i < _cardHitBoxes.Count; i++)
                {
                    var box = _cardHitBoxes[i];
                    if (box.CardRect.Bottom < listTop || box.CardRect.Top > listBottom)
                    {
                        continue;
                    }
                    bool isActive = !string.IsNullOrEmpty(activeId) &&
                                    string.Equals(activeId, box.Profile.Id, StringComparison.OrdinalIgnoreCase);
                    bool isSelected = string.Equals(_selectedProfileId, box.Profile.Id, StringComparison.OrdinalIgnoreCase);
                    bool isHovered = _hoveredCardIndex == i;

                    DrawDnsCard(g, box, isActive, isSelected, isHovered);
                }
            }

            g.Clip = oldClip;

            // Bottom Action Bar Buttons
            DrawPillButton(g, _btnAddDnsRect, "plus", "Add DNS", true, _hoveredElement == "btn_add");
            DrawPillButton(g, _btnEditDnsRect, "edit", "Edit", false, _hoveredElement == "btn_edit");
            DrawPillButton(g, _btnDeleteDnsRect, "trash", "Delete", false, _hoveredElement == "btn_delete");
            DrawPillButton(g, _btnFlushBottomRect, "flush", "Flush DNS", false, _hoveredElement == "btn_flush_bottom");
        }

        private void DrawDnsCard(Graphics g, CardHitBox box, bool isActive, bool isSelected, bool isHovered)
        {
            Rectangle r = box.CardRect;
            DnsProfile p = box.Profile;

            if (isActive)
            {
                Theme.FillRoundedGradient(g, r, 12, Theme.ActiveCardStart, Theme.ActiveCardEnd, LinearGradientMode.Horizontal);
                using (Pen glowPen = new Pen(Theme.ActiveCardBorder, 1.8f))
                {
                    Theme.DrawRoundedRect(g, glowPen, r, 12);
                }
            }
            else
            {
                Color bg = isHovered ? Theme.CardHoverBackground : Theme.CardBackground;
                Color border = isSelected
                    ? Color.FromArgb(140, Theme.AccentViolet)
                    : (isHovered ? Theme.CardHoverBorder : Theme.CardBorder);

                using (SolidBrush b = new SolidBrush(bg))
                using (Pen pen = new Pen(border, isSelected ? 1.4f : 1f))
                {
                    Theme.FillRoundedRect(g, b, r, 12);
                    Theme.DrawRoundedRect(g, pen, r, 12);
                }
            }

            // Left Provider Icon
            RectangleF iconRect = new RectangleF(r.X + 18, r.Y + 18, 32, 32);
            IconRenderer.DrawProviderIcon(g, p.IconKey, iconRect);

            // Title + optional Tag
            string displayName = (string.Equals(p.Id, "google", StringComparison.OrdinalIgnoreCase) && p.Name == "Google")
                ? "Google DNS"
                : p.Name;

            using (Font nameFont = Theme.CreateBoldFont(10.8f))
            using (Font tagFont = Theme.CreateFont(9.5f))
            using (SolidBrush nameBrush = new SolidBrush(Theme.TextPrimary))
            {
                float nameX = r.X + 66;
                float nameY = r.Y + 13;
                g.DrawString(displayName, nameFont, nameBrush, nameX, nameY);

                if (!string.IsNullOrWhiteSpace(p.Tag))
                {
                    SizeF nameSize = g.MeasureString(displayName, nameFont);
                    Color tagColor = string.Equals(p.Tag, "Recommended", StringComparison.OrdinalIgnoreCase)
                        ? Theme.TextTagPurple
                        : Theme.TextTagNeutral;
                    using (SolidBrush tagBrush = new SolidBrush(tagColor))
                    {
                        g.DrawString("(" + p.Tag + ")", tagFont, tagBrush, nameX + nameSize.Width + 2, nameY + 1);
                    }
                }
            }

            // Primary | Secondary DNS addresses
            using (Font ipFont = Theme.CreateFont(9.5f))
            using (SolidBrush ipBrush = new SolidBrush(isActive ? Theme.TextPrimary : Theme.TextSecondary))
            using (Pen sepPen = new Pen(Theme.TextMuted, 1f))
            {
                float ipX = r.X + 66;
                float ipY = r.Y + 38;
                g.DrawString(p.PrimaryDns, ipFont, ipBrush, ipX, ipY);

                if (!string.IsNullOrWhiteSpace(p.SecondaryDns))
                {
                    SizeF p1Size = g.MeasureString(p.PrimaryDns, ipFont);
                    float sepX = ipX + p1Size.Width + 10;
                    g.DrawLine(sepPen, sepX, ipY + 3, sepX, ipY + 15);
                    g.DrawString(p.SecondaryDns, ipFont, ipBrush, sepX + 12, ipY);
                }
            }

            // Optional DNS Test latency badge
            if (_testResults.ContainsKey(p.Id))
            {
                DnsTestResult tr = _testResults[p.Id];
                string badgeText = tr.IsReachable ? (tr.LatencyMs + " ms") : "Timeout";
                Color badgeColor = !tr.IsReachable
                    ? Theme.DangerRed
                    : (tr.LatencyMs < 50 ? Theme.StatusGreen : Theme.StatusWarning);

                using (Font bf = Theme.CreateSemiboldFont(8.2f))
                {
                    SizeF bsz = g.MeasureString(badgeText, bf);
                    Rectangle badgeRect = new Rectangle(box.StarRect.Left - (int)bsz.Width - 26, r.Y + (r.Height - 22) / 2, (int)bsz.Width + 16, 22);
                    using (SolidBrush bb = new SolidBrush(Color.FromArgb(35, badgeColor)))
                    using (Pen bp = new Pen(Color.FromArgb(120, badgeColor), 1f))
                    using (SolidBrush bt = new SolidBrush(badgeColor))
                    {
                        Theme.FillRoundedRect(g, bb, badgeRect, 10);
                        Theme.DrawRoundedRect(g, bp, badgeRect, 10);
                        g.DrawString(badgeText, bf, bt, badgeRect.X + 8, badgeRect.Y + 3);
                    }
                }
            }

            // Star Favorite Button
            RectangleF starBounds = new RectangleF(box.StarRect.X + 5, box.StarRect.Y + 5, 20, 20);
            Color starColor = p.IsFavorite ? Theme.StarGold : Theme.StarOutline;
            IconRenderer.DrawIcon(g, "star", starBounds, starColor, p.IsFavorite);

            // Active Purple Checkmark Circle OR 3-Dots More Button
            if (isActive && !(isHovered && _hoveredCardPart == "more"))
            {
                Rectangle checkCircle = new Rectangle(box.MoreRect.X + 2, box.MoreRect.Y + 2, 26, 26);
                using (SolidBrush cb = new SolidBrush(Theme.ActiveCardBorder))
                {
                    g.FillEllipse(cb, checkCircle);
                }
                IconRenderer.DrawIcon(g, "check", new RectangleF(checkCircle.X + 5, checkCircle.Y + 5, 16, 16), Color.White);
            }
            else
            {
                if (isHovered && _hoveredCardPart == "more")
                {
                    using (SolidBrush hb = new SolidBrush(Theme.SecondaryButtonHover))
                    {
                        g.FillEllipse(hb, box.MoreRect);
                    }
                }
                IconRenderer.DrawIcon(g, "more-vertical", new RectangleF(box.MoreRect.X + 5, box.MoreRect.Y + 5, 20, 20), Theme.TextSecondary);
            }
        }

        private void DrawPillButton(Graphics g, Rectangle rect, string iconName, string text, bool isPrimary, bool isHovered)
        {
            if (isPrimary)
            {
                Color c1 = isHovered ? Theme.PrimaryButtonHover : Theme.PrimaryButtonStart;
                Color c2 = isHovered ? Theme.PrimaryButtonHover : Theme.PrimaryButtonEnd;
                Theme.FillRoundedGradient(g, rect, 10, c1, c2, LinearGradientMode.Horizontal);
                using (Pen p = new Pen(Theme.PrimaryButtonBorder, 1f))
                {
                    Theme.DrawRoundedRect(g, p, rect, 10);
                }
            }
            else
            {
                Color bg = isHovered ? Theme.SecondaryButtonHover : Theme.SecondaryButtonBackground;
                using (SolidBrush b = new SolidBrush(bg))
                using (Pen p = new Pen(isHovered ? Theme.CardHoverBorder : Theme.SecondaryButtonBorder, 1f))
                {
                    Theme.FillRoundedRect(g, b, rect, 10);
                    Theme.DrawRoundedRect(g, p, rect, 10);
                }
            }

            Color fg = isPrimary ? Color.White : Theme.TextPrimary;
            IconRenderer.DrawIcon(g, iconName, new RectangleF(rect.X + 14, rect.Y + 11, 18, 18), fg);

            using (Font f = Theme.CreateSemiboldFont(9.5f))
            using (SolidBrush tb = new SolidBrush(fg))
            {
                g.DrawString(text, f, tb, rect.X + 38, rect.Y + 10);
            }
        }

        private void DrawRightColumn(Graphics g, int w)
        {
            int rightColWidth = 266;
            int rightColLeft = w - rightColWidth - 24;

            // 1. Top Card: DNS Active Status Card
            Rectangle statusCardRect = new Rectangle(rightColLeft, 72, rightColWidth, 276);
            using (SolidBrush cardBg = new SolidBrush(Theme.CardBackground))
            using (Pen cardBorder = new Pen(Theme.CardBorder, 1f))
            {
                Theme.FillRoundedRect(g, cardBg, statusCardRect, 14);
                Theme.DrawRoundedRect(g, cardBorder, statusCardRect, 14);
            }

            bool isCustomActive = _currentSnapshot != null && !_currentSnapshot.IsDhcpDns && _currentSnapshot.IsConnected;

            // Header gradient inside status card
            Rectangle headerBox = new Rectangle(statusCardRect.X + 10, statusCardRect.Y + 10, statusCardRect.Width - 20, 82);
            if (isCustomActive)
            {
                Theme.FillRoundedGradient(g, headerBox, 12, Theme.StatusHeaderActiveStart, Theme.StatusHeaderActiveEnd, LinearGradientMode.Vertical);
                using (Pen hp = new Pen(Theme.StatusHeaderActiveBorder, 1f))
                {
                    Theme.DrawRoundedRect(g, hp, headerBox, 12);
                }
            }
            else
            {
                using (SolidBrush hb = new SolidBrush(Theme.SecondaryButtonBackground))
                using (Pen hp = new Pen(Theme.SecondaryButtonBorder, 1f))
                {
                    Theme.FillRoundedRect(g, hb, headerBox, 12);
                    Theme.DrawRoundedRect(g, hp, headerBox, 12);
                }
            }

            // Status Circle Icon
            Rectangle circleRect = new Rectangle(headerBox.X + 16, headerBox.Y + 20, 42, 42);
            Color badgeColor = isCustomActive ? Theme.StatusGreen : Theme.AccentViolet;
            using (Pen circlePen = new Pen(badgeColor, 2.2f))
            {
                g.DrawEllipse(circlePen, circleRect);
            }
            IconRenderer.DrawIcon(
                g,
                isCustomActive ? "check" : "wifi",
                new RectangleF(circleRect.X + 10, circleRect.Y + 10, 22, 22),
                badgeColor);

            // "DNS Active" + Provider Name
            string statusHeadline = isCustomActive ? "DNS Active" : "Automatic DNS";
            string statusProvider = _currentSnapshot != null ? _currentSnapshot.ActiveProfileName : "System Default";

            using (Font hf = Theme.CreateBoldFont(12.5f))
            using (Font sf = Theme.CreateFont(9.8f))
            using (SolidBrush hb = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sb = new SolidBrush(isCustomActive ? Theme.StatusEmerald : Theme.TextSecondary))
            {
                g.DrawString(statusHeadline, hf, hb, headerBox.X + 70, headerBox.Y + 18);
                g.DrawString(statusProvider, sf, sb, headerBox.X + 70, headerBox.Y + 44);
            }

            // Info rows inside Status Card
            string priDns = (_currentSnapshot != null && !string.IsNullOrWhiteSpace(_currentSnapshot.ActualPrimaryDns))
                ? _currentSnapshot.ActualPrimaryDns
                : "Auto (DHCP)";
            string secDns = (_currentSnapshot != null && !string.IsNullOrWhiteSpace(_currentSnapshot.ActualSecondaryDns))
                ? _currentSnapshot.ActualSecondaryDns
                : "—";

            DrawStatusRow(g, statusCardRect.X + 18, statusCardRect.Y + 112, statusCardRect.Width - 36, "server", "Primary DNS", priDns, Theme.TextPrimary);
            DrawStatusRow(g, statusCardRect.X + 18, statusCardRect.Y + 146, statusCardRect.Width - 36, "server", "Secondary DNS", secDns, Theme.TextPrimary);

            using (Pen divPen = new Pen(Theme.CardBorder, 1f))
            {
                g.DrawLine(divPen, statusCardRect.X + 16, statusCardRect.Y + 182, statusCardRect.Right - 16, statusCardRect.Y + 182);
            }

            bool isConnected = _currentSnapshot != null && _currentSnapshot.IsConnected;
            DrawStatusRow(
                g,
                statusCardRect.X + 18,
                statusCardRect.Y + 198,
                statusCardRect.Width - 36,
                "check",
                "Status",
                isConnected ? "● Connected" : "● Disconnected",
                isConnected ? Theme.StatusGreen : Theme.DangerRed);

            DrawStatusRow(
                g,
                statusCardRect.X + 18,
                statusCardRect.Y + 232,
                statusCardRect.Width - 36,
                "clock",
                "Last Change",
                _configService.Settings.LastChangeText ?? "Ready",
                Theme.TextPrimary);

            // 2. Bottom Card: Quick Actions
            Rectangle qaCardRect = new Rectangle(rightColLeft, 368, rightColWidth, 246);
            using (SolidBrush cardBg = new SolidBrush(Theme.CardBackground))
            using (Pen cardBorder = new Pen(Theme.CardBorder, 1f))
            {
                Theme.FillRoundedRect(g, cardBg, qaCardRect, 14);
                Theme.DrawRoundedRect(g, cardBorder, qaCardRect, 14);
            }

            using (Font qf = Theme.CreateBoldFont(11.5f))
            using (SolidBrush qb = new SolidBrush(Theme.TextPrimary))
            {
                g.DrawString("Quick Actions", qf, qb, qaCardRect.X + 16, qaCardRect.Y + 14);
            }

            DrawQuickActionButton(g, _qaDisableRect, "disable", "Disable DNS", "(Use System Default)", _hoveredElement == "qa_disable");
            DrawQuickActionButton(g, _qaFlushRect, "flush", "Flush DNS Cache", null, _hoveredElement == "qa_flush");
            DrawQuickActionButton(g, _qaTestRect, "test", "Test DNS", "(Check Response)", _hoveredElement == "qa_test");
        }

        private void DrawStatusRow(Graphics g, int x, int y, int width, string iconName, string label, string value, Color valueColor)
        {
            IconRenderer.DrawIcon(g, iconName, new RectangleF(x, y + 2, 16, 16), Theme.TextSecondary);

            using (Font lf = Theme.CreateFont(9f))
            using (Font vf = Theme.CreateSemiboldFont(9.2f))
            using (SolidBrush lb = new SolidBrush(Theme.TextSecondary))
            using (SolidBrush vb = new SolidBrush(valueColor))
            {
                g.DrawString(label, lf, lb, x + 24, y);
                SizeF vsz = g.MeasureString(value, vf);
                g.DrawString(value, vf, vb, x + width - vsz.Width, y);
            }
        }

        private void DrawQuickActionButton(Graphics g, Rectangle rect, string iconName, string title, string subtitle, bool isHovered)
        {
            Color bg = isHovered ? Theme.SecondaryButtonHover : Theme.SecondaryButtonBackground;
            Color border = isHovered ? Theme.CardHoverBorder : Theme.SecondaryButtonBorder;

            using (SolidBrush b = new SolidBrush(bg))
            using (Pen p = new Pen(border, 1f))
            {
                Theme.FillRoundedRect(g, b, rect, 10);
                Theme.DrawRoundedRect(g, p, rect, 10);
            }

            IconRenderer.DrawIcon(g, iconName, new RectangleF(rect.X + 16, rect.Y + (rect.Height - 22) / 2f, 22, 22), Theme.TextPrimary);

            if (string.IsNullOrEmpty(subtitle))
            {
                using (Font tf = Theme.CreateSemiboldFont(9.8f))
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                {
                    g.DrawString(title, tf, tb, rect.X + 52, rect.Y + (rect.Height - 18) / 2f);
                }
            }
            else
            {
                using (Font tf = Theme.CreateSemiboldFont(9.5f))
                using (Font sf = Theme.CreateFont(8.4f))
                using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
                using (SolidBrush sb = new SolidBrush(Theme.TextSecondary))
                {
                    g.DrawString(title, tf, tb, rect.X + 52, rect.Y + 9);
                    g.DrawString(subtitle, sf, sb, rect.X + 52, rect.Y + 28);
                }
            }
        }

        private void DrawSettingsView(Graphics g, int w, int h)
        {
            _settingsHitRects.Clear();
            int left = 236;
            int contentW = w - left - 28;

            using (Font hFont = Theme.CreateBoldFont(15.5f))
            using (Font sFont = Theme.CreateFont(9.5f))
            using (SolidBrush hBrush = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sBrush = new SolidBrush(Theme.TextSecondary))
            {
                g.DrawString("Settings", hFont, hBrush, left - 2, 68);
                g.DrawString("Configure startup behavior, network adapter selection, appearance, sound, and notifications.", sFont, sBrush, left, 100);
            }

            int colW = (contentW - 20) / 2;
            int col1X = left;
            int col2X = left + colW + 20;

            // Left Column: General & DNS
            DrawSettingsCard(g, new Rectangle(col1X, 134, colW, 224), "General");
            DrawToggleRow(g, "set_start_win", col1X + 18, 174, colW - 36, "Start with Windows", _configService.Settings.StartWithWindows);
            DrawToggleRow(g, "set_min_tray", col1X + 18, 214, colW - 36, "Minimize to system tray", _configService.Settings.MinimizeToTray);
            DrawToggleRow(g, "set_close_tray", col1X + 18, 254, colW - 36, "Close button minimizes to tray", _configService.Settings.CloseMinimizesToTray);
            DrawToggleRow(g, "set_start_min", col1X + 18, 294, colW - 36, "Start minimized in background", _configService.Settings.StartMinimized);

            DrawSettingsCard(g, new Rectangle(col1X, 374, colW, 232), "DNS & Network Adapter");
            DrawToggleRow(g, "set_auto_adapter", col1X + 18, 414, colW - 36, "Automatically detect active adapter", _configService.Settings.AutoDetectAdapter);
            DrawToggleRow(g, "set_confirm_disable", col1X + 18, 454, colW - 36, "Confirm before disabling DNS", _configService.Settings.ConfirmBeforeDisable);

            // Adapter selector button
            Rectangle adapterBox = new Rectangle(col1X + 18, 502, colW - 36, 42);
            _settingsHitRects["set_cycle_adapter"] = adapterBox;
            string adapterLabel = _configService.Settings.AutoDetectAdapter
                ? ("Auto: " + (_currentSnapshot != null ? _currentSnapshot.AdapterName : "Active Adapter"))
                : ("Selected: " + (_currentSnapshot != null ? _currentSnapshot.AdapterName : _configService.Settings.SelectedAdapterId));

            using (SolidBrush ab = new SolidBrush(_hoveredElement == "set_cycle_adapter" ? Theme.SecondaryButtonHover : Theme.SecondaryButtonBackground))
            using (Pen ap = new Pen(Theme.SecondaryButtonBorder, 1f))
            using (Font af = Theme.CreateSemiboldFont(9f))
            using (SolidBrush at = new SolidBrush(Theme.TextPrimary))
            {
                Theme.FillRoundedRect(g, ab, adapterBox, 8);
                Theme.DrawRoundedRect(g, ap, adapterBox, 8);
                IconRenderer.DrawIcon(g, "wifi", new RectangleF(adapterBox.X + 12, adapterBox.Y + 12, 18, 18), Theme.AccentViolet);
                g.DrawString(adapterLabel + "  (Click to switch)", af, at, adapterBox.X + 38, adapterBox.Y + 12);
            }

            // Right Column: Appearance, Sound, Notifications, In-Game Overlay, Maintenance
            DrawSettingsCard(g, new Rectangle(col2X, 134, colW, 92), "Appearance");
            DrawToggleRow(g, "set_dark_mode", col2X + 18, 174, colW - 36, "Dark Mode (Gaming Navy Theme)", _configService.Settings.DarkMode);

            DrawSettingsCard(g, new Rectangle(col2X, 236, colW, 130), "Sound & Notifications");
            DrawToggleRow(g, "set_sounds", col2X + 18, 274, colW - 36, "Enable connection sound effects", _configService.Settings.EnableSounds);
            DrawToggleRow(g, "set_notifications", col2X + 18, 314, colW - 36, "Enable Windows tray notifications", _configService.Settings.EnableNotifications);

            DrawSettingsCard(g, new Rectangle(col2X, 376, colW, 108), "In-Game Overlay");
            Rectangle shortcutBox = new Rectangle(col2X + 18, 420, colW - 144, 42);
            _settingsHitRects["set_overlay_shortcut"] = shortcutBox;

            bool scHovered = _hoveredElement == "set_overlay_shortcut";
            Color scBorder = _isRecordingOverlayShortcut
                ? Theme.ActiveCardBorder
                : (scHovered ? Theme.CardHoverBorder : Theme.SecondaryButtonBorder);
            using (SolidBrush sb = new SolidBrush(scHovered ? Theme.SecondaryButtonHover : Theme.SecondaryButtonBackground))
            using (Pen sp = new Pen(scBorder, _isRecordingOverlayShortcut ? 1.6f : 1f))
            using (Font sf = Theme.CreateSemiboldFont(9f))
            using (SolidBrush st = new SolidBrush(_isRecordingOverlayShortcut ? Theme.AccentViolet : Theme.TextPrimary))
            {
                Theme.FillRoundedRect(g, sb, shortcutBox, 8);
                Theme.DrawRoundedRect(g, sp, shortcutBox, 8);
                IconRenderer.DrawIcon(g, "gamepad", new RectangleF(shortcutBox.X + 12, shortcutBox.Y + 12, 18, 18), Theme.AccentViolet);
                string curShortcut = string.IsNullOrWhiteSpace(_configService.Settings.OverlayShortcut) ? "Shift+Tab" : _configService.Settings.OverlayShortcut;
                string scText = _isRecordingOverlayShortcut
                    ? "Press keys... (or click to cycle)"
                    : ("Shortcut: " + curShortcut);
                g.DrawString(scText, sf, st, shortcutBox.X + 38, shortcutBox.Y + 12);
            }

            Rectangle btnOpenOverlay = new Rectangle(shortcutBox.Right + 10, 420, 116, 42);
            _settingsHitRects["set_open_overlay"] = btnOpenOverlay;
            DrawPillButton(g, btnOpenOverlay, "play", "Overlay", true, _hoveredElement == "set_open_overlay");

            DrawSettingsCard(g, new Rectangle(col2X, 494, colW, 112), "Maintenance & Profiles");
            int halfBtnW = (colW - 46) / 2;
            Rectangle btnTestSound = new Rectangle(col2X + 18, 538, halfBtnW, 42);
            _settingsHitRects["set_test_sound"] = btnTestSound;
            DrawPillButton(g, btnTestSound, "volume", "Test Chime", false, _hoveredElement == "set_test_sound");

            Rectangle btnResetDefaults = new Rectangle(btnTestSound.Right + 10, 538, halfBtnW, 42);
            _settingsHitRects["set_reset_defaults"] = btnResetDefaults;
            DrawPillButton(g, btnResetDefaults, "flush", "Reset DNS", false, _hoveredElement == "set_reset_defaults");
        }

        private void DrawSettingsCard(Graphics g, Rectangle rect, string title)
        {
            using (SolidBrush bg = new SolidBrush(Theme.CardBackground))
            using (Pen border = new Pen(Theme.CardBorder, 1f))
            using (Font tf = Theme.CreateBoldFont(10.8f))
            using (SolidBrush tb = new SolidBrush(Theme.TextPrimary))
            {
                Theme.FillRoundedRect(g, bg, rect, 12);
                Theme.DrawRoundedRect(g, border, rect, 12);
                g.DrawString(title, tf, tb, rect.X + 18, rect.Y + 14);
            }
        }

        private void DrawToggleRow(Graphics g, string key, int x, int y, int width, string label, bool isChecked)
        {
            Rectangle rowRect = new Rectangle(x, y, width, 30);
            _settingsHitRects[key] = rowRect;

            using (Font lf = Theme.CreateFont(9.5f))
            using (SolidBrush lb = new SolidBrush(Theme.TextPrimary))
            {
                g.DrawString(label, lf, lb, x, y + 4);
            }

            Rectangle switchRect = new Rectangle(x + width - 44, y + 3, 42, 22);
            Color trackColor = isChecked ? Theme.ActiveCardBorder : Theme.SecondaryButtonBorder;
            using (SolidBrush sb = new SolidBrush(trackColor))
            {
                Theme.FillRoundedRect(g, sb, switchRect, 11);
            }

            int knobX = isChecked ? (switchRect.Right - 19) : (switchRect.X + 3);
            using (SolidBrush kb = new SolidBrush(Color.White))
            {
                g.FillEllipse(kb, knobX, switchRect.Y + 3, 16, 16);
            }
        }

        private void DrawAboutView(Graphics g, int w, int h)
        {
            int left = 236;
            int contentW = w - left - 28;

            using (Font hFont = Theme.CreateBoldFont(15.5f))
            using (Font sFont = Theme.CreateFont(9.5f))
            using (SolidBrush hBrush = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sBrush = new SolidBrush(Theme.TextSecondary))
            {
                g.DrawString("About DNS Party", hFont, hBrush, left - 2, 68);
                g.DrawString("Lightweight, high-speed Windows DNS Manager built for gamers and power users.", sFont, sBrush, left, 100);
            }

            Rectangle cardRect = new Rectangle(left, 134, contentW, h - 170);
            using (SolidBrush bg = new SolidBrush(Theme.CardBackground))
            using (Pen border = new Pen(Theme.CardBorder, 1f))
            {
                Theme.FillRoundedRect(g, bg, cardRect, 14);
                Theme.DrawRoundedRect(g, border, cardRect, 14);
            }

            IconRenderer.DrawAppLogo(g, new RectangleF(cardRect.X + 28, cardRect.Y + 28, 56, 56), true);

            using (Font titleFont = Theme.CreateBoldFont(16f))
            using (Font verFont = Theme.CreateSemiboldFont(10f))
            using (Font bodyFont = Theme.CreateFont(9.6f))
            using (SolidBrush pBrush = new SolidBrush(Theme.TextPrimary))
            using (SolidBrush sBrush = new SolidBrush(Theme.TextSecondary))
            using (SolidBrush vBrush = new SolidBrush(Theme.AccentViolet))
            {
                g.DrawString("DNS Party — Windows DNS Manager", titleFont, pBrush, cardRect.X + 100, cardRect.Y + 28);
                g.DrawString("Version 1.0.0  •  Native .NET / Win32 Edition", verFont, vBrush, cardRect.X + 102, cardRect.Y + 58);

                string details =
                    "• Instant 1-Click DNS Switching across IPv4 and IPv6 network adapters.\r\n" +
                    "• Real-time Windows Registry & NetworkInterface verification (never reports false status).\r\n" +
                    "• Direct RFC 1035 UDP port 53 DNS query latency measurement in milliseconds.\r\n" +
                    "• Lightweight System Tray background operation with zero polling and zero Alt+Tab clutter.\r\n" +
                    "• Automatic DHCP DNS restoration & instant Windows DNS Resolver Cache flushing.";

                g.DrawString(details, bodyFont, sBrush, new RectangleF(cardRect.X + 30, cardRect.Y + 106, cardRect.Width - 60, 140));

                string diagTitle = "Active System Diagnostics";
                g.DrawString(diagTitle, verFont, pBrush, cardRect.X + 30, cardRect.Y + 250);

                string adapterInfo = _currentSnapshot != null
                    ? string.Format("Active Adapter: {0} ({1})", _currentSnapshot.AdapterName, _currentSnapshot.AdapterDescription)
                    : "Active Adapter: None";
                string dnsInfo = _currentSnapshot != null
                    ? string.Format("Detected Windows DNS: {0}  (Mode: {1})",
                        string.IsNullOrEmpty(_currentSnapshot.ActualPrimaryDns) ? "None" : _currentSnapshot.ActualPrimaryDns,
                        _currentSnapshot.IsDhcpDns ? "Automatic DHCP" : "Static Custom DNS")
                    : "Detected Windows DNS: Unknown";
                string cfgInfo = "Configuration File: " + _configService.ConfigFilePath;

                g.DrawString(adapterInfo, bodyFont, sBrush, cardRect.X + 30, cardRect.Y + 280);
                g.DrawString(dnsInfo, bodyFont, sBrush, cardRect.X + 30, cardRect.Y + 306);
                g.DrawString(cfgInfo, bodyFont, sBrush, cardRect.X + 30, cardRect.Y + 332);
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_activeTab == NavTab.Home || _activeTab == NavTab.Favorites)
            {
                if (_maxProfileScroll > 0)
                {
                    _profileScrollOffset -= (e.Delta / 120) * 38;
                    if (_profileScrollOffset < 0) _profileScrollOffset = 0;
                    if (_profileScrollOffset > _maxProfileScroll) _profileScrollOffset = _maxProfileScroll;
                    RecalculateLayout();
                    Invalidate();
                }
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && _titleBarRect.Contains(e.Location) &&
                !_minBtnRect.Contains(e.Location) && !_closeBtnRect.Contains(e.Location))
            {
                _isDraggingTitleBar = true;
                _dragStartPoint = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isDraggingTitleBar)
            {
                Point p = PointToScreen(e.Location);
                Location = new Point(p.X - _dragStartPoint.X, p.Y - _dragStartPoint.Y);
                return;
            }

            string prevHover = _hoveredElement;
            int prevCard = _hoveredCardIndex;
            string prevPart = _hoveredCardPart;

            _hoveredElement = string.Empty;
            _hoveredCardIndex = -1;
            _hoveredCardPart = string.Empty;

            if (_closeBtnRect.Contains(e.Location)) _hoveredElement = "close";
            else if (_minBtnRect.Contains(e.Location)) _hoveredElement = "min";
            else
            {
                for (int i = 0; i < 5; i++)
                {
                    if (_sidebarItemRects[i].Contains(e.Location))
                    {
                        _hoveredElement = "nav_" + i;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(_hoveredElement))
            {
                if (_activeTab == NavTab.Home || _activeTab == NavTab.Favorites)
                {
                    if (_btnAddDnsRect.Contains(e.Location)) _hoveredElement = "btn_add";
                    else if (_btnEditDnsRect.Contains(e.Location)) _hoveredElement = "btn_edit";
                    else if (_btnDeleteDnsRect.Contains(e.Location)) _hoveredElement = "btn_delete";
                    else if (_btnFlushBottomRect.Contains(e.Location)) _hoveredElement = "btn_flush_bottom";
                    else if (_qaDisableRect.Contains(e.Location)) _hoveredElement = "qa_disable";
                    else if (_qaFlushRect.Contains(e.Location)) _hoveredElement = "qa_flush";
                    else if (_qaTestRect.Contains(e.Location)) _hoveredElement = "qa_test";
                    else
                    {
                        for (int i = 0; i < _cardHitBoxes.Count; i++)
                        {
                            var box = _cardHitBoxes[i];
                            if (box.CardRect.Contains(e.Location))
                            {
                                _hoveredCardIndex = i;
                                if (box.StarRect.Contains(e.Location)) _hoveredCardPart = "star";
                                else if (box.MoreRect.Contains(e.Location)) _hoveredCardPart = "more";
                                else _hoveredCardPart = "card";
                                break;
                            }
                        }
                    }
                }
                else if (_activeTab == NavTab.Settings)
                {
                    foreach (var kvp in _settingsHitRects)
                    {
                        if (kvp.Value.Contains(e.Location))
                        {
                            _hoveredElement = kvp.Key;
                            break;
                        }
                    }
                }
            }

            bool hasHand = !string.IsNullOrEmpty(_hoveredElement) || _hoveredCardIndex >= 0;
            Cursor = hasHand ? Cursors.Hand : Cursors.Default;

            if (prevHover != _hoveredElement || prevCard != _hoveredCardIndex || prevPart != _hoveredCardPart)
            {
                Invalidate();
            }
        }

        protected override async void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_isDraggingTitleBar)
            {
                _isDraggingTitleBar = false;
                return;
            }

            // Right-click on a DNS card opens its context menu
            if (e.Button == MouseButtons.Right && (_activeTab == NavTab.Home || _activeTab == NavTab.Favorites))
            {
                for (int i = 0; i < _cardHitBoxes.Count; i++)
                {
                    if (_cardHitBoxes[i].CardRect.Contains(e.Location))
                    {
                        ShowCardContextMenu(_cardHitBoxes[i].Profile, PointToScreen(e.Location));
                        return;
                    }
                }
            }

            if (e.Button != MouseButtons.Left) return;

            // Titlebar buttons
            if (_closeBtnRect.Contains(e.Location))
            {
                if (_configService.Settings.CloseMinimizesToTray)
                {
                    HideToTray(false);
                }
                else
                {
                    ExitApplication();
                }
                return;
            }

            if (_minBtnRect.Contains(e.Location))
            {
                if (_configService.Settings.MinimizeToTray)
                {
                    HideToTray(false);
                }
                else
                {
                    WindowState = FormWindowState.Minimized;
                }
                return;
            }

            // Sidebar buttons
            for (int i = 0; i < 5; i++)
            {
                if (_sidebarItemRects[i].Contains(e.Location))
                {
                    if (i == 0) { _activeTab = NavTab.Home; _profileScrollOffset = 0; RecalculateLayout(); Invalidate(); }
                    else if (i == 1) { _activeTab = NavTab.Favorites; _profileScrollOffset = 0; RecalculateLayout(); Invalidate(); }
                    else if (i == 2) { OpenAddDnsDialog(); }
                    else if (i == 3) { _activeTab = NavTab.Settings; RecalculateLayout(); Invalidate(); }
                    else if (i == 4) { _activeTab = NavTab.About; RecalculateLayout(); Invalidate(); }
                    return;
                }
            }

            if (_activeTab == NavTab.Home || _activeTab == NavTab.Favorites)
            {
                if (_btnAddDnsRect.Contains(e.Location)) { OpenAddDnsDialog(); return; }
                if (_btnEditDnsRect.Contains(e.Location)) { OpenEditDnsDialog(null); return; }
                if (_btnDeleteDnsRect.Contains(e.Location)) { DeleteDnsProfile(null); return; }
                if (_btnFlushBottomRect.Contains(e.Location)) { await FlushDnsCacheAsync(false); return; }

                if (_qaDisableRect.Contains(e.Location)) { await DisableDnsAsync(false); return; }
                if (_qaFlushRect.Contains(e.Location)) { await FlushDnsCacheAsync(false); return; }
                if (_qaTestRect.Contains(e.Location)) { await TestAllOrSelectedDnsAsync(null); return; }

                for (int i = 0; i < _cardHitBoxes.Count; i++)
                {
                    var box = _cardHitBoxes[i];
                    if (box.CardRect.Contains(e.Location))
                    {
                        if (box.StarRect.Contains(e.Location))
                        {
                            box.Profile.IsFavorite = !box.Profile.IsFavorite;
                            _configService.Save();
                            RebuildTrayMenu();
                            RecalculateLayout();
                            Invalidate();
                            return;
                        }

                        if (box.MoreRect.Contains(e.Location))
                        {
                            ShowCardContextMenu(box.Profile, PointToScreen(new Point(box.MoreRect.Left, box.MoreRect.Bottom)));
                            return;
                        }

                        // Clicking the card selects and applies the DNS profile
                        _selectedProfileId = box.Profile.Id;
                        await ApplyDnsProfileAsync(box.Profile, false);
                        return;
                    }
                }
            }
            else if (_activeTab == NavTab.Settings)
            {
                HandleSettingsClick(e.Location);
            }
        }

        private void HandleSettingsClick(Point pt)
        {
            foreach (var kvp in _settingsHitRects)
            {
                if (!kvp.Value.Contains(pt)) continue;

                var s = _configService.Settings;
                switch (kvp.Key)
                {
                    case "set_start_win":
                        _configService.SetStartWithWindows(!s.StartWithWindows);
                        break;
                    case "set_min_tray":
                        s.MinimizeToTray = !s.MinimizeToTray;
                        break;
                    case "set_close_tray":
                        s.CloseMinimizesToTray = !s.CloseMinimizesToTray;
                        break;
                    case "set_start_min":
                        s.StartMinimized = !s.StartMinimized;
                        if (s.StartWithWindows) _configService.SetStartWithWindows(true);
                        break;
                    case "set_auto_adapter":
                        s.AutoDetectAdapter = !s.AutoDetectAdapter;
                        RefreshActualDnsState();
                        break;
                    case "set_confirm_disable":
                        s.ConfirmBeforeDisable = !s.ConfirmBeforeDisable;
                        break;
                    case "set_cycle_adapter":
                        var adapters = _dnsService.GetCandidateAdapters();
                        if (adapters.Count > 0)
                        {
                            s.AutoDetectAdapter = false;
                            int idx = adapters.FindIndex(a => string.Equals(a.Id, s.SelectedAdapterId, StringComparison.OrdinalIgnoreCase));
                            int next = (idx + 1) % adapters.Count;
                            s.SelectedAdapterId = adapters[next].Id;
                            RefreshActualDnsState();
                            ShowBannerNotification("Selected adapter: " + adapters[next].Name, false);
                        }
                        break;
                    case "set_dark_mode":
                        s.DarkMode = !s.DarkMode;
                        Theme.DarkMode = s.DarkMode;
                        BackColor = Theme.WindowBackground;
                        RebuildTrayMenu();
                        break;
                    case "set_sounds":
                        s.EnableSounds = !s.EnableSounds;
                        break;
                    case "set_notifications":
                        s.EnableNotifications = !s.EnableNotifications;
                        break;
                    case "set_overlay_shortcut":
                        if (_isRecordingOverlayShortcut)
                        {
                            string[] presets = { "Shift+Tab", "Ctrl+Shift+D", "Alt+D", "Ctrl+Tab", "F8" };
                            int pIdx = Array.FindIndex(presets, p => string.Equals(p, s.OverlayShortcut, StringComparison.OrdinalIgnoreCase));
                            s.OverlayShortcut = presets[(pIdx + 1) % presets.Length];
                            _isRecordingOverlayShortcut = false;
                            
                            if (_hotkeyService != null)
                            {
                                _hotkeyService.RegisterShortcut(s.OverlayShortcut, out string errorMsg);
                                if (!string.IsNullOrEmpty(errorMsg))
                                {
                                    ShowBannerNotification(errorMsg, true);
                                    Invalidate();
                                    return;
                                }
                            }

                            RebuildTrayMenu();
                            ShowBannerNotification("Overlay shortcut set to " + s.OverlayShortcut, false);
                        }
                        else
                        {
                            _isRecordingOverlayShortcut = true;
                        }
                        break;
                    case "set_open_overlay":
                        _isRecordingOverlayShortcut = false;
                        ShowOverlay();
                        break;
                    case "set_test_sound":
                        _soundService.PlayConnect(true);
                        ShowBannerNotification("Played connection chime.", false);
                        break;
                    case "set_reset_defaults":
                        _configService.ResetDefaultProfiles();
                        RebuildTrayMenu();
                        ShowBannerNotification("Restored default DNS providers.", false);
                        break;
                }

                _configService.Save();
                Invalidate();
                break;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!_isRecordingOverlayShortcut) return;

            if (e.KeyCode == Keys.Escape)
            {
                _isRecordingOverlayShortcut = false;
                Invalidate();
                e.Handled = true;
                return;
            }

            // Ignore standalone modifier key presses until a primary key is pressed
            if (e.KeyCode == Keys.ShiftKey || e.KeyCode == Keys.ControlKey || e.KeyCode == Keys.Menu ||
                e.KeyCode == Keys.LWin || e.KeyCode == Keys.RWin)
            {
                return;
            }

            var parts = new List<string>();
            if (e.Control) parts.Add("Ctrl");
            if (e.Alt) parts.Add("Alt");
            if (e.Shift) parts.Add("Shift");

            string keyName = e.KeyCode.ToString();
            if (e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9)
            {
                keyName = ((int)(e.KeyCode - Keys.D0)).ToString();
            }
            parts.Add(keyName);

            _configService.Settings.OverlayShortcut = string.Join("+", parts);
            _isRecordingOverlayShortcut = false;

            if (_hotkeyService != null)
            {
                _hotkeyService.RegisterShortcut(_configService.Settings.OverlayShortcut, out string errorMsg);
                if (!string.IsNullOrEmpty(errorMsg))
                {
                    ShowBannerNotification(errorMsg, true);
                    Invalidate();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            _configService.Save();
            RebuildTrayMenu();
            ShowBannerNotification("Overlay shortcut updated to " + _configService.Settings.OverlayShortcut, false);
            Invalidate();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }
}
